export interface AmbientTrack {
  id: string;
  displayName: string;
  description: string;
}

const TRACKS: Record<string, AmbientTrack> = {
  default: {
    id: 'default',
    displayName: 'Stone Silence',
    description: 'Generic dungeon ambient',
  },
  'bloom-site': {
    id: 'bloom-site',
    displayName: 'Fungal Drip',
    description: 'Dripping spores and organic hum',
  },
  'broken-engine': {
    id: 'broken-engine',
    displayName: 'Machine Hum',
    description: 'Low mechanical drone',
  },
};

export function getAmbientTrack(dungeonType: string): AmbientTrack {
  return TRACKS[dungeonType] ?? TRACKS.default;
}

export class AmbientAudioManager {
  currentTrack: AmbientTrack | null = null;

  play(trackId: string): void {
    const track = getAmbientTrack(trackId);
    this.currentTrack = track;
    console.log(`[AmbientAudio] Playing: ${track.id}_${track.displayName.toLowerCase().replace(/\s+/g, '_')}_loop`);
  }

  stop(): void {
    if (this.currentTrack) {
      console.log(`[AmbientAudio] Stopped: ${this.currentTrack.displayName}`);
    }
    this.currentTrack = null;
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
