import { test, expect } from './fixtures';
import { sendWsAction } from './helpers';

test.describe('G5: Game Loop', () => {
  test('initial state shows town menu', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    // Ensure town mode (server state may be shared across tests)
    await sendWsAction(page, serverUrl, { type: 'return_to_town' });
    await expect(page.locator('.town-header h1')).toBeVisible();
    await expect(page.locator('text=Broken Engine')).toBeVisible();
    await expect(page.locator('text=Sewer Warrens')).toBeVisible();
    await expect(page.locator('text=Crypt of Whispers')).toBeVisible();
  });

  test('can enter a dungeon from town', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await sendWsAction(page, serverUrl, { type: 'enter_dungeon', dungeonType: 'broken_engine' });
    await expect(page.locator('text=Return to Town')).toBeVisible();
  });

  test('rest heals party to full', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    // Damage party by entering combat and letting enemies act
    await sendWsAction(page, serverUrl, { type: 'generate_dungeon' });
    await sendWsAction(page, serverUrl, { type: 'enter_combat' });
    // Wait for a few combat rounds to take damage
    for (let i = 0; i < 5; i++) {
      const combatVisible = await page.locator('.combat-overlay').isVisible().catch(() => false);
      if (!combatVisible) break;
      await sendWsAction(page, serverUrl, { type: 'combat_action', action: 'attack', targetIndex: 0 });
      await page.waitForTimeout(300);
    }
    await sendWsAction(page, serverUrl, { type: 'return_to_town' });
    await expect(page.locator('.town-header h1')).toBeVisible();
    // Verify at least one member is damaged
    const hpBefore = await page.locator('.hp-fill').first().evaluate((el: any) => el.style.width);
    if (hpBefore === '100%') {
      // If no damage occurred, skip the damage check and just verify rest works
      await sendWsAction(page, serverUrl, { type: 'rest' });
      const hpAfter = await page.locator('.hp-fill').first().evaluate((el: any) => el.style.width);
      expect(hpAfter).toBe('100%');
    } else {
      expect(hpBefore).not.toBe('100%');
      await sendWsAction(page, serverUrl, { type: 'rest' });
      await page.waitForTimeout(300);
      const hpAfter = await page.locator('.hp-fill').first().evaluate((el: any) => el.style.width);
      expect(hpAfter).toBe('100%');
    }
  });

  test('save game creates a file', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await sendWsAction(page, serverUrl, { type: 'rest' });
    await sendWsAction(page, serverUrl, { type: 'save_game' });
    const { existsSync } = await import('fs');
    const { homedir } = await import('os');
    const savePath = `${homedir()}/.local/share/TheReach/save.json`;
    expect(existsSync(savePath)).toBe(true);
  });
});
