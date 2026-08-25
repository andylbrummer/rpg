import type { ActionLogEntry } from './game';

/**
 * Typed, validated representation of the subset of server action-log entries the
 * client renders as player-facing feedback. Replaces ad-hoc `any` scanning of
 * raw `ActionLogEntry` records: each variant carries only the fields the client
 * needs, with defaults applied for sparse payloads. Unrecognized entries parse
 * to `null`, so callers tolerate truncated or foreign action-log tails.
 */
export interface RepChangedEvent {
  kind: 'repChanged';
  turn: number;
  factionId: string;
  delta: number;
  source: string;
}

export interface SynergyTriggeredEvent {
  kind: 'synergyTriggered';
  turn: number;
  synergyId: string | null;
  targetId: string | null;
}

export interface FactionResolutionEvent {
  kind: 'factionResolution';
  turn: number;
  description: string;
}

export interface FactionCollisionEvent {
  kind: 'factionCollision';
  turn: number;
  factions: string;
}

export interface FactionTimelineEvent {
  kind: 'factionTimeline';
  turn: number;
  factionId: string;
}

export interface FactionCampaignEvent {
  kind: 'factionEvent';
  turn: number;
  eventName: string;
}

export type ActionLogClientEvent =
  | RepChangedEvent
  | SynergyTriggeredEvent
  | FactionResolutionEvent
  | FactionCollisionEvent
  | FactionTimelineEvent
  | FactionCampaignEvent;

function parseIntSafe(raw: string | undefined): number {
  const n = parseInt(raw ?? '0', 10);
  return Number.isNaN(n) ? 0 : n;
}

/**
 * Validate and project a single action-log entry into a typed client event.
 * Returns `null` for any entry the client does not render (movement, combat
 * mechanics, unknown faction sub-types, etc.).
 */
export function parseActionLogEntry(entry: ActionLogEntry): ActionLogClientEvent | null {
  const payload = entry.payload ?? {};

  switch (entry.type) {
    case 'rep_changed':
      return {
        kind: 'repChanged',
        turn: entry.turn,
        factionId: payload.factionId ?? 'unknown',
        delta: parseIntSafe(payload.delta),
        source: payload.source ?? '',
      };
    case 'synergy_triggered':
      return {
        kind: 'synergyTriggered',
        turn: entry.turn,
        synergyId: payload.synergyId ?? null,
        targetId: payload.targetId ?? null,
      };
  }

  if (entry.category === 'faction') {
    switch (entry.type) {
      case 'resolution':
        return {
          kind: 'factionResolution',
          turn: entry.turn,
          description: payload.description ?? 'Faction conflict resolved.',
        };
      case 'executing_collision':
        return {
          kind: 'factionCollision',
          turn: entry.turn,
          factions: payload.factions ?? '',
        };
      case 'timeline_modified':
        return {
          kind: 'factionTimeline',
          turn: entry.turn,
          factionId: payload.factionId ?? 'A faction',
        };
      case 'event_fired':
        return {
          kind: 'factionEvent',
          turn: entry.turn,
          eventName: payload.eventName ?? 'A campaign event unfolds.',
        };
    }
  }

  return null;
}
