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

const boneyardTheme: DungeonTheme = {
  wallColor: '#b8b0a0',
  floorColor: '#8a8278',
  doorColor: '#5c5448',
  accentColor: '#e0d8c8',
  stairsUp: 0xe0d8c8,
  stairsDown: 0x8a8278,
  secretDoor: 0x9a9288,
  torchColor: 0xffcc88,
  ambientColor: 0x8a8278,
  fillColor: 0xb8b0a0,
  rimColor: 0xe0d8c8,
  fogColor: 0x1a1814,
  backgroundColor: 0x1a1814,
  glowIntensity: 2,
};

const sealedVaultTheme: DungeonTheme = {
  wallColor: '#6b5c3a',
  floorColor: '#3a3020',
  doorColor: '#8a7a50',
  accentColor: '#c4a84a',
  stairsUp: 0xc4a84a,
  stairsDown: 0x6b5c3a,
  secretDoor: 0x7a6a48,
  torchColor: 0x44aaff,
  ambientColor: 0x3a3020,
  fillColor: 0x6b5c3a,
  rimColor: 0xc4a84a,
  fogColor: 0x0a0804,
  backgroundColor: 0x0a0804,
  glowIntensity: 3,
};

const settlementTheme: DungeonTheme = {
  wallColor: '#7a6050',
  floorColor: '#4a3a30',
  doorColor: '#5c4a3e',
  accentColor: '#a08060',
  stairsUp: 0xa08060,
  stairsDown: 0x7a6050,
  secretDoor: 0x8a7060,
  torchColor: 0xff8844,
  ambientColor: 0x4a3a30,
  fillColor: 0x7a6050,
  rimColor: 0xa08060,
  fogColor: 0x1a1410,
  backgroundColor: 0x1a1410,
  glowIntensity: 2,
};

const ossuaryTheme: DungeonTheme = {
  wallColor: '#5a5a60',
  floorColor: '#3a3a40',
  doorColor: '#4a4a50',
  accentColor: '#8888a0',
  stairsUp: 0x8888a0,
  stairsDown: 0x5a5a60,
  secretDoor: 0x6a6a70,
  torchColor: 0xaabbcc,
  ambientColor: 0x3a3a40,
  fillColor: 0x5a5a60,
  rimColor: 0x8888a0,
  fogColor: 0x0a0a10,
  backgroundColor: 0x0a0a10,
  glowIntensity: 2,
};

export function getTheme(dungeonType: string | undefined): DungeonTheme {
  const normalized = dungeonType?.toLowerCase().replace(/_/g, '-');
  switch (normalized) {
    case 'bloom-site':
      return bloomTheme;
    case 'boneyard':
      return boneyardTheme;
    case 'sealed-vault':
      return sealedVaultTheme;
    case 'settlement-gone-wrong':
      return settlementTheme;
    case 'ossuary':
      return ossuaryTheme;
    default:
      return defaultTheme;
  }
}
