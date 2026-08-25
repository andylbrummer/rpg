import { test, expect, type Page } from '@playwright/test';

function menuState() {
  return {
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
      factionVendors: [],
      factionContacts: [],
      tavernRoster: [],
      viewedMissions: [],
      questLog: [],
    },
    overworld: { currentNodeId: 'the_reach', nodes: [], routes: [], turns: 0 },
    reputation: {},
    partyGold: 0,
    partyInventory: [],
    actionLog: [],
  };
}

async function load(page: Page) {
  await page.goto('/app');
  await page.waitForFunction(() => Boolean((window as any).gameStore?.__testSetState && (window as any).__rpc_subtitles));
}

test.describe('Subtitle system', () => {
  test('a tagged audio caption appears in the subtitle overlay', async ({ page }) => {
    await load(page);

    await page.evaluate((state) => {
      (window as any).__rpc_subtitles.add('[Synergy chime — abilities combine]', 5000);
      // A state update drives the overlay poll.
      (window as any).gameStore.__testSetState(state);
    }, menuState());

    await expect(page.locator('.subtitle-overlay')).toBeVisible();
    await expect(page.locator('.subtitle-line')).toContainText('Synergy chime');
  });

  test('expired captions are dropped from the overlay', async ({ page }) => {
    await load(page);

    await page.evaluate((state) => {
      (window as any).__rpc_subtitles.add('[Brief caption]', 200);
      (window as any).gameStore.__testSetState(state);
    }, menuState());
    await expect(page.locator('.subtitle-line')).toContainText('Brief caption');

    // After the duration elapses, the next poll filters it out.
    await page.waitForTimeout(400);
    await page.evaluate((state) => (window as any).gameStore.__testSetState(state), menuState());
    await expect(page.locator('.subtitle-line')).toHaveCount(0);
  });
});
