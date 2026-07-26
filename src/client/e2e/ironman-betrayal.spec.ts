import { test, expect, type Page } from '@playwright/test';
import { loadTown, MENU_STATE } from './townHarness';

/**
 * Both of these were engine features with no way in: the commands existed, were handled and were
 * tested, but no action string built them and no control emitted one. These tests assert the thing
 * that was actually missing — that a player can reach them — so the gap cannot reopen quietly.
 */

async function installActionCapture(page: Page) {
  await page.evaluate(() => {
    const client = (window as any).gameClient;
    const captured: unknown[] = [];
    (window as any).__capturedActions = captured;
    client.sendAction = (action: unknown) => {
      captured.push(action);
    };
  });
}

async function getCaptured(page: Page): Promise<any[]> {
  return page.evaluate(() => (window as any).__capturedActions ?? []);
}

async function setState(page: Page, state: Record<string, unknown>) {
  await page.evaluate((s) => {
    (window as any).__rpc_enableTestHooks();
    (window as any).gameStore.__testSetState(s);
  }, state);
}

test.describe('Ironman mode', () => {
  test('the settings toggle reflects the run and sends set_ironman', async ({ page }) => {
    await loadTown(page);
    await installActionCapture(page);
    await setState(page, { ...MENU_STATE, isIronman: false });

    await page.getByRole('button', { name: 'Settings' }).first().click();
    await expect(page.locator('.settings-panel')).toBeVisible();

    const toggle = page.locator('.settings-section', { hasText: 'Gameplay' }).locator('input[type="checkbox"]');
    await expect(toggle).not.toBeChecked();

    await toggle.check();

    expect(await getCaptured(page)).toContainEqual({ type: 'set_ironman', enabled: true });
  });

  test('a run already in ironman shows the toggle on', async ({ page }) => {
    await loadTown(page);
    await setState(page, { ...MENU_STATE, isIronman: true });

    await page.getByRole('button', { name: 'Settings' }).first().click();
    const toggle = page.locator('.settings-section', { hasText: 'Gameplay' }).locator('input[type="checkbox"]');

    await expect(toggle).toBeChecked();
  });
});

test.describe('Betrayal', () => {
  async function openMissions(page: Page) {
    await page.locator('.town-nav-btn').filter({ hasText: 'Missions' }).click();
  }

  test('the offer is hidden until the party has evidence against the mastermind', async ({ page }) => {
    await loadTown(page);
    await setState(page, {
      ...MENU_STATE,
      evidence: { canConfront: false, canAccuse: false, hasIrrefutableProof: false, canBetray: false, onBetrayalPath: false },
    });
    await openMissions(page);

    await expect(page.locator('.betrayal-offer')).toHaveCount(0);
  });

  test('the offer appears once it is available and sends choose_betrayal', async ({ page }) => {
    await loadTown(page);
    await installActionCapture(page);
    await setState(page, {
      ...MENU_STATE,
      evidence: { canConfront: false, canAccuse: false, hasIrrefutableProof: false, canBetray: true, onBetrayalPath: false },
    });
    await openMissions(page);

    await expect(page.locator('.betrayal-offer')).toBeVisible();
    await page.getByRole('button', { name: 'Throw in with them' }).click();

    expect(await getCaptured(page)).toContainEqual({ type: 'choose_betrayal' });
  });

  test('a committed run shows the path instead of the offer', async ({ page }) => {
    await loadTown(page);
    await setState(page, {
      ...MENU_STATE,
      evidence: { canConfront: false, canAccuse: false, hasIrrefutableProof: false, canBetray: false, onBetrayalPath: true },
    });
    await openMissions(page);

    await expect(page.locator('.betrayal-active')).toBeVisible();
    await expect(page.locator('.betrayal-offer')).toHaveCount(0);
  });
});
