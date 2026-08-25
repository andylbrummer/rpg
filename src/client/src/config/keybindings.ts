export type KeybindingContext = 'global' | 'combat' | 'overworld' | 'town';

export interface Keybinding {
  action: string;
  key: string;
  /** Context the binding is active in. Omitted/legacy bindings are treated as 'global'. */
  context?: KeybindingContext;
}

export interface ActionDef {
  action: string;
  label: string;
  context: KeybindingContext;
}

export const CONTEXT_LABELS: Record<KeybindingContext, string> = {
  global: 'Global',
  overworld: 'Exploration & Overworld',
  combat: 'Combat',
  town: 'Town',
};

export const CONTEXT_ORDER: KeybindingContext[] = ['overworld', 'combat', 'town', 'global'];

/** Source of truth for bindable actions and the context each belongs to. */
export const ACTIONS: ActionDef[] = [
  { action: 'move_forward', label: 'Move Forward', context: 'overworld' },
  { action: 'move_back', label: 'Move Back', context: 'overworld' },
  { action: 'strafe_left', label: 'Strafe Left', context: 'overworld' },
  { action: 'strafe_right', label: 'Strafe Right', context: 'overworld' },
  { action: 'turn_left', label: 'Turn Left', context: 'overworld' },
  { action: 'turn_right', label: 'Turn Right', context: 'overworld' },
  { action: 'attack', label: 'Attack', context: 'combat' },
  { action: 'defend', label: 'Defend', context: 'combat' },
  { action: 'flee', label: 'Flee', context: 'combat' },
  { action: 'cycle_target', label: 'Cycle Target', context: 'combat' },
  { action: 'open_map', label: 'Open Map', context: 'town' },
  { action: 'open_party', label: 'Open Party', context: 'town' },
  { action: 'rest', label: 'Rest', context: 'town' },
];

export const ACTION_LABELS: Record<string, string> =
  Object.fromEntries(ACTIONS.map(a => [a.action, a.label]));

export const ACTION_CONTEXT: Record<string, KeybindingContext> =
  Object.fromEntries(ACTIONS.map(a => [a.action, a.context]));

export const DEFAULT_BINDINGS: Keybinding[] = [
  { action: 'move_forward', key: 'w', context: 'overworld' },
  { action: 'move_forward', key: 'ArrowUp', context: 'overworld' },
  { action: 'move_back', key: 's', context: 'overworld' },
  { action: 'move_back', key: 'ArrowDown', context: 'overworld' },
  { action: 'strafe_left', key: 'a', context: 'overworld' },
  { action: 'strafe_right', key: 'd', context: 'overworld' },
  { action: 'turn_left', key: 'q', context: 'overworld' },
  { action: 'turn_left', key: 'ArrowLeft', context: 'overworld' },
  { action: 'turn_right', key: 'e', context: 'overworld' },
  { action: 'turn_right', key: 'ArrowRight', context: 'overworld' },
  { action: 'attack', key: '1', context: 'combat' },
  { action: 'defend', key: '2', context: 'combat' },
  { action: 'flee', key: 'f', context: 'combat' },
  { action: 'cycle_target', key: 'Tab', context: 'combat' },
  { action: 'open_map', key: 'm', context: 'town' },
  { action: 'open_party', key: 'p', context: 'town' },
  { action: 'rest', key: 'r', context: 'town' },
];

const STORAGE_KEY = 'rpc_keybindings';

/** Normalize a keyboard event into a chord string, e.g. "Ctrl+Shift+S" or "ArrowUp". */
export function eventToChord(event: KeyboardEvent): string {
  const parts: string[] = [];
  if (event.ctrlKey) parts.push('Ctrl');
  if (event.altKey) parts.push('Alt');
  if (event.shiftKey) parts.push('Shift');
  if (event.metaKey) parts.push('Meta');

  let key = event.key;
  // A bare modifier press is not a complete chord.
  if (key === 'Control' || key === 'Alt' || key === 'Shift' || key === 'Meta') {
    return parts.join('+');
  }
  if (key === ' ') key = 'Space';
  else if (key.length === 1) key = key.toUpperCase();

  parts.push(key);
  return parts.join('+');
}

function contextOf(b: Keybinding): KeybindingContext {
  return b.context ?? 'global';
}

export function loadBindings(): Keybinding[] {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (raw) return JSON.parse(raw);
  } catch {
    // ignore
  }
  return structuredClone(DEFAULT_BINDINGS);
}

export function saveBindings(bindings: Keybinding[]) {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(bindings));
}

export function resetToDefaults(): Keybinding[] {
  const defaults = structuredClone(DEFAULT_BINDINGS);
  saveBindings(defaults);
  return defaults;
}

/**
 * Resolve a chord to an action within a context. Bindings whose context matches (or that are
 * global) are considered; a context-specific match wins over a global one.
 */
export function keyToAction(
  bindings: Keybinding[],
  key: string,
  context: KeybindingContext = 'global',
): string | null {
  const lower = key.toLowerCase();
  const active = bindings.filter(b => {
    const c = contextOf(b);
    return c === context || c === 'global';
  });
  const matches = active.filter(b => b.key === key || b.key.toLowerCase() === lower);
  if (matches.length === 0) return null;
  // Prefer a context-specific binding over a global fallback.
  const specific = matches.find(b => contextOf(b) === context);
  return (specific ?? matches[0]).action;
}

/**
 * Conflicts are duplicate keys within the same context. Returned map is keyed "context|key"
 * with the list of conflicting actions.
 */
export function findConflicts(bindings: Keybinding[]): Map<string, string[]> {
  const byBucket = new Map<string, string[]>();
  for (const b of bindings) {
    const bucket = `${contextOf(b)}|${b.key}`;
    const list = byBucket.get(bucket) ?? [];
    list.push(b.action);
    byBucket.set(bucket, list);
  }
  const conflicts = new Map<string, string[]>();
  for (const [bucket, actions] of byBucket) {
    if (actions.length > 1) conflicts.set(bucket, actions);
  }
  return conflicts;
}
