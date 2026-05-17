export interface SubtitleEntry {
  text: string;
  duration: number;
  timestamp: number;
}

export class SubtitleSystem {
  private entries: SubtitleEntry[] = [];
  private maxEntries = 3;

  add(text: string, durationMs = 2000): void {
    const entry: SubtitleEntry = {
      text,
      duration: durationMs,
      timestamp: performance.now(),
    };
    this.entries.push(entry);
    if (this.entries.length > this.maxEntries) {
      this.entries.shift();
    }
  }

  getActive(): SubtitleEntry[] {
    const now = performance.now();
    this.entries = this.entries.filter((e) => now - e.timestamp < e.duration);
    return this.entries;
  }

  clear(): void {
    this.entries = [];
  }
}
