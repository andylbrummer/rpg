import * as THREE from 'three';
import type { DungeonTheme } from './DungeonTheme';

export class BloomCluster {
  mesh: THREE.Mesh;
  private baseScale = 1;
  private phase: number;

  constructor(position: THREE.Vector3, theme: DungeonTheme) {
    const geometry = new THREE.SphereGeometry(0.15, 8, 8);
    const material = new THREE.MeshStandardMaterial({
      color: theme.accentColor,
      emissive: theme.accentColor,
      emissiveIntensity: 1.5,
      transparent: true,
      opacity: 0.9,
    });
    this.mesh = new THREE.Mesh(geometry, material);
    this.mesh.position.copy(position);
    this.phase = Math.random() * Math.PI * 2;
  }

  update(time: number): void {
    const scale = this.baseScale + Math.sin(time * 2 + this.phase) * 0.15;
    this.mesh.scale.set(scale, scale, scale);
    const mat = this.mesh.material as THREE.MeshStandardMaterial;
    mat.emissiveIntensity = 1.2 + Math.sin(time * 3 + this.phase) * 0.5;
  }

  dispose(): void {
    this.mesh.geometry.dispose();
    (this.mesh.material as THREE.Material).dispose();
  }
}

export class BloomParticleSystem {
  points: THREE.Points;
  private velocities: Float32Array;
  private count: number;

  constructor(position: THREE.Vector3, theme: DungeonTheme, count = 80) {
    this.count = count;
    const geometry = new THREE.BufferGeometry();
    const positions = new Float32Array(count * 3);
    this.velocities = new Float32Array(count * 3);

    for (let i = 0; i < count; i++) {
      positions[i * 3] = position.x + (Math.random() - 0.5) * 8;
      positions[i * 3 + 1] = position.y + Math.random() * 4;
      positions[i * 3 + 2] = position.z + (Math.random() - 0.5) * 8;
      this.velocities[i * 3] = (Math.random() - 0.5) * 0.003;
      this.velocities[i * 3 + 1] = 0.002 + Math.random() * 0.005;
      this.velocities[i * 3 + 2] = (Math.random() - 0.5) * 0.003;
    }

    geometry.setAttribute('position', new THREE.BufferAttribute(positions, 3));

    const material = new THREE.PointsMaterial({
      color: theme.accentColor,
      size: 0.08,
      transparent: true,
      opacity: 0.7,
      blending: THREE.AdditiveBlending,
      depthWrite: false,
    });

    this.points = new THREE.Points(geometry, material);
  }

  update(): void {
    const positions = this.points.geometry.attributes.position.array as Float32Array;
    for (let i = 0; i < this.count; i++) {
      positions[i * 3] += this.velocities[i * 3];
      positions[i * 3 + 1] += this.velocities[i * 3 + 1];
      positions[i * 3 + 2] += this.velocities[i * 3 + 2];

      if (positions[i * 3 + 1] > 5) {
        positions[i * 3 + 1] = 0.1;
        positions[i * 3] += (Math.random() - 0.5) * 0.5;
        positions[i * 3 + 2] += (Math.random() - 0.5) * 0.5;
      }
    }
    this.points.geometry.attributes.position.needsUpdate = true;
  }

  dispose(): void {
    this.points.geometry.dispose();
    (this.points.material as THREE.Material).dispose();
  }
}
