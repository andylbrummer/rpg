import { writable } from 'svelte/store';
import { GameClient } from '../net/GameClient';
import type { GameState, PlayerAction, ErrorPayload } from '../types/game';

const client = new GameClient();

// Expose for e2e tests
if (typeof window !== 'undefined') {
  (window as any).gameClient = client;
}

function createGameStore() {
  const { subscribe, set } = writable<GameState | null>(null);

  const errorStore = writable<ErrorPayload | null>(null);

  client.onState((state) => {
    set(state);
  });

  client.onError((error) => {
    console.error('Server error:', error.code, error.message);
    errorStore.set(error);
    // Auto-clear after 4s
    setTimeout(() => errorStore.set(null), 4000);
  });

  client.connect();

  return {
    subscribe,
    sendAction: (action: PlayerAction) => client.sendAction(action),
    errorStore,
    __testSetState: set,
  };
}

export const gameStore = createGameStore();
export const sendAction = gameStore.sendAction;
export const serverErrorStore = gameStore.errorStore;

if (typeof window !== 'undefined') {
  (window as any).gameStore = gameStore;
}
