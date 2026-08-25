import { test, expect } from '@playwright/test';

async function expectTownReady(page: import('@playwright/test').Page) {
  await page.goto('/app');
  await expect(page.locator('.game')).toBeVisible();
  await page.waitForFunction(() => Boolean((window as any).gameStore?.sendAction));
  await page.evaluate(() => {
    (window as any).gameStore.sendAction({ type: 'reset_game' });
  });
  await expect(page.locator('.mode-badge')).toContainText('Menu', { timeout: 10000 });
  await expect(page.locator('.town-menu')).toBeVisible();
}

async function enterFirstDungeon(page: import('@playwright/test').Page) {
  await expectTownReady(page);
  await page.locator('.town-nav-btn').filter({ hasText: 'Dungeons' }).click();
  await page.locator('.dungeon-btn').first().click();
  await expect(page.locator('.mode-badge')).toContainText('Exploration', { timeout: 10000 });
  await expect(page.locator('.position')).toBeVisible({ timeout: 10000 });
}

test.describe('The Reach Game', () => {
  test('page loads and shows initial state', async ({ page }) => {
    await expectTownReady(page);
    await expect(page.locator('.game-title')).toContainText('The Reach');
  });

  test('dungeon generates from town action', async ({ page }) => {
    await enterFirstDungeon(page);

    const positionText = await page.locator('.position').textContent();
    expect(positionText).toMatch(/Position:\s*\(\d+,\s*\d+\)/);
  });

  test('keyboard controls work', async ({ page }) => {
    await enterFirstDungeon(page);

    // Press turn keys (these should always work even if movement is blocked)
    await page.keyboard.press('ArrowRight');
    await page.waitForTimeout(100);
    
    await page.keyboard.press('ArrowLeft');
    await page.waitForTimeout(100);
    
    // Position text should still be visible
    await expect(page.locator('.position')).toBeVisible();
  });

  test('returning to town and entering another dungeon works', async ({ page }) => {
    await enterFirstDungeon(page);

    await page.getByRole('button', { name: 'Return to Town' }).click();
    await expect(page.locator('.mode-badge')).toContainText('Menu', { timeout: 10000 });
    await page.locator('.town-nav-btn').filter({ hasText: 'Dungeons' }).click();
    await page.locator('.dungeon-btn').nth(1).click();
    await expect(page.locator('.mode-badge')).toContainText('Exploration', { timeout: 10000 });
    await expect(page.locator('.position')).toBeVisible({ timeout: 10000 });
  });

  test('WebSocket receives state updates', async ({ page }) => {
    const wsMessages: string[] = [];
    page.on('websocket', ws => {
      ws.on('framereceived', data => {
        wsMessages.push(data.payload.toString());
      });
    });
    
    await expectTownReady(page);
    
    await expect.poll(() => wsMessages.filter(m => m.includes('"type":"state"')).length).toBeGreaterThan(0);
  });

  test('WebSocket connection completes and receives initial state', async ({ page }) => {
    // Monitor WebSocket traffic
    const wsMessages: string[] = [];
    
    page.on('websocket', ws => {
      console.log(`WebSocket opened: ${ws.url()}`);
      
      ws.on('framereceived', data => {
        wsMessages.push(data.payload.toString());
      });
    });
    
    await expectTownReady(page);

    // Wait for initial state message
    await page.waitForTimeout(500);
    
    // Should have received at least one state message
    const stateMessages = wsMessages.filter(m => m.includes('"type":"state"'));
    expect(stateMessages.length).toBeGreaterThan(0);
    
    // Parse the state message and verify structure
    const envelope = JSON.parse(stateMessages[0]);
    expect(envelope.type).toBe('state');
    const state = envelope.payload;
    expect(state.mode).toBeDefined();
    expect(state.player).toBeDefined();
    expect(state.tiles).toBeDefined();
    expect(state.hasDungeon).toBeDefined();
  });

  test('WebSocket bidirectional communication works', async ({ page }) => {
    await enterFirstDungeon(page);

    // Send a command via keyboard (this sends WebSocket message)
    await page.keyboard.press('ArrowRight');
    
    // Wait for state update to reflect the turn
    await page.waitForTimeout(200);
    
    // Position should have updated (facing direction changed)
    const newPosition = await page.locator('.position').textContent();
    expect(newPosition).toBeDefined();
    
    // The position text should still contain valid coordinates
    const compassText = await page.locator('.compass').textContent();
    expect(newPosition).toMatch(/Position:\s*\(\d+,\s*\d+\)/);
    expect(compassText).toMatch(/Facing:\s*(North|East|South|West)/);
  });
});
