export interface Keybinding {
  action: string;
  key: string;
}

export const DEFAULT_BINDINGS: Keybinding[] = [
  { action: 'move_forward', key: 'w' },
  { action: 'move_forward', key: 'ArrowUp' },
  { action: 'move_back', key: 's' },
  { action: 'move_back', key: 'ArrowDown' },
  { action: 'strafe_left', key: 'a' },
  { action: 'strafe_right', key: 'd' },
  { action: 'turn_left', key: 'q' },
  { action: 'turn_left', key: 'ArrowLeft' },
  { action: 'turn_right', key: 'e' },
  { action: 'turn_right', key: 'ArrowRight' },
];

export const ACTION_LABELS: Record<string, string> = {
  move_forward: 'Move Forward',
  move_back: 'Move Back',
  strafe_left: 'Strafe Left',
  strafe_right: 'Strafe Right',
  turn_left: 'Turn Left',
  turn_right: 'Turn Right',
};

const STORAGE_KEY = 'rpc_keybindings';

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

export function keyToAction(bindings: Keybinding[], key: string): string | null {
  const lowerKey = key.toLowerCase();
  // Check exact match first, then case-insensitive
  const exact = bindings.find(b => b.key === key);
  if (exact) return exact.action;
  const fuzzy = bindings.find(b => b.key.toLowerCase() === lowerKey);
  return fuzzy?.action ?? null;
}

export function findConflicts(bindings: Keybinding[]): Map<string, string[]> {
  const byKey = new Map<string, string[]>();
  for (const b of bindings) {
    const list = byKey.get(b.key) ?? [];
    list.push(b.action);
    byKey.set(b.key, list);
  }
  const conflicts = new Map<string, string[]>();
  for (const [key, actions] of byKey) {
    if (actions.length > 1) conflicts.set(key, actions);
  }
  return conflicts;
}
