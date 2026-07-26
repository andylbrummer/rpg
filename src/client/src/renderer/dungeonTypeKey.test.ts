import { describe, it, expect } from 'vitest';
import { getTheme, dungeonTypeKey } from './DungeonTheme';
import { getParticlePreset } from './AmbientParticles';
import { torchProfileFor } from './TorchProfiles';
import { getAmbientTrack } from './AmbientAudio';

/**
 * Dungeon type ids reach the client in two spellings: content authored under the schemes uses
 * `broken_engine`, dungeon templates use `bloom-site`. Every renderer lookup therefore has to
 * agree on one canonical key, and the ones that skipped normalizing turned real content into a
 * silent no-op — no ambient particles, no dungeon-specific torch behaviour — with nothing failing
 * to point at.
 */
describe('dungeon type key', () => {
  it('canonicalizes both spellings to the same key', () => {
    expect(dungeonTypeKey('broken_engine')).toBe('broken-engine');
    expect(dungeonTypeKey('broken-engine')).toBe('broken-engine');
    expect(dungeonTypeKey('BLOOM_SITE')).toBe('bloom-site');
    expect(dungeonTypeKey(undefined)).toBe('');
  });

  it('resolves a theme regardless of which spelling arrives', () => {
    expect(getTheme('bloom_site')).toBe(getTheme('bloom-site'));
    expect(getTheme('sealed_vault')).toBe(getTheme('sealed-vault'));
  });

  it('resolves ambient particles regardless of which spelling arrives', () => {
    expect(getParticlePreset('broken_engine')).not.toBeNull();
    expect(getParticlePreset('broken_engine')).toBe(getParticlePreset('broken-engine'));
    expect(getParticlePreset('bloom_site')).toBe(getParticlePreset('bloom-site'));
  });

  it('resolves the ambient track regardless of which spelling arrives', () => {
    // Falling back to the default track is how this failed: audible, plausible, and wrong.
    expect(getAmbientTrack('broken_engine')).toBe(getAmbientTrack('broken-engine'));
    expect(getAmbientTrack('broken_engine')).not.toBe(getAmbientTrack('no-such-dungeon'));
  });

  it('has no preset for an unknown dungeon type', () => {
    expect(getParticlePreset('nowhere-at-all')).toBeNull();
  });
});

describe('torch profiles', () => {
  const BASE = 2;

  it('gives the broken engine its emergency strobe', () => {
    // The strobe is the whole point of the profile: at some moment in the cycle the torch must
    // run brighter than its unstrobed self.
    const samples = Array.from({ length: 200 }, (_, i) => torchProfileFor('broken_engine', BASE, i * 0.05));
    const peak = Math.max(...samples.map((s) => s.intensity));
    const floor = Math.min(...samples.map((s) => s.intensity));
    expect(peak).toBeGreaterThan(floor * 1.2);
    expect(samples[0].color).toBe(0xffaa44);
  });

  it('pulses the bloom site', () => {
    const samples = Array.from({ length: 200 }, (_, i) => torchProfileFor('bloom-site', BASE, i * 0.05));
    expect(Math.max(...samples.map((s) => s.intensity))).toBeGreaterThan(BASE);
    expect(samples[0].color).toBe(0x88ff44);
  });

  it('leaves an unthemed dungeon on the plain flicker and its theme colour', () => {
    const profile = torchProfileFor('somewhere-else', BASE, 1.5);
    expect(profile.color).toBeNull();
    // Flicker stays within a few percent of the base intensity.
    expect(profile.intensity).toBeGreaterThan(BASE * 0.9);
    expect(profile.intensity).toBeLessThan(BASE * 1.1);
  });

  it('scales with the theme base intensity rather than hard-coding one', () => {
    const dim = torchProfileFor('crypt', 1, 2.0);
    const bright = torchProfileFor('crypt', 4, 2.0);
    expect(bright.intensity).toBeCloseTo(dim.intensity * 4, 6);
  });
});
