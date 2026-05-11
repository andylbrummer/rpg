import { test, expect } from './fixtures';
import { sendWsAction } from './helpers';

test.describe('G4: Combat', () => {
  test('combat state has combatants after trigger', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await sendWsAction(page, serverUrl, { type: 'enter_dungeon', dungeonType: 'broken_engine' });
    await sendWsAction(page, serverUrl, { type: 'enter_combat' });
    await expect(page.locator('.combat-overlay')).toBeVisible();
  });

  test('flee combat returns to exploration', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await sendWsAction(page, serverUrl, { type: 'enter_dungeon', dungeonType: 'broken_engine' });
    await sendWsAction(page, serverUrl, { type: 'enter_combat' });
    await expect(page.locator('.combat-overlay')).toBeVisible();
    await sendWsAction(page, serverUrl, { type: 'flee_combat' });
    await expect(page.locator('text=Return to Town')).toBeVisible();
  });
});
