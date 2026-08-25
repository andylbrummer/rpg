import type { PlayerAction } from '$shared/types/game';

export interface GamepadConfig {
  deadzone: number;
  repeatDelayMs: number;
  enabled: boolean;
}

const DEFAULT_CONFIG: GamepadConfig = {
  deadzone: 0.15,
  repeatDelayMs: 250,
  enabled: true,
};

export class GamepadManager {
  private config: GamepadConfig;
  private lastAxes = [0, 0];
  private lastButtons = new Map<number, boolean>();
  private repeatTimers = new Map<string, ReturnType<typeof setTimeout>>();
  private onAction: (action: PlayerAction) => void;
  private rafId: number | null = null;
  private connected = false;

  constructor(onAction: (action: PlayerAction) => void, config?: Partial<GamepadConfig>) {
    this.onAction = onAction;
    this.config = { ...DEFAULT_CONFIG, ...config };

    window.addEventListener('gamepadconnected', (e) => {
      this.connected = true;
      this.startPolling();
    });

    window.addEventListener('gamepaddisconnected', (e) => {
      const pads = navigator.getGamepads();
      this.connected = pads.some(p => p !== null);
      if (!this.connected) {
        this.stopPolling();
      }
    });

    // Check if already connected
    const pads = navigator.getGamepads();
    if (pads.some(p => p !== null)) {
      this.connected = true;
      this.startPolling();
    }
  }

  isConnected(): boolean {
    return this.connected;
  }

  setConfig(config: Partial<GamepadConfig>) {
    this.config = { ...this.config, ...config };
  }

  getConfig(): GamepadConfig {
    return { ...this.config };
  }

  dispose() {
    this.stopPolling();
    for (const timer of this.repeatTimers.values()) {
      clearTimeout(timer);
    }
    this.repeatTimers.clear();
  }

  private startPolling() {
    if (this.rafId !== null) return;
    const poll = () => {
      if (!this.config.enabled) {
        this.rafId = requestAnimationFrame(poll);
        return;
      }
      this.processGamepads();
      this.rafId = requestAnimationFrame(poll);
    };
    this.rafId = requestAnimationFrame(poll);
  }

  private stopPolling() {
    if (this.rafId !== null) {
      cancelAnimationFrame(this.rafId);
      this.rafId = null;
    }
  }

  private processGamepads() {
    const pads = navigator.getGamepads();
    for (const pad of pads) {
      if (!pad) continue;
      this.processAxes(pad.axes);
      this.processButtons(pad.buttons);
      break; // Only use first gamepad
    }
  }

  private processAxes(axes: readonly number[]) {
    const [lx, ly] = axes;
    const dz = this.config.deadzone;

    // Left stick → movement
    const x = Math.abs(lx) > dz ? lx : 0;
    const y = Math.abs(ly) > dz ? ly : 0;

    const prevX = Math.abs(this.lastAxes[0]) > dz ? this.lastAxes[0] : 0;
    const prevY = Math.abs(this.lastAxes[1]) > dz ? this.lastAxes[1] : 0;

    // Forward / back (Y axis inverted)
    if (y < -dz && prevY >= -dz) this.sendAction({ type: 'move_forward' });
    if (y > dz && prevY <= dz) this.sendAction({ type: 'move_back' });

    // Strafe left / right
    if (x < -dz && prevX >= -dz) this.sendAction({ type: 'strafe_left' });
    if (x > dz && prevX <= dz) this.sendAction({ type: 'strafe_right' });

    this.lastAxes = [lx, ly];
  }

  private processButtons(buttons: readonly GamepadButton[]) {
    // Standard mapping:
    // 0 = A (bottom) → confirm / interact
    // 1 = B (right) → cancel / back
    // 2 = X (left) → inventory / field notes
    // 3 = Y (top) → map / stats
    // 4 = LB → cycle target left
    // 5 = RB → cycle target right
    // 6 = LT → turn left
    // 7 = RT → turn right
    // 8 = Select / View → settings
    // 9 = Start / Menu → menu / save
    // 12 = D-pad Up
    // 13 = D-pad Down
    // 14 = D-pad Left
    // 15 = D-pad Right

    const mapping: Record<number, PlayerAction> = {
      0: { type: 'enter_combat' },
      1: { type: 'cancel' },
      2: { type: 'return_to_town' },
      6: { type: 'turn_left' },
      7: { type: 'turn_right' },
      12: { type: 'move_forward' },
      13: { type: 'move_back' },
      14: { type: 'strafe_left' },
      15: { type: 'strafe_right' },
    };

    for (const [idxStr, action] of Object.entries(mapping)) {
      const idx = parseInt(idxStr, 10);
      const pressed = buttons[idx]?.pressed ?? false;
      const wasPressed = this.lastButtons.get(idx) ?? false;

      if (pressed && !wasPressed) {
        this.sendAction(action);
      }

      this.lastButtons.set(idx, pressed);
    }
  }

  private sendAction(action: PlayerAction) {
    const key = JSON.stringify(action);
    if (this.repeatTimers.has(key)) return;

    this.onAction(action);
    const timer = setTimeout(() => {
      this.repeatTimers.delete(key);
    }, this.config.repeatDelayMs);
    this.repeatTimers.set(key, timer);
  }
}
