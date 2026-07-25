import { test, expect } from './fixtures';

async function injectGameState(page: any, state: any) {
  // Reset action-log turn tracker so injected low-turn entries are processed
  await page.evaluate(() => {
    const store = (window as any).gameStore;
    store.__testSetState({ type: 'state', actionLog: [] });
  });
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
      localStorage.setItem('rpc_discovered_synergies', JSON.stringify(['stillblade_hollow_smoke_silence']));
      localStorage.setItem('rpc_revealed_synergies', JSON.stringify(['stillblade_hollow_smoke_silence']));
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

    // The total is derived from synergy content, so it moves whenever content is added.
    // Assert the relationship the screen is actually responsible for — exactly one entry
    // discovered and every other one masked — instead of pinning today's content count.
    const countText = await page.locator('.field-notes-count').textContent();
    const match = countText?.match(/^(\d+)\/(\d+) discovered$/);
    expect(match, `unexpected count format: ${countText}`).not.toBeNull();

    const discovered = Number(match![1]);
    const total = Number(match![2]);
    expect(discovered).toBe(1);
    expect(total).toBeGreaterThan(1);

    await expect(page.locator('.field-note-entry .field-note-names', { hasText: 'silence_strike + smoke_bomb' })).toBeVisible();
    await expect(page.locator('.field-note-entry .field-note-names', { hasText: '??? + ???' })).toHaveCount(total - 1);
  });

  test('Replay button opens modal', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await page.waitForTimeout(500);

    await page.evaluate(() => {
      localStorage.setItem('rpc_discovered_synergies', JSON.stringify(['stillblade_hollow_smoke_silence']));
      localStorage.setItem('rpc_revealed_synergies', JSON.stringify(['stillblade_hollow_smoke_silence']));
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
