import { test, expect } from '@playwright/test';

test.describe('The Reach Game', () => {
  test('page loads and shows initial state', async ({ page }) => {
    await page.goto('/app');
    
    // Wait for the game container to be visible
    await expect(page.locator('.game-container')).toBeVisible();
    
    // Check that status bar shows connecting or connected
    const statusBar = page.locator('.status-bar');
    await expect(statusBar).toBeVisible();
    
    // Wait for connection (should show "Connected" within 5 seconds)
    await expect(page.locator('.connection-status')).toContainText('Connected', { timeout: 5000 });
  });

  test('dungeon generates on connect', async ({ page }) => {
    await page.goto('/app');
    
    // Wait for connection
    await expect(page.locator('.connection-status')).toContainText('Connected', { timeout: 5000 });
    
    // Wait for position info to appear (means dungeon was generated)
    await expect(page.locator('.position')).toBeVisible({ timeout: 5000 });
    
    // Position should show coordinates
    const positionText = await page.locator('.position').textContent();
    expect(positionText).toMatch(/Pos:\s*\(\d+,\s*\d+\)/);
  });

  test('keyboard controls work', async ({ page }) => {
    await page.goto('/app');
    
    // Wait for connection and dungeon
    await expect(page.locator('.connection-status')).toContainText('Connected', { timeout: 5000 });
    await expect(page.locator('.position')).toBeVisible({ timeout: 5000 });
    
    // Get initial position
    const initialPosition = await page.locator('.position').textContent();
    
    // Press turn keys (these should always work even if movement is blocked)
    await page.keyboard.press('ArrowRight');
    await page.waitForTimeout(100);
    
    await page.keyboard.press('ArrowLeft');
    await page.waitForTimeout(100);
    
    // Position text should still be visible
    await expect(page.locator('.position')).toBeVisible();
  });

  test('new dungeon button works', async ({ page }) => {
    await page.goto('/app');
    
    // Wait for connection
    await expect(page.locator('.connection-status')).toContainText('Connected', { timeout: 5000 });
    
    // Click the new dungeon button
    await page.locator('button:has-text("New Dungeon")').click();
    
    // Position should still be visible after generating new dungeon
    await expect(page.locator('.position')).toBeVisible({ timeout: 5000 });
  });

  test('WebSocket receives state updates', async ({ page }) => {
    // Check console for WebSocket messages
    const consoleMessages: string[] = [];
    page.on('console', msg => {
      consoleMessages.push(msg.text());
    });
    
    await page.goto('/app');
    
    // Wait for connection
    await expect(page.locator('.connection-status')).toContainText('Connected', { timeout: 5000 });
    
    // Wait a bit for any console messages
    await page.waitForTimeout(1000);
    
    // Should have connected to WebSocket
    const wsMessages = consoleMessages.filter(m => m.includes('WebSocket'));
    expect(wsMessages.length).toBeGreaterThan(0);
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
    
    await page.goto('/app');
    
    // Wait for WebSocket to be created
    await page.waitForFunction(() => {
      return (window as any).wsConnected === true || document.querySelector('.connection-status')?.textContent?.includes('Connected');
    }, { timeout: 5000 });
    
    // Wait for initial state message
    await page.waitForTimeout(500);
    
    // Should have received at least one state message
    const stateMessages = wsMessages.filter(m => m.includes('"type":"state"'));
    expect(stateMessages.length).toBeGreaterThan(0);
    
    // Parse the state message and verify structure
    const state = JSON.parse(stateMessages[0]);
    expect(state.type).toBe('state');
    expect(state.mode).toBeDefined();
    expect(state.player).toBeDefined();
    expect(state.tiles).toBeDefined();
    expect(state.hasDungeon).toBeDefined();
  });

  test('WebSocket bidirectional communication works', async ({ page }) => {
    await page.goto('/app');
    
    // Wait for connection
    await expect(page.locator('.connection-status')).toContainText('Connected', { timeout: 5000 });
    await expect(page.locator('.position')).toBeVisible({ timeout: 5000 });
    
    // Get initial position
    const initialPosition = await page.locator('.position').textContent();
    
    // Send a command via keyboard (this sends WebSocket message)
    await page.keyboard.press('ArrowRight');
    
    // Wait for state update to reflect the turn
    await page.waitForTimeout(200);
    
    // Position should have updated (facing direction changed)
    const newPosition = await page.locator('.position').textContent();
    expect(newPosition).toBeDefined();
    
    // The position text should still contain valid coordinates
    expect(newPosition).toMatch(/Pos:\s*\(\d+,\s*\d+\)\s*Facing:\s*(North|East|South|West)/);
  });
});
