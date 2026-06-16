import { test, expect, type Page } from '@playwright/test';

async function resetToTown(page: Page) {
  await page.goto('/app');
  await expect(page.locator('.game')).toBeVisible({ timeout: 20000 });
  await page.waitForFunction(() => Boolean((window as any).gameStore?.sendAction));
  await page.evaluate(() => {
    (window as any).gameStore.sendAction({ type: 'reset_game' });
  });
  await expect(page.locator('.town-menu')).toBeVisible({ timeout: 20000 });
}

/**
 * Injects an Exploration state shaped exactly as the ExplorationPresenter emits it: a discovered,
 * still-intact breakable wall in `breakableWalls`, and a separate detected-but-unrevealed secret in
 * `detectedSecrets` (the "?" search set). The HUD must render a Break control for the former only.
 */
async function injectExplorationState(page: Page) {
  await page.evaluate(() => {
    (window as any).gameStore.__testSetState({
      type: 'state',
      mode: 'Exploration',
      player: { x: 3, y: 2, facing: 'North' },
      tiles: [{ x: 3, y: 2, type: 'Floor', north: 'CrackedWall', south: 'None', east: 'None', west: 'None' }],
      explored: [],
      hasDungeon: true,
      dungeonType: 'test',
      detectedSecrets: [{ id: 'hidden', x: 5, y: 5, wall: 'East' }],
      breakableWalls: [{ id: 'crack', x: 3, y: 2, wall: 'North' }],
      party: [],
    });
  });
}

test.describe('Break-wall HUD wiring', () => {
  test('renders a Break control for a discovered breakable wall and emits break_wall on click', async ({ page }) => {
    const sentPayloads: any[] = [];
    page.on('websocket', (ws) => {
      ws.on('framesent', (frame) => {
        try {
          const env = JSON.parse(frame.payload as string);
          if (env?.type === 'action' && env?.payload) sentPayloads.push(env.payload);
        } catch {
          /* non-JSON frame */
        }
      });
    });

    await resetToTown(page);
    await injectExplorationState(page);

    const breakBtn = page.locator('[data-testid="break-wall-btn"][data-secret-id="crack"]');
    await expect(breakBtn).toBeVisible();
    await expect(breakBtn).toContainText('Break wall (North)');

    await breakBtn.click();

    await expect.poll(() => sentPayloads.some((a) => a.type === 'break_wall')).toBe(true);
    const breakWall = sentPayloads.find((a) => a.type === 'break_wall');
    expect(breakWall).toMatchObject({ type: 'break_wall', targetId: 'crack' });
  });

  test('does not render a Break control for a detected-but-undiscovered secret', async ({ page }) => {
    await resetToTown(page);
    await injectExplorationState(page);

    // The Search affordance is always present; the detected "?" secret gets no Break control.
    await expect(page.locator('.search-btn')).toBeVisible();
    await expect(page.locator('[data-testid="break-wall-btn"][data-secret-id="hidden"]')).toHaveCount(0);
    await expect(page.locator('[data-testid="break-wall-btn"]')).toHaveCount(1);
  });
});
