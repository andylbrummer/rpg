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
    if (testStateOverrideActive) return;
    state.set(s);
  });

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
