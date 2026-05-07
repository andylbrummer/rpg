import * as THREE from 'three';
import type { GameState, Tile } from '../types/game';

export class DungeonRenderer {
  private scene: THREE.Scene;
  private camera: THREE.PerspectiveCamera;
  private renderer: THREE.WebGLRenderer;
  private tileMeshes: Map<string, THREE.Mesh> = new Map();
  private tileSize = 2;
  private wallHeight = 3;
  private currentState: GameState | null = null;
  private isDisposed = false;
  private torchLight: THREE.PointLight;
  private wallTexture: THREE.CanvasTexture;
  private floorTexture: THREE.CanvasTexture;
  private doorTexture: THREE.CanvasTexture;

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

    // Generate procedural textures
    this.wallTexture = this.createBrickTexture();
    this.floorTexture = this.createStoneTileTexture();
    this.doorTexture = this.createWoodTexture();

    // Scene setup
    this.scene = new THREE.Scene();
    this.scene.background = new THREE.Color(0x111111);
    this.scene.fog = new THREE.Fog(0x111111, 10, 30);

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
    const ambientLight = new THREE.AmbientLight(0x666666, 0.4);
    this.scene.add(ambientLight);

    this.torchLight = new THREE.PointLight(0xffaa44, 2, 25);
    this.torchLight.position.set(0, 2, 0);
    this.torchLight.castShadow = true;
    this.torchLight.shadow.mapSize.width = 512;
    this.torchLight.shadow.mapSize.height = 512;
    this.scene.add(this.torchLight);
    
    // Fill light from above
    const fillLight = new THREE.DirectionalLight(0xaaccff, 0.3);
    fillLight.position.set(5, 10, 5);
    this.scene.add(fillLight);

    // Rim light for depth
    const rimLight = new THREE.DirectionalLight(0xffddaa, 0.2);
    rimLight.position.set(-5, 3, -5);
    this.scene.add(rimLight);

    // Handle resize
    window.addEventListener('resize', () => this.handleResize(container));

    // Start render loop
    this.animate();
  }

  private createBrickTexture(): THREE.CanvasTexture {
    const canvas = document.createElement('canvas');
    canvas.width = 256;
    canvas.height = 256;
    const ctx = canvas.getContext('2d')!;

    // Base color
    ctx.fillStyle = '#7a5c4a';
    ctx.fillRect(0, 0, 256, 256);

    // Brick rows
    const brickHeight = 32;
    const brickWidth = 64;
    const rows = 256 / brickHeight;

    for (let row = 0; row < rows; row++) {
      const offset = (row % 2) * (brickWidth / 2);
      for (let col = -1; col < 5; col++) {
        const x = col * brickWidth + offset;
        const y = row * brickHeight;
        
        // Slight color variation per brick
        const hue = 20 + Math.random() * 10;
        const sat = 30 + Math.random() * 15;
        const light = 40 + Math.random() * 10;
        ctx.fillStyle = `hsl(${hue}, ${sat}%, ${light}%)`;
        ctx.fillRect(x + 1, y + 1, brickWidth - 2, brickHeight - 2);
        
        // Add some noise/texture
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

  private createStoneTileTexture(): THREE.CanvasTexture {
    const canvas = document.createElement('canvas');
    canvas.width = 256;
    canvas.height = 256;
    const ctx = canvas.getContext('2d')!;

    // Base stone color
    ctx.fillStyle = '#555555';
    ctx.fillRect(0, 0, 256, 256);

    // Tile grid
    const tileSize = 64;
    const cols = 256 / tileSize;
    const rows = 256 / tileSize;

    for (let row = 0; row < rows; row++) {
      for (let col = 0; col < cols; col++) {
        const x = col * tileSize;
        const y = row * tileSize;
        
        // Tile color variation
        const light = 45 + Math.random() * 15;
        ctx.fillStyle = `hsl(0, 0%, ${light}%)`;
        ctx.fillRect(x + 1, y + 1, tileSize - 2, tileSize - 2);
        
        // Stone noise
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

  private createWoodTexture(): THREE.CanvasTexture {
    const canvas = document.createElement('canvas');
    canvas.width = 128;
    canvas.height = 128;
    const ctx = canvas.getContext('2d')!;

    ctx.fillStyle = '#654321';
    ctx.fillRect(0, 0, 128, 128);

    // Wood grain lines
    for (let i = 0; i < 20; i++) {
      const y = Math.random() * 128;
      const width = 1 + Math.random() * 2;
      ctx.fillStyle = `rgba(60, 40, 20, ${0.2 + Math.random() * 0.3})`;
      ctx.fillRect(0, y, 128, width);
    }

    const texture = new THREE.CanvasTexture(canvas);
    texture.wrapS = THREE.RepeatWrapping;
    texture.wrapT = THREE.RepeatWrapping;
    return texture;
  }

  updateState(state: GameState): void {
    this.currentState = state;
    
    if (state.hasDungeon) {
      this.renderTiles(state.tiles);
      this.updateCamera(state.player);
      this.updateTorch(state.player);
    } else {
      this.renderDefaultScene();
    }
  }

  private updateTorch(player: { x: number; y: number }): void {
    const x = player.x * this.tileSize;
    const z = player.y * this.tileSize;
    this.torchLight.position.set(x, 2, z);
  }

  private renderDefaultScene(): void {
    this.clearTiles();
    
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
    const markerMat = new THREE.MeshStandardMaterial({ color: 0xff0000 });
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
  }

  private renderTiles(tiles: Tile[]): void {
    // Don't clear all tiles every time - only remove ones that are no longer visible
    const visibleKeys = new Set(tiles.map(t => `${t.x},${t.y}`));
    for (const [key, mesh] of this.tileMeshes) {
      if (!visibleKeys.has(key)) {
        this.scene.remove(mesh);
        mesh.geometry.dispose();
        (mesh.material as THREE.Material).dispose();
        this.tileMeshes.delete(key);
      }
    }

    // Add or update tiles
    let added = 0;
    for (const tile of tiles) {
      const key = `${tile.x},${tile.y}`;
      
      if (!this.tileMeshes.has(key)) {
        const mesh = this.createTileMesh(tile);
        if (mesh) {
          this.tileMeshes.set(key, mesh);
          this.scene.add(mesh);
          added++;
        }
      }
    }
  }

  private createTileMesh(tile: Tile): THREE.Mesh | null {
    const x = tile.x * this.tileSize;
    const z = tile.y * this.tileSize;

    switch (tile.type) {
      case 'Floor':
        return this.createFloor(x, z);
      case 'Wall':
        return this.createWall(x, z);
      case 'Door':
        return this.createDoor(x, z);
      case 'SecretDoor':
        return this.createSecretDoor(x, z);
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

  private createWall(x: number, z: number): THREE.Mesh {
    const geometry = new THREE.BoxGeometry(
      this.tileSize * 0.95, 
      this.wallHeight, 
      this.tileSize * 0.95
    );
    const material = new THREE.MeshStandardMaterial({ 
      map: this.wallTexture,
      roughness: 0.9,
      bumpMap: this.wallTexture,
      bumpScale: 0.1
    });
    const mesh = new THREE.Mesh(geometry, material);
    mesh.position.set(x, this.wallHeight / 2, z);
    mesh.castShadow = true;
    mesh.receiveShadow = true;
    return mesh;
  }

  private createDoor(x: number, z: number): THREE.Mesh {
    const geometry = new THREE.BoxGeometry(
      this.tileSize * 0.8, 
      this.wallHeight * 0.9, 
      this.tileSize * 0.2
    );
    const material = new THREE.MeshStandardMaterial({ 
      map: this.doorTexture,
      roughness: 0.7
    });
    const mesh = new THREE.Mesh(geometry, material);
    mesh.position.set(x, this.wallHeight / 2, z);
    mesh.castShadow = true;
    return mesh;
  }

  private createSecretDoor(x: number, z: number): THREE.Mesh {
    // Looks like a wall but slightly different
    const geometry = new THREE.BoxGeometry(
      this.tileSize * 0.95, 
      this.wallHeight, 
      this.tileSize * 0.95
    );
    const material = new THREE.MeshStandardMaterial({ 
      map: this.wallTexture,
      roughness: 0.9,
      color: 0x998877
    });
    const mesh = new THREE.Mesh(geometry, material);
    mesh.position.set(x, this.wallHeight / 2, z);
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
      color: isUp ? 0xccaa66 : 0x886644,
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
    this.renderer.render(this.scene, this.camera);
  }

  dispose(): void {
    this.isDisposed = true;
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
