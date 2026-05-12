export interface DungeonTheme {
  wallColor: string;
  floorColor: string;
  doorColor: string;
  accentColor: string;
  stairsUp: number;
  stairsDown: number;
  secretDoor: number;
  torchColor: number;
  ambientColor: number;
  fillColor: number;
  rimColor: number;
  fogColor: number;
  backgroundColor: number;
  glowIntensity: number;
}

const defaultTheme: DungeonTheme = {
  wallColor: '#7a5c4a',
  floorColor: '#555555',
  doorColor: '#654321',
  accentColor: '#d4a84b',
  stairsUp: 0xccaa66,
  stairsDown: 0x886644,
  secretDoor: 0x998877,
  torchColor: 0xffaa44,
  ambientColor: 0x666666,
  fillColor: 0xaaccff,
  rimColor: 0xffddaa,
  fogColor: 0x111111,
  backgroundColor: 0x111111,
  glowIntensity: 2,
};

const bloomTheme: DungeonTheme = {
  wallColor: '#2d5016',
  floorColor: '#1a0f2e',
  doorColor: '#6b2d5c',
  accentColor: '#88ff44',
  stairsUp: 0x88ff44,
  stairsDown: 0x6b2d5c,
  secretDoor: 0x4a2d6b,
  torchColor: 0x88ff44,
  ambientColor: 0x1a0f2e,
  fillColor: 0x2d5016,
  rimColor: 0x6b2d5c,
  fogColor: 0x0a0514,
  backgroundColor: 0x0a0514,
  glowIntensity: 3,
};

export function getTheme(dungeonType: string | undefined): DungeonTheme {
  const normalized = dungeonType?.toLowerCase().replace(/_/g, '-');
  if (normalized === 'bloom-site') {
    return bloomTheme;
  }
  return defaultTheme;
}
