import { test, expect } from './fixtures';
import { sendWsAction } from './helpers';

test.describe('Reputation consequences', () => {
  test('completing side mission shows toast with faction, delta, and source', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await page.waitForSelector('.town-menu', { timeout: 10000 });

    await sendWsAction(page, serverUrl, { type: 'reset_game' });
    await page.waitForTimeout(500);
    await sendWsAction(page, serverUrl, { type: 'set_reputation', targetId: 'bureau', value: 30 });
    await page.waitForTimeout(500);

    const contactSection = page.locator('.town-services h2:has-text("Faction Contacts") + .service-list');
    const bureauContact = contactSection.locator('.contact-card').filter({ hasText: 'Agent Voss' });
    const acceptBtn = bureauContact.locator('.mission-offer .action-btn').first();
    await acceptBtn.click();
    await page.waitForTimeout(600);

    await sendWsAction(page, serverUrl, { type: 'complete_mission', targetId: 'mission-bureau-1' });
    await page.waitForTimeout(600);

    const toast = page.locator('.rep-toast').filter({ hasText: 'bureau' }).first();
    await expect(toast).toBeVisible();
    await expect(toast.locator('.rep-toast-delta')).toHaveText('+5');
    await expect(toast.locator('.rep-toast-source')).toContainText('mission_complete');
  });

  test('convocation vendor disappears at -25 rep', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await page.waitForSelector('.town-menu', { timeout: 10000 });

    await sendWsAction(page, serverUrl, { type: 'reset_game' });
    await page.waitForTimeout(500);
    await sendWsAction(page, serverUrl, { type: 'set_reputation', targetId: 'convocation', value: -25 });
    await page.waitForTimeout(500);

    const convocationHeading = page.locator('.town-services h2:has-text("Convocation Arcanist")');
    await expect(convocationHeading).toHaveCount(0);
  });

  test('convocation contact at -25 shows hostility only', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await page.waitForSelector('.town-menu', { timeout: 10000 });

    await sendWsAction(page, serverUrl, { type: 'reset_game' });
    await page.waitForTimeout(500);
    await sendWsAction(page, serverUrl, { type: 'set_reputation', targetId: 'convocation', value: -25 });
    await page.waitForTimeout(500);

    const contactSection = page.locator('.town-services h2:has-text("Faction Contacts") + .service-list');
    const convocationContact = contactSection.locator('.contact-card').filter({ hasText: 'Seer Maren' });

    await expect(convocationContact.locator('.dialogue-line.hostile')).toBeVisible();
    await expect(convocationContact.locator('.dialogue-line.greeting')).toHaveCount(0);
    await expect(convocationContact.locator('.dialogue-line.dismissive')).toHaveCount(0);
    await expect(convocationContact.locator('.mission-offer')).toHaveCount(0);
  });

  test('both faction rep bars shift visibly after mission completion', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await page.waitForSelector('.town-menu', { timeout: 10000 });

    await sendWsAction(page, serverUrl, { type: 'reset_game' });
    await page.waitForTimeout(500);
    await sendWsAction(page, serverUrl, { type: 'set_reputation', targetId: 'bureau', value: 30 });
    await page.waitForTimeout(500);

    const contactSection = page.locator('.town-services h2:has-text("Faction Contacts") + .service-list');
    const bureauContact = contactSection.locator('.contact-card').filter({ hasText: 'Agent Voss' });
    const acceptBtn = bureauContact.locator('.mission-offer .action-btn').first();
    await acceptBtn.click();
    await page.waitForTimeout(600);

    const bureauRepBefore = await bureauContact.locator('.rep-value').first().textContent();
    expect(bureauRepBefore).toBe('30');

    const convocationContact = contactSection.locator('.contact-card').filter({ hasText: 'Seer Maren' });
    const convocationRepBefore = await convocationContact.locator('.rep-value').first().textContent();
    expect(convocationRepBefore).toBe('0');

    await sendWsAction(page, serverUrl, { type: 'complete_mission', targetId: 'mission-bureau-1' });
    await page.waitForTimeout(600);

    const bureauContact2 = contactSection.locator('.contact-card').filter({ hasText: 'Agent Voss' });
    const bureauRepAfter = await bureauContact2.locator('.rep-value').first().textContent();
    expect(bureauRepAfter).toBe('35');

    const convocationContact2 = contactSection.locator('.contact-card').filter({ hasText: 'Seer Maren' });
    const convocationRepAfter = await convocationContact2.locator('.rep-value').first().textContent();
    expect(convocationRepAfter).toBe('-2');
  });
});
