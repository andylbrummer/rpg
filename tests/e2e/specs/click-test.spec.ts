import { test, expect } from './fixtures';

test('click dungeon without selecting character', async ({ page, serverUrl }) => {
  await page.goto(`${serverUrl}/app`);
  
  const messages: any[] = [];
  page.on('console', msg => messages.push({type: msg.type(), text: msg.text()}));
  page.on('pageerror', err => messages.push({type: 'pageerror', text: err.message}));
  
  // Click Broken Engine directly, no character selection
  await page.locator('.dungeon-btn').first().click();
  await page.waitForTimeout(1000);
  
  const mode = await page.locator('.mode-badge').textContent().catch(() => 'not found');
  console.log('Mode:', mode);
  console.log('Return to Town visible:', await page.locator('text=Return to Town').isVisible().catch(() => false));
  console.log('Console errors:', messages.filter(m => m.type === 'error' || m.type === 'pageerror'));
});
