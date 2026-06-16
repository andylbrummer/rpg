import { writable, get, type Readable } from 'svelte/store';
import { gameStore, type GameStore } from '$shared/stores/gameStore';
import { playSynergyChime } from '$renderer/UISounds';
import { subtitles as sharedSubtitles } from '$renderer/SubtitleSystem';
import { parseActionLogEntry } from '$shared/types/actionLogEvents';
import {
  reconcileJournal,
  localStorageJournalPersistence,
  type JournalPersistence,
} from '$shared/stores/synergyJournal';

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

const REP_TOAST_TTL_MS = 4000;
const FACTION_NOTIFICATION_TTL_MS = 6000;

/**
 * Interprets the server action log into player-facing feedback: reputation toasts,
 * faction-event notifications, synergy discovery/journal persistence, and the synergy
 * combat-flash target. Entries are projected through a typed, validated parser
 * (no `any`). Discovered/revealed journal state is reconciled against the server
 * save snapshot (state.journal) on every frame, with a localStorage mirror for
 * instant render. Subscribes to the game store on creation; call dispose() to detach.
 */
export interface ActionLogFeedback {
  repToasts: Readable<RepToast[]>;
  factionNotifications: Readable<FactionNotification[]>;
  discoveredOrder: Readable<string[]>;
  revealedSynergies: Readable<Set<string>>;
  synergyFlashTargetId: Readable<string | null>;
  dispose: () => void;
}

export function createActionLogFeedback(
  store: GameStore = gameStore,
  persistence: JournalPersistence = localStorageJournalPersistence
): ActionLogFeedback {
  const repToasts = writable<RepToast[]>([]);
  const factionNotifications = writable<FactionNotification[]>([]);
  const discoveredOrder = writable<string[]>(persistence.loadDiscovered());
  const revealedSynergies = writable<Set<string>>(new Set(persistence.loadRevealed()));
  const synergyFlashTargetId = writable<string | null>(null);

  let lastActionLogTurn = 0;
  let lastMode: string | null = null;
  let pendingReveals: string[] = [];
  let nextToastId = 0;
  let lastServerJournalSig: string | null = null;

  function persistDiscovered(ids: string[]) {
    persistence.saveDiscovered(ids);
  }
  function persistRevealed(ids: Set<string>) {
    persistence.saveRevealed([...ids]);
  }

  const unsubscribe = store.subscribe((s) => {
    const wasCombat = lastMode === 'Combat';
    lastMode = s?.mode ?? null;

    // Reconcile the local journal mirror against authoritative server save state.
    // Runs before action-log scanning so newly-triggered synergies append onto the
    // reconciled order rather than being overwritten by it.
    const serverDiscovered = s?.journal?.discoveredSynergies;
    if (serverDiscovered) {
      const sig = serverDiscovered.join('');
      if (sig !== lastServerJournalSig) {
        lastServerJournalSig = sig;
        const result = reconcileJournal(
          get(discoveredOrder),
          [...get(revealedSynergies)],
          serverDiscovered
        );
        if (result.changed) {
          discoveredOrder.set(result.discoveredOrder);
          persistDiscovered(result.discoveredOrder);
          const revealedSet = new Set(result.revealed);
          revealedSynergies.set(revealedSet);
          persistRevealed(revealedSet);
        }
      }
    }

    const actionLog = s?.actionLog ?? [];
    const maxTurn = actionLog.length > 0 ? Math.max(...actionLog.map((e) => e.turn)) : 0;
    if (maxTurn > lastActionLogTurn) {
      const events = actionLog
        .filter((e) => e.turn > lastActionLogTurn)
        .map(parseActionLogEntry);

      let synergyTriggered = false;

      for (const ev of events) {
        if (!ev) continue;
        switch (ev.kind) {
          case 'repChanged': {
            const toast: RepToast = {
              id: nextToastId++,
              factionId: ev.factionId,
              delta: ev.delta,
              source: ev.source,
            };
            repToasts.update((t) => [...t, toast]);
            setTimeout(() => {
              repToasts.update((t) => t.filter((x) => x.id !== toast.id));
            }, REP_TOAST_TTL_MS);
            break;
          }
          case 'synergyTriggered': {
            synergyTriggered = true;
            if (ev.synergyId) {
              const sid = ev.synergyId;
              discoveredOrder.update((order) => {
                if (order.includes(sid)) return order;
                const next = [...order, sid];
                persistDiscovered(next);
                return next;
              });
              if (!get(revealedSynergies).has(sid) && !pendingReveals.includes(sid)) {
                pendingReveals = [...pendingReveals, sid];
              }
            }
            if (ev.targetId) {
              synergyFlashTargetId.set(ev.targetId);
            }
            break;
          }
          case 'factionResolution':
            pushFactionNotification(ev.description);
            break;
          case 'factionCollision':
            pushFactionNotification(
              `Multiple factions are executing their schemes: ${ev.factions}`
            );
            break;
          case 'factionTimeline':
            pushFactionNotification(`${ev.factionId}'s timeline shifts.`);
            break;
          case 'factionEvent':
            pushFactionNotification(ev.eventName);
            break;
        }
      }

      if (synergyTriggered) {
        // Audio event: synergy chime, captioned for subtitle/a11y support.
        playSynergyChime();
        sharedSubtitles.add('[Synergy chime — abilities combine]', 2500);
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
        persistRevealed(next);
        return next;
      });
      pendingReveals = [];
    }
  });

  function pushFactionNotification(text: string) {
    if (!text) return;
    const noteId = nextToastId++;
    factionNotifications.update((n) => [...n, { id: noteId, text }]);
    setTimeout(() => {
      factionNotifications.update((n) => n.filter((x) => x.id !== noteId));
    }, FACTION_NOTIFICATION_TTL_MS);
  }

  return {
    repToasts,
    factionNotifications,
    discoveredOrder,
    revealedSynergies,
    synergyFlashTargetId,
    dispose: unsubscribe,
  };
}
