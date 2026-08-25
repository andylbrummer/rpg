import { describe, it, expect } from 'vitest';
import { toRenderModel } from './RenderModel';
import type { GameState, Tile, CombatState } from '$shared/types/game';

function baseState(overrides: Partial<GameState> = {}): GameState {
  return {
    type: 'state',
    mode: 'Exploration',
    player: { x: 2, y: 3, facing: 'East' },
    tiles: [],
    explored: [],
    hasDungeon: true,
    party: [],
    ...overrides,
  };
}

const floorTile: Tile = {
  x: 1,
  y: 1,
  type: 'Floor',
  north: 'Wall',
  south: 'None',
  east: 'Door',
  west: 'None',
};

describe('toRenderModel', () => {
  describe('exploration scene', () => {
    it('maps player, tiles, and dungeon type, with no combat', () => {
      const model = toRenderModel(
        baseState({
          mode: 'Exploration',
          dungeonType: 'crypt',
          tiles: [floorTile],
          player: { x: 4, y: 5, facing: 'South' },
        })
      );

      expect(model.hasDungeon).toBe(true);
      expect(model.dungeonType).toBe('crypt');
      expect(model.tiles).toEqual([floorTile]);
      expect(model.player).toEqual({ x: 4, y: 5, facing: 'South' });
      expect(model.combat).toBeNull();
    });
  });

  describe('default / empty scene', () => {
    it('produces no dungeon and empty tiles with no combat', () => {
      const model = toRenderModel(
        baseState({ hasDungeon: false, dungeonType: undefined, tiles: [] })
      );

      expect(model.hasDungeon).toBe(false);
      expect(model.dungeonType).toBeUndefined();
      expect(model.tiles).toEqual([]);
      expect(model.combat).toBeNull();
    });

    it('drops combat data when mode is not Combat even if combat state is present', () => {
      const combat: CombatState = {
        phase: 'active',
        round: 1,
        combatants: [
          {
            id: 'g1',
            name: 'Goblin',
            isPlayer: false,
            hp: 5,
            maxHp: 5,
            speed: 3,
            row: 0,
            alive: true,
            isCurrent: false,
          },
        ],
        initiativeOrder: ['g1'],
        currentTurnIndex: 0,
        log: [],
        isFinished: false,
      };
      const model = toRenderModel(baseState({ mode: 'Exploration', combat }));
      expect(model.combat).toBeNull();
    });
  });

  describe('combat creature placement', () => {
    const combat: CombatState = {
      phase: 'active',
      round: 2,
      combatants: [
        {
          id: 'hero-1',
          name: 'Aria',
          isPlayer: true,
          hp: 10,
          maxHp: 10,
          speed: 5,
          row: 0,
          alive: true,
          isCurrent: true,
        },
        {
          id: 'goblin-2',
          name: 'Goblin',
          isPlayer: false,
          hp: 4,
          maxHp: 4,
          speed: 3,
          row: 0,
          alive: true,
          isCurrent: false,
        },
        {
          id: 'thing-3',
          name: 'The Unaccounted',
          isPlayer: false,
          hp: 8,
          maxHp: 8,
          speed: 2,
          row: 1,
          alive: true,
          isCurrent: false,
          isUnaccounted: true,
        },
      ],
      initiativeOrder: ['hero-1', 'goblin-2', 'thing-3'],
      currentTurnIndex: 0,
      log: [{ actor: 'The Unaccounted', message: 'attacks wildly', round: 2 }],
      isFinished: false,
    };

    it('maps combatants for placement when in Combat mode', () => {
      const model = toRenderModel(baseState({ mode: 'Combat', combat }));

      expect(model.combat).not.toBeNull();
      expect(model.combat!.combatants).toEqual([
        { id: 'hero-1', name: 'Aria', isPlayer: true, alive: true, row: 0, isUnaccounted: false },
        { id: 'goblin-2', name: 'Goblin', isPlayer: false, alive: true, row: 0, isUnaccounted: false },
        {
          id: 'thing-3',
          name: 'The Unaccounted',
          isPlayer: false,
          alive: true,
          row: 1,
          isUnaccounted: true,
        },
      ]);
    });

    it('maps the combat log for unaccounted-attack detection', () => {
      const model = toRenderModel(baseState({ mode: 'Combat', combat }));

      expect(model.combat!.log).toEqual([
        { actor: 'The Unaccounted', message: 'attacks wildly' },
      ]);
    });

    it('yields null combat when mode is Combat but no combat state exists', () => {
      const model = toRenderModel(baseState({ mode: 'Combat', combat: undefined }));
      expect(model.combat).toBeNull();
    });
  });
});
