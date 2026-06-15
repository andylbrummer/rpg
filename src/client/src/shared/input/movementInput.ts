import type { PlayerAction } from '$shared/types/game';
import { keyToAction, type Keybinding } from '$config/keybindings';

// The only player actions reachable via keyboard in the overworld context are movement.
// Validating against this set narrows the loosely-typed binding string to a real PlayerAction,
// so a mis-bound or unknown action string can never be dispatched through keyboard input.
export const KEYBOARD_MOVEMENT_ACTIONS = [
  'move_forward', 'move_back', 'strafe_left', 'strafe_right', 'turn_left', 'turn_right',
] as const;
type KeyboardMovementAction = (typeof KEYBOARD_MOVEMENT_ACTIONS)[number];

/**
 * Resolve a raw key to a movement PlayerAction using the overworld binding context.
 * Returns null for any key that is unbound or bound to a non-movement action, so the
 * keyboard path can only ever dispatch a validated movement action.
 */
export function resolveKeyToAction(bindings: Keybinding[], key: string): PlayerAction | null {
  // Movement bindings live in the 'overworld' context (exploration + overworld traversal).
  const action = keyToAction(bindings, key, 'overworld');
  if (action && (KEYBOARD_MOVEMENT_ACTIONS as readonly string[]).includes(action)) {
    return { type: action as KeyboardMovementAction };
  }
  return null;
}

const INPUT_BUFFER_SIZE = 2;
const REPEAT_INITIAL_MS = 300;
const REPEAT_INTERVAL_MS = 200;
const PENDING_TIMEOUT_MS = 500;

/**
 * Owns keyboard/gamepad movement buffering: a short input queue, single-in-flight pending
 * action with timeout, and key-repeat timers. Dispatch is delegated to the injected
 * sendAction so the controller stays decoupled from the transport/store.
 */
export class MovementInputController {
  private readonly send: (action: PlayerAction) => void;
  private inputBuffer: PlayerAction[] = [];
  private pendingAction: PlayerAction | null = null;
  private pendingTimer: ReturnType<typeof setTimeout> | null = null;
  private heldKeys = new Set<string>();
  private repeatTimers = new Map<string, ReturnType<typeof setTimeout>>();

  constructor(send: (action: PlayerAction) => void) {
    this.send = send;
  }

  private clearPending() {
    if (this.pendingTimer) {
      clearTimeout(this.pendingTimer);
      this.pendingTimer = null;
    }
    this.pendingAction = null;
  }

  private drainBuffer() {
    if (this.pendingAction || this.inputBuffer.length === 0) return;
    const action = this.inputBuffer.shift()!;
    this.pendingAction = action;
    this.send(action);
    this.pendingTimer = setTimeout(() => {
      this.pendingAction = null;
      this.pendingTimer = null;
      this.drainBuffer();
    }, PENDING_TIMEOUT_MS);
  }

  enqueue(action: PlayerAction) {
    if (this.inputBuffer.length < INPUT_BUFFER_SIZE) {
      this.inputBuffer.push(action);
      this.drainBuffer();
    }
  }

  private startRepeat(key: string, action: PlayerAction) {
    if (this.repeatTimers.has(key)) return;
    const timer = setTimeout(() => {
      this.repeatTimers.delete(key);
      if (this.heldKeys.has(key)) {
        this.enqueue(action);
        const intervalTimer = setInterval(() => {
          if (!this.heldKeys.has(key)) {
            clearInterval(intervalTimer);
            return;
          }
          this.enqueue(action);
        }, REPEAT_INTERVAL_MS);
        this.repeatTimers.set(key, intervalTimer);
      }
    }, REPEAT_INITIAL_MS);
    this.repeatTimers.set(key, timer);
  }

  private stopRepeat(key: string) {
    const timer = this.repeatTimers.get(key);
    if (timer) {
      clearTimeout(timer);
      clearInterval(timer);
      this.repeatTimers.delete(key);
    }
    this.heldKeys.delete(key);
  }

  private stopAllRepeats() {
    for (const timer of this.repeatTimers.values()) {
      clearTimeout(timer);
      clearInterval(timer);
    }
    this.repeatTimers.clear();
    this.heldKeys.clear();
  }

  /** Begin holding a movement key: enqueue once immediately, then start key-repeat. */
  keyDown(key: string, action: PlayerAction) {
    if (!this.heldKeys.has(key)) {
      this.heldKeys.add(key);
      this.enqueue(action);
      this.startRepeat(key, action);
    }
  }

  /** Release a movement key, stopping its repeat. */
  keyUp(key: string) {
    this.stopRepeat(key);
  }

  /** Flush the buffer and stop all repeats, then dispatch a cancel action. */
  cancel() {
    this.inputBuffer = [];
    this.clearPending();
    this.stopAllRepeats();
    this.send({ type: 'cancel' });
  }

  /** Called whenever a fresh server state settles: clear the in-flight gate and drain. */
  notifyStateSettled() {
    this.clearPending();
    this.drainBuffer();
  }

  dispose() {
    this.stopAllRepeats();
    this.clearPending();
  }
}
