export interface SynergyDef {
  id: string;
  abilities: string[];
  anti: boolean;
  effect: {
    type: string;
    value?: number;
    appliesAfter?: string;
  };
  hint: string;
  fieldNotes?: {
    discoveredBy?: string | null;
  };
  hidden?: boolean;
}

const modules = import.meta.glob<{ default: SynergyDef }>('../../../../content/synergies/*.json', { eager: true });

export const ALL_SYNERGIES: SynergyDef[] = Object.values(modules).map(m => m.default);
export const VISIBLE_SYNERGIES = ALL_SYNERGIES.filter(s => !s.hidden);
export const HIDDEN_SYNERGIES = ALL_SYNERGIES.filter(s => s.hidden);
