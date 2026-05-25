import * as THREE from 'three';
import type { DungeonTheme } from './DungeonTheme';

/** Deterministic per-cluster PRNG so a given tile always renders the same variant. */
function mulberry32(seed: number): () => number {
  let a = seed >>> 0;
  return () => {
    a |= 0;
    a = (a + 0x6d2b79f5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

function seedFromPosition(p: THREE.Vector3): number {
  // Quantize to the tile grid before hashing so jitter in y doesn't change the seed.
  const x = Math.round(p.x * 4);
  const z = Math.round(p.z * 4);
  return (x * 73856093) ^ (z * 19349663);
}

export type BloomVariant = 'pod' | 'lobe' | 'spire';
export const BLOOM_VARIANTS: readonly BloomVariant[] = ['pod', 'lobe', 'spire'];

/** Displace each vertex outward along its normal by a random amount for an organic, non-placeholder look. */
function jitterVertices(geometry: THREE.BufferGeometry, amount: number, rand: () => number): void {
  geometry.computeVertexNormals();
  const pos = geometry.attributes.position as THREE.BufferAttribute;
  const nrm = geometry.attributes.normal as THREE.BufferAttribute;
  for (let i = 0; i < pos.count; i++) {
    const d = (rand() - 0.5) * 2 * amount;
    pos.setXYZ(
      i,
      pos.getX(i) + nrm.getX(i) * d,
      pos.getY(i) + nrm.getY(i) * d,
      pos.getZ(i) + nrm.getZ(i) * d,
    );
  }
  pos.needsUpdate = true;
  geometry.computeVertexNormals();
}

/** Build one of several procedural bloom-creature meshes, replacing the old fixed sphere placeholder. */
export function createVariantGeometry(variant: BloomVariant, rand: () => number): THREE.BufferGeometry {
  let geo: THREE.BufferGeometry;
  switch (variant) {
    case 'lobe':
      // Clumpy multi-faceted blob.
      geo = new THREE.DodecahedronGeometry(0.17, 0);
      jitterVertices(geo, 0.06, rand);
      break;
    case 'spire':
      // Taller fruiting body.
      geo = new THREE.ConeGeometry(0.12, 0.42, 7, 2);
      geo.translate(0, 0.1, 0);
      jitterVertices(geo, 0.03, rand);
      break;
    case 'pod':
    default:
      // Rounded organic pod.
      geo = new THREE.IcosahedronGeometry(0.16, 1);
      jitterVertices(geo, 0.045, rand);
      break;
  }
  return geo;
}

/** Shift a color toward a sickly magenta to read as "mutated / hazardous". */
function mutationColorOf(base: THREE.Color): THREE.Color {
  const hsl = { h: 0, s: 0, l: 0 };
  base.getHSL(hsl);
  const c = new THREE.Color();
  c.setHSL((hsl.h + 0.42) % 1, Math.min(1, hsl.s + 0.2), Math.min(0.85, hsl.l + 0.15));
  return c;
}

export class BloomCluster {
  mesh: THREE.Mesh;
  readonly variant: BloomVariant;
  private baseScale = 1;
  private phase: number;
  private baseColor: THREE.Color;
  private mutColor: THREE.Color;

  // Expanding translucent shell used as the mutation transition VFX.
  private shell: THREE.Mesh;
  private mutating = false;
  private mutStart = 0;
  private static readonly MUT_DURATION = 1.4; // seconds

  constructor(position: THREE.Vector3, theme: DungeonTheme, variant?: BloomVariant) {
    const rand = mulberry32(seedFromPosition(position));
    this.variant = variant ?? BLOOM_VARIANTS[Math.floor(rand() * BLOOM_VARIANTS.length)];
    this.phase = rand() * Math.PI * 2;
    this.baseScale = 0.85 + rand() * 0.4;

    this.baseColor = new THREE.Color(theme.accentColor);
    this.mutColor = mutationColorOf(this.baseColor);

    const geometry = createVariantGeometry(this.variant, rand);
    const material = new THREE.MeshStandardMaterial({
      color: this.baseColor,
      emissive: this.baseColor,
      emissiveIntensity: 1.5,
      transparent: true,
      opacity: 0.9,
      roughness: 0.4,
    });
    this.mesh = new THREE.Mesh(geometry, material);
    this.mesh.position.copy(position);
    this.mesh.rotation.y = rand() * Math.PI * 2;

    const shellGeo = new THREE.SphereGeometry(0.22, 12, 12);
    const shellMat = new THREE.MeshBasicMaterial({
      color: this.mutColor,
      transparent: true,
      opacity: 0,
      blending: THREE.AdditiveBlending,
      depthWrite: false,
      side: THREE.BackSide,
    });
    this.shell = new THREE.Mesh(shellGeo, shellMat);
    this.mesh.add(this.shell);
  }

  isMutating(): boolean {
    return this.mutating;
  }

  /** Begin a mutation transition. No-op while one is already running. */
  mutate(time: number): void {
    if (this.mutating) return;
    this.mutating = true;
    this.mutStart = time;
  }

  update(time: number): void {
    const idlePulse = this.baseScale + Math.sin(time * 2 + this.phase) * 0.15;
    const mat = this.mesh.material as THREE.MeshStandardMaterial;
    const shellMat = this.shell.material as THREE.MeshBasicMaterial;

    if (this.mutating) {
      const t = (time - this.mutStart) / BloomCluster.MUT_DURATION;
      if (t >= 1) {
        // Settle back to the resting appearance.
        this.mutating = false;
        mat.color.copy(this.baseColor);
        mat.emissive.copy(this.baseColor);
        mat.emissiveIntensity = 1.5;
        shellMat.opacity = 0;
        this.shell.scale.setScalar(1);
        this.mesh.scale.setScalar(idlePulse);
        return;
      }
      // Color surges to the mutation hue at mid-transition, then eases back.
      const blend = Math.sin(t * Math.PI);
      mat.color.copy(this.baseColor).lerp(this.mutColor, blend);
      mat.emissive.copy(mat.color);
      mat.emissiveIntensity = 1.5 + blend * 2.5;
      // Shell expands outward and fades — the visible "transition" burst.
      const shellScale = 1 + t * 2.2;
      this.shell.scale.setScalar(shellScale);
      shellMat.opacity = (1 - t) * 0.5;
      // Body throbs during the change.
      const throb = idlePulse * (1 + blend * 0.3);
      this.mesh.scale.setScalar(throb);
      return;
    }

    this.mesh.scale.setScalar(idlePulse);
    mat.emissiveIntensity = 1.2 + Math.sin(time * 3 + this.phase) * 0.5;
  }

  dispose(): void {
    this.mesh.geometry.dispose();
    (this.mesh.material as THREE.Material).dispose();
    this.shell.geometry.dispose();
    (this.shell.material as THREE.Material).dispose();
  }
}

/** Flat pulsing ring laid on the floor to mark a bloom-contaminated hazard tile. */
export class BloomHazardOverlay {
  mesh: THREE.Mesh;
  private phase: number;

  constructor(position: THREE.Vector3, theme: DungeonTheme, radius = 0.62) {
    const color = mutationColorOf(new THREE.Color(theme.accentColor));
    const geometry = new THREE.RingGeometry(radius * 0.45, radius, 28);
    const material = new THREE.MeshBasicMaterial({
      color,
      transparent: true,
      opacity: 0.3,
      side: THREE.DoubleSide,
      depthWrite: false,
      blending: THREE.AdditiveBlending,
    });
    this.mesh = new THREE.Mesh(geometry, material);
    this.mesh.rotation.x = -Math.PI / 2;
    this.mesh.position.set(position.x, 0.02, position.z);
    this.phase = (Math.abs(seedFromPosition(position)) % 628) / 100;
  }

  update(time: number): void {
    const mat = this.mesh.material as THREE.MeshBasicMaterial;
    const pulse = 0.5 + Math.sin(time * 1.5 + this.phase) * 0.5;
    mat.opacity = 0.15 + pulse * 0.3;
    const scale = 0.95 + pulse * 0.1;
    this.mesh.scale.set(scale, scale, scale);
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
