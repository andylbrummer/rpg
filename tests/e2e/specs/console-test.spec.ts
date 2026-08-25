import { test, expect } from './fixtures';

test('console check', async ({ page, serverUrl }) => {
  page.on('console', msg => console.log('PAGE LOG:', msg.text()));
  page.on('pageerror', err => console.log('PAGE ERROR:', err.message));
  await page.goto(`${serverUrl}/app`);
  await page.waitForTimeout(1000);
  await page.evaluate(() => {
    (window as any).gameClient?.sendAction({ type: 'enter_dungeon', dungeonType: 'broken_engine' });
  });
  await page.waitForTimeout(600);
  await page.evaluate(() => {
    (window as any).gameClient?.sendAction({ type: 'enter_combat' });
  });
  await page.waitForTimeout(1000);
  const html = await page.content();
  console.log('HTML contains combat-overlay:', html.includes('combat-overlay'));
});
