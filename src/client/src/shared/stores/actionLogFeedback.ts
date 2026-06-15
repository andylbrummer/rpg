import { writable, get, type Readable } from 'svelte/store';
import { gameStore, type GameStore } from '$shared/stores/gameStore';
import { playSynergyChime } from '$renderer/UISounds';
import { subtitles as sharedSubtitles } from '$renderer/SubtitleSystem';

export interface RepToast {
  id: number;
  factionId: string;
  delta: number;
  source: string;
}

export interface FactionNotification {
  id: number;
  text: string;
}

const DISCOVERY_KEY = 'rpc_discovered_synergies';
const REVEALED_KEY = 'rpc_revealed_synergies';
const REP_TOAST_TTL_MS = 4000;
const FACTION_NOTIFICATION_TTL_MS = 6000;

function loadSet(key: string): Set<string> {
  try {
    const raw = localStorage.getItem(key);
    return new Set(raw ? JSON.parse(raw) : []);
  } catch {
    return new Set();
  }
}

function saveSet(key: string, ids: Set<string>) {
  localStorage.setItem(key, JSON.stringify([...ids]));
}

function loadArray(key: string): string[] {
  try {
    const raw = localStorage.getItem(key);
    return raw ? JSON.parse(raw) : [];
  } catch {
    return [];
  }
}

function saveArray(key: string, ids: string[]) {
  localStorage.setItem(key, JSON.stringify(ids));
}

/**
 * Interprets the server action log into player-facing feedback: reputation toasts,
 * faction-event notifications, synergy discovery/journal persistence, and the synergy
 * combat-flash target. Owns its own localStorage-backed journal state and post-combat
 * reveal timing. Subscribes to the game store on creation; call dispose() to detach.
 */
export interface ActionLogFeedback {
  repToasts: Readable<RepToast[]>;
  factionNotifications: Readable<FactionNotification[]>;
  discoveredOrder: Readable<string[]>;
  revealedSynergies: Readable<Set<string>>;
  synergyFlashTargetId: Readable<string | null>;
  dispose: () => void;
}

export function createActionLogFeedback(store: GameStore = gameStore): ActionLogFeedback {
  const repToasts = writable<RepToast[]>([]);
  const factionNotifications = writable<FactionNotification[]>([]);
  const discoveredOrder = writable<string[]>(loadArray(DISCOVERY_KEY));
  const revealedSynergies = writable<Set<string>>(loadSet(REVEALED_KEY));
  const synergyFlashTargetId = writable<string | null>(null);

  let lastActionLogTurn = 0;
  let lastMode: string | null = null;
  let pendingReveals: string[] = [];
  let nextToastId = 0;

  const unsubscribe = store.subscribe((s) => {
    const wasCombat = lastMode === 'Combat';
    lastMode = s?.mode ?? null;

    const actionLog = s?.actionLog ?? [];
    const maxTurn = actionLog.length > 0 ? Math.max(...actionLog.map((e: any) => e.turn)) : 0;
    if (maxTurn > lastActionLogTurn) {
      const newEntries = actionLog.filter((e: any) => e.turn > lastActionLogTurn && e.type === 'rep_changed');
      for (const entry of newEntries) {
        const toast: RepToast = {
          id: nextToastId++,
          factionId: entry.payload.factionId ?? 'unknown',
          delta: parseInt(entry.payload.delta ?? '0', 10),
          source: entry.payload.source ?? '',
        };
        repToasts.update((t) => [...t, toast]);
        setTimeout(() => {
          repToasts.update((t) => t.filter((x) => x.id !== toast.id));
        }, REP_TOAST_TTL_MS);
      }

      const newSynergyEntries = actionLog.filter((e: any) => e.turn > lastActionLogTurn && e.type === 'synergy_triggered');
      if (newSynergyEntries.length > 0) {
        // Audio event: synergy chime, captioned for subtitle/a11y support.
        playSynergyChime();
        sharedSubtitles.add('[Synergy chime — abilities combine]', 2500);
      }
      for (const entry of newSynergyEntries) {
        const sid = entry.payload?.synergyId;
        const tid = entry.payload?.targetId;
        if (sid) {
          discoveredOrder.update((order) => {
            if (order.includes(sid)) return order;
            const next = [...order, sid];
            saveArray(DISCOVERY_KEY, next);
            return next;
          });
          if (!get(revealedSynergies).has(sid) && !pendingReveals.includes(sid)) {
            pendingReveals = [...pendingReveals, sid];
          }
        }
        if (tid) {
          synergyFlashTargetId.set(tid);
        }
      }

      const factionEvents = actionLog.filter((e: any) => e.turn > lastActionLogTurn && e.category === 'faction' && ['resolution', 'executing_collision', 'timeline_modified', 'event_fired'].includes(e.type));
      for (const entry of factionEvents) {
        let text = '';
        if (entry.type === 'resolution') {
          text = entry.payload?.description ?? 'Faction conflict resolved.';
        } else if (entry.type === 'executing_collision') {
          text = `Multiple factions are executing their schemes: ${entry.payload?.factions ?? ''}`;
        } else if (entry.type === 'timeline_modified') {
          text = `${entry.payload?.factionId ?? 'A faction'}'s timeline shifts.`;
        } else if (entry.type === 'event_fired') {
          text = entry.payload?.eventName ?? 'A campaign event unfolds.';
        }
        if (text) {
          const noteId = nextToastId++;
          factionNotifications.update((n) => [...n, { id: noteId, text }]);
          setTimeout(() => {
            factionNotifications.update((n) => n.filter((x) => x.id !== noteId));
          }, FACTION_NOTIFICATION_TTL_MS);
        }
      }

      lastActionLogTurn = maxTurn;
    } else if (actionLog.length === 0 && lastActionLogTurn > 0) {
      // Reset detected — clear turn tracker so future toasts fire
      lastActionLogTurn = 0;
    }

    // Reveal field notes entries post-combat
    if (wasCombat && s?.mode !== 'Combat' && pendingReveals.length > 0) {
      revealedSynergies.update((set) => {
        const next = new Set([...set, ...pendingReveals]);
        saveSet(REVEALED_KEY, next);
        return next;
      });
      pendingReveals = [];
    }
  });

  return {
    repToasts,
    factionNotifications,
    discoveredOrder,
    revealedSynergies,
    synergyFlashTargetId,
    dispose: unsubscribe,
  };
}
