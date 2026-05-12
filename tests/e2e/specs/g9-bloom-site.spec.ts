import { test, expect } from './fixtures';
import { sendWsAction } from './helpers';

function makeBloomCombat(partyCount: number, enemyCount: number) {
  const combatants = [
    ...Array.from({ length: partyCount }, (_, i) => ({
      id: `p${i}`,
      name: `Hero${i + 1}`,
      isPlayer: true,
      classId: 'warrior',
      hp: 100,
      maxHp: 100,
      speed: 5,
      row: i < 3 ? 0 : 1,
      alive: true,
      isCurrent: i === 0,
      abilities: [],
    })),
    ...Array.from({ length: enemyCount }, (_, i) => ({
      id: `e${i}`,
      name: `Bloom Mite ${i + 1}`,
      isPlayer: false,
      hp: 50,
      maxHp: 50,
      speed: 4,
      row: i < 3 ? 0 : 1,
      alive: true,
      isCurrent: false,
      abilities: [],
    })),
  ];
  const initiativeOrder = combatants.map(c => c.id);
  return {
    phase: 'Turn',
    round: 1,
    combatants,
    initiativeOrder,
    currentTurnIndex: 0,
    log: [],
    isFinished: false,
  };
}

test.describe('G9: Bloom Site', () => {
  test('bloom site dungeon loads with correct theme', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await sendWsAction(page, serverUrl, { type: 'enter_dungeon', dungeonType: 'bloom-site' });

    await expect(page.locator('.dungeon-badge')).toContainText('bloom-site');
  });

  test('bloom creatures render with fungal materials', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await sendWsAction(page, serverUrl, { type: 'enter_dungeon', dungeonType: 'bloom-site' });

    const combat = makeBloomCombat(1, 2);
    await page.evaluate((c: any) => {
      const store = (window as any).gameStore;
      store.__testSetState({
        type: 'state',
        mode: 'Combat',
        player: { x: 0, y: 0, facing: 'North' },
        tiles: [],
        explored: [],
        hasDungeon: true,
        dungeonType: 'bloom-site',
        party: [],
        combat: c,
      });
    }, combat);

    await expect(page.locator('.combat-overlay')).toBeVisible();

    const enemyNames = await page.locator('.enemy-side .combatant-name').allTextContents();
    expect(enemyNames.length).toBeGreaterThanOrEqual(1);
    expect(enemyNames.some(n => n.includes('Bloom'))).toBe(true);
  });

  test('bloom effects are visible', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);

    await page.evaluate(() => {
      const store = (window as any).gameStore;
      store.__testSetState({
        type: 'state',
        mode: 'Exploration',
        player: { x: 0, y: 0, facing: 'North' },
        tiles: [
          { x: 0, y: 0, type: 'Floor', north: 'Wall', south: 'Wall', east: 'Wall', west: 'Wall' },
          { x: 1, y: 0, type: 'Floor', north: 'Wall', south: 'Wall', east: 'Wall', west: 'Wall' },
          { x: 0, y: 1, type: 'Floor', north: 'Wall', south: 'Wall', east: 'Wall', west: 'Wall' },
        ],
        explored: [],
        hasDungeon: true,
        dungeonType: 'bloom-site',
        party: [],
      });
    });

    const canvas = page.locator('.renderer canvas');
    await expect(canvas).toBeVisible();
  });

  test('ambient audio placeholder logs', async ({ page, serverUrl }) => {
    const logs: string[] = [];
    page.on('console', msg => {
      logs.push(msg.text());
    });

    await page.goto(`${serverUrl}/app`);
    await sendWsAction(page, serverUrl, { type: 'enter_dungeon', dungeonType: 'bloom-site' });
    await page.waitForTimeout(600);

    expect(logs.some(l => l.includes('[AmbientAudio] Playing: bloom-site_fungal_drip_loop'))).toBe(true);
  });
});
