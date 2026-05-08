import { writable } from 'svelte/store';
import { GameClient } from '../net/GameClient';
import type { GameState, PlayerAction } from '../types/game';

const client = new GameClient();

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
