import { describe, it, expect } from 'vitest';
import { parseActionLogEntry } from './actionLogEvents';
import type { ActionLogEntry } from './game';

function entry(partial: Partial<ActionLogEntry>): ActionLogEntry {
  return {
    turn: 1,
    act: 1,
    category: '',
    type: '',
    payload: {},
    ...partial,
  };
}

describe('parseActionLogEntry', () => {
  it('parses a rep_changed entry into a typed repChanged event', () => {
    const ev = parseActionLogEntry(
      entry({
        turn: 7,
        type: 'rep_changed',
        payload: { factionId: 'ashen_circle', delta: '-3', source: 'betrayal' },
      })
    );
    expect(ev).toEqual({
      kind: 'repChanged',
      turn: 7,
      factionId: 'ashen_circle',
      delta: -3,
      source: 'betrayal',
    });
  });

  it('defaults rep_changed fields when payload is sparse', () => {
    const ev = parseActionLogEntry(entry({ type: 'rep_changed', payload: {} }));
    expect(ev).toEqual({
      kind: 'repChanged',
      turn: 1,
      factionId: 'unknown',
      delta: 0,
      source: '',
    });
  });

  it('parses a synergy_triggered entry with synergy and target ids', () => {
    const ev = parseActionLogEntry(
      entry({
        turn: 4,
        type: 'synergy_triggered',
        payload: { synergyId: 'bonewarden_hollow_bone_shiv', targetId: 'goblin-2' },
      })
    );
    expect(ev).toEqual({
      kind: 'synergyTriggered',
      turn: 4,
      synergyId: 'bonewarden_hollow_bone_shiv',
      targetId: 'goblin-2',
    });
  });

  it('yields null synergy/target ids when absent rather than throwing', () => {
    const ev = parseActionLogEntry(entry({ type: 'synergy_triggered', payload: {} }));
    expect(ev).toEqual({ kind: 'synergyTriggered', turn: 1, synergyId: null, targetId: null });
  });

  it('parses faction resolution / collision / timeline / event entries', () => {
    expect(
      parseActionLogEntry(
        entry({ category: 'faction', type: 'resolution', payload: { description: 'War averted.' } })
      )
    ).toMatchObject({ kind: 'factionResolution', description: 'War averted.' });

    expect(
      parseActionLogEntry(
        entry({ category: 'faction', type: 'executing_collision', payload: { factions: 'A, B' } })
      )
    ).toMatchObject({ kind: 'factionCollision', factions: 'A, B' });

    expect(
      parseActionLogEntry(
        entry({ category: 'faction', type: 'timeline_modified', payload: { factionId: 'cult' } })
      )
    ).toMatchObject({ kind: 'factionTimeline', factionId: 'cult' });

    expect(
      parseActionLogEntry(
        entry({ category: 'faction', type: 'event_fired', payload: { eventName: 'Eclipse' } })
      )
    ).toMatchObject({ kind: 'factionEvent', eventName: 'Eclipse' });
  });

  it('returns null for unrecognized entry types (tolerates truncated/foreign tails)', () => {
    expect(parseActionLogEntry(entry({ type: 'movement', category: 'world' }))).toBeNull();
    expect(parseActionLogEntry(entry({ category: 'faction', type: 'unknown_kind' }))).toBeNull();
  });
});
