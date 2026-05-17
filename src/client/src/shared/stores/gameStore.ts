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
}

const state = writable<GameState | null>(null);
const errorStore = writable<ErrorPayload | null>(null);
const testSetStateCallbacks: Array<(s: GameState | null) => void> = [];

export function onTestSetState(cb: (s: GameState | null) => void) {
  testSetStateCallbacks.push(cb);
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
    state.set(s);
    testSetStateCallbacks.forEach(cb => cb(s));
  },
};

export let sendAction: (action: PlayerAction) => void = gameStore.sendAction;
export let serverErrorStore: typeof errorStore = errorStore;

export function bootstrapGameStore(client: GameClient): GameStore {
  client.onState((s) => {
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
