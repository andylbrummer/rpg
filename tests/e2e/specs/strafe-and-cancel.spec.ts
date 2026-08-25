import { test, expect } from './fixtures';
import { sendWsAction } from './helpers';

test.describe('Strafe and Cancel', () => {
  test('strafe actions accepted by server', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await sendWsAction(page, serverUrl, { type: 'enter_dungeon', dungeonType: 'broken_engine' });

    const errors: string[] = [];
    page.on('console', (msg) => {
      if (msg.type() === 'error') errors.push(msg.text());
    });

    await sendWsAction(page, serverUrl, { type: 'strafe_right' });
    await sendWsAction(page, serverUrl, { type: 'strafe_left' });
    await sendWsAction(page, serverUrl, { type: 'move_back' });

    const invalidErrors = errors.filter((e) => e.includes('invalid_action'));
    expect(invalidErrors).toHaveLength(0);
  });

  test('key rebind: A/D strafe, Q/E turn, no protocol errors', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await sendWsAction(page, serverUrl, { type: 'enter_dungeon', dungeonType: 'broken_engine' });

    const errors: string[] = [];
    page.on('console', (msg) => {
      if (msg.type() === 'error') errors.push(msg.text());
    });

    // A = strafe_left, D = strafe_right, Q = turn_left, E = turn_right
    await page.keyboard.press('a');
    await page.waitForTimeout(300);
    await page.keyboard.press('d');
    await page.waitForTimeout(300);
    await page.keyboard.press('q');
    await page.waitForTimeout(300);
    await page.keyboard.press('e');
    await page.waitForTimeout(300);

    const invalidErrors = errors.filter((e) => e.includes('invalid_action'));
    expect(invalidErrors).toHaveLength(0);
  });

  test('escape during combat targeting cancels targeting', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await sendWsAction(page, serverUrl, { type: 'enter_dungeon', dungeonType: 'broken_engine' });
    await sendWsAction(page, serverUrl, { type: 'enter_combat' });
    await expect(page.locator('.combat-overlay')).toBeVisible();

    // Wait for player turn and select an enemy target
    const enemyButton = page.locator('.enemy-side .combatant').first();
    await expect(enemyButton).toBeVisible();
    await enemyButton.click();

    // Verify enemy is selected
    await expect(enemyButton).toHaveClass(/selected/);

    // Press Escape to cancel targeting
    await page.keyboard.press('Escape');
    await page.waitForTimeout(300);

    // Verify enemy is no longer selected but combat is still active
    await expect(enemyButton).not.toHaveClass(/selected/);
    await expect(page.locator('.combat-overlay')).toBeVisible();
  });
});
