import { describe, it, expect } from 'vitest';
import {
  reconcileJournal,
  createMemoryJournalPersistence,
} from './synergyJournal';

describe('reconcileJournal', () => {
  it('treats server-discovered synergies as authoritative discovery order', () => {
    const result = reconcileJournal([], [], ['a', 'b', 'c']);
    expect(result.discoveredOrder).toEqual(['a', 'b', 'c']);
  });

  it('marks prior-session (server) discoveries as already revealed', () => {
    // A synergy discovered in a prior save session has no pending post-combat
    // animation on reload, so it must surface as revealed in the journal.
    const result = reconcileJournal([], [], ['a', 'b']);
    expect(new Set(result.revealed)).toEqual(new Set(['a', 'b']));
  });

  it('appends local-only discoveries the server does not yet know about', () => {
    const result = reconcileJournal(['a', 'z'], ['a'], ['a', 'b']);
    expect(result.discoveredOrder).toEqual(['a', 'b', 'z']);
    expect(new Set(result.revealed)).toEqual(new Set(['a', 'b']));
  });

  it('preserves locally-revealed ids not present in server discovery', () => {
    const result = reconcileJournal(['x'], ['x'], []);
    expect(new Set(result.revealed)).toEqual(new Set(['x']));
    expect(result.discoveredOrder).toEqual(['x']);
  });

  it('reports changed=false when local already matches the server', () => {
    const result = reconcileJournal(['a', 'b'], ['a', 'b'], ['a', 'b']);
    expect(result.changed).toBe(false);
  });

  it('reports changed=true when reconciliation alters local state', () => {
    const result = reconcileJournal([], [], ['a']);
    expect(result.changed).toBe(true);
  });
});

describe('createMemoryJournalPersistence', () => {
  it('round-trips discovered and revealed sets without a browser', () => {
    const p = createMemoryJournalPersistence();
    p.saveDiscovered(['a', 'b']);
    p.saveRevealed(['a']);
    expect(p.loadDiscovered()).toEqual(['a', 'b']);
    expect(p.loadRevealed()).toEqual(['a']);
  });

  it('seeds from initial values', () => {
    const p = createMemoryJournalPersistence({ discovered: ['x'], revealed: ['x'] });
    expect(p.loadDiscovered()).toEqual(['x']);
    expect(p.loadRevealed()).toEqual(['x']);
  });
});
