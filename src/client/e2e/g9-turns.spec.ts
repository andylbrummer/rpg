import { test, expect } from '@playwright/test';

async function injectState(page: any, turns: number, campaignEnded = false) {
  await page.evaluate((state: any) => {
    (window as any).__rpc_enableTestHooks();
    const store = (window as any).gameStore;
    store.__testSetState(state);
  }, {
    type: 'state',
    mode: 'Menu',
    player: { x: 0, y: 0, facing: 'North' },
    tiles: [],
    explored: [],
    hasDungeon: false,
    party: [],
    town: {
      currentTownId: 'the_reach',
      availableMissions: [],
      vendorStock: [],
      factionContacts: [],
      tavernRoster: [],
      viewedMissions: []
    },
    overworld: {
      currentNodeId: 'the_reach',
      nodes: [
        { id: 'the_reach', name: 'The Reach', type: 'town' }
      ],
      routes: [],
      turns
    },
    campaignEnded
  });
}

test.describe('Turn Counter', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/app');
    await expect(page.locator('.game')).toBeVisible({ timeout: 5000 });
  });

  test('shows turn counter in top bar', async ({ page }) => {
    await injectState(page, 3);
    const counter = page.locator('.turn-counter');
    await expect(counter).toBeVisible();
    await expect(counter).toContainText('Turn 3/15');
  });

  test('default color at turns 1-9', async ({ page }) => {
    await injectState(page, 9);
    const counter = page.locator('.turn-counter');
    await expect(counter).toHaveCSS('color', 'rgb(204, 204, 204)');
  });

  test('yellow color at turns 10-12', async ({ page }) => {
    await injectState(page, 10);
    const counter = page.locator('.turn-counter');
    await expect(counter).toHaveCSS('color', 'rgb(212, 168, 75)');
  });

  test('red color at turns 13-15', async ({ page }) => {
    await injectState(page, 13);
    const counter = page.locator('.turn-counter');
    await expect(counter).toHaveCSS('color', 'rgb(204, 68, 68)');
  });

  test('campaign end overlay shown when campaignEnded is true', async ({ page }) => {
    await injectState(page, 15, true);
    const overlay = page.locator('.campaign-end-overlay');
    await expect(overlay).toBeVisible();
    await expect(page.locator('.campaign-end-title')).toContainText('Campaign Complete');
    await expect(page.locator('.campaign-end-turns')).toContainText('Final Turn: 15/15');
  });
});
