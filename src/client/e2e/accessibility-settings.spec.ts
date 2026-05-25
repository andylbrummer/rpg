import { test, expect, type Page } from '@playwright/test';

async function openSettings(page: Page) {
  await page.goto('/app');
  await page.waitForFunction(() => Boolean((window as any).gameStore?.__testSetState));
  await page.evaluate(() => localStorage.removeItem('rpc_accessibility_settings'));
  await page.getByRole('button', { name: 'Settings' }).first().click();
  await expect(page.locator('.settings-panel')).toBeVisible();
}

test.describe('Accessibility settings UI', () => {
  test('reduce motion toggles the document attribute', async ({ page }) => {
    await openSettings(page);
    await page.getByText('Reduce motion', { exact: false }).click();
    await expect(page.locator('html')).toHaveAttribute('data-reduce-motion', 'on');
  });

  test('high contrast toggles the document attribute', async ({ page }) => {
    await openSettings(page);
    await page.getByText('High contrast', { exact: false }).click();
    await expect(page.locator('html')).toHaveAttribute('data-high-contrast', 'on');
  });

  test('colorblind mode sets the document attribute', async ({ page }) => {
    await openSettings(page);
    await page.locator('#colorblind-select').selectOption('deuteranopia');
    await expect(page.locator('html')).toHaveAttribute('data-colorblind', 'deuteranopia');
  });

  test('text size slider scales text and persists', async ({ page }) => {
    await openSettings(page);
    await page.locator('#text-scale').fill('1.3');

    const scale = await page.evaluate(() =>
      getComputedStyle(document.documentElement).getPropertyValue('--text-scale').trim());
    expect(scale).toBe('1.3');

    const stored = await page.evaluate(() =>
      JSON.parse(localStorage.getItem('rpc_accessibility_settings') || '{}').textScale);
    expect(stored).toBe(1.3);
  });

  test('reset restores defaults', async ({ page }) => {
    await openSettings(page);
    await page.getByText('High contrast', { exact: false }).click();
    await expect(page.locator('html')).toHaveAttribute('data-high-contrast', 'on');

    await page.getByRole('button', { name: 'Reset Accessibility' }).click();
    await expect(page.locator('html')).toHaveAttribute('data-high-contrast', 'off');
  });
});
