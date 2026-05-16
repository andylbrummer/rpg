import { test, expect } from './fixtures';
import { sendWsAction } from './helpers';

test.describe('Town: server-authoritative state', () => {
  test('initial state includes tavern roster with 6 recruits', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await sendWsAction(page, serverUrl, { type: 'reset_game' });
    await page.waitForTimeout(500);
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

  test('missions, faction contacts, and vendor sections render without errors', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await page.waitForSelector('.town-menu', { timeout: 10000 });

    await sendWsAction(page, serverUrl, { type: 'reset_game' });
    await page.waitForTimeout(500);

    const missionsSection = page.locator('.town-services h2:has-text("Missions") + .service-list');
    const factionSection = page.locator('.town-services h2:has-text("Faction Contacts") + .service-list');
    const vendorSection = page.locator('.town-services h2:has-text("Vendor") + .service-list');

    await expect(missionsSection.locator('.service-item')).toHaveCount(20);
    await expect(factionSection.locator('.contact-card')).toHaveCount(5);
    await expect(vendorSection.locator('.empty-state')).toBeVisible();
  });

  test('websocket state message includes town object', async ({ page, serverUrl }) => {
    await sendWsAction(page, serverUrl, { type: 'reset_game' });
    await page.waitForTimeout(500);

    const captured = await page.evaluate((url) => {
      return new Promise<any>((resolve) => {
        const ws = new WebSocket(`ws://${new URL(url).host}/`);
        let clientSeq = 1;
        ws.onmessage = (e) => {
          const envelope = JSON.parse(e.data);
          if (envelope.type === 'hello') {
            ws.send(JSON.stringify({ v: 2, type: 'ready', seq: clientSeq++, payload: {} }));
          } else if (envelope.type === 'state') {
            ws.close();
            resolve(envelope.payload);
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
    expect(captured.town.availableMissions.length).toBe(20);
    expect(Array.isArray(captured.town.factionContacts)).toBe(true);
    expect(captured.town.factionContacts.length).toBe(5);
    expect(Array.isArray(captured.town.vendorStock)).toBe(true);
  });
});
