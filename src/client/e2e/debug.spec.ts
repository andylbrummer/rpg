import { test, expect } from '@playwright/test';

test('debug page load', async ({ page }) => {
  // Capture console messages
  const consoleMessages: string[] = [];
  page.on('console', msg => {
    consoleMessages.push(`${msg.type()}: ${msg.text()}`);
  });
  
  // Capture page errors
  const pageErrors: string[] = [];
  page.on('pageerror', error => {
    pageErrors.push(error.message);
    console.log('PAGE ERROR:', error.message);
  });
  
  await page.goto('/app');
  
  // Wait a bit for any errors to occur
  await page.waitForTimeout(3000);
  
  // Take a screenshot
  await page.screenshot({ path: 'test-results/debug-screenshot.png', fullPage: true });
  
  // Log the page HTML
  const html = await page.content();
  console.log('PAGE HTML:', html.substring(0, 2000));
  
  // Log console messages
  console.log('CONSOLE MESSAGES:', consoleMessages);
  
  // Log page errors
  console.log('PAGE ERRORS:', pageErrors);
  
  // Basic check that page loaded something
  expect(html).toContain('<html');
});
