import { dungeonTypeKey } from './DungeonTheme';

/** What the torch should look like at one instant: null colour means "keep the theme's". */
export interface TorchProfile {
  intensity: number;
  color: number | null;
}

/**
 * The universal flicker every dungeon gets, as a multiplier on the theme's glow intensity.
 * Two detuned sines so it reads as fire rather than as a pulse.
 */
function flicker(time: number): number {
  return 1 + Math.sin(time * 10) * 0.03 + Math.sin(time * 23) * 0.02;
}

/**
 * The torch's colour and intensity for a dungeon at a moment in time.
 *
 * <p>Kept as a pure function of (type, base intensity, time) so the per-dungeon lighting can be
 * asserted directly — it used to be a switch inside the render loop, keyed on a string spelled
 * one way in the switch and another way by the time it got there, so the broken engine's
 * emergency strobe never ran and nothing could notice.</p>
 */
export function torchProfileFor(dungeonType: string | undefined, baseIntensity: number, time: number): TorchProfile {
  const base = baseIntensity * flicker(time);

  switch (dungeonTypeKey(dungeonType)) {
    case 'broken-engine':
      // Emergency red strobe
      return { intensity: base * (Math.sin(time * 3) > 0.7 ? 1.5 : 1.0), color: 0xffaa44 };
    case 'bloom-site':
      // Bioluminescent pulse
      return { intensity: baseIntensity * (1 + Math.sin(time * 2) * 0.3), color: 0x88ff44 };
    case 'sealed-vault':
      // Ward hum — gentle blue oscillation
      return { intensity: baseIntensity * (1 + Math.sin(time * 1.5) * 0.15), color: 0x44aaff };
    case 'crypt':
      // Ghostly whisper — slow purple drift
      return { intensity: baseIntensity * (1 + Math.sin(time * 0.8) * 0.2), color: 0x9966ff };
    default:
      return { intensity: base, color: null };
  }
}
