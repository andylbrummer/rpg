import { test, expect } from './fixtures';

test.describe('Town: server-authoritative state', () => {
  test('initial state includes tavern roster with 6 recruits', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await page.waitForSelector('.town-menu', { timeout: 10000 });

    const recruitCards = page.locator('.town-services .service-item');
    // We have 3 sections (tavern, missions, vendor); tavern should have 6 recruits
    const tavernSection = page.locator('.town-services h2:has-text("Tavern") + .service-list');
    const recruits = tavernSection.locator('.service-item');
    await expect(recruits).toHaveCount(6);
  });

  test('refresh keeps same tavern roster', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await page.waitForSelector('.town-menu', { timeout: 10000 });

    const tavernSection = page.locator('.town-services h2:has-text("Tavern") + .service-list');
    const recruits = tavernSection.locator('.service-item');
    await expect(recruits).toHaveCount(6);

    const beforeNames = await recruits.locator('.recruit-name').allTextContents();
    expect(beforeNames.length).toBe(6);

    await page.reload();
    await page.waitForSelector('.town-menu', { timeout: 10000 });

    const afterNames = await recruits.locator('.recruit-name').allTextContents();
    expect(afterNames).toEqual(beforeNames);
  });

  test('missions and vendor sections render empty without errors', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await page.waitForSelector('.town-menu', { timeout: 10000 });

    const missionsSection = page.locator('.town-services h2:has-text("Missions") + .service-list');
    const vendorSection = page.locator('.town-services h2:has-text("Vendor") + .service-list');

    await expect(missionsSection.locator('.empty-state')).toBeVisible();
    await expect(vendorSection.locator('.empty-state')).toBeVisible();
  });

  test('websocket state message includes town object', async ({ page, serverUrl }) => {
    const captured = await page.evaluate((url) => {
      return new Promise<any>((resolve) => {
        const ws = new WebSocket(`ws://${new URL(url).host}/`);
        ws.onmessage = (e) => {
          const data = JSON.parse(e.data);
          if (data.type === 'state') {
            ws.close();
            resolve(data);
          }
        };
        ws.onerror = () => { ws.close(); resolve(null); };
        setTimeout(() => { ws.close(); resolve(null); }, 3000);
      });
    }, serverUrl);

    expect(captured).not.toBeNull();
    expect(captured.town).toBeDefined();
    expect(captured.town.currentTownId).toBe('the_reach');
    expect(Array.isArray(captured.town.tavernRoster)).toBe(true);
    expect(captured.town.tavernRoster.length).toBe(6);
    expect(Array.isArray(captured.town.availableMissions)).toBe(true);
    expect(Array.isArray(captured.town.vendorStock)).toBe(true);
  });
});
