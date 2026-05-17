import { SubtitleSystem } from './SubtitleSystem';
import type { GameState, CombatLogEntry } from '$shared/types/game';

export class UnaccountedAudioManager {
  private ctx: AudioContext | null = null;
  private droneNode: OscillatorNode | null = null;
  private droneGain: GainNode | null = null;
  private lfoNode: OscillatorNode | null = null;
  private lfoGain: GainNode | null = null;
  private subtitleSystem = new SubtitleSystem();
  private hasUnaccounted = false;
  private lastCombatRound = -1;
  private lastLogLength = 0;
  private warningPlayed = false;
  private baseFreq = 110; // A2

  get subtitles(): SubtitleSystem {
    return this.subtitleSystem;
  }

  private ensureContext(): AudioContext {
    if (!this.ctx) {
      this.ctx = new AudioContext();
    }
    if (this.ctx.state === 'suspended') {
      this.ctx.resume();
    }
    return this.ctx;
  }

  private startDrone(frequency: number, detune = 0, lfoRate = 0.2): void {
    const ctx = this.ensureContext();
    this.stopDrone();

    this.droneNode = ctx.createOscillator();
    this.droneGain = ctx.createGain();
    this.lfoNode = ctx.createOscillator();
    this.lfoGain = ctx.createGain();

    this.droneNode.type = 'sine';
    this.droneNode.frequency.value = frequency;
    this.droneNode.detune.value = detune;

    this.lfoNode.type = 'triangle';
    this.lfoNode.frequency.value = lfoRate;
    this.lfoGain.gain.value = frequency * 0.03;

    this.droneGain.gain.value = 0.02;

    this.lfoNode.connect(this.lfoGain);
    this.lfoGain.connect(this.droneNode.frequency);
    this.droneNode.connect(this.droneGain);
    this.droneGain.connect(ctx.destination);

    this.droneNode.start();
    this.lfoNode.start();
  }

  private stopDrone(): void {
    try {
      this.droneNode?.stop();
      this.lfoNode?.stop();
    } catch {}
    this.droneNode = null;
    this.lfoNode = null;
    this.droneGain = null;
    this.lfoGain = null;
  }

  private playGlitchCue(duration = 0.3): void {
    const ctx = this.ensureContext();
    const t = ctx.currentTime;

    // Reversed-envelope noise burst
    const bufferSize = ctx.sampleRate * duration;
    const buffer = ctx.createBuffer(1, bufferSize, ctx.sampleRate);
    const data = buffer.getChannelData(0);
    for (let i = 0; i < bufferSize; i++) {
      data[i] = (Math.random() * 2 - 1) * (1 - i / bufferSize);
    }

    const src = ctx.createBufferSource();
    src.buffer = buffer;
    src.playbackRate.value = 0.5 + Math.random() * 0.3;

    const filter = ctx.createBiquadFilter();
    filter.type = 'bandpass';
    filter.frequency.value = 800 + Math.random() * 600;
    filter.Q.value = 8;

    const gain = ctx.createGain();
    gain.gain.setValueAtTime(0.08, t);
    gain.gain.exponentialRampToValueAtTime(0.001, t + duration);

    src.connect(filter);
    filter.connect(gain);
    gain.connect(ctx.destination);
    src.start(t);
    src.stop(t + duration);
  }

  private playWarningCue(): void {
    const ctx = this.ensureContext();
    const t = ctx.currentTime;

    // Dissonant chord sweep
    const freqs = [146.83, 185.0, 220.0]; // D3, F#3, A3 — unsettling
    for (const f of freqs) {
      const osc = ctx.createOscillator();
      const gain = ctx.createGain();
      osc.type = 'sawtooth';
      osc.frequency.value = f;
      gain.gain.setValueAtTime(0.0, t);
      gain.gain.linearRampToValueAtTime(0.03, t + 0.5);
      gain.gain.linearRampToValueAtTime(0.0, t + 2.5);
      osc.connect(gain);
      gain.connect(ctx.destination);
      osc.start(t);
      osc.stop(t + 2.5);
    }

    this.subtitleSystem.add('[Wrong pitch drone]', 3000);
  }

  update(state: GameState | null): void {
    const combat = state?.combat;
    const unaccountedPresent = combat?.combatants.some(
      (c) => c.isUnaccounted && c.alive
    ) ?? false;

    // Warning on entering combat with Unaccounted
    if (unaccountedPresent && !this.hasUnaccounted && !this.warningPlayed) {
      this.warningPlayed = true;
      this.playWarningCue();
    }

    // Reset warning flag when leaving combat
    if (!combat && this.warningPlayed) {
      this.warningPlayed = false;
    }

    // Drone shift when Unaccounted are visible
    if (unaccountedPresent) {
      if (!this.hasUnaccounted) {
        // Shift to uncomfortable frequency
        this.startDrone(146.83, 15, 0.4); // D3 + detune, faster LFO
        this.subtitleSystem.add('[Unnatural silence]', 3000);
      }
    } else {
      if (this.hasUnaccounted) {
        this.stopDrone();
      }
    }

    this.hasUnaccounted = unaccountedPresent;

    // Detect Unaccounted action from combat log
    if (combat && combat.log.length > this.lastLogLength) {
      const newEntries = combat.log.slice(this.lastLogLength);
      for (const entry of newEntries) {
        if (this.isUnaccountedActor(entry, combat)) {
          this.playGlitchCue(0.4);
          this.subtitleSystem.add('[Reversed scream]', 2000);
        }
      }
    }
    this.lastLogLength = combat?.log.length ?? 0;
    this.lastCombatRound = combat?.round ?? -1;
  }

  private isUnaccountedActor(entry: CombatLogEntry, combat: NonNullable<GameState['combat']>): boolean {
    const actor = combat.combatants.find((c) => c.name === entry.actor);
    return actor?.isUnaccounted ?? false;
  }

  dispose(): void {
    this.stopDrone();
    this.ctx?.close();
    this.ctx = null;
  }
}
