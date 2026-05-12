import { test, expect } from './fixtures';

async function injectGameState(page: any, state: any) {
  await page.evaluate((s: any) => {
    const store = (window as any).gameStore;
    store.__testSetState(s);
  }, state);
}

test.describe('Field Notes Journal', () => {
  test('J key opens and closes panel from town', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await page.waitForTimeout(500);

    await injectGameState(page, {
      type: 'state',
      mode: 'Menu',
      player: { x: 0, y: 0, facing: 'North' },
      tiles: [],
      explored: [],
      hasDungeon: false,
      party: [],
    });

    await page.keyboard.press('j');
    await expect(page.locator('[role="dialog"][aria-label="Field Notes"]')).toBeVisible();

    await page.keyboard.press('j');
    await expect(page.locator('[role="dialog"][aria-label="Field Notes"]')).not.toBeVisible();
  });

  test('J key opens panel from dungeon exploration', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await page.waitForTimeout(500);

    await injectGameState(page, {
      type: 'state',
      mode: 'Exploration',
      player: { x: 0, y: 0, facing: 'North' },
      tiles: [],
      explored: [],
      hasDungeon: true,
      party: [],
    });

    await page.keyboard.press('j');
    await expect(page.locator('[role="dialog"][aria-label="Field Notes"]')).toBeVisible();
  });

  test('Escape closes field notes panel', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await page.waitForTimeout(500);

    await injectGameState(page, {
      type: 'state',
      mode: 'Menu',
      player: { x: 0, y: 0, facing: 'North' },
      tiles: [],
      explored: [],
      hasDungeon: false,
      party: [],
    });

    await page.keyboard.press('j');
    await expect(page.locator('[role="dialog"][aria-label="Field Notes"]')).toBeVisible();

    await page.keyboard.press('Escape');
    await expect(page.locator('[role="dialog"][aria-label="Field Notes"]')).not.toBeVisible();
  });

  test('discovered vs locked entries with count', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await page.waitForTimeout(500);

    await page.evaluate(() => {
      localStorage.setItem('rpc_discovered_synergies', JSON.stringify(['stillblade_hollow_backstep']));
      localStorage.setItem('rpc_revealed_synergies', JSON.stringify(['stillblade_hollow_backstep']));
    });
    await page.reload();
    await page.waitForTimeout(500);

    await injectGameState(page, {
      type: 'state',
      mode: 'Menu',
      player: { x: 0, y: 0, facing: 'North' },
      tiles: [],
      explored: [],
      hasDungeon: false,
      party: [],
    });

    await page.waitForSelector('.field-notes-toggle');

    await page.locator('.field-notes-toggle').click();

    await expect(page.locator('.field-notes-count')).toHaveText('1/4 discovered');
    await expect(page.locator('.field-note-entry .field-note-names', { hasText: 'backstep + cheap_shot' })).toBeVisible();
    await expect(page.locator('.field-note-entry .field-note-names', { hasText: '??? + ???' })).toHaveCount(3);
  });

  test('Replay button opens modal', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await page.waitForTimeout(500);

    await page.evaluate(() => {
      localStorage.setItem('rpc_discovered_synergies', JSON.stringify(['stillblade_hollow_backstep']));
      localStorage.setItem('rpc_revealed_synergies', JSON.stringify(['stillblade_hollow_backstep']));
    });
    await page.reload();
    await page.waitForTimeout(500);

    await injectGameState(page, {
      type: 'state',
      mode: 'Menu',
      player: { x: 0, y: 0, facing: 'North' },
      tiles: [],
      explored: [],
      hasDungeon: false,
      party: [],
    });

    await page.waitForSelector('.field-notes-toggle');

    await page.locator('.field-notes-toggle').click();
    await page.locator('.replay-btn').first().click();

    await expect(page.locator('.replay-modal-overlay')).toBeVisible();
    await expect(page.locator('.replay-anim')).toBeVisible();

    await page.locator('.replay-close-btn').click();
    await expect(page.locator('.replay-modal-overlay')).not.toBeVisible();
  });
});
