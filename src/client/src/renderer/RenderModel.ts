import type { GameState, Tile } from '$shared/types/game';

// Re-export the geometry tile type so the renderer depends on the render-model
// boundary rather than reaching into the full client GameState module.
export type { Tile };

/** Player placement the renderer needs: grid position + facing for camera/torch. */
export interface RenderPlayer {
  x: number;
  y: number;
  facing: string;
}

/** A single combatant reduced to the fields the renderer uses for mesh placement. */
export interface RenderCombatant {
  id: string;
  name: string;
  isPlayer: boolean;
  alive: boolean;
  row: number;
  isUnaccounted: boolean;
}

/** Combat log entry reduced to what unaccounted-attack detection reads. */
export interface RenderCombatLogEntry {
  actor: string;
  message: string;
}

/** Combat data the renderer draws; present only while combat is on-screen. */
export interface RenderCombat {
  combatants: RenderCombatant[];
  log: RenderCombatLogEntry[];
}

/**
 * Renderer-specific view of the world. Contains only the geometry, theme, and
 * creature-placement data the 3D renderer needs — no gameplay, town, party, or
 * server-schema concerns leak across this boundary.
 */
export interface RenderModel {
  dungeonType?: string;
  hasDungeon: boolean;
  tiles: Tile[];
  player: RenderPlayer;
  combat: RenderCombat | null;
}

/**
 * Pure mapping from the client GameState to the renderer view model. Lives
 * outside the renderer so the renderer never imports GameState or combat types.
 * Combat is included only while the game is in Combat mode with live combat state.
 */
export function toRenderModel(state: GameState): RenderModel {
  const showCombat = state.mode === 'Combat' && state.combat !== undefined;
  const combat: RenderCombat | null = showCombat
    ? {
        combatants: state.combat!.combatants.map((c) => ({
          id: c.id,
          name: c.name,
          isPlayer: c.isPlayer,
          alive: c.alive,
          row: c.row,
          isUnaccounted: c.isUnaccounted ?? false,
        })),
        log: state.combat!.log.map((e) => ({ actor: e.actor, message: e.message })),
      }
    : null;

  return {
    dungeonType: state.dungeonType,
    hasDungeon: state.hasDungeon,
    tiles: state.tiles,
    player: { x: state.player.x, y: state.player.y, facing: state.player.facing },
    combat,
  };
}
