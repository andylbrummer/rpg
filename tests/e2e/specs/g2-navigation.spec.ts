import { test, expect } from './fixtures';
import { sendWsAction, getPositionText } from './helpers';

test.describe('G2: Navigation', () => {
  test('generate dungeon switches to exploration mode', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await sendWsAction(page, serverUrl, { type: 'generate_dungeon' });
    await expect(page.locator('text=Return to Town')).toBeVisible();
  });

  test('movement updates player position', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await sendWsAction(page, serverUrl, { type: 'generate_dungeon' });
    const before = await getPositionText(page);
    // Try turning and moving to find an open tile
    await sendWsAction(page, serverUrl, { type: 'turn_right' });
    await sendWsAction(page, serverUrl, { type: 'move_forward' });
    const after = await getPositionText(page);
    expect(after).not.toBe(before);
    expect(after).toContain('Pos:');
  });

  test('automap receives tiles', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await sendWsAction(page, serverUrl, { type: 'generate_dungeon' });
    await expect(page.locator('.automap-container')).toBeVisible();
  });
});
