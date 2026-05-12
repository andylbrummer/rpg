import { test, expect } from './fixtures';
import { sendWsAction } from './helpers';

test.describe('G9: Overworld Map UI', () => {
  test('renders map with distinct node icons', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await sendWsAction(page, serverUrl, { type: 'reset_game' });
    await page.getByRole('button', { name: 'Overworld Map' }).click();
    const mapPanel = page.getByRole('dialog', { name: 'Overworld map' });
    await expect(mapPanel).toBeVisible();

    await expect(mapPanel.getByRole('button', { name: 'The Reach' })).toBeVisible();
    await expect(mapPanel.getByRole('button', { name: 'Broken Engine' })).toBeVisible();

    const reachGroup = mapPanel.getByRole('button', { name: 'The Reach' });
    const engineGroup = mapPanel.getByRole('button', { name: 'Broken Engine' });
    await expect(reachGroup.locator('path[d="M12 3L2 12h3v8h6v-6h2v6h6v-8h3L12 3z"]')).toBeVisible();
    await expect(engineGroup.locator('path[d="M2 22l2-6h3l1.5-4h5L15 16h3l2 6H2zM12 2C8 2 5 5 5 9c0 2 1 4 2.5 5h9C18 13 19 11 19 9c0-4-3-7-7-7z"]')).toBeVisible();
  });

  test('hover route shows tooltip with distance and danger', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await sendWsAction(page, serverUrl, { type: 'reset_game' });
    await page.getByRole('button', { name: 'Overworld Map' }).click();
    const mapPanel = page.getByRole('dialog', { name: 'Overworld map' });
    await expect(mapPanel).toBeVisible();
    const route = mapPanel.locator('[aria-label="Route danger 3"]');
    await route.hover({ force: true });
    const tooltip = page.locator('.tooltip');
    await expect(tooltip).toBeVisible();
    await expect(tooltip).toContainText('Distance: 2 turns');
    await expect(tooltip).toContainText('Danger: 3');
    await expect(tooltip).toContainText('Terrain: caves');
  });

  test('travel confirm flow updates current node', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await sendWsAction(page, serverUrl, { type: 'reset_game' });
    await page.getByRole('button', { name: 'Overworld Map' }).click();
    const mapPanel = page.getByRole('dialog', { name: 'Overworld map' });
    await expect(mapPanel).toBeVisible();

    await mapPanel.getByRole('button', { name: 'Broken Engine' }).click();
    const dialog = page.getByRole('alertdialog', { name: 'Confirm travel' });
    await expect(dialog).toBeVisible();
    await expect(dialog).toContainText('Cost: 2 turns');

    await dialog.getByRole('button', { name: 'Travel' }).click();
    await expect(dialog).not.toBeVisible();

    await expect(page.locator('.node-group.current').getByText('Broken Engine')).toBeVisible();
  });

  test('clicking current node does nothing', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await sendWsAction(page, serverUrl, { type: 'reset_game' });
    await page.getByRole('button', { name: 'Overworld Map' }).click();
    const mapPanel = page.getByRole('dialog', { name: 'Overworld map' });
    await expect(mapPanel).toBeVisible();

    await mapPanel.getByRole('button', { name: 'The Reach' }).click();
    await expect(page.getByRole('alertdialog', { name: 'Confirm travel' })).not.toBeVisible();
  });
});
