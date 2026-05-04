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

  constructor(container: HTMLElement) {
    // Minimum dimensions to ensure visibility
    const MIN_WIDTH = 800;
    const MIN_HEIGHT = 600;
    
    const width = Math.max(container.clientWidth || MIN_WIDTH, MIN_WIDTH);
    const height = Math.max(container.clientHeight || MIN_HEIGHT, MIN_HEIGHT);
    
    console.log('DungeonRenderer: Initializing with container', container.clientWidth, 'x', container.clientHeight, '-> using', width, 'x', height);
    
    // Scene setup
    this.scene = new THREE.Scene();
    this.scene.background = new THREE.Color(0x111111);

    // Camera
    this.camera = new THREE.PerspectiveCamera(75, width / height, 0.1, 1000);

    // Renderer
    this.renderer = new THREE.WebGLRenderer({ antialias: true });
    this.renderer.setSize(width, height);
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    this.renderer.shadowMap.enabled = true;
    
    // Ensure canvas fills container
    this.renderer.domElement.style.width = '100%';
    this.renderer.domElement.style.height = '100%';
    this.renderer.domElement.style.display = 'block';
    
    container.appendChild(this.renderer.domElement);
    console.log('DungeonRenderer: Canvas appended to container');

    // Lighting
    const ambientLight = new THREE.AmbientLight(0x404040, 0.5);
    this.scene.add(ambientLight);

    const torchLight = new THREE.PointLight(0xffaa44, 1, 20);
    torchLight.position.set(0, 2, 0);
    torchLight.castShadow = true;
    this.scene.add(torchLight);
    
    // Add a debug light to ensure things are visible
    const debugLight = new THREE.DirectionalLight(0xffffff, 0.5);
    debugLight.position.set(5, 10, 5);
    this.scene.add(debugLight);

    // Handle resize
    window.addEventListener('resize', () => this.handleResize(container));

    // Start render loop
    this.animate();
    console.log('DungeonRenderer: Initialization complete');
  }

  updateState(state: GameState): void {
    console.log('DungeonRenderer: updateState called, hasDungeon:', state.hasDungeon, 'tiles:', state.tiles.length);
    this.currentState = state;
    
    if (state.hasDungeon) {
      this.renderTiles(state.tiles);
      this.updateCamera(state.player);
    } else {
      // Show a default floor when no dungeon is loaded
      this.renderDefaultScene();
    }
  }

  private renderDefaultScene(): void {
    console.log('DungeonRenderer: Rendering default scene');
    // Clear existing tiles
    for (const [key, mesh] of this.tileMeshes) {
      this.scene.remove(mesh);
      mesh.geometry.dispose();
      (mesh.material as THREE.Material).dispose();
    }
    this.tileMeshes.clear();
    
    // Add a simple floor
    const geometry = new THREE.PlaneGeometry(10, 10);
    const material = new THREE.MeshStandardMaterial({ color: 0x444444 });
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
    console.log('DungeonRenderer: Default scene rendered');
  }

  private renderTiles(tiles: Tile[]): void {
    console.log('DungeonRenderer: Rendering', tiles.length, 'tiles');
    
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
        this.tileMeshes.set(key, mesh);
        this.scene.add(mesh);
        added++;
      }
    }
    console.log('DungeonRenderer: Added', added, 'new tiles');
  }

  private createTileMesh(tile: Tile): THREE.Mesh {
    const x = tile.x * this.tileSize;
    const z = tile.y * this.tileSize;

    switch (tile.type) {
      case 'Floor':
        return this.createFloor(x, z);
      case 'Wall':
        return this.createWall(x, z);
      case 'Door':
        return this.createDoor(x, z);
      default:
        return this.createFloor(x, z);
    }
  }

  private createFloor(x: number, z: number): THREE.Mesh {
    const geometry = new THREE.PlaneGeometry(this.tileSize * 0.95, this.tileSize * 0.95);
    const material = new THREE.MeshStandardMaterial({ 
      color: 0x555555,
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
      color: 0x888888,
      roughness: 0.9
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
      color: 0x8B4513,
      roughness: 0.7
    });
    const mesh = new THREE.Mesh(geometry, material);
    mesh.position.set(x, this.wallHeight / 2, z);
    mesh.castShadow = true;
    return mesh;
  }

  private updateCamera(player: { x: number; y: number; facing: string }): void {
    const x = player.x * this.tileSize;
    const z = player.y * this.tileSize;

    // Position camera at player position, eye level
    this.camera.position.set(x, 1.6, z);

    // Set rotation based on facing direction
    const facingRad = this.facingToRadians(player.facing);
    this.camera.rotation.set(0, facingRad, 0);

    // Move camera target forward
    const targetDistance = 5;
    const targetX = x + Math.sin(facingRad) * targetDistance;
    const targetZ = z + Math.cos(facingRad) * targetDistance;
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
    
    console.log('DungeonRenderer: Resizing to', width, 'x', height);
    
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
    for (const mesh of this.tileMeshes.values()) {
      mesh.geometry.dispose();
      (mesh.material as THREE.Material).dispose();
    }
    this.tileMeshes.clear();
  }
}
