import type { GameClient } from './GameClient';
import type { GameState, PlayerAction } from '$shared/types/game';

/**
 * Dev/test automation + validation harness. Attaches `window.__rpg`, which drives the
 * game through the real GameClient + gameStore (not DOM), so an external driver (agnt
 * proxy exec, Playwright, console) can script sequences AND assert engine invariants
 * end-to-end through the live protocol → engine → state-projection stack.
 *
 * The validators exercise things unit tests can't: real dungeon generation reachability,
 * cross-expedition determinism, combat termination, loot placement correctness — caught
 * over the actual WebSocket. DEV-only; never attached in production.
 */

interface StoreLike {
  subscribe: (run: (value: GameState) => void) => (() => void);
}

type Dir = 'North' | 'East' | 'South' | 'West';
const DIRS: Dir[] = ['North', 'East', 'South', 'West'];
const BLOCKING = new Set(['Wall', 'SecretDoor', 'BreakableWall', 'ConcealedCompartment']);
const WALKABLE = new Set(['Floor', 'StairsUp', 'StairsDown', 'IllusoryFloor']);
const DELTA: Record<Dir, [number, number]> = { North: [0, -1], East: [1, 0], South: [0, 1], West: [-1, 0] };

interface ClientTile { x: number; y: number; type: string; north: string; south: string; east: string; west: string; hasLoot?: boolean; lootName?: string | null; }

export interface ValidationResult { name: string; pass: boolean; detail: string; data?: unknown; }

export interface RpgHarness {
  state: () => GameState;
  send: (type: string, extra?: Record<string, unknown>) => void;
  waitFor: (predicate: (s: GameState) => boolean, timeoutMs?: number) => Promise<GameState>;
  connection: () => { ready: boolean; readyState: number | undefined; queued: number; attempts: number };
  // navigation
  knownMap: () => Map<string, ClientTile>;
  pathTo: (x: number, y: number) => Dir[] | null;
  goTo: (x: number, y: number, opts?: { autoCombat?: boolean }) => Promise<boolean>;
  exploreFully: (opts?: { maxSteps?: number; autoCombat?: boolean }) => Promise<{ tiles: number; loot: Array<{ x: number; y: number; lootName: string | null }>; combats: number; stoppedAtCombat: boolean }>;
  // primitives
  resolveBranchGate: () => Promise<number>;
  enterDungeon: (dungeonType?: string) => Promise<GameState>;
  returnToTown: () => Promise<GameState>;
  forward: () => Promise<GameState>;
  turnTo: (dir: Dir) => Promise<GameState>;
  loot: () => Array<{ x: number; y: number; lootName: string | null }>;
  pickup: () => Promise<GameState>;
  combat: {
    current: () => { id: string; name: string; isPlayer: boolean } | null;
    enemies: () => Array<{ id: string; name: string; alive: boolean; row: number; hp: number }>;
    attack: (targetName?: string) => Promise<GameState>;
    auto: (maxTurns?: number) => Promise<'won' | 'lost' | 'ongoing'>;
  };
  // validation
  validate: {
    movement: () => Promise<ValidationResult>;
    dungeonLoot: (dungeonType?: string) => Promise<ValidationResult>;
    determinism: (dungeonType?: string) => Promise<ValidationResult>;
    combatTermination: (dungeonType?: string) => Promise<ValidationResult>;
  };
  suite: (dungeonType?: string) => Promise<{ pass: boolean; results: ValidationResult[] }>;
}

const sleep = (ms: number) => new Promise((r) => setTimeout(r, ms));

export function installTestHarness(client: GameClient, store: StoreLike): void {
  const snapshot = (): GameState => {
    let s: GameState | null = null;
    const unsub = store.subscribe((v) => (s = v));
    unsub();
    return s as unknown as GameState;
  };
  const anyState = () => snapshot() as unknown as Record<string, any>;

  const send = (type: string, extra: Record<string, unknown> = {}) =>
    client.sendAction({ type, ...extra } as unknown as PlayerAction);

  const waitFor = (predicate: (s: GameState) => boolean, timeoutMs = 8000): Promise<GameState> =>
    new Promise((resolve, reject) => {
      if (predicate(snapshot())) return resolve(snapshot());
      let settled = false;
      const unsub = store.subscribe((v) => {
        if (settled) return;
        if (predicate(v as unknown as GameState)) {
          settled = true; setTimeout(unsub, 0); resolve(v as unknown as GameState);
        }
      });
      setTimeout(() => { if (!settled) { settled = true; setTimeout(unsub, 0); reject(new Error(`waitFor timeout ${timeoutMs}ms`)); } }, timeoutMs);
    });

  // --- map / navigation -----------------------------------------------------

  const knownMap = (): Map<string, ClientTile> => {
    const s = anyState();
    const all = [...(s.tiles ?? []), ...(s.explored ?? [])] as ClientTile[];
    const m = new Map<string, ClientTile>();
    for (const t of all) m.set(`${t.x},${t.y}`, t); // later (explored) wins, fine — same geometry
    return m;
  };

  const border = (t: ClientTile, d: Dir): string => (d === 'North' ? t.north : d === 'South' ? t.south : d === 'East' ? t.east : t.west);
  const canStep = (t: ClientTile, d: Dir): boolean => !BLOCKING.has(border(t, d));

  // BFS over KNOWN walkable tiles from the player to (tx,ty). Returns the sequence of
  // grid directions, or null if unreachable in the known map.
  const pathTo = (tx: number, ty: number): Dir[] | null => {
    const map = knownMap();
    const s = anyState();
    const start = `${s.player.x},${s.player.y}`;
    const goal = `${tx},${ty}`;
    if (start === goal) return [];
    const prev = new Map<string, { from: string; dir: Dir }>();
    const seen = new Set([start]);
    const q = [start];
    while (q.length) {
      const cur = q.shift()!;
      const [cx, cy] = cur.split(',').map(Number);
      const ct = map.get(cur);
      if (!ct) continue;
      for (const d of DIRS) {
        if (!canStep(ct, d)) continue;
        const [dx, dy] = DELTA[d];
        const nk = `${cx + dx},${cy + dy}`;
        const nt = map.get(nk);
        if (!nt || !WALKABLE.has(nt.type) || seen.has(nk)) continue;
        seen.add(nk); prev.set(nk, { from: cur, dir: d });
        if (nk === goal) {
          const path: Dir[] = [];
          let k = goal;
          while (k !== start) { const p = prev.get(k)!; path.unshift(p.dir); k = p.from; }
          return path;
        }
        q.push(nk);
      }
    }
    return null;
  };

  const turnTo = async (dir: Dir): Promise<GameState> => {
    const facing = anyState().player.facing as Dir;
    if (facing === dir) return snapshot();
    const d = (DIRS.indexOf(dir) - DIRS.indexOf(facing) + 4) % 4; // turn_right steps
    const action = d <= 2 ? 'turn_right' : 'turn_left';
    const n = d <= 2 ? d : 4 - d;
    for (let i = 0; i < n; i++) { send(action); await waitFor((s) => true, 1200).catch(() => {}); await sleep(60); }
    return snapshot();
  };

  const forward = async (): Promise<GameState> => {
    const before = `${anyState().player.x},${anyState().player.y}`;
    send('move_forward');
    await waitFor((s) => `${(s as any).player.x},${(s as any).player.y}` !== before || (s as any).mode === 'Combat', 2500).catch(() => {});
    return snapshot();
  };

  const stepDir = async (d: Dir): Promise<boolean> => {
    await turnTo(d);
    const before = `${anyState().player.x},${anyState().player.y}`;
    await forward();
    return `${anyState().player.x},${anyState().player.y}` !== before;
  };

  const goTo = async (x: number, y: number, opts: { autoCombat?: boolean } = {}): Promise<boolean> => {
    for (let attempt = 0; attempt < 64; attempt++) {
      const s = anyState();
      if (s.player.x === x && s.player.y === y) return true;
      if (s.mode === 'Combat') { if (opts.autoCombat) { await combat.auto(); continue; } return false; }
      const path = pathTo(x, y);
      if (!path || path.length === 0) return s.player.x === x && s.player.y === y;
      const moved = await stepDir(path[0]);
      if (!moved && anyState().mode !== 'Combat') return false; // blocked unexpectedly
    }
    return anyState().player.x === x && anyState().player.y === y;
  };

  // Frontier exploration: repeatedly walk to a known tile that borders the unknown,
  // step into it, until no reachable frontier remains.
  const exploreFully = async (opts: { maxSteps?: number; autoCombat?: boolean } = {}) => {
    const maxSteps = opts.maxSteps ?? 400;
    const autoCombat = opts.autoCombat ?? true;
    let combats = 0; let steps = 0; let stoppedAtCombat = false;
    while (steps < maxSteps) {
      const s = anyState();
      if (s.mode === 'Combat') { if (autoCombat) { await combat.auto(); combats++; continue; } stoppedAtCombat = true; break; }
      if (s.mode !== 'Exploration') break;
      const map = knownMap();
      // Find a frontier: known walkable tile with an open border toward an unknown cell.
      let target: { x: number; y: number } | null = null;
      let best = Infinity;
      const px = s.player.x, py = s.player.y;
      for (const t of map.values()) {
        if (!WALKABLE.has(t.type)) continue;
        for (const d of DIRS) {
          if (!canStep(t, d)) continue;
          const [dx, dy] = DELTA[d];
          if (!map.has(`${t.x + dx},${t.y + dy}`)) {
            // frontier tile = t; reachable? prefer nearest by manhattan to player as a cheap heuristic
            const dist = Math.abs(t.x - px) + Math.abs(t.y - py);
            if (dist < best && pathTo(t.x, t.y) !== null) { best = dist; target = { x: t.x, y: t.y }; }
            break;
          }
        }
      }
      if (!target) break; // fully explored (reachable portion)
      const reached = await goTo(target.x, target.y, { autoCombat });
      steps++;
      if (!reached) {
        // couldn't reach the chosen frontier; nudge into its unknown side anyway then continue
        const t = knownMap().get(`${target.x},${target.y}`);
        if (t) { for (const d of DIRS) { if (canStep(t, d) && !knownMap().has(`${t.x + DELTA[d][0]},${t.y + DELTA[d][1]}`)) { if (anyState().player.x === t.x && anyState().player.y === t.y) await stepDir(d); break; } } }
      } else {
        // step once into the unknown to reveal it
        const t = knownMap().get(`${target.x},${target.y}`);
        if (t) for (const d of DIRS) { if (canStep(t, d) && !knownMap().has(`${t.x + DELTA[d][0]},${t.y + DELTA[d][1]}`)) { await stepDir(d); break; } }
      }
    }
    return { tiles: knownMap().size, loot: lootTiles(), combats, stoppedAtCombat };
  };

  // --- loot / combat --------------------------------------------------------

  const lootTiles = () => {
    const out = new Map<string, { x: number; y: number; lootName: string | null }>();
    for (const t of knownMap().values()) if (t.hasLoot) out.set(`${t.x},${t.y}`, { x: t.x, y: t.y, lootName: t.lootName ?? null });
    return [...out.values()];
  };

  const combat = {
    current: () => {
      const c = anyState().combat; if (!c) return null;
      const cur = c.combatants.find((x: any) => x.id === c.initiativeOrder[c.currentTurnIndex]);
      return cur ? { id: cur.id, name: cur.name, isPlayer: cur.isPlayer } : null;
    },
    enemies: () => {
      const c = anyState().combat;
      return c ? c.combatants.filter((x: any) => !x.isPlayer).map((x: any) => ({ id: x.id, name: x.name, alive: x.alive, row: x.row, hp: x.hp })) : [];
    },
    attack: async (targetName?: string): Promise<GameState> => {
      const c = anyState().combat; if (!c) throw new Error('not in combat');
      const cur = c.combatants.find((x: any) => x.id === c.initiativeOrder[c.currentTurnIndex]);
      if (!cur || !cur.isPlayer) throw new Error('not a player turn');
      const target = c.combatants.find((x: any) => !x.isPlayer && x.alive && (!targetName || x.name === targetName));
      if (!target) throw new Error('no enemy');
      const before = `${c.round}:${c.currentTurnIndex}:${target.hp}`;
      client.sendAction({ type: 'combat_action', action: { actorId: cur.id, type: 'Attack', targetId: target.id } } as unknown as PlayerAction);
      return waitFor((s) => {
        const c2 = (s as any).combat; if (!c2) return true;
        const t2 = c2.combatants.find((x: any) => x.id === target.id);
        return `${c2.round}:${c2.currentTurnIndex}:${t2 ? t2.hp : 'gone'}` !== before;
      }, 5000);
    },
    auto: async (maxTurns = 60): Promise<'won' | 'lost' | 'ongoing'> => {
      for (let i = 0; i < maxTurns; i++) {
        const s = anyState();
        if (s.mode !== 'Combat' || !s.combat) {
          const partyAlive = (s.party ?? []).some((m: any) => m.alive);
          return s.mode !== 'Combat' ? 'won' : (partyAlive ? 'won' : 'lost');
        }
        const cur = s.combat.combatants.find((x: any) => x.id === s.combat.initiativeOrder[s.combat.currentTurnIndex]);
        if (cur && cur.isPlayer) { try { await combat.attack(); } catch { await sleep(200); } }
        else await sleep(250);
      }
      return 'ongoing';
    },
  };

  // Resolve the forced level-up branch-choice gate (blocks dungeon entry) by picking
  // each pending member's first available branch. Idempotent once all are chosen.
  const resolveBranchGate = async (): Promise<number> => {
    let chosen = 0;
    for (let pass = 0; pass < 8; pass++) {
      const party = (anyState().party ?? []) as any[];
      const pending = party.filter((m) => m.awaitingBranchChoice && (m.availableBranches?.length ?? 0) > 0);
      if (pending.length === 0) break;
      const m = pending[0];
      send('branch_choose', { targetId: m.id, branch: m.availableBranches[0] });
      try { await waitFor((s) => !((s as any).party ?? []).find((p: any) => p.id === m.id)?.awaitingBranchChoice, 4000); chosen++; }
      catch { break; }
    }
    return chosen;
  };

  const enterDungeon = async (dungeonType = 'broken_engine') => {
    await resolveBranchGate(); // clear forced branch choices that block dungeon entry
    send('enter_dungeon', { dungeonType });
    return waitFor((s) => (s as any).mode === 'Exploration');
  };
  const returnToTown = async () => { send('return_to_town'); return waitFor((s) => (s as any).mode === 'Menu'); };
  const pickup = async () => { send('pickup_loot'); return waitFor(() => true, 1500).catch(() => snapshot()); };

  // --- validators -----------------------------------------------------------

  const layoutSignature = (): string => {
    const tiles = [...knownMap().values()].sort((a, b) => a.y - b.y || a.x - b.x);
    return tiles.map((t) => `${t.x},${t.y}:${t.type}:${t.north[0]}${t.south[0]}${t.east[0]}${t.west[0]}${t.hasLoot ? 'L' : ''}`).join('|');
  };

  const validate = {
    movement: async (): Promise<ValidationResult> => {
      if (anyState().mode !== 'Exploration') return { name: 'movement', pass: false, detail: 'not in a dungeon' };
      const before = `${anyState().player.x},${anyState().player.y}`;
      // try all four directions for at least one successful move
      for (const d of DIRS) { if (await stepDir(d)) return { name: 'movement', pass: true, detail: `moved ${d} from ${before}` }; }
      return { name: 'movement', pass: false, detail: `player at ${before} could not move in any direction` };
    },

    dungeonLoot: async (dungeonType = 'broken_engine'): Promise<ValidationResult> => {
      if (anyState().mode !== 'Exploration') await enterDungeon(dungeonType);
      const res = await exploreFully({ autoCombat: true });
      const loot = res.loot;
      const map = knownMap();
      const problems: string[] = [];
      for (const l of loot) {
        const t = map.get(`${l.x},${l.y}`)!;
        if (!WALKABLE.has(t.type)) problems.push(`loot on non-walkable ${l.x},${l.y}`);
        if (t.type === 'StairsUp') problems.push(`loot on entrance ${l.x},${l.y}`);
      }
      const pass = loot.length >= 1 && problems.length === 0;
      return {
        name: 'dungeonLoot', pass,
        detail: pass ? `${loot.length} reachable loot tile(s) across ${res.tiles} tiles, ${res.combats} combats` : (loot.length === 0 ? `NO loot found in ${res.tiles} fully-explored tiles` : problems.join('; ')),
        data: { loot, tiles: res.tiles, combats: res.combats },
      };
    },

    determinism: async (dungeonType = 'broken_engine'): Promise<ValidationResult> => {
      if (anyState().mode === 'Combat') await combat.auto();
      if (anyState().mode !== 'Menu') await returnToTown();
      await enterDungeon(dungeonType);
      await exploreFully({ autoCombat: true });
      const sigA = layoutSignature();
      await returnToTown();
      await enterDungeon(dungeonType);
      await exploreFully({ autoCombat: true });
      const sigB = layoutSignature();
      const pass = sigA === sigB && sigA.length > 0;
      return { name: 'determinism', pass, detail: pass ? `identical layout+loot across two entries (${sigA.split('|').length} tiles)` : `layouts differ (A=${sigA.split('|').length} tiles, B=${sigB.split('|').length})` };
    },

    combatTermination: async (dungeonType = 'broken_engine'): Promise<ValidationResult> => {
      if (anyState().mode !== 'Exploration') await enterDungeon(dungeonType);
      // walk until a combat triggers, then assert it terminates with bounded HP
      let triggered = false;
      for (let i = 0; i < 60 && !triggered; i++) {
        const r = await exploreFully({ autoCombat: false, maxSteps: 30 });
        if (r.stoppedAtCombat) triggered = true;
      }
      if (!triggered) return { name: 'combatTermination', pass: true, detail: 'no combat encountered (vacuous pass)' };
      const enemies = combat.enemies();
      const outcome = await combat.auto();
      const c = anyState().combat;
      const hpOk = !c || c.combatants.every((x: any) => x.hp >= 0);
      const pass = (outcome === 'won' || outcome === 'lost') && hpOk;
      return { name: 'combatTermination', pass, detail: `outcome=${outcome}, ${enemies.length} enemies, hp bounded=${hpOk}` };
    },
  };

  const suite = async (dungeonType = 'broken_engine') => {
    const results: ValidationResult[] = [];
    try { if (anyState().mode !== 'Exploration') await enterDungeon(dungeonType); results.push(await validate.movement()); } catch (e) { results.push({ name: 'movement', pass: false, detail: String(e) }); }
    try { results.push(await validate.dungeonLoot(dungeonType)); } catch (e) { results.push({ name: 'dungeonLoot', pass: false, detail: String(e) }); }
    try { results.push(await validate.combatTermination(dungeonType)); } catch (e) { results.push({ name: 'combatTermination', pass: false, detail: String(e) }); }
    try { results.push(await validate.determinism(dungeonType)); } catch (e) { results.push({ name: 'determinism', pass: false, detail: String(e) }); }
    return { pass: results.every((r) => r.pass), results };
  };

  const harness: RpgHarness = {
    state: snapshot, send, waitFor,
    connection: () => { const c = client as any; return { ready: c.isReady, readyState: c.ws?.readyState, queued: c.actionQueue.length, attempts: c.reconnectAttempts }; },
    knownMap, pathTo, goTo, exploreFully,
    resolveBranchGate, enterDungeon, returnToTown, forward, turnTo, loot: lootTiles, pickup, combat, validate, suite,
  };

  (window as unknown as { __rpg: RpgHarness }).__rpg = harness;
  // eslint-disable-next-line no-console
  console.info('[test-harness] window.__rpg ready — suite(), exploreFully(), validate.*, goTo(), pathTo(), combat.auto()');
}
