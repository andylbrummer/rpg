import { test, expect } from './fixtures';
import { sendWsAction } from './helpers';

test.describe('Setpiece Encounter', () => {
  test('walking into tagged boss tile triggers expected enemies', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await sendWsAction(page, serverUrl, { type: 'enter_dungeon', dungeonType: 'broken_engine' });

    // Face south and step onto the guaranteed boss tile
    await sendWsAction(page, serverUrl, { type: 'turn_left' });
    await sendWsAction(page, serverUrl, { type: 'turn_left' });
    await sendWsAction(page, serverUrl, { type: 'move_forward' });

    await expect(page.locator('.combat-overlay')).toBeVisible({ timeout: 5000 });
    await expect(page.locator('.enemy-side')).toContainText('bone_archer_1', { timeout: 5000 });
  });
});
