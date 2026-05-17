import * as THREE from 'three';
import type { GameState, Tile } from '$shared/types/game';
import { getTheme, type DungeonTheme } from './DungeonTheme';
import { BloomCluster, BloomParticleSystem } from './BloomEffects';
import { getCreatureMaterials, type CreatureMaterialSet } from './CreatureMaterials';
import { createUnaccountedMaterial } from './UnaccountedMaterial';
import { AmbientParticleSystem, getParticlePreset } from './AmbientParticles';

export class DungeonRenderer {
  private scene: THREE.Scene;
  private camera: THREE.PerspectiveCamera;
  private renderer: THREE.WebGLRenderer;
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
  private currentTheme: DungeonTheme;
  private currentDungeonType: string | undefined;
  private bloomClusters: BloomCluster[] = [];
  private bloomParticles: BloomParticleSystem[] = [];
  private ambientParticleSystem: AmbientParticleSystem | null = null;
  private bloomEffectsAdded = false;
  private creatureMeshes: Map<string, THREE.Object3D> = new Map();

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

    // Scene setup
    this.scene = new THREE.Scene();
    this.scene.background = new THREE.Color(this.currentTheme.backgroundColor);
    this.scene.fog = new THREE.Fog(this.currentTheme.fogColor, 10, 30);

    // Camera
    this.camera = new THREE.PerspectiveCamera(75, width / height, 0.1, 1000);

    // Renderer
    this.renderer = new THREE.WebGLRenderer({ antialias: true });
    this.renderer.setSize(width, height);
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
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
    this.wallTexture = this.createBrickTexture(theme);
    this.floorTexture = this.createStoneTileTexture(theme);
    this.doorTexture = this.createWoodTexture(theme);

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
    for (const [key, mesh] of this.tileMeshes) {
      this.scene.remove(mesh);
      mesh.geometry.dispose();
      (mesh.material as THREE.Material).dispose();
    }
    this.tileMeshes.clear();
    this.clearBloomEffects();
  }

  private clearCreatures(): void {
    for (const [, mesh] of this.creatureMeshes) {
      this.scene.remove(mesh);
      mesh.geometry.dispose();
      (mesh.material as THREE.Material).dispose();
    }
    this.creatureMeshes.clear();
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
    this.bloomEffectsAdded = false;
  }

  private updateCreatures(state: GameState): void {
    if (state.mode !== 'Combat' || !state.combat) {
      this.clearCreatures();
      return;
    }
    const mats = getCreatureMaterials(this.currentDungeonType ?? '', this.currentTheme);
    const enemies = state.combat.combatants.filter(c => !c.isPlayer && c.alive);
    const alive = new Set(enemies.map(c => c.id));
    for (const [id, mesh] of this.creatureMeshes) {
      if (!alive.has(id)) {
        this.scene.remove(mesh);
        mesh.geometry.dispose();
        (mesh.material as THREE.Material).dispose();
        this.creatureMeshes.delete(id);
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
    for (const obj of this.creatureMeshes.values()) {
      if (!(obj instanceof THREE.Group) || obj.userData.baseY === undefined) continue;
      const seed = obj.userData.twitchSeed ?? 0;
      const speed = obj.userData.speed ?? 1;

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
    }
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
    const hasUnaccounted = Array.from(this.creatureMeshes.values()).some(
      (obj) => obj instanceof THREE.Group
    );
    const canvas = this.renderer.domElement;
    if (hasUnaccounted) {
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
        const cluster = new BloomCluster(new THREE.Vector3(fx, 0.15, fz), this.currentTheme);
        this.bloomClusters.push(cluster);
        this.scene.add(cluster.mesh);
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
      case 'Empty':
        return null;
      default:
        return this.createFloor(x, z);
    }
  }

  private createFloor(x: number, z: number): THREE.Mesh {
    const geometry = new THREE.PlaneGeometry(this.tileSize * 0.95, this.tileSize * 0.95);
    const material = new THREE.MeshStandardMaterial({
      map: this.floorTexture,
      roughness: 0.8
    });
    const mesh = new THREE.Mesh(geometry, material);
    mesh.rotation.x = -Math.PI / 2;
    mesh.position.set(x, 0, z);
    mesh.receiveShadow = true;
    return mesh;
  }

  private createBorderPanel(x: number, z: number, side: 'north' | 'south' | 'east' | 'west', borderType: string): THREE.Mesh {
    const isDoor = borderType === 'Door';
    const isSecret = borderType === 'SecretDoor';

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

  private animate(): void {
    if (this.isDisposed) return;
    requestAnimationFrame(() => this.animate());
    const time = performance.now() * 0.001;
    for (const cluster of this.bloomClusters) {
      cluster.update(time);
    }
    for (const particles of this.bloomParticles) {
      particles.update();
    }
    this.ambientParticleSystem?.update(time);
    this.updateUnaccountedAnimations(time);
    this.updateUnaccountedShaderTime(time);
    this.renderer.render(this.scene, this.camera);
  }

  dispose(): void {
    this.isDisposed = true;
    this.clearBloomEffects();
    this.clearCreatures();
    this.ambientParticleSystem?.dispose();
    this.ambientParticleSystem = null;
    this.renderer.dispose();
    this.wallTexture.dispose();
    this.floorTexture.dispose();
    this.doorTexture.dispose();
    for (const mesh of this.tileMeshes.values()) {
      mesh.geometry.dispose();
      (mesh.material as THREE.Material).dispose();
    }
    this.tileMeshes.clear();
  }
}
