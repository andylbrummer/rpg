export type ColorblindMode = 'none' | 'protanopia' | 'deuteranopia' | 'tritanopia';

export const COLORBLIND_MODES: ColorblindMode[] = ['none', 'protanopia', 'deuteranopia', 'tritanopia'];

export const COLORBLIND_LABELS: Record<ColorblindMode, string> = {
  none: 'Off',
  protanopia: 'Protanopia (red-weak)',
  deuteranopia: 'Deuteranopia (green-weak)',
  tritanopia: 'Tritanopia (blue-weak)',
};

export interface AccessibilitySettings {
  colorblindMode: ColorblindMode;
  /** Multiplier applied to base UI font size. */
  textScale: number;
  reduceMotion: boolean;
  highContrast: boolean;
}

export const TEXT_SCALE_MIN = 0.8;
export const TEXT_SCALE_MAX = 1.5;

export const DEFAULT_ACCESSIBILITY_SETTINGS: AccessibilitySettings = {
  colorblindMode: 'none',
  textScale: 1,
  reduceMotion: false,
  highContrast: false,
};

const STORAGE_KEY = 'rpc_accessibility_settings';

function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value));
}

export function normalizeAccessibilitySettings(
  raw: Partial<AccessibilitySettings> | null | undefined,
): AccessibilitySettings {
  const d = DEFAULT_ACCESSIBILITY_SETTINGS;
  if (!raw) return { ...d };
  return {
    colorblindMode: COLORBLIND_MODES.includes(raw.colorblindMode as ColorblindMode)
      ? (raw.colorblindMode as ColorblindMode)
      : d.colorblindMode,
    textScale: clamp(typeof raw.textScale === 'number' ? raw.textScale : d.textScale, TEXT_SCALE_MIN, TEXT_SCALE_MAX),
    reduceMotion: typeof raw.reduceMotion === 'boolean' ? raw.reduceMotion : d.reduceMotion,
    highContrast: typeof raw.highContrast === 'boolean' ? raw.highContrast : d.highContrast,
  };
}

export function loadAccessibilitySettings(): AccessibilitySettings {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (raw) return normalizeAccessibilitySettings(JSON.parse(raw));
  } catch {
    // ignore
  }
  return { ...DEFAULT_ACCESSIBILITY_SETTINGS };
}

export function saveAccessibilitySettings(settings: AccessibilitySettings): void {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(normalizeAccessibilitySettings(settings)));
}

export function resetAccessibilitySettings(): AccessibilitySettings {
  const defaults = { ...DEFAULT_ACCESSIBILITY_SETTINGS };
  saveAccessibilitySettings(defaults);
  return defaults;
}

/**
 * Apply settings to the document: data-* attributes drive CSS (colorblind palette, high-contrast
 * outlines, motion reduction) and a CSS custom property scales text. Safe to call repeatedly.
 */
export function applyAccessibilityToDocument(settings: AccessibilitySettings): void {
  if (typeof document === 'undefined') return;
  const root = document.documentElement;
  root.dataset.colorblind = settings.colorblindMode;
  root.dataset.highContrast = settings.highContrast ? 'on' : 'off';
  root.dataset.reduceMotion = settings.reduceMotion ? 'on' : 'off';
  root.style.setProperty('--text-scale', String(settings.textScale));
}
