export interface AmbientTrack {
  id: string;
  displayName: string;
  description: string;
  frequencies: number[];
  waveform: OscillatorType;
  modulation: number;
}

const TRACKS: Record<string, AmbientTrack> = {
  default: {
    id: 'default',
    displayName: 'Stone Silence',
    description: 'Generic dungeon ambient',
    frequencies: [110, 165],
    waveform: 'sine',
    modulation: 0.3,
  },
  'bloom-site': {
    id: 'bloom-site',
    displayName: 'Fungal Drip',
    description: 'Dripping spores and organic hum',
    frequencies: [80, 120, 180],
    waveform: 'triangle',
    modulation: 0.8,
  },
  'broken-engine': {
    id: 'broken-engine',
    displayName: 'Machine Hum',
    description: 'Low mechanical drone',
    frequencies: [55, 110, 220],
    waveform: 'sawtooth',
    modulation: 0.2,
  },
  'boneyard': {
    id: 'boneyard',
    displayName: 'Bone Sort',
    description: 'Bone-on-bone sorting and distant filing',
    frequencies: [90, 135],
    waveform: 'square',
    modulation: 0.4,
  },
  'sealed-vault': {
    id: 'sealed-vault',
    displayName: 'Ward Hum',
    description: 'Echoing drips and ward hums',
    frequencies: [150, 225, 300],
    waveform: 'sine',
    modulation: 0.5,
  },
  'settlement-gone-wrong': {
    id: 'settlement-gone-wrong',
    displayName: 'Wrong Silence',
    description: 'Silence and occasional screams',
    frequencies: [60, 90],
    waveform: 'sine',
    modulation: 1.2,
  },
  'ossuary': {
    id: 'ossuary',
    displayName: 'Memorial Chimes',
    description: 'Whispering and memorial chimes',
    frequencies: [200, 300, 450],
    waveform: 'triangle',
    modulation: 0.6,
  },
};

export function getAmbientTrack(dungeonType: string): AmbientTrack {
  return TRACKS[dungeonType] ?? TRACKS.default;
}

export class AmbientAudioManager {
  currentTrack: AmbientTrack | null = null;
  private ctx: AudioContext | null = null;
  private oscillators: OscillatorNode[] = [];
  private gainNode: GainNode | null = null;
  private lfo: OscillatorNode | null = null;
  private lfoGain: GainNode | null = null;
  private enabled = true;

  setEnabled(value: boolean): void {
    this.enabled = value;
    if (!value) {
      this.stop();
    } else if (this.currentTrack) {
      this.play(this.currentTrack.id);
    }
  }

  private getContext(): AudioContext {
    if (!this.ctx) {
      this.ctx = new AudioContext();
    }
    if (this.ctx.state === 'suspended') {
      this.ctx.resume();
    }
    return this.ctx;
  }

  play(trackId: string): void {
    const track = getAmbientTrack(trackId);
    if (this.currentTrack?.id === track.id) return;

    this.stopInternal();
    this.currentTrack = track;
    if (!this.enabled) return;

    try {
      const ctx = this.getContext();
      this.gainNode = ctx.createGain();
      this.gainNode.gain.setValueAtTime(0, ctx.currentTime);
      this.gainNode.gain.linearRampToValueAtTime(0.08, ctx.currentTime + 2);
      this.gainNode.connect(ctx.destination);

      // LFO for subtle modulation
      this.lfo = ctx.createOscillator();
      this.lfo.frequency.value = 0.1 + track.modulation * 0.2;
      this.lfoGain = ctx.createGain();
      this.lfoGain.gain.value = track.modulation * 5;
      this.lfo.connect(this.lfoGain);
      this.lfo.start();

      for (const freq of track.frequencies) {
        const osc = ctx.createOscillator();
        osc.type = track.waveform;
        osc.frequency.value = freq;

        const oscGain = ctx.createGain();
        oscGain.gain.value = 1 / track.frequencies.length;

        // Connect LFO to frequency for vibrato/tremolo effect
        if (this.lfoGain) {
          this.lfoGain.connect(osc.frequency);
        }

        osc.connect(oscGain);
        oscGain.connect(this.gainNode);
        osc.start();
        this.oscillators.push(osc);
      }
    } catch {
      // WebAudio not available — fallback to silent
    }
  }

  stop(): void {
    this.stopInternal();
    this.currentTrack = null;
  }

  private stopInternal(): void {
    if (this.gainNode && this.ctx) {
      const now = this.ctx.currentTime;
      this.gainNode.gain.cancelScheduledValues(now);
      this.gainNode.gain.setValueAtTime(this.gainNode.gain.value, now);
      this.gainNode.gain.linearRampToValueAtTime(0, now + 1);

      setTimeout(() => {
        for (const osc of this.oscillators) {
          try { osc.stop(); } catch {}
        }
        this.oscillators = [];
        try { this.lfo?.stop(); } catch {}
        this.lfo = null;
        this.lfoGain = null;
        this.gainNode = null;
      }, 1100);
    } else {
      for (const osc of this.oscillators) {
        try { osc.stop(); } catch {}
      }
      this.oscillators = [];
      try { this.lfo?.stop(); } catch {}
      this.lfo = null;
      this.lfoGain = null;
      this.gainNode = null;
    }
  }

  update(dungeonType: string | undefined): void {
    if (!dungeonType) {
      if (this.currentTrack) {
        this.stop();
      }
      return;
    }

    const target = getAmbientTrack(dungeonType);
    if (this.currentTrack?.id === target.id) {
      return;
    }

    this.play(target.id);
  }
}
