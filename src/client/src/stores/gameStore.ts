import { writable } from 'svelte/store';
import { GameClient } from '../net/GameClient';
import type { GameState, PlayerAction, ErrorPayload } from '../types/game';

export interface GameStore {
  subscribe: (callback: (state: GameState | null) => void) => () => void;
  sendAction: (action: PlayerAction) => void;
  errorStore: { subscribe: (callback: (error: ErrorPayload | null) => void) => () => void };
  connect: () => void;
  disconnect: () => void;
  __testSetState: (state: GameState | null) => void;
}

const state = writable<GameState | null>(null);
const errorStore = writable<ErrorPayload | null>(null);

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
  __testSetState: state.set,
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
