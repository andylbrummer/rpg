import { test, expect, type Page } from '@playwright/test';

async function openSettings(page: Page) {
  await page.goto('/app');
  await page.waitForFunction(() => Boolean((window as any).gameStore?.__testSetState));
  // Start from a clean binding slate each run.
  await page.evaluate(() => localStorage.removeItem('rpc_keybindings'));
  await page.getByRole('button', { name: 'Settings' }).first().click();
  await expect(page.locator('.settings-panel')).toBeVisible();
}

test.describe('Keybinding rebind UI', () => {
  test('groups bindings by context', async ({ page }) => {
    await openSettings(page);
    const labels = page.locator('.binding-context-label');
    await expect(labels.filter({ hasText: 'Combat' })).toHaveCount(1);
    await expect(labels.filter({ hasText: 'Town' })).toHaveCount(1);
    await expect(labels.filter({ hasText: 'Exploration' })).toHaveCount(1);
  });

  test('captures a modifier chord', async ({ page }) => {
    await openSettings(page);

    // The Attack row lives in the Combat context (default "1").
    const attackRow = page.locator('.binding-row', { hasText: 'Attack' });
    await attackRow.locator('.binding-key').click();
    await expect(attackRow.locator('.binding-key')).toHaveText('Press a key…');

    await page.keyboard.press('Control+Shift+KeyK');
    await expect(attackRow.locator('.binding-key')).toContainText('Ctrl+Shift+K');
  });

  test('warns on a conflict within the same context', async ({ page }) => {
    await openSettings(page);

    const attack = page.locator('.binding-row', { hasText: 'Attack' }).locator('.binding-key');
    const defend = page.locator('.binding-row', { hasText: 'Defend' }).locator('.binding-key');

    await attack.click();
    await page.keyboard.press('KeyZ');
    await defend.click();
    await page.keyboard.press('KeyZ');

    await expect(page.locator('.conflict-banner')).toBeVisible();
  });

  test('reset to defaults restores the Attack binding', async ({ page }) => {
    await openSettings(page);

    const attack = page.locator('.binding-row', { hasText: 'Attack' }).locator('.binding-key');
    await attack.click();
    await page.keyboard.press('KeyZ');
    await expect(attack).toContainText('Z');

    await page.getByRole('button', { name: 'Reset to Defaults' }).click();
    await expect(attack).toContainText('1');
  });
});
