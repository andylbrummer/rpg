import { test, expect, type Page } from '@playwright/test';

async function openSettings(page: Page) {
  await page.goto('/app');
  await page.waitForFunction(() => Boolean((window as any).gameStore?.__testSetState));
  await page.evaluate(() => localStorage.removeItem('rpc_display_settings'));
  await page.getByRole('button', { name: 'Settings' }).first().click();
  await expect(page.locator('.settings-panel')).toBeVisible();
}

test.describe('Display settings UI', () => {
  test('shows FOV, resolution, V-Sync and fullscreen controls', async ({ page }) => {
    await openSettings(page);
    await expect(page.locator('.fov-slider')).toBeVisible();
    await expect(page.locator('.display-select')).toBeVisible();
    await expect(page.getByText('V-Sync', { exact: false })).toBeVisible();
    await expect(page.getByRole('button', { name: /Fullscreen/ })).toBeVisible();
  });

  test('FOV slider updates the readout and persists', async ({ page }) => {
    await openSettings(page);

    await page.locator('.fov-slider').fill('100');
    await expect(page.locator('.display-value')).toHaveText('100°');

    const stored = await page.evaluate(() =>
      JSON.parse(localStorage.getItem('rpc_display_settings') || '{}').fov);
    expect(stored).toBe(100);
  });

  test('reset display restores default FOV', async ({ page }) => {
    await openSettings(page);

    await page.locator('.fov-slider').fill('105');
    await expect(page.locator('.display-value')).toHaveText('105°');

    await page.getByRole('button', { name: 'Reset Display' }).click();
    await expect(page.locator('.display-value')).toHaveText('75°');
  });
});
