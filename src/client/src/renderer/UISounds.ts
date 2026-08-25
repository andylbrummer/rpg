let ctx: AudioContext | null = null;

function ensureContext(): AudioContext {
  if (!ctx) {
    ctx = new AudioContext();
  }
  if (ctx.state === 'suspended') {
    ctx.resume();
  }
  return ctx;
}

export function playClick(): void {
  try {
    const audio = ensureContext();
    const osc = audio.createOscillator();
    const gain = audio.createGain();
    osc.type = 'sine';
    osc.frequency.setValueAtTime(800, audio.currentTime);
    osc.frequency.exponentialRampToValueAtTime(400, audio.currentTime + 0.05);
    gain.gain.setValueAtTime(0.05, audio.currentTime);
    gain.gain.exponentialRampToValueAtTime(0.001, audio.currentTime + 0.05);
    osc.connect(gain);
    gain.connect(audio.destination);
    osc.start();
    osc.stop(audio.currentTime + 0.06);
  } catch {
    // ignore
  }
}

export function playConfirm(): void {
  try {
    const audio = ensureContext();
    const osc = audio.createOscillator();
    const gain = audio.createGain();
    osc.type = 'sine';
    osc.frequency.setValueAtTime(523, audio.currentTime);
    osc.frequency.setValueAtTime(659, audio.currentTime + 0.08);
    gain.gain.setValueAtTime(0.06, audio.currentTime);
    gain.gain.exponentialRampToValueAtTime(0.001, audio.currentTime + 0.2);
    osc.connect(gain);
    gain.connect(audio.destination);
    osc.start();
    osc.stop(audio.currentTime + 0.22);
  } catch {
    // ignore
  }
}

export function playWarning(): void {
  try {
    const audio = ensureContext();
    const osc = audio.createOscillator();
    const gain = audio.createGain();
    osc.type = 'triangle';
    osc.frequency.setValueAtTime(300, audio.currentTime);
    osc.frequency.linearRampToValueAtTime(250, audio.currentTime + 0.15);
    gain.gain.setValueAtTime(0.06, audio.currentTime);
    gain.gain.exponentialRampToValueAtTime(0.001, audio.currentTime + 0.2);
    osc.connect(gain);
    gain.connect(audio.destination);
    osc.start();
    osc.stop(audio.currentTime + 0.22);
  } catch {
    // ignore
  }
}

export function playSynergyChime(): void {
  try {
    const audio = ensureContext();
    const osc = audio.createOscillator();
    const gain = audio.createGain();
    osc.type = 'sine';
    osc.frequency.setValueAtTime(880, audio.currentTime);
    osc.frequency.setValueAtTime(1100, audio.currentTime + 0.1);
    osc.frequency.setValueAtTime(1320, audio.currentTime + 0.2);
    gain.gain.setValueAtTime(0.06, audio.currentTime);
    gain.gain.exponentialRampToValueAtTime(0.001, audio.currentTime + 0.4);
    osc.connect(gain);
    gain.connect(audio.destination);
    osc.start();
    osc.stop(audio.currentTime + 0.42);
  } catch {
    // ignore
  }
}
