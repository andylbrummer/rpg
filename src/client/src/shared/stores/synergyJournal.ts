/**
 * Synergy-journal persistence and server reconciliation.
 *
 * Discovered-synergy state is authoritative on the server: it lives in the
 * campaign Journal (DiscoveryOrder) and is part of the save file. The client
 * keeps a localStorage mirror so the journal renders instantly and survives
 * brief disconnects, but on every server snapshot it reconciles that mirror
 * against the server's discovery list so the journal cannot silently diverge
 * from save state across machines or reloads.
 */

export const DISCOVERY_KEY = 'rpc_discovered_synergies';
export const REVEALED_KEY = 'rpc_revealed_synergies';

export interface JournalPersistence {
  loadDiscovered(): string[];
  loadRevealed(): string[];
  saveDiscovered(ids: string[]): void;
  saveRevealed(ids: string[]): void;
}

function readArray(key: string): string[] {
  try {
    const raw = localStorage.getItem(key);
    return raw ? JSON.parse(raw) : [];
  } catch {
    return [];
  }
}

function writeArray(key: string, ids: string[]): void {
  try {
    localStorage.setItem(key, JSON.stringify(ids));
  } catch {
    // localStorage unavailable (private mode, SSR, tests) — server state remains
    // the source of truth, so a failed cache write is non-fatal.
  }
}

/** Browser-backed persistence used by the running client. */
export const localStorageJournalPersistence: JournalPersistence = {
  loadDiscovered: () => readArray(DISCOVERY_KEY),
  loadRevealed: () => readArray(REVEALED_KEY),
  saveDiscovered: (ids) => writeArray(DISCOVERY_KEY, ids),
  saveRevealed: (ids) => writeArray(REVEALED_KEY, ids),
};

/** In-memory persistence for tests and non-browser environments. */
export function createMemoryJournalPersistence(
  initial: { discovered?: string[]; revealed?: string[] } = {}
): JournalPersistence {
  let discovered = [...(initial.discovered ?? [])];
  let revealed = [...(initial.revealed ?? [])];
  return {
    loadDiscovered: () => [...discovered],
    loadRevealed: () => [...revealed],
    saveDiscovered: (ids) => {
      discovered = [...ids];
    },
    saveRevealed: (ids) => {
      revealed = [...ids];
    },
  };
}

export interface JournalReconcileResult {
  discoveredOrder: string[];
  revealed: string[];
  changed: boolean;
}

function sameMembers(a: string[], b: string[]): boolean {
  if (a.length !== b.length) return false;
  const setB = new Set(b);
  return a.every((x) => setB.has(x));
}

/**
 * Merge the client's local journal mirror with the server's authoritative
 * discovery list.
 *
 * - Discovery order: server order leads, then any local-only ids are appended
 *   (covers a discovery made client-side that has not yet round-tripped).
 * - Revealed: union of locally-revealed ids and all server discoveries. A
 *   synergy discovered in a prior save session has no pending post-combat
 *   reveal animation on reload, so it must already read as revealed.
 */
export function reconcileJournal(
  localDiscovered: string[],
  localRevealed: string[],
  serverDiscovered: string[]
): JournalReconcileResult {
  const discoveredOrder = [...serverDiscovered];
  for (const id of localDiscovered) {
    if (!discoveredOrder.includes(id)) discoveredOrder.push(id);
  }

  const revealed = [...new Set([...localRevealed, ...serverDiscovered])];

  const changed =
    discoveredOrder.length !== localDiscovered.length ||
    !discoveredOrder.every((id, i) => localDiscovered[i] === id) ||
    !sameMembers(revealed, localRevealed);

  return { discoveredOrder, revealed, changed };
}
