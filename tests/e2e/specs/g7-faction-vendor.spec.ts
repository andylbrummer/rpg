import { test, expect } from './fixtures';
import { getGameState, resetGame, sendWsAction } from './helpers';

test.describe('Faction vendors in town', () => {
  test('bureau vendor hidden at -25 rep', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await page.waitForSelector('.town-menu', { timeout: 10000 });

    await resetGame(page, serverUrl);
    await sendWsAction(page, serverUrl, { type: 'set_reputation', targetId: 'bureau', value: -25 });
    await page.waitForTimeout(500);

    const bureauHeading = page.locator('.town-services h2:has-text("Bureau Quartermaster")');
    await expect(bureauHeading).toHaveCount(0);
  });

  test('bureau vendor visible but locked at 24 rep', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await page.waitForSelector('.town-menu', { timeout: 10000 });

    await resetGame(page, serverUrl);
    await sendWsAction(page, serverUrl, { type: 'set_reputation', targetId: 'bureau', value: 24 });
    await page.waitForTimeout(500);

    await page.getByRole('button', { name: 'Market', exact: true }).click();
    const bureauHeading = page.locator('.town-services h2:has-text("Bureau Quartermaster")');
    await expect(bureauHeading).toBeVisible();

    const lockText = page.locator('.town-services h2:has-text("Bureau Quartermaster") + .service-list .lock-text').first();
    await expect(lockText).toBeVisible();
    await expect(lockText).toHaveText('Requires 25 bureau reputation');

    await page.locator('.town-nav-btn').filter({ hasText: 'Market' }).click();
    const buyButtons = page.locator('.town-services h2:has-text("Bureau Quartermaster") + .service-list .action-btn');
    await expect(buyButtons).toHaveCount(0);
  });

  test('bureau vendor visible and unlocked at 25 rep', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await page.waitForSelector('.town-menu', { timeout: 10000 });

    await resetGame(page, serverUrl);
    await sendWsAction(page, serverUrl, { type: 'set_reputation', targetId: 'bureau', value: 25 });
    await page.waitForTimeout(500);

    // Stock size is content, not behaviour: take it from the state the server sent rather than
    // pinning a number that every catalogue change breaks. What this test owns is that at or
    // above the threshold the vendor is unlocked and every stocked item is offered for sale.
    const state = await getGameState(page);
    const stockCount = state.town?.factionVendors
      ?.find((v: any) => v.factionId === 'bureau')?.stock?.length ?? 0;
    expect(stockCount).toBeGreaterThan(0);

    await page.getByRole('button', { name: 'Market', exact: true }).click();
    const bureauHeading = page.locator('.town-services h2:has-text("Bureau Quartermaster")');
    await expect(bureauHeading).toBeVisible();
    await expect(bureauHeading).not.toHaveClass(/locked-heading/);

    const stockItems = page.locator('.town-services h2:has-text("Bureau Quartermaster") + .service-list .service-item');
    await expect(stockItems).toHaveCount(stockCount);

    const buyButtons = page.locator('.town-services h2:has-text("Bureau Quartermaster") + .service-list .action-btn');
    await expect(buyButtons).toHaveCount(stockCount);
  });

  test('purchasing faction item reduces gold and adds to inventory', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await page.waitForSelector('.town-menu', { timeout: 10000 });

    // resetGame waits for a state only a completed reset produces; a bare send plus a fixed
    // sleep let this read the gold the previous purchase test had already spent.
    await resetGame(page, serverUrl);
    await sendWsAction(page, serverUrl, { type: 'set_reputation', targetId: 'bureau', value: 25 });
    await page.waitForTimeout(500);

    // Gold is asserted from game state rather than from a badge in the town chrome. The
    // ".gold-badge" this used to read lived in PartyPanel, which the broadsheet rework
    // orphaned, so no screen has rendered it since.
    const before = await getGameState(page);
    expect(before.partyGold).toBe(500);

    const itemId = before.town?.factionVendors
      ?.find((v: any) => v.factionId === 'bureau')?.stock?.[0]?.itemId ?? null;
    expect(itemId).not.toBeNull();

    await sendWsAction(page, serverUrl, { type: 'vendor_purchase', targetId: itemId });
    await page.waitForTimeout(1200);

    const after = await getGameState(page);
    expect(after.partyGold).toBeLessThan(500);

    await page.getByRole('button', { name: 'Market', exact: true }).click();
    const inventoryHeading = page.locator('.town-services h2:has-text("Inventory")');
    await expect(inventoryHeading).toBeVisible();

    const inventoryItems = page.locator('.town-services h2:has-text("Inventory") + .service-list .service-item');
    await expect(inventoryItems).toHaveCount(1);
  });

  test('convocation vendor visible with correct stock at 25 rep', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await page.waitForSelector('.town-menu', { timeout: 10000 });

    await resetGame(page, serverUrl);
    await sendWsAction(page, serverUrl, { type: 'set_reputation', targetId: 'convocation', value: 25 });
    await page.waitForTimeout(500);

    await page.getByRole('button', { name: 'Market', exact: true }).click();
    const convocationHeading = page.locator('.town-services h2:has-text("Convocation Arcanist")');
    await expect(convocationHeading).toBeVisible();

    const stockItems = page.locator('.town-services h2:has-text("Convocation Arcanist") + .service-list .service-item');
    await expect(stockItems).toHaveCount(8);
  });
});
