import { test, expect } from './fixtures';
import { sendWsAction } from './helpers';

test.describe('Faction contacts in town', () => {
  test('contact at 0 rep shows greeting + dismissive line only', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await page.waitForSelector('.town-menu', { timeout: 10000 });

    await sendWsAction(page, serverUrl, { type: 'reset_game' });
    await page.waitForTimeout(500);

    const contactSection = page.locator('.town-services h2:has-text("Faction Contacts") + .service-list');
    const contacts = contactSection.locator('.contact-card');
    await expect(contacts).toHaveCount(5);

    const bureauContact = contacts.filter({ hasText: 'Agent Voss' });
    await expect(bureauContact.locator('.dialogue-line.greeting')).toBeVisible();
    await expect(bureauContact.locator('.dialogue-line.dismissive')).toBeVisible();
    await expect(bureauContact.locator('.dialogue-line.rumor')).toHaveCount(0);
    await expect(bureauContact.locator('.mission-offer')).toHaveCount(0);
  });

  test('contact at 10 rep shows greeting + rumor', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await page.waitForSelector('.town-menu', { timeout: 10000 });

    await sendWsAction(page, serverUrl, { type: 'reset_game' });
    await page.waitForTimeout(500);
    await sendWsAction(page, serverUrl, { type: 'set_reputation', targetId: 'bureau', value: 10 });
    await page.waitForTimeout(500);

    const contactSection = page.locator('.town-services h2:has-text("Faction Contacts") + .service-list');
    const bureauContact = contactSection.locator('.contact-card').filter({ hasText: 'Agent Voss' });

    await expect(bureauContact.locator('.dialogue-line.greeting')).toBeVisible();
    await expect(bureauContact.locator('.dialogue-line.rumor')).toBeVisible();
    await expect(bureauContact.locator('.mission-offer')).toHaveCount(0);
  });

  test('contact at 30 rep shows greeting, 2 mission offers, and rumor', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await page.waitForSelector('.town-menu', { timeout: 10000 });

    await sendWsAction(page, serverUrl, { type: 'reset_game' });
    await page.waitForTimeout(500);
    await sendWsAction(page, serverUrl, { type: 'set_reputation', targetId: 'bureau', value: 30 });
    await page.waitForTimeout(500);

    const contactSection = page.locator('.town-services h2:has-text("Faction Contacts") + .service-list');
    const bureauContact = contactSection.locator('.contact-card').filter({ hasText: 'Agent Voss' });

    await expect(bureauContact.locator('.dialogue-line.greeting')).toBeVisible();
    await expect(bureauContact.locator('.dialogue-line.rumor')).toBeVisible();
    await expect(bureauContact.locator('.mission-offer')).toHaveCount(4);
  });

  test('accepting mission updates quest log', async ({ page, serverUrl }) => {
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

    const questSection = page.locator('.town-services h2:has-text("Quest Log") + .service-list');
    const quests = questSection.locator('.service-item');
    await expect(quests).toHaveCount(1);
  });

  test('completing mission applies rep delta', async ({ page, serverUrl }) => {
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

    const questSection = page.locator('.town-services h2:has-text("Quest Log") + .service-list');
    const questStatus = questSection.locator('.quest-status').first();
    await expect(questStatus).toHaveText('completed');

    const bureauContact2 = contactSection.locator('.contact-card').filter({ hasText: 'Agent Voss' });
    const repValue = bureauContact2.locator('.rep-value').first();
    await expect(repValue).toHaveText('35');
  });

  test('both Bureau and Convocation contacts render with correct faction badge', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await page.waitForSelector('.town-menu', { timeout: 10000 });

    const contactSection = page.locator('.town-services h2:has-text("Faction Contacts") + .service-list');
    const contacts = contactSection.locator('.contact-card');
    await expect(contacts).toHaveCount(5);

    const bureauContact = contacts.filter({ hasText: 'Agent Voss' });
    await expect(bureauContact.locator('.contact-faction')).toHaveText('bureau');

    const convocationContact = contacts.filter({ hasText: 'Seer Maren' });
    await expect(convocationContact.locator('.contact-faction')).toHaveText('convocation');

    const stillnessContact = contacts.filter({ hasText: 'Null-Vector Silas' });
    await expect(stillnessContact.locator('.contact-faction')).toHaveText('stillness');

    const inkbloodContact = contacts.filter({ hasText: 'Scribe-Mother Yrsa' });
    await expect(inkbloodContact.locator('.contact-faction')).toHaveText('inkblood');

    const cartographyContact = contacts.filter({ hasText: 'Wayfinder Kael' });
    await expect(cartographyContact.locator('.contact-faction')).toHaveText('cartography');
  });
});
