import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { GamepadManager } from './GamepadManager';
import type { PlayerAction } from '$shared/types/game';

/**
 * Minimal stand-in for the browser globals GamepadManager reaches for. Records listeners so a
 * test can both fire them and check they were detached.
 */
class FakeWindow {
  listeners = new Map<string, Set<(e: unknown) => void>>();

  addEventListener(type: string, handler: (e: unknown) => void): void {
    if (!this.listeners.has(type)) this.listeners.set(type, new Set());
    this.listeners.get(type)!.add(handler);
  }

  removeEventListener(type: string, handler: (e: unknown) => void): void {
    this.listeners.get(type)?.delete(handler);
  }

  count(type: string): number {
    return this.listeners.get(type)?.size ?? 0;
  }

  fire(type: string): void {
    for (const handler of [...(this.listeners.get(type) ?? [])]) handler({});
  }
}

/** A gamepad with both sticks pushed forward, which reads as a move_forward. */
function pushedForward(): Gamepad {
  return {
    axes: [0, -1],
    buttons: [],
  } as unknown as Gamepad;
}

describe('GamepadManager', () => {
  let fakeWindow: FakeWindow;
  let pads: (Gamepad | null)[];
  let frames: Array<() => void>;

  beforeEach(() => {
    fakeWindow = new FakeWindow();
    pads = [null];
    frames = [];

    // defineProperty rather than assignment: node exposes `navigator` as a getter-only global,
    // so a plain write throws.
    const stub = (key: string, value: unknown) =>
      Object.defineProperty(globalThis, key, { value, configurable: true, writable: true });

    stub('window', fakeWindow);
    stub('navigator', { getGamepads: () => pads });
    stub('requestAnimationFrame', (cb: () => void) => {
      frames.push(cb);
      return frames.length;
    });
    stub('cancelAnimationFrame', () => {});
  });

  afterEach(() => {
    for (const key of ['window', 'navigator', 'requestAnimationFrame', 'cancelAnimationFrame']) {
      delete (globalThis as Record<string, unknown>)[key];
    }
  });

  it('detaches its window listeners on dispose', () => {
    const manager = new GamepadManager(() => {});
    expect(fakeWindow.count('gamepadconnected')).toBe(1);
    expect(fakeWindow.count('gamepaddisconnected')).toBe(1);

    manager.dispose();

    expect(fakeWindow.count('gamepadconnected')).toBe(0);
    expect(fakeWindow.count('gamepaddisconnected')).toBe(0);
  });

  /**
   * The listeners used to be anonymous, so dispose could not detach them. A pad connecting after
   * the component unmounted restarted polling on a dead manager, which then sent input through a
   * callback whose socket was gone.
   */
  it('does not send input after dispose when a pad connects later', () => {
    const sent: PlayerAction[] = [];
    const manager = new GamepadManager((action) => sent.push(action));

    manager.dispose();

    pads = [pushedForward()];
    fakeWindow.fire('gamepadconnected');
    for (const frame of [...frames]) frame();

    expect(sent).toEqual([]);
  });

  it('sends input from a pad connected while it is alive', () => {
    const sent: PlayerAction[] = [];
    const manager = new GamepadManager((action) => sent.push(action));

    pads = [pushedForward()];
    fakeWindow.fire('gamepadconnected');
    for (const frame of [...frames]) frame();

    expect(sent).toContainEqual({ type: 'move_forward' });
    manager.dispose();
  });
});
