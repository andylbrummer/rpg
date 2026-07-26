import { writable, type Writable } from 'svelte/store';
import { GameClient } from '$shared/net/GameClient';
import type { GameState, PlayerAction, ErrorPayload } from '$shared/types/game';

export interface GameStore {
  subscribe: (callback: (state: GameState | null) => void) => () => void;
  sendAction: (action: PlayerAction) => void;
  errorStore: Writable<ErrorPayload | null>;
  connect: () => void;
  disconnect: () => void;
  __testSetState: (state: GameState | null) => void;
  __testClearStateOverride: () => void;
}

/**
 * `connecting` covers both the initial handshake and every reconnect attempt: an open socket is
 * not yet a usable session, because the server will not accept an action until the ready
 * handshake has produced a first state.
 */
export type ConnectionStatus = 'connecting' | 'connected' | 'disconnected';

/**
 * Whether the session can currently carry the player's input. GameClient has always reported
 * connect/disconnect and nothing listened, so a dropped session was invisible — the party simply
 * stopped responding. Input made while disconnected is deliberately dropped rather than replayed
 * on reconnect, which makes saying so out loud a requirement rather than a nicety.
 */
export const connectionStatus = writable<ConnectionStatus>('connecting');

const state = writable<GameState | null>(null);
const errorStore = writable<ErrorPayload | null>(null);
const testSetStateCallbacks: Array<(s: GameState | null) => void> = [];
let testStateOverrideActive = false;

export function onTestSetState(cb: (s: GameState | null) => void): () => void {
  testSetStateCallbacks.push(cb);
  return () => {
    const index = testSetStateCallbacks.indexOf(cb);
    if (index >= 0) {
      testSetStateCallbacks.splice(index, 1);
    }
  };
}

export const gameStore: GameStore = {
  subscribe: state.subscribe,
  sendAction: () => {
    console.warn('sendAction called before game store bootstrap');
  },
  errorStore,
  connect: () => {
    console.warn('connect called before game store bootstrap');
  },
  disconnect: () => {
    console.warn('disconnect called before game store bootstrap');
  },
  __testSetState: (s: GameState | null) => {
    testStateOverrideActive = true;
    state.set(s);
    for (const cb of [...testSetStateCallbacks]) {
      cb(s);
    }
  },
  __testClearStateOverride: () => {
    testStateOverrideActive = false;
  },
};

export let sendAction: (action: PlayerAction) => void = gameStore.sendAction;
export let serverErrorStore: typeof errorStore = errorStore;

export function bootstrapGameStore(client: GameClient): GameStore {
  client.onState((s) => {
    // State arriving is the only proof the session is live end to end, so it — not socket open —
    // is what promotes the status to connected.
    connectionStatus.set('connected');
    if (testStateOverrideActive) return;
    state.set(s);
  });

  client.onConnect(() => connectionStatus.set('connecting'));
  client.onDisconnect(() => connectionStatus.set('disconnected'));

  client.onError((err) => {
    console.error('Server error:', err.code, err.message);
    errorStore.set(err);
    setTimeout(() => errorStore.set(null), 4000);
  });

  gameStore.sendAction = (action: PlayerAction) => client.sendAction(action);
  gameStore.connect = () => client.connect();
  gameStore.disconnect = () => client.disconnect();

  sendAction = gameStore.sendAction;
  serverErrorStore = gameStore.errorStore;

  return gameStore;
}
