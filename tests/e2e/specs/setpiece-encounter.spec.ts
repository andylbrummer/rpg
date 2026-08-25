import { test, expect } from './fixtures';

function makeMockCombatWithBoss() {
  const combatants = [
    { id: 'p0', name: 'Hero1', isPlayer: true, classId: 'warrior', hp: 100, maxHp: 100, speed: 5, row: 0, alive: true, isCurrent: true, abilities: [] },
    { id: 'e0', name: 'bone_archer_1', isPlayer: false, hp: 50, maxHp: 50, speed: 4, row: 0, alive: true, isCurrent: false, abilities: [] },
    { id: 'e1', name: 'bone_archer_2', isPlayer: false, hp: 50, maxHp: 50, speed: 4, row: 1, alive: true, isCurrent: false, abilities: [] },
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

async function injectGameState(page: any, state: any) {
  await page.evaluate((s: any) => {
    const store = (window as any).gameStore;
    store.__testSetState(s);
  }, state);
}

test.describe('Setpiece Encounter', () => {
  test('boss encounter renders expected enemies', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await page.waitForTimeout(500);

    const combat = makeMockCombatWithBoss();
    await injectGameState(page, {
      type: 'state',
      mode: 'Combat',
      player: { x: 0, y: 0, facing: 'North' },
      tiles: [],
      explored: [],
      hasDungeon: true,
      party: [],
      combat,
      actionLog: [
        { turn: 1, category: 'combat', type: 'encounter_started', payload: { encounterId: 'boss-encounter-1' } }
      ]
    });

    await expect(page.locator('.combat-overlay')).toBeVisible({ timeout: 5000 });
    await expect(page.locator('.enemy-side')).toContainText('bone_archer_1', { timeout: 5000 });
  });
});
