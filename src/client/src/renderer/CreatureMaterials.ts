import type { DungeonTheme } from './DungeonTheme';

export interface CreatureMaterialSet {
  body: number;
  detail: number;
  emissive: number;
}

export function getCreatureMaterials(
  dungeonType: string,
  _theme: DungeonTheme
): CreatureMaterialSet {
  const normalized = dungeonType?.toLowerCase().replace(/_/g, '-') ?? '';
  if (normalized === 'bloom-site') {
    return {
      body: 0x1a2d0f,
      detail: 0x6b2d5c,
      emissive: 0x88ff44,
    };
  }
  return {
    body: 0x8b7355,
    detail: 0x5c4a3a,
    emissive: 0x000000,
  };
}
