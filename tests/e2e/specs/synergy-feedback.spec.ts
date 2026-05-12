import { test, expect } from './fixtures';

function makeMockCombat(partyCount: number, enemyCount: number) {
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
      name: `Enemy${i + 1}`,
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

async function injectGameState(page: any, state: any) {
  await page.evaluate((s: any) => {
    const store = (window as any).gameStore;
    store.__testSetState(s);
  }, state);
}

test.describe('Synergy Feedback', () => {
  test('synergy trigger shows 500ms flash on target', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await page.waitForTimeout(500);

    const combat = makeMockCombat(2, 2);
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
        { turn: 100, category: 'combat', type: 'encounter_started', payload: { encounterId: 'enc-1' } },
        { turn: 101, category: 'combat', type: 'synergy_triggered', payload: { synergyId: 'stillblade_hollow_backstep', encounterId: 'enc-1', targetId: 'e0' } }
      ]
    });

    await expect(page.locator('.combat-overlay')).toBeVisible();
    await page.waitForTimeout(100);

    // Debug: check store state and flash class
    const storeState = await page.evaluate(() => {
      const store = (window as any).gameStore;
      let currentState: any = null;
      const unsub = store.subscribe((s: any) => { currentState = s; });
      unsub();
      return {
        mode: currentState?.mode,
        actionLogCount: currentState?.actionLog?.length,
        hasFlash: document.querySelector('.synergy-flash') !== null,
        flashTarget: document.querySelector('[data-testid="combat-overlay"]')?.getAttribute('data-flash-target') ?? 'no-overlay',
      };
    });
    // Force output via expect message
    expect(storeState.hasFlash, JSON.stringify(storeState)).toBe(true);

    const target = page.locator('.enemy-side .row-band.front-band .combatant').first();
    await expect(target).toHaveClass(/synergy-flash/);

    // Wait for 500ms flash to end
    await page.waitForTimeout(600);
    await expect(target).not.toHaveClass(/synergy-flash/);
  });

  test('field notes reveals entry post-combat not during', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await page.waitForTimeout(500);

    const combat = makeMockCombat(2, 2);
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
        { turn: 1, category: 'combat', type: 'encounter_started', payload: { encounterId: 'enc-1' } },
        { turn: 2, category: 'combat', type: 'synergy_triggered', payload: { synergyId: 'stillblade_hollow_backstep', encounterId: 'enc-1', targetId: 'e0' } }
      ]
    });

    // Field Notes button should not be visible during combat
    await expect(page.locator('.field-notes-toggle')).not.toBeVisible();

    // End combat by switching to exploration
    await injectGameState(page, {
      type: 'state',
      mode: 'Exploration',
      player: { x: 0, y: 0, facing: 'North' },
      tiles: [],
      explored: [],
      hasDungeon: true,
      party: [],
      actionLog: [
        { turn: 100, category: 'combat', type: 'encounter_started', payload: { encounterId: 'enc-1' } },
        { turn: 101, category: 'combat', type: 'synergy_triggered', payload: { synergyId: 'stillblade_hollow_backstep', encounterId: 'enc-1', targetId: 'e0' } },
        { turn: 102, category: 'combat', type: 'encounter_won', payload: { encounterId: 'enc-1' } }
      ]
    });

    // Field Notes button should now be visible
    await expect(page.locator('.field-notes-toggle')).toBeVisible();
    await page.locator('.field-notes-toggle').click();

    // The discovered synergy should now be revealed
    await expect(page.locator('.field-note-entry .field-note-names', { hasText: 'backstep + cheap_shot' })).toBeVisible();

    // Undiscovered synergies should still show ???
    await expect(page.locator('.field-note-entry .field-note-names', { hasText: '??? + ???' })).toHaveCount(3);
  });

  test('replay modal opens and shows animation', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await page.waitForTimeout(500);

    // Pre-seed discovered and revealed synergies
    await page.evaluate(() => {
      localStorage.setItem('rpc_discovered_synergies', JSON.stringify(['stillblade_hollow_backstep']));
      localStorage.setItem('rpc_revealed_synergies', JSON.stringify(['stillblade_hollow_backstep']));
    });

    // Reload so the app picks up localStorage
    await page.reload();
    await page.waitForSelector('.field-notes-toggle');

    await page.locator('.field-notes-toggle').click();
    await page.locator('.replay-btn').first().click();

    // Verify modal and animation are visible
    await expect(page.locator('.replay-modal-overlay')).toBeVisible();
    await expect(page.locator('.replay-anim')).toBeVisible();

    // Close modal
    await page.locator('.replay-close-btn').click();
    await expect(page.locator('.replay-modal-overlay')).not.toBeVisible();
  });
});
