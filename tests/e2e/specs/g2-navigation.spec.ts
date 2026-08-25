import { test, expect } from './fixtures';
import { enterDungeon, getPositionText, resetGame, sendWsAction } from './helpers';

test.describe('G2: Navigation', () => {
  test('generate dungeon switches to exploration mode', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await resetGame(page, serverUrl);
    await enterDungeon(page, serverUrl, 'broken_engine');
    await expect(page.locator('text=Return to Town')).toBeVisible();
  });

  test('movement updates player position', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await resetGame(page, serverUrl);
    await enterDungeon(page, serverUrl, 'broken_engine');
    await page.waitForTimeout(1000);

    const getPlayerPos = async () => {
      return page.evaluate(() => {
        let s: any = null;
        const unsub = (window as any).gameStore?.subscribe((v: any) => { s = v; });
        unsub?.();
        return { x: s?.player?.x, y: s?.player?.y, mode: s?.mode };
      });
    };

    const before = await getPlayerPos();
    expect(before.mode).toBe('Exploration');

    // Try turning and moving; if combat triggers, flee and retry
    for (let attempt = 0; attempt < 8; attempt++) {
      await sendWsAction(page, serverUrl, { type: 'turn_right' });
      await page.waitForTimeout(300);
      await sendWsAction(page, serverUrl, { type: 'move_forward' });
      await page.waitForTimeout(400);

      const state = await getPlayerPos();
      if (state.mode === 'Combat') {
        await sendWsAction(page, serverUrl, { type: 'flee_combat' });
        await page.waitForTimeout(400);
        continue;
      }
      if (state.x !== before.x || state.y !== before.y) {
        return; // success
      }
    }
    throw new Error('Player position did not change after multiple movement attempts');
  });

  test('automap receives tiles', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await resetGame(page, serverUrl);
    await enterDungeon(page, serverUrl, 'broken_engine');
    await expect(page.locator('.automap-container')).toBeVisible();
  });
});
