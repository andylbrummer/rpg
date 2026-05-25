import * as THREE from 'three';
import type { GameState, Tile } from '$shared/types/game';
import { getTheme, type DungeonTheme } from './DungeonTheme';
import { BloomCluster, BloomParticleSystem, BloomHazardOverlay } from './BloomEffects';
import { getCreatureMaterials, type CreatureMaterialSet } from './CreatureMaterials';
import { createUnaccountedMaterial } from './UnaccountedMaterial';
import { AmbientParticleSystem, getParticlePreset } from './AmbientParticles';

export class DungeonRenderer {
  private scene: THREE.Scene;
  private camera: THREE.PerspectiveCamera;
  private renderer: THREE.WebGLRenderer;
  private resolutionScale = 1;
  private reduceMotion = false;
  private tileMeshes: Map<string, THREE.Mesh> = new Map();
  private tileSize = 2;
  private wallHeight = 3;
  private wallThickness = 0.15;
  private currentState: GameState | null = null;
  private isDisposed = false;
  private torchLight: THREE.PointLight;
  private ambientLight: THREE.AmbientLight;
  private fillLight: THREE.DirectionalLight;
  private rimLight: THREE.DirectionalLight;
  private wallTexture: THREE.CanvasTexture;
  private floorTexture: THREE.CanvasTexture;
  private doorTexture: THREE.CanvasTexture;
  private breakableWallTexture: THREE.CanvasTexture;
  private breakingWalls: Map<string, { mesh: THREE.Mesh; startTime: number; duration: number }> = new Map();
  /** Public for tests / debug overlays. ms duration of the break animation. */
  static readonly BREAK_ANIMATION_MS = 600;
  private currentTheme: DungeonTheme;
  private currentDungeonType: string | undefined;
  private bloomClusters: BloomCluster[] = [];
  private bloomParticles: BloomParticleSystem[] = [];
  private bloomHazards: BloomHazardOverlay[] = [];
  private ambientParticleSystem: AmbientParticleSystem | null = null;
  private bloomEffectsAdded = false;
  private nextBloomMutation = 0;
  private creatureMeshes: Map<string, THREE.Object3D> = new Map();
  private dyingUnaccounted: Map<string, { mesh: THREE.Group; startTime: number }> = new Map();
  private lastCombatLogLength = 0;
  private unaccountedAttackBoosts: Map<string, number> = new Map();

  static isSupported(): boolean {
    try {
      const canvas = document.createElement('canvas');
      return !!(window.WebGLRenderingContext && canvas.getContext('webgl'));
    } catch {
      return false;
    }
  }

  constructor(container: HTMLElement) {
    const MIN_WIDTH = 800;
    const MIN_HEIGHT = 600;

    const width = Math.max(container.clientWidth || MIN_WIDTH, MIN_WIDTH);
    const height = Math.max(container.clientHeight || MIN_HEIGHT, MIN_HEIGHT);

    // Theme setup
    this.currentTheme = getTheme(undefined);

    // Generate procedural textures
    this.wallTexture = this.createBrickTexture(this.currentTheme);
    this.floorTexture = this.createStoneTileTexture(this.currentTheme);
    this.doorTexture = this.createWoodTexture(this.currentTheme);
    this.breakableWallTexture = this.createBreakableWallTexture(this.currentTheme);

    // Scene setup
    this.scene = new THREE.Scene();
    this.scene.background = new THREE.Color(this.currentTheme.backgroundColor);
    this.scene.fog = new THREE.Fog(this.currentTheme.fogColor, 10, 30);

    // Camera
    this.camera = new THREE.PerspectiveCamera(75, width / height, 0.1, 1000);

    // Renderer
    this.renderer = new THREE.WebGLRenderer({ antialias: true });
    this.renderer.setSize(width, height);
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2) * this.resolutionScale);
    this.renderer.shadowMap.enabled = true;
    this.renderer.shadowMap.type = THREE.PCFSoftShadowMap;

    // Ensure canvas fills container
    this.renderer.domElement.style.width = '100%';
    this.renderer.domElement.style.height = '100%';
    this.renderer.domElement.style.display = 'block';

    container.appendChild(this.renderer.domElement);

    // Lighting
    this.ambientLight = new THREE.AmbientLight(this.currentTheme.ambientColor, 0.4);
    this.scene.add(this.ambientLight);

    this.torchLight = new THREE.PointLight(this.currentTheme.torchColor, this.currentTheme.glowIntensity, 25);
    this.torchLight.position.set(0, 2, 0);
    this.torchLight.castShadow = true;
    this.torchLight.shadow.mapSize.width = 512;
    this.torchLight.shadow.mapSize.height = 512;
    this.scene.add(this.torchLight);

    // Fill light from above
    this.fillLight = new THREE.DirectionalLight(this.currentTheme.fillColor, 0.3);
    this.fillLight.position.set(5, 10, 5);
    this.scene.add(this.fillLight);

    // Rim light for depth
    this.rimLight = new THREE.DirectionalLight(this.currentTheme.rimColor, 0.2);
    this.rimLight.position.set(-5, 3, -5);
    this.scene.add(this.rimLight);

    // Handle resize
    window.addEventListener('resize', () => this.handleResize(container));

    // Start render loop
    this.animate();
  }

  private createBrickTexture(theme: DungeonTheme): THREE.CanvasTexture {
    const canvas = document.createElement('canvas');
    canvas.width = 256;
    canvas.height = 256;
    const ctx = canvas.getContext('2d')!;

    ctx.fillStyle = theme.wallColor;
    ctx.fillRect(0, 0, 256, 256);

    const brickHeight = 32;
    const brickWidth = 64;
    const rows = 256 / brickHeight;

    for (let row = 0; row < rows; row++) {
      const offset = (row % 2) * (brickWidth / 2);
      for (let col = -1; col < 5; col++) {
        const x = col * brickWidth + offset;
        const y = row * brickHeight;

        ctx.globalAlpha = 0.08 + Math.random() * 0.16;
        ctx.fillStyle = Math.random() > 0.5 ? '#ffffff' : '#000000';
        ctx.fillRect(x + 1, y + 1, brickWidth - 2, brickHeight - 2);
        ctx.globalAlpha = 1.0;

        for (let i = 0; i < 8; i++) {
          const nx = x + Math.random() * brickWidth;
          const ny = y + Math.random() * brickHeight;
          ctx.fillStyle = `rgba(0,0,0,${0.05 + Math.random() * 0.1})`;
          ctx.fillRect(nx, ny, 2, 2);
        }
      }
    }

    const texture = new THREE.CanvasTexture(canvas);
    texture.wrapS = THREE.RepeatWrapping;
    texture.wrapT = THREE.RepeatWrapping;
    texture.repeat.set(1, 1.5);
    return texture;
  }

  private createStoneTileTexture(theme: DungeonTheme): THREE.CanvasTexture {
    const canvas = document.createElement('canvas');
    canvas.width = 256;
    canvas.height = 256;
    const ctx = canvas.getContext('2d')!;

    ctx.fillStyle = theme.floorColor;
    ctx.fillRect(0, 0, 256, 256);

    const tileSize = 64;
    const cols = 256 / tileSize;
    const rows = 256 / tileSize;

    for (let row = 0; row < rows; row++) {
      for (let col = 0; col < cols; col++) {
        const x = col * tileSize;
        const y = row * tileSize;

        ctx.globalAlpha = 0.08 + Math.random() * 0.16;
        ctx.fillStyle = Math.random() > 0.5 ? '#ffffff' : '#000000';
        ctx.fillRect(x + 1, y + 1, tileSize - 2, tileSize - 2);
        ctx.globalAlpha = 1.0;

        for (let i = 0; i < 20; i++) {
          const nx = x + Math.random() * tileSize;
          const ny = y + Math.random() * tileSize;
          const val = Math.random() > 0.5 ? 255 : 0;
          ctx.fillStyle = `rgba(${val},${val},${val},${0.05 + Math.random() * 0.08})`;
          ctx.fillRect(nx, ny, 2, 2);
        }
      }
    }

    const texture = new THREE.CanvasTexture(canvas);
    texture.wrapS = THREE.RepeatWrapping;
    texture.wrapT = THREE.RepeatWrapping;
    texture.repeat.set(2, 2);
    return texture;
  }

  private createWoodTexture(theme: DungeonTheme): THREE.CanvasTexture {
    const canvas = document.createElement('canvas');
    canvas.width = 128;
    canvas.height = 128;
    const ctx = canvas.getContext('2d')!;

    ctx.fillStyle = theme.doorColor;
    ctx.fillRect(0, 0, 128, 128);

    for (let i = 0; i < 20; i++) {
      const y = Math.random() * 128;
      const width = 1 + Math.random() * 2;
      ctx.fillStyle = `rgba(0,0,0,${0.2 + Math.random() * 0.3})`;
      ctx.fillRect(0, y, 128, width);
    }

    const texture = new THREE.CanvasTexture(canvas);
    texture.wrapS = THREE.RepeatWrapping;
    texture.wrapT = THREE.RepeatWrapping;
    return texture;
  }

  private createBreakableWallTexture(theme: DungeonTheme): THREE.CanvasTexture {
    const canvas = document.createElement('canvas');
    canvas.width = 256;
    canvas.height = 256;
    const ctx = canvas.getContext('2d')!;

    ctx.fillStyle = theme.wallColor;
    ctx.fillRect(0, 0, 256, 256);

    const brickHeight = 32;
    const brickWidth = 64;
    const rows = 256 / brickHeight;

    for (let row = 0; row < rows; row++) {
      const offset = (row % 2) * (brickWidth / 2);
      for (let col = -1; col < 5; col++) {
        const x = col * brickWidth + offset;
        const y = row * brickHeight;

        ctx.globalAlpha = 0.12 + Math.random() * 0.18;
        ctx.fillStyle = Math.random() > 0.5 ? '#ffffff' : '#000000';
        ctx.fillRect(x + 1, y + 1, brickWidth - 2, brickHeight - 2);
        ctx.globalAlpha = 1.0;
      }
    }

    // Crack network — branching dark fractures, deterministic per-texture
    ctx.strokeStyle = 'rgba(0,0,0,0.65)';
    ctx.lineCap = 'round';
    const trunks = 3;
    for (let t = 0; t < trunks; t++) {
      const startX = 40 + t * 80 + Math.random() * 20;
      let x = startX;
      let y = 20 + Math.random() * 30;
      let angle = Math.PI / 2 + (Math.random() - 0.5) * 0.6;
      const segments = 14 + Math.floor(Math.random() * 6);
      ctx.lineWidth = 2.2;
      for (let s = 0; s < segments; s++) {
        const len = 8 + Math.random() * 14;
        const nx = x + Math.cos(angle) * len;
        const ny = y + Math.sin(angle) * len;
        ctx.beginPath();
        ctx.moveTo(x, y);
        ctx.lineTo(nx, ny);
        ctx.stroke();
        x = nx;
        y = ny;
        angle += (Math.random() - 0.5) * 0.9;

        if (Math.random() < 0.25) {
          // Spawn branch
          const branchAngle = angle + (Math.random() > 0.5 ? 1 : -1) * (0.6 + Math.random() * 0.4);
          let bx = x;
          let by = y;
          ctx.lineWidth = 1.2;
          for (let b = 0; b < 4 + Math.floor(Math.random() * 4); b++) {
            const blen = 5 + Math.random() * 8;
            const bnx = bx + Math.cos(branchAngle) * blen;
            const bny = by + Math.sin(branchAngle) * blen;
            ctx.beginPath();
            ctx.moveTo(bx, by);
            ctx.lineTo(bnx, bny);
            ctx.stroke();
            bx = bnx;
            by = bny;
          }
          ctx.lineWidth = 2.2;
        }
      }
    }

    // Highlight along cracks for depth
    ctx.strokeStyle = 'rgba(255,255,255,0.08)';
    ctx.lineWidth = 0.8;
    for (let i = 0; i < 40; i++) {
      const sx = Math.random() * 256;
      const sy = Math.random() * 256;
      ctx.beginPath();
      ctx.moveTo(sx, sy);
      ctx.lineTo(sx + (Math.random() - 0.5) * 6, sy + (Math.random() - 0.5) * 6);
      ctx.stroke();
    }

    const texture = new THREE.CanvasTexture(canvas);
    texture.wrapS = THREE.RepeatWrapping;
    texture.wrapT = THREE.RepeatWrapping;
    texture.repeat.set(1, 1.5);
    return texture;
  }

  updateState(state: GameState): void {
    this.currentState = state;

    const dungeonType = state.dungeonType;
    if (dungeonType !== this.currentDungeonType) {
      this.currentDungeonType = dungeonType;
      this.currentTheme = getTheme(dungeonType);
      this.applyTheme(this.currentTheme);
      this.setupAmbientParticles(dungeonType);
    }

    if (state.hasDungeon) {
      this.renderTiles(state.tiles);
      this.updateCamera(state.player);
      this.updateTorch(state.player);
      this.updateCreatures(state);
    } else {
      this.renderDefaultScene();
    }
  }

  private applyTheme(theme: DungeonTheme): void {
    this.scene.background = new THREE.Color(theme.backgroundColor);
    this.scene.fog = new THREE.Fog(theme.fogColor, 10, 30);

    this.ambientLight.color.setHex(theme.ambientColor);
    this.torchLight.color.setHex(theme.torchColor);
    this.torchLight.intensity = theme.glowIntensity;
    this.fillLight.color.setHex(theme.fillColor);
    this.rimLight.color.setHex(theme.rimColor);

    this.wallTexture.dispose();
    this.floorTexture.dispose();
    this.doorTexture.dispose();
    this.breakableWallTexture.dispose();
    this.wallTexture = this.createBrickTexture(theme);
    this.floorTexture = this.createStoneTileTexture(theme);
    this.doorTexture = this.createWoodTexture(theme);
    this.breakableWallTexture = this.createBreakableWallTexture(theme);

    this.clearTiles();
    this.clearCreatures();
  }

  private updateTorch(player: { x: number; y: number }): void {
    const x = player.x * this.tileSize;
    const z = player.y * this.tileSize;
    this.torchLight.position.set(x, 2, z);
  }

  private renderDefaultScene(): void {
    this.clearTiles();
    this.clearCreatures();

    // Add a simple floor
    const geometry = new THREE.PlaneGeometry(10, 10);
    const material = new THREE.MeshStandardMaterial({
      map: this.floorTexture,
      roughness: 0.8
    });
    const mesh = new THREE.Mesh(geometry, material);
    mesh.rotation.x = -Math.PI / 2;
    mesh.position.set(0, 0, 0);
    mesh.receiveShadow = true;
    this.tileMeshes.set('default', mesh);
    this.scene.add(mesh);

    // Add a visible marker
    const markerGeo = new THREE.BoxGeometry(0.5, 0.5, 0.5);
    const markerMat = new THREE.MeshStandardMaterial({ color: this.currentTheme.accentColor });
    const marker = new THREE.Mesh(markerGeo, markerMat);
    marker.position.set(0, 0.5, 0);
    this.tileMeshes.set('marker', marker);
    this.scene.add(marker);

    // Position camera
    this.camera.position.set(0, 2, 5);
    this.camera.lookAt(0, 0, 0);
  }

  private clearTiles(): void {
    // Drop break-animation refs first; the actual mesh disposal happens in the
    // tileMeshes loop below, but tickBreakingWalls would otherwise touch a
    // disposed material on its next frame.
    this.clearBreakingWalls();
    for (const [, mesh] of this.tileMeshes) {
      this.scene.remove(mesh);
      mesh.geometry.dispose();
      (mesh.material as THREE.Material).dispose();
    }
    this.tileMeshes.clear();
    this.clearBloomEffects();
  }

  private clearBreakingWalls(): void {
    this.breakingWalls.clear();
  }

  private clearCreatures(): void {
    for (const [, mesh] of this.creatureMeshes) {
      this.scene.remove(mesh);
      mesh.traverse((child) => {
        if (child instanceof THREE.Mesh) {
          child.geometry.dispose();
          if (Array.isArray(child.material)) {
            child.material.forEach(m => m.dispose());
          } else {
            child.material.dispose();
          }
        }
      });
    }
    this.creatureMeshes.clear();
    for (const [, data] of this.dyingUnaccounted) {
      this.scene.remove(data.mesh);
      data.mesh.traverse((child) => {
        if (child instanceof THREE.Mesh) {
          child.geometry.dispose();
          if (Array.isArray(child.material)) {
            child.material.forEach(m => m.dispose());
          } else {
            child.material.dispose();
          }
        }
      });
    }
    this.dyingUnaccounted.clear();
    this.lastCombatLogLength = 0;
    this.unaccountedAttackBoosts.clear();
  }

  private clearBloomEffects(): void {
    for (const cluster of this.bloomClusters) {
      this.scene.remove(cluster.mesh);
      cluster.dispose();
    }
    this.bloomClusters = [];
    for (const particles of this.bloomParticles) {
      this.scene.remove(particles.points);
      particles.dispose();
    }
    this.bloomParticles = [];
    for (const hazard of this.bloomHazards) {
      this.scene.remove(hazard.mesh);
      hazard.dispose();
    }
    this.bloomHazards = [];
    this.bloomEffectsAdded = false;
    this.nextBloomMutation = 0;
  }

  private updateCreatures(state: GameState): void {
    if (state.mode !== 'Combat' || !state.combat) {
      this.clearCreatures();
      return;
    }
    const mats = getCreatureMaterials(this.currentDungeonType ?? '', this.currentTheme);
    const enemies = state.combat.combatants.filter(c => !c.isPlayer && c.alive);
    const alive = new Set(enemies.map(c => c.id));
    // Detect unaccounted attacks for wrong-speed animation
    this.detectUnaccountedAttacks(state);

    for (const [id, mesh] of this.creatureMeshes) {
      if (!alive.has(id)) {
        const isUnaccounted = mesh.userData.isUnaccounted ?? false;
        if (isUnaccounted && mesh instanceof THREE.Group) {
          // Start death animation instead of instant removal
          if (!this.dyingUnaccounted.has(id)) {
            this.dyingUnaccounted.set(id, { mesh, startTime: performance.now() * 0.001 });
          }
        } else {
          this.scene.remove(mesh);
          mesh.traverse((child) => {
            if (child instanceof THREE.Mesh) {
              child.geometry.dispose();
              if (Array.isArray(child.material)) {
                child.material.forEach(m => m.dispose());
              } else {
                child.material.dispose();
              }
            }
          });
          this.creatureMeshes.delete(id);
        }
      }
    }
    const px = state.player.x * this.tileSize;
    const pz = state.player.y * this.tileSize;
    const rad = this.facingToRadians(state.player.facing);
    const fx = Math.sin(rad);
    const fz = -Math.cos(rad);
    const rx = Math.cos(rad);
    const rz = Math.sin(rad);
    let fi = 0;
    let bi = 0;
    for (const e of enemies) {
      if (this.creatureMeshes.has(e.id)) continue;
      const front = e.row === 0;
      const i = front ? fi++ : bi++;
      const d = front ? 3 : 5;
      const o = (i - 1) * 1.2;
      const mesh = e.isUnaccounted
        ? this.createUnaccountedMesh()
        : this.createCreatureMesh(mats);
      mesh.position.set(px + fx * d + rx * o, 0.75, pz + fz * d + rz * o);
      if (e.isUnaccounted) {
        mesh.userData.isUnaccounted = true;
        mesh.userData.combatantId = e.id;
        mesh.userData.baseY = 0.75;
        mesh.userData.twitchSeed = Math.random() * 100;
        mesh.userData.speed = 0.5 + Math.random() * 1.5; // wrong-speed
      }
      this.creatureMeshes.set(e.id, mesh);
      this.scene.add(mesh);
    }
    this.updateChromaticAberration();
  }

  private createCreatureMesh(mats: CreatureMaterialSet): THREE.Mesh {
    const geo = new THREE.SphereGeometry(0.4, 16, 12);
    const mat = new THREE.MeshStandardMaterial({
      color: mats.body,
      emissive: mats.emissive,
      emissiveIntensity: mats.emissive ? 0.6 : 0,
      roughness: 0.7,
    });
    const mesh = new THREE.Mesh(geo, mat);
    mesh.castShadow = true;
    return mesh;
  }

  private unaccountedMaterial: THREE.ShaderMaterial = createUnaccountedMaterial();

  private createUnaccountedMesh(): THREE.Group {
    const group = new THREE.Group();

    // Distorted main body — stretched vertically wrong with custom shader
    const bodyGeo = new THREE.SphereGeometry(0.3, 8, 6);
    bodyGeo.scale(0.6, 1.6, 0.6);
    const body = new THREE.Mesh(bodyGeo, this.unaccountedMaterial.clone());
    body.position.y = 0.4;
    group.add(body);

    // Wireframe shell — glitchy aura
    const shellGeo = new THREE.IcosahedronGeometry(0.55, 0);
    const shellMat = new THREE.MeshBasicMaterial({
      color: 0xaa00ff,
      wireframe: true,
      transparent: true,
      opacity: 0.25,
    });
    const shell = new THREE.Mesh(shellGeo, shellMat);
    shell.position.y = 0.4;
    group.add(shell);

    // Wrong limb — too long, wrong angle
    const limbGeo = new THREE.CylinderGeometry(0.04, 0.02, 0.9, 4);
    const limbMat = new THREE.MeshStandardMaterial({
      color: 0x2a1a3a,
      emissive: 0x330033,
      emissiveIntensity: 0.4,
    });
    const limb1 = new THREE.Mesh(limbGeo, limbMat);
    limb1.position.set(0.25, 0.5, 0.1);
    limb1.rotation.z = -0.6;
    limb1.rotation.x = 0.3;
    group.add(limb1);

    const limb2 = new THREE.Mesh(limbGeo, limbMat);
    limb2.position.set(-0.2, 0.3, -0.15);
    limb2.rotation.z = 0.8;
    limb2.rotation.y = 0.5;
    group.add(limb2);

    // Floating fragment — detached geometry
    const fragGeo = new THREE.OctahedronGeometry(0.08, 0);
    const fragMat = new THREE.MeshBasicMaterial({ color: 0xff0044 });
    const frag = new THREE.Mesh(fragGeo, fragMat);
    frag.position.set(0.1, 0.9, 0.2);
    group.add(frag);

    return group;
  }

  private updateUnaccountedAnimations(time: number): void {
    // Update dying unaccounted (fold + fade) — gameplay feedback, always shown.
    this.updateUnaccountedDeathAnimations(time);

    // Motion reduction: skip the unsettling idle float/twitch/pulse/flicker.
    if (this.reduceMotion) return;

    for (const obj of this.creatureMeshes.values()) {
      if (!(obj instanceof THREE.Group) || obj.userData.baseY === undefined) continue;
      const seed = obj.userData.twitchSeed ?? 0;
      let speed = obj.userData.speed ?? 1;

      // Wrong-speed attack: temporary boost from combat log detection
      const boost = this.unaccountedAttackBoosts.get(obj.userData.combatantId ?? '');
      if (boost !== undefined) {
        const elapsed = time - boost;
        if (elapsed < 0.5) {
          speed *= (elapsed < 0.25 ? 2.0 : 0.5); // First half 2x, second half 0.5x
        } else {
          this.unaccountedAttackBoosts.delete(obj.userData.combatantId ?? '');
        }
      }

      // Float with wrong frequency
      obj.position.y = obj.userData.baseY + Math.sin(time * speed * 2 + seed) * 0.15;

      // Twitch — sudden jerky rotations
      const twitchPhase = (time * 3 + seed) % 1;
      if (twitchPhase < 0.05) {
        obj.rotation.z = Math.sin(time * 20 + seed) * 0.15;
        obj.rotation.x = Math.cos(time * 17 + seed) * 0.1;
      } else {
        obj.rotation.z *= 0.9;
        obj.rotation.x *= 0.9;
      }

      // Pulse scale wrong-speed
      const s = 1 + Math.sin(time * speed * 4 + seed) * 0.08;
      obj.scale.set(s, 1 / s, s);

      // Color inversion flicker — brief random flashes
      const flicker = Math.sin(time * 7 + seed) > 0.95 ? 1.0 : 0.0;
      obj.traverse((child) => {
        if (child instanceof THREE.Mesh && child.material instanceof THREE.ShaderMaterial) {
          child.material.uniforms.uInvert.value = flicker;
        }
      });
    }
  }

  private updateUnaccountedDeathAnimations(time: number): void {
    for (const [id, data] of this.dyingUnaccounted) {
      const elapsed = time - data.startTime;
      const mesh = data.mesh;

      // Fold: rotate limbs inward, compress vertically
      mesh.rotation.z = Math.sin(elapsed * 3) * 0.3;
      mesh.rotation.x = Math.cos(elapsed * 2.5) * 0.2;
      const foldScale = Math.max(0.1, 1.0 - elapsed * 0.8);
      mesh.scale.set(foldScale * 0.6, foldScale * 0.3, foldScale * 0.6);

      // Fade: reduce opacity over 2 seconds
      const opacity = Math.max(0, 1.0 - elapsed * 0.5);
      mesh.traverse((child) => {
        if (child instanceof THREE.Mesh) {
          if (child.material instanceof THREE.ShaderMaterial) {
            child.material.uniforms.uOpacity.value = opacity;
          } else if ('opacity' in child.material) {
            (child.material as THREE.Material & { opacity: number }).opacity = opacity;
            child.material.transparent = true;
          }
        }
      });

      // Remove after 2.5s
      if (elapsed > 2.5) {
        this.scene.remove(mesh);
        mesh.traverse((child) => {
          if (child instanceof THREE.Mesh) {
            child.geometry.dispose();
            if (Array.isArray(child.material)) {
              child.material.forEach(m => m.dispose());
            } else {
              child.material.dispose();
            }
          }
        });
        this.dyingUnaccounted.delete(id);
        this.creatureMeshes.delete(id);
      }
    }
  }

  private detectUnaccountedAttacks(state: GameState): void {
    const combat = state.combat;
    if (!combat) return;
    if (combat.log.length <= this.lastCombatLogLength) {
      this.lastCombatLogLength = combat.log.length;
      return;
    }
    const newEntries = combat.log.slice(this.lastCombatLogLength);
    const unaccountedNames = new Set(
      combat.combatants.filter(c => c.isUnaccounted).map(c => c.name)
    );
    for (const entry of newEntries) {
      if (unaccountedNames.has(entry.actor) && entry.message.toLowerCase().includes('attack')) {
        // Find the combatant id for this actor
        const combatant = combat.combatants.find(c => c.name === entry.actor);
        if (combatant) {
          this.unaccountedAttackBoosts.set(combatant.id, performance.now() * 0.001);
        }
      }
    }
    this.lastCombatLogLength = combat.log.length;
  }

  private updateUnaccountedShaderTime(time: number): void {
    for (const obj of this.creatureMeshes.values()) {
      if (!(obj instanceof THREE.Group)) continue;
      obj.traverse((child) => {
        if (child instanceof THREE.Mesh && child.material instanceof THREE.ShaderMaterial) {
          child.material.uniforms.uTime.value = time;
        }
      });
    }
  }

  private updateChromaticAberration(): void {
    const hasAliveUnaccounted = Array.from(this.creatureMeshes.values()).some(
      (obj) => obj.userData.isUnaccounted === true
    );
    const hasDyingUnaccounted = this.dyingUnaccounted.size > 0;
    const canvas = this.renderer.domElement;
    if (hasAliveUnaccounted || hasDyingUnaccounted) {
      canvas.style.filter =
        'drop-shadow(2px 0 0 rgba(255,0,0,0.25)) drop-shadow(-2px 0 0 rgba(0,255,255,0.25))';
    } else {
      canvas.style.filter = '';
    }
  }

  private setupAmbientParticles(dungeonType: string | undefined): void {
    if (this.ambientParticleSystem) {
      this.scene.remove(this.ambientParticleSystem.mesh);
      this.ambientParticleSystem.dispose();
      this.ambientParticleSystem = null;
    }

    const preset = dungeonType ? getParticlePreset(dungeonType) : null;
    if (preset) {
      this.ambientParticleSystem = new AmbientParticleSystem(preset);
      this.scene.add(this.ambientParticleSystem.mesh);
    }
  }

  private addBloomEffects(tiles: Tile[]): void {
    if (this.bloomEffectsAdded) return;
    if (this.currentDungeonType !== 'bloom-site') return;

    let floorIndex = 0;
    for (const tile of tiles) {
      if (tile.type !== 'Floor') continue;
      if (floorIndex % 3 === 0) {
        const fx = tile.x * this.tileSize;
        const fz = tile.y * this.tileSize;
        const clusterPos = new THREE.Vector3(fx, 0.15, fz);
        const cluster = new BloomCluster(clusterPos, this.currentTheme);
        this.bloomClusters.push(cluster);
        this.scene.add(cluster.mesh);

        // Mark the contaminated tile beneath each cluster with a hazard overlay.
        const hazard = new BloomHazardOverlay(clusterPos, this.currentTheme, this.tileSize * 0.62);
        this.bloomHazards.push(hazard);
        this.scene.add(hazard.mesh);
      }
      floorIndex++;
    }

    const center = new THREE.Vector3(0, 0, 0);
    const particles = new BloomParticleSystem(center, this.currentTheme, 80);
    this.bloomParticles.push(particles);
    this.scene.add(particles.points);
    this.bloomEffectsAdded = true;
  }

  private renderTiles(tiles: Tile[]): void {
    // Build set of visible tile keys and border keys
    const visibleKeys = new Set<string>();
    for (const tile of tiles) {
      visibleKeys.add(`floor:${tile.x},${tile.y}`);
      if (tile.north !== 'None') visibleKeys.add(`border:${tile.x},${tile.y}:N`);
      if (tile.south !== 'None') visibleKeys.add(`border:${tile.x},${tile.y}:S`);
      if (tile.east !== 'None') visibleKeys.add(`border:${tile.x},${tile.y}:E`);
      if (tile.west !== 'None') visibleKeys.add(`border:${tile.x},${tile.y}:W`);
    }

    // Remove meshes that are no longer visible
    for (const [key, mesh] of this.tileMeshes) {
      if (key === 'default' || key === 'marker') continue;
      if (!visibleKeys.has(key)) {
        this.scene.remove(mesh);
        mesh.geometry.dispose();
        (mesh.material as THREE.Material).dispose();
        this.tileMeshes.delete(key);
      }
    }

    this.addBloomEffects(tiles);

    // Add or update tiles
    for (const tile of tiles) {
      const fx = tile.x * this.tileSize;
      const fz = tile.y * this.tileSize;

      // Floor / stairs
      const floorKey = `floor:${tile.x},${tile.y}`;
      if (!this.tileMeshes.has(floorKey)) {
        const mesh = this.createBaseMesh(tile, fx, fz);
        if (mesh) {
          this.tileMeshes.set(floorKey, mesh);
          this.scene.add(mesh);
        }
      }

      // Borders
      if (tile.north !== 'None') {
        const key = `border:${tile.x},${tile.y}:N`;
        if (!this.tileMeshes.has(key)) {
          const mesh = this.createBorderPanel(fx, fz, 'north', tile.north);
          this.tileMeshes.set(key, mesh);
          this.scene.add(mesh);
        }
      }
      if (tile.south !== 'None') {
        const key = `border:${tile.x},${tile.y}:S`;
        if (!this.tileMeshes.has(key)) {
          const mesh = this.createBorderPanel(fx, fz, 'south', tile.south);
          this.tileMeshes.set(key, mesh);
          this.scene.add(mesh);
        }
      }
      if (tile.east !== 'None') {
        const key = `border:${tile.x},${tile.y}:E`;
        if (!this.tileMeshes.has(key)) {
          const mesh = this.createBorderPanel(fx, fz, 'east', tile.east);
          this.tileMeshes.set(key, mesh);
          this.scene.add(mesh);
        }
      }
      if (tile.west !== 'None') {
        const key = `border:${tile.x},${tile.y}:W`;
        if (!this.tileMeshes.has(key)) {
          const mesh = this.createBorderPanel(fx, fz, 'west', tile.west);
          this.tileMeshes.set(key, mesh);
          this.scene.add(mesh);
        }
      }
    }
  }

  private createBaseMesh(tile: Tile, x: number, z: number): THREE.Mesh | null {
    switch (tile.type) {
      case 'Floor':
        return this.createFloor(x, z);
      case 'StairsUp':
        return this.createStairs(x, z, true);
      case 'StairsDown':
        return this.createStairs(x, z, false);
      case 'IllusoryFloor':
        return this.createFloor(x, z, true);
      case 'Empty':
        return null;
      default:
        return this.createFloor(x, z);
    }
  }

  private createFloor(x: number, z: number, illusory = false): THREE.Mesh {
    const geometry = new THREE.PlaneGeometry(this.tileSize * 0.95, this.tileSize * 0.95);
    const material = new THREE.MeshStandardMaterial({
      map: this.floorTexture,
      // A revealed illusory floor reads as a darkened, sunken pit trap.
      roughness: illusory ? 1 : 0.8,
      color: illusory ? 0x4a3530 : 0xffffff
    });
    const mesh = new THREE.Mesh(geometry, material);
    mesh.rotation.x = -Math.PI / 2;
    // Recess the pit slightly below the surrounding floor plane.
    mesh.position.set(x, illusory ? -0.12 : 0, z);
    mesh.receiveShadow = true;
    if (illusory) mesh.name = 'illusory-floor';
    return mesh;
  }

  private createBorderPanel(x: number, z: number, side: 'north' | 'south' | 'east' | 'west', borderType: string): THREE.Mesh {
    const isDoor = borderType === 'Door';
    const isSecret = borderType === 'SecretDoor';
    const isBreakable = borderType === 'BreakableWall';
    const isCompartment = borderType === 'ConcealedCompartment';

    let geometry: THREE.BoxGeometry;
    let material: THREE.MeshStandardMaterial;

    if (isDoor) {
      geometry = new THREE.BoxGeometry(
        side === 'east' || side === 'west' ? this.wallThickness * 0.8 : this.tileSize * 0.85,
        this.wallHeight * 0.95,
        side === 'north' || side === 'south' ? this.wallThickness * 0.8 : this.tileSize * 0.85
      );
      material = new THREE.MeshStandardMaterial({
        map: this.doorTexture,
        roughness: 0.7
      });
    } else if (isBreakable) {
      // Slightly inset + thicker bump to read as compromised structure
      geometry = new THREE.BoxGeometry(
        side === 'east' || side === 'west' ? this.wallThickness * 1.15 : this.tileSize * 0.98,
        this.wallHeight * 0.98,
        side === 'north' || side === 'south' ? this.wallThickness * 1.15 : this.tileSize * 0.98
      );
      this.jitterVertices(geometry, 0.04);
      const tint = this.currentTheme.breakableWall ?? this.currentTheme.secretDoor;
      material = new THREE.MeshStandardMaterial({
        map: this.breakableWallTexture,
        roughness: 0.95,
        bumpMap: this.breakableWallTexture,
        bumpScale: 0.22,
        color: tint,
        transparent: true,
        opacity: 1
      });
    } else if (isCompartment) {
      // A wall with a faint seam betraying a hidden compartment — wall-like, secret-tinted.
      geometry = new THREE.BoxGeometry(
        side === 'east' || side === 'west' ? this.wallThickness * 1.05 : this.tileSize,
        this.wallHeight,
        side === 'north' || side === 'south' ? this.wallThickness * 1.05 : this.tileSize
      );
      material = new THREE.MeshStandardMaterial({
        map: this.wallTexture,
        roughness: 0.85,
        bumpMap: this.wallTexture,
        bumpScale: 0.18,
        color: this.currentTheme.secretDoor
      });
    } else {
      geometry = new THREE.BoxGeometry(
        side === 'east' || side === 'west' ? this.wallThickness : this.tileSize,
        this.wallHeight,
        side === 'north' || side === 'south' ? this.wallThickness : this.tileSize
      );
      material = new THREE.MeshStandardMaterial({
        map: this.wallTexture,
        roughness: 0.9,
        bumpMap: this.wallTexture,
        bumpScale: 0.1,
        color: isSecret ? this.currentTheme.secretDoor : 0xffffff
      });
    }

    const mesh = new THREE.Mesh(geometry, material);

    switch (side) {
      case 'north':
        mesh.position.set(x, this.wallHeight / 2, z - this.tileSize / 2);
        break;
      case 'south':
        mesh.position.set(x, this.wallHeight / 2, z + this.tileSize / 2);
        break;
      case 'east':
        mesh.position.set(x + this.tileSize / 2, this.wallHeight / 2, z);
        break;
      case 'west':
        mesh.position.set(x - this.tileSize / 2, this.wallHeight / 2, z);
        break;
    }

    mesh.castShadow = true;
    mesh.receiveShadow = true;
    return mesh;
  }

  /**
   * Displace box vertices by up to `amplitude` units to break the flat-wall silhouette
   * on breakable walls. Cheaper than custom geometry and keeps UVs valid.
   */
  private jitterVertices(geometry: THREE.BoxGeometry, amplitude: number): void {
    const pos = geometry.attributes.position;
    for (let i = 0; i < pos.count; i++) {
      pos.setX(i, pos.getX(i) + (Math.random() - 0.5) * amplitude);
      pos.setY(i, pos.getY(i) + (Math.random() - 0.5) * amplitude * 0.5);
      pos.setZ(i, pos.getZ(i) + (Math.random() - 0.5) * amplitude);
    }
    pos.needsUpdate = true;
    geometry.computeVertexNormals();
  }

  /**
   * Trigger the break animation for a wall at (tileX, tileY) on the given side.
   * Mesh fades + shrinks over BREAK_ANIMATION_MS and is then disposed and removed
   * from the scene. No-op if no mesh exists at that key.
   */
  breakWall(tileX: number, tileY: number, side: 'N' | 'S' | 'E' | 'W'): boolean {
    const key = `border:${tileX},${tileY}:${side}`;
    const mesh = this.tileMeshes.get(key);
    if (!mesh || this.breakingWalls.has(key)) return false;

    this.breakingWalls.set(key, {
      mesh,
      startTime: performance.now(),
      duration: DungeonRenderer.BREAK_ANIMATION_MS
    });
    return true;
  }

  private tickBreakingWalls(now: number): void {
    if (this.breakingWalls.size === 0) return;
    const finished: string[] = [];
    for (const [key, entry] of this.breakingWalls) {
      const t = Math.min(1, (now - entry.startTime) / entry.duration);
      const eased = 1 - (1 - t) * (1 - t);
      const material = entry.mesh.material as THREE.MeshStandardMaterial;
      material.opacity = 1 - eased;
      const scale = 1 - eased * 0.4;
      entry.mesh.scale.set(scale, Math.max(0.05, 1 - eased), scale);
      entry.mesh.position.y += (1 - t) * 0.002; // tiny upward shudder
      if (t >= 1) finished.push(key);
    }
    for (const key of finished) {
      const entry = this.breakingWalls.get(key)!;
      this.scene.remove(entry.mesh);
      entry.mesh.geometry.dispose();
      (entry.mesh.material as THREE.Material).dispose();
      this.tileMeshes.delete(key);
      this.breakingWalls.delete(key);
    }
  }

  private createStairs(x: number, z: number, isUp: boolean): THREE.Mesh {
    const geometry = new THREE.BoxGeometry(
      this.tileSize * 0.9,
      this.tileSize * 0.3,
      this.tileSize * 0.9
    );
    const material = new THREE.MeshStandardMaterial({
      color: isUp ? this.currentTheme.stairsUp : this.currentTheme.stairsDown,
      roughness: 0.8
    });
    const mesh = new THREE.Mesh(geometry, material);
    mesh.position.set(x, 0.15, z);
    mesh.receiveShadow = true;
    return mesh;
  }

  private updateCamera(player: { x: number; y: number; facing: string }): void {
    const x = player.x * this.tileSize;
    const z = player.y * this.tileSize;

    // Position camera at player position, eye level
    this.camera.position.set(x, 1.6, z);

    // Set rotation based on facing direction
    const facingRad = this.facingToRadians(player.facing);

    // Move camera target forward
    const targetDistance = 5;
    const targetX = x + Math.sin(facingRad) * targetDistance;
    const targetZ = z - Math.cos(facingRad) * targetDistance;
    this.camera.lookAt(targetX, 1.6, targetZ);
  }

  private facingToRadians(facing: string): number {
    switch (facing) {
      case 'North': return 0;
      case 'East': return Math.PI / 2;
      case 'South': return Math.PI;
      case 'West': return -Math.PI / 2;
      default: return 0;
    }
  }

  private handleResize(container: HTMLElement): void {
    if (this.isDisposed) return;

    const MIN_WIDTH = 800;
    const MIN_HEIGHT = 600;

    const width = Math.max(container.clientWidth || MIN_WIDTH, MIN_WIDTH);
    const height = Math.max(container.clientHeight || MIN_HEIGHT, MIN_HEIGHT);

    this.camera.aspect = width / height;
    this.camera.updateProjectionMatrix();
    this.renderer.setSize(width, height);
  }

  /** Set the camera vertical field of view (degrees). */
  setFov(fov: number): void {
    this.camera.fov = fov;
    this.camera.updateProjectionMatrix();
  }

  /** Set the render resolution scale (multiplies device pixel ratio). */
  setResolutionScale(scale: number): void {
    this.resolutionScale = scale;
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2) * scale);
  }

  /** When enabled, freeze unsettling idle creature motion (accessibility motion reduction). */
  setReduceMotion(value: boolean): void {
    this.reduceMotion = value;
  }

  private animate(): void {
    if (this.isDisposed) return;
    requestAnimationFrame(() => this.animate());
    const time = performance.now() * 0.001;
    // Periodically trigger a mutation transition on a random cluster.
    if (!this.reduceMotion && this.bloomClusters.length > 0 && time >= this.nextBloomMutation) {
      const idle = this.bloomClusters.filter((c) => !c.isMutating());
      if (idle.length > 0) {
        idle[Math.floor(Math.random() * idle.length)].mutate(time);
      }
      this.nextBloomMutation = time + 2.5 + Math.random() * 3.5;
    }
    for (const cluster of this.bloomClusters) {
      cluster.update(time);
    }
    for (const particles of this.bloomParticles) {
      particles.update();
    }
    for (const hazard of this.bloomHazards) {
      hazard.update(time);
    }
    this.ambientParticleSystem?.update(time);
    this.updateUnaccountedAnimations(time);
    this.updateUnaccountedShaderTime(time);
    this.updateAnimatedLighting(time);
    this.tickBreakingWalls(performance.now());
    this.renderer.render(this.scene, this.camera);
  }

  private updateAnimatedLighting(time: number): void {
    const type = this.currentDungeonType?.toLowerCase().replace(/_/g, '-');
    const baseIntensity = this.currentTheme.glowIntensity;

    // Universal subtle torch flicker
    const flicker = 1 + Math.sin(time * 10) * 0.03 + Math.sin(time * 23) * 0.02;
    this.torchLight.intensity = baseIntensity * flicker;

    switch (type) {
      case 'broken_engine': {
        // Emergency red strobe
        const strobe = Math.sin(time * 3) > 0.7 ? 1.5 : 1.0;
        this.torchLight.color.setHex(0xffaa44);
        this.torchLight.intensity = baseIntensity * flicker * strobe;
        break;
      }
      case 'bloom-site': {
        // Bioluminescent pulse
        const pulse = 1 + Math.sin(time * 2) * 0.3;
        this.torchLight.color.setHex(0x88ff44);
        this.torchLight.intensity = baseIntensity * pulse;
        break;
      }
      case 'sealed-vault': {
        // Ward hum — gentle blue oscillation
        const hum = 1 + Math.sin(time * 1.5) * 0.15;
        this.torchLight.color.setHex(0x44aaff);
        this.torchLight.intensity = baseIntensity * hum;
        break;
      }
      case 'crypt': {
        // Ghostly whisper — slow purple drift
        const drift = 1 + Math.sin(time * 0.8) * 0.2;
        this.torchLight.color.setHex(0x9966ff);
        this.torchLight.intensity = baseIntensity * drift;
        break;
      }
      default:
        // Standard torch flicker already applied
        break;
    }
  }

  dispose(): void {
    this.isDisposed = true;
    this.clearBloomEffects();
    this.clearCreatures();
    this.ambientParticleSystem?.dispose();
    this.ambientParticleSystem = null;
    this.clearBreakingWalls();
    this.renderer.dispose();
    this.wallTexture.dispose();
    this.floorTexture.dispose();
    this.doorTexture.dispose();
    this.breakableWallTexture.dispose();
    for (const mesh of this.tileMeshes.values()) {
      mesh.geometry.dispose();
      (mesh.material as THREE.Material).dispose();
    }
    this.tileMeshes.clear();
  }
}
