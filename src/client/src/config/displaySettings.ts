export interface DisplaySettings {
  /** Vertical field of view in degrees. */
  fov: number;
  /** Render resolution scale (1 = native, <1 sharper perf, >1 supersample). */
  resolutionScale: number;
  vsync: boolean;
  fullscreen: boolean;
}

export const FOV_MIN = 60;
export const FOV_MAX = 110;

export const RESOLUTION_SCALES = [0.75, 1, 1.25] as const;

export const DEFAULT_DISPLAY_SETTINGS: DisplaySettings = {
  fov: 75,
  resolutionScale: 1,
  vsync: true,
  fullscreen: false,
};

const STORAGE_KEY = 'rpc_display_settings';

function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value));
}

/** Coerce arbitrary input into a valid DisplaySettings, clamping out-of-range values. */
export function normalizeDisplaySettings(raw: Partial<DisplaySettings> | null | undefined): DisplaySettings {
  const d = DEFAULT_DISPLAY_SETTINGS;
  if (!raw) return { ...d };
  const scale = typeof raw.resolutionScale === 'number' ? raw.resolutionScale : d.resolutionScale;
  return {
    fov: clamp(typeof raw.fov === 'number' ? raw.fov : d.fov, FOV_MIN, FOV_MAX),
    resolutionScale: clamp(scale, 0.5, 2),
    vsync: typeof raw.vsync === 'boolean' ? raw.vsync : d.vsync,
    fullscreen: typeof raw.fullscreen === 'boolean' ? raw.fullscreen : d.fullscreen,
  };
}

export function loadDisplaySettings(): DisplaySettings {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (raw) return normalizeDisplaySettings(JSON.parse(raw));
  } catch {
    // ignore
  }
  return { ...DEFAULT_DISPLAY_SETTINGS };
}

export function saveDisplaySettings(settings: DisplaySettings): void {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(normalizeDisplaySettings(settings)));
}

export function resetDisplaySettings(): DisplaySettings {
  const defaults = { ...DEFAULT_DISPLAY_SETTINGS };
  saveDisplaySettings(defaults);
  return defaults;
}
