import { test, expect } from './fixtures';
import { sendWsAction } from './helpers';

test.describe('Faction vendors in town', () => {
  test('bureau vendor hidden at -25 rep', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await page.waitForSelector('.town-menu', { timeout: 10000 });

    await sendWsAction(page, serverUrl, { type: 'reset_game' });
    await page.waitForTimeout(500);
    await sendWsAction(page, serverUrl, { type: 'set_reputation', targetId: 'bureau', value: -25 });
    await page.waitForTimeout(500);

    const bureauHeading = page.locator('.town-services h2:has-text("Bureau Quartermaster")');
    await expect(bureauHeading).toHaveCount(0);
  });

  test('bureau vendor visible but locked at 24 rep', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await page.waitForSelector('.town-menu', { timeout: 10000 });

    await sendWsAction(page, serverUrl, { type: 'reset_game' });
    await page.waitForTimeout(500);
    await sendWsAction(page, serverUrl, { type: 'set_reputation', targetId: 'bureau', value: 24 });
    await page.waitForTimeout(500);

    const bureauHeading = page.locator('.town-services h2:has-text("Bureau Quartermaster")');
    await expect(bureauHeading).toBeVisible();

    const lockText = page.locator('.town-services h2:has-text("Bureau Quartermaster") + .service-list .lock-text').first();
    await expect(lockText).toBeVisible();
    await expect(lockText).toHaveText('Requires 25 bureau reputation');

    const buyButtons = page.locator('.town-services h2:has-text("Bureau Quartermaster") + .service-list .action-btn');
    await expect(buyButtons).toHaveCount(0);
  });

  test('bureau vendor visible and unlocked at 25 rep', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await page.waitForSelector('.town-menu', { timeout: 10000 });

    await sendWsAction(page, serverUrl, { type: 'reset_game' });
    await page.waitForTimeout(500);
    await sendWsAction(page, serverUrl, { type: 'set_reputation', targetId: 'bureau', value: 25 });
    await page.waitForTimeout(500);

    const bureauHeading = page.locator('.town-services h2:has-text("Bureau Quartermaster")');
    await expect(bureauHeading).toBeVisible();
    await expect(bureauHeading).not.toHaveClass(/locked-heading/);

    const stockItems = page.locator('.town-services h2:has-text("Bureau Quartermaster") + .service-list .service-item');
    await expect(stockItems).toHaveCount(3);

    const buyButtons = page.locator('.town-services h2:has-text("Bureau Quartermaster") + .service-list .action-btn');
    await expect(buyButtons).toHaveCount(3);
  });

  test('purchasing faction item reduces gold and adds to inventory', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await page.waitForSelector('.town-menu', { timeout: 10000 });

    await sendWsAction(page, serverUrl, { type: 'reset_game' });
    await page.waitForTimeout(500);
    await sendWsAction(page, serverUrl, { type: 'set_reputation', targetId: 'bureau', value: 25 });
    await page.waitForTimeout(500);

    const goldBadge = page.locator('.gold-badge');
    const initialGold = await goldBadge.textContent();
    expect(initialGold).toBe('500g');

    const buyButton = page.locator('.town-services h2:has-text("Bureau Quartermaster") + .service-list .action-btn').first();
    await buyButton.click();
    await page.waitForTimeout(600);

    const newGold = await goldBadge.textContent();
    expect(newGold).toBe('465g');

    const inventoryHeading = page.locator('.town-services h2:has-text("Inventory")');
    await expect(inventoryHeading).toBeVisible();

    const inventoryItems = page.locator('.town-services h2:has-text("Inventory") + .service-list .service-item');
    await expect(inventoryItems).toHaveCount(1);
  });

  test('convocation vendor visible with correct stock at 25 rep', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await page.waitForSelector('.town-menu', { timeout: 10000 });

    await sendWsAction(page, serverUrl, { type: 'reset_game' });
    await page.waitForTimeout(500);
    await sendWsAction(page, serverUrl, { type: 'set_reputation', targetId: 'convocation', value: 25 });
    await page.waitForTimeout(500);

    const convocationHeading = page.locator('.town-services h2:has-text("Convocation Arcanist")');
    await expect(convocationHeading).toBeVisible();

    const stockItems = page.locator('.town-services h2:has-text("Convocation Arcanist") + .service-list .service-item');
    await expect(stockItems).toHaveCount(3);
  });
});
