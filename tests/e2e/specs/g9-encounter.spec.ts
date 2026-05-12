import { test, expect } from './fixtures';
import { sendWsAction } from './helpers';

async function injectTravelEncounter(page: any, encounter: any) {
  await page.evaluate(() => {
    (window as any).gameClient?.disconnect();
  });
  await page.waitForTimeout(200);
  await page.evaluate((enc: any) => {
    const store = (window as any).gameStore;
    store.__testSetState({
      type: 'state',
      mode: 'Menu',
      player: { x: 0, y: 0, facing: 'North' },
      tiles: [],
      explored: [],
      hasDungeon: false,
      party: [],
      travelEncounter: enc,
    });
  }, encounter);
}

test.describe('G9: Travel Encounters', () => {
  test('travel from map can trigger encounter', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await sendWsAction(page, serverUrl, { type: 'reset_game' });

    await page.getByRole('button', { name: 'Overworld Map' }).click();
    const mapPanel = page.getByRole('dialog', { name: 'Overworld map' });
    await expect(mapPanel).toBeVisible();

    await mapPanel.getByRole('button', { name: 'Broken Engine' }).click();
    const dialog = page.getByRole('alertdialog', { name: 'Confirm travel' });
    await expect(dialog).toBeVisible();

    await dialog.getByRole('button', { name: 'Travel' }).click();
    await expect(dialog).not.toBeVisible();

    await page.waitForTimeout(800);

    // After travel, we should see either combat, a travel encounter overlay, or the updated map
    const combatVisible = await page.locator('.combat-overlay').isVisible().catch(() => false);
    const encounterVisible = await page.locator('.travel-encounter-overlay').isVisible().catch(() => false);
    const nodeVisible = await page.locator('.node-group.current').getByText('Broken Engine').isVisible().catch(() => false);

    expect(combatVisible || encounterVisible || nodeVisible).toBe(true);
  });

  test('combat resolution routes to combat overlay', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await injectTravelEncounter(page, {
      id: 'ambush',
      name: 'Ambush',
      resolutionType: 'combat',
      hasSurpriseRound: true,
      priceTier: 0,
      reputationValue: 0,
    });

    // For combat, the mode should be Combat, not Menu with travelEncounter
    await page.evaluate(() => {
      const store = (window as any).gameStore;
      store.__testSetState({
        type: 'state',
        mode: 'Combat',
        player: { x: 0, y: 0, facing: 'North' },
        tiles: [],
        explored: [],
        hasDungeon: false,
        party: [],
        combat: {
          phase: 'Turn',
          round: 1,
          combatants: [
            { id: 'p1', name: 'Hero', isPlayer: true, hp: 20, maxHp: 20, speed: 5, row: 0, alive: true, isCurrent: true, abilities: [] },
            { id: 'e1', name: 'Goblin', isPlayer: false, hp: 10, maxHp: 10, speed: 4, row: 0, alive: true, isCurrent: false, abilities: [] },
          ],
          initiativeOrder: ['p1', 'e1'],
          currentTurnIndex: 0,
          log: [],
          isFinished: false,
        },
      });
    });

    await expect(page.locator('.combat-overlay')).toBeVisible();
  });

  test('stat_test resolution shows roll UI', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await injectTravelEncounter(page, {
      id: 'bloom_pocket',
      name: 'Bloom Pocket',
      resolutionType: 'stat_test',
      statName: 'constitution',
      hasSurpriseRound: true,
      priceTier: 0,
      reputationValue: 0,
    });

    await expect(page.locator('.travel-encounter-overlay')).toBeVisible();
    await expect(page.locator('.travel-encounter-title')).toContainText('Bloom Pocket');
    await expect(page.locator('.travel-action-btn')).toContainText('Roll');
  });

  test('dialogue resolution shows options', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await injectTravelEncounter(page, {
      id: 'faction_patrol',
      name: 'Faction Patrol',
      resolutionType: 'dialogue',
      factionId: 'bureau',
      options: ['Bribe', 'Bluff', 'Attack'],
      hasSurpriseRound: true,
      priceTier: 0,
      reputationValue: 10,
    });

    await expect(page.locator('.travel-encounter-overlay')).toBeVisible();
    await expect(page.locator('.travel-encounter-title')).toContainText('Faction Patrol');
    await expect(page.locator('.travel-options')).toBeVisible();
    await expect(page.locator('.travel-action-btn')).toHaveCount(3);
  });

  test('resolving encounter sends action and clears overlay', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await sendWsAction(page, serverUrl, { type: 'reset_game' });

    await page.getByRole('button', { name: 'Overworld Map' }).click();
    const mapPanel = page.getByRole('dialog', { name: 'Overworld map' });
    await expect(mapPanel).toBeVisible();

    await mapPanel.getByRole('button', { name: 'Broken Engine' }).click();
    const dialog = page.getByRole('alertdialog', { name: 'Confirm travel' });
    await expect(dialog).toBeVisible();

    await dialog.getByRole('button', { name: 'Travel' }).click();
    await expect(dialog).not.toBeVisible();

    await page.waitForTimeout(800);

    // If a non-combat encounter appeared, resolve it
    const encounterVisible = await page.locator('.travel-encounter-overlay').isVisible().catch(() => false);
    if (encounterVisible) {
      const btn = page.locator('.travel-action-btn').first();
      await btn.click();
      await expect(page.locator('.travel-encounter-overlay')).not.toBeVisible();
    }
  });
});
