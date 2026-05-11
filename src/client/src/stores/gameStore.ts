import { writable } from 'svelte/store';
import { GameClient } from '../net/GameClient';
import type { GameState, PlayerAction } from '../types/game';

const client = new GameClient();

// Expose for e2e tests
if (typeof window !== 'undefined') {
  (window as any).gameClient = client;
}

function createGameStore() {
  const { subscribe, set } = writable<GameState | null>(null);

  client.onState((state) => {
    set(state);
  });

  client.connect();

  return {
    subscribe,
    sendAction: (action: PlayerAction) => client.sendAction(action),
  };
}

export const gameStore = createGameStore();
export const sendAction = gameStore.sendAction;

if (typeof window !== 'undefined') {
  (window as any).gameStore = gameStore;
}
