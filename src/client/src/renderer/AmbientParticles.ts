import * as THREE from 'three';

export interface ParticlePreset {
  count: number;
  color: number;
  size: number;
  speed: number;
  spread: number;
  opacity: number;
}

const PRESETS: Record<string, ParticlePreset> = {
  'bloom-site': {
    count: 80,
    color: 0x88ff44,
    size: 0.08,
    speed: 0.3,
    spread: 8,
    opacity: 0.6,
  },
  'broken-engine': {
    count: 60,
    color: 0xff6600,
    size: 0.06,
    speed: 0.5,
    spread: 10,
    opacity: 0.4,
  },
  'boneyard': {
    count: 50,
    color: 0xdddddd,
    size: 0.05,
    speed: 0.2,
    spread: 12,
    opacity: 0.3,
  },
  'sealed-vault': {
    count: 40,
    color: 0xffd700,
    size: 0.1,
    speed: 0.15,
    spread: 6,
    opacity: 0.5,
  },
  'settlement-gone-wrong': {
    count: 70,
    color: 0x888888,
    size: 0.07,
    speed: 0.4,
    spread: 9,
    opacity: 0.35,
  },
  'ossuary': {
    count: 45,
    color: 0xaaccff,
    size: 0.09,
    speed: 0.25,
    spread: 7,
    opacity: 0.45,
  },
};

export function getParticlePreset(dungeonType: string): ParticlePreset | null {
  return PRESETS[dungeonType] ?? null;
}

export class AmbientParticleSystem {
  mesh: THREE.Points;
  private velocities: Float32Array;
  private preset: ParticlePreset;
  private bounds: number;

  constructor(preset: ParticlePreset, bounds: number = 20) {
    this.preset = preset;
    this.bounds = bounds;

    const geometry = new THREE.BufferGeometry();
    const positions = new Float32Array(preset.count * 3);
    this.velocities = new Float32Array(preset.count * 3);

    for (let i = 0; i < preset.count; i++) {
      positions[i * 3] = (Math.random() - 0.5) * preset.spread;
      positions[i * 3 + 1] = Math.random() * 3;
      positions[i * 3 + 2] = (Math.random() - 0.5) * preset.spread;

      this.velocities[i * 3] = (Math.random() - 0.5) * preset.speed * 0.5;
      this.velocities[i * 3 + 1] = Math.random() * preset.speed * 0.3;
      this.velocities[i * 3 + 2] = (Math.random() - 0.5) * preset.speed * 0.5;
    }

    geometry.setAttribute('position', new THREE.BufferAttribute(positions, 3));

    const material = new THREE.PointsMaterial({
      color: preset.color,
      size: preset.size,
      transparent: true,
      opacity: preset.opacity,
      blending: THREE.AdditiveBlending,
      depthWrite: false,
    });

    this.mesh = new THREE.Points(geometry, material);
  }

  update(time: number): void {
    const positions = this.mesh.geometry.attributes.position.array as Float32Array;
    const count = this.preset.count;

    for (let i = 0; i < count; i++) {
      const ix = i * 3;
      const iy = i * 3 + 1;
      const iz = i * 3 + 2;

      positions[ix] += this.velocities[ix] * 0.016;
      positions[iy] += this.velocities[iy] * 0.016;
      positions[iz] += this.velocities[iz] * 0.016;

      // Add subtle sine wave drift
      positions[ix] += Math.sin(time + i) * 0.002;
      positions[iy] += Math.cos(time * 0.7 + i) * 0.001;

      // Wrap around bounds
      if (positions[iy] > 3.5) {
        positions[iy] = 0;
        positions[ix] = (Math.random() - 0.5) * this.preset.spread;
        positions[iz] = (Math.random() - 0.5) * this.preset.spread;
      }
      if (Math.abs(positions[ix]) > this.bounds) {
        positions[ix] *= -0.9;
        this.velocities[ix] *= -0.9;
      }
      if (Math.abs(positions[iz]) > this.bounds) {
        positions[iz] *= -0.9;
        this.velocities[iz] *= -0.9;
      }
    }

    this.mesh.geometry.attributes.position.needsUpdate = true;
  }

  dispose(): void {
    this.mesh.geometry.dispose();
    (this.mesh.material as THREE.Material).dispose();
  }
}
