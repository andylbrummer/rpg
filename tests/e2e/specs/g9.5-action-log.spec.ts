import { test, expect } from './fixtures';
import { sendWsAction } from './helpers';

test.describe('G9.5 Action Log Categories', () => {
  test('completing bureau side mission emits mission_completed + rep_changed + vendor_unlocked', async ({ page, serverUrl, request }) => {
    await page.goto(`${serverUrl}/app`);
    await sendWsAction(page, serverUrl, { type: 'reset_game' });
    await page.waitForTimeout(500);

    await sendWsAction(page, serverUrl, { type: 'set_reputation', targetId: 'bureau', value: 23 });
    await page.waitForTimeout(300);

    await sendWsAction(page, serverUrl, { type: 'mission_accept', targetId: 'mission-bureau-1' });
    await page.waitForTimeout(300);

    await sendWsAction(page, serverUrl, { type: 'complete_mission', targetId: 'mission-bureau-1' });
    await page.waitForTimeout(300);

    const res = await request.get(`${serverUrl}/api/action-log`);
    expect(res.ok()).toBeTruthy();
    const log = await res.json();

    const missionCompleted = log.events.find((e: any) => e.type === 'mission_completed');
    expect(missionCompleted).toBeTruthy();
    expect(missionCompleted.category).toBe('faction');
    expect(missionCompleted.payload.factionId).toBe('bureau');

    const repChanged = log.events.filter((e: any) => e.type === 'rep_changed');
    expect(repChanged.length).toBeGreaterThanOrEqual(2);
    expect(repChanged.some((e: any) => e.payload.factionId === 'bureau')).toBe(true);
    expect(repChanged.some((e: any) => e.payload.factionId === 'convocation')).toBe(true);

    const vendorUnlocked = log.events.find((e: any) => e.type === 'vendor_unlocked');
    expect(vendorUnlocked).toBeTruthy();
    expect(vendorUnlocked.payload.factionId).toBe('bureau');
    expect(vendorUnlocked.payload.threshold).toBe('25');
  });

  test('travel emits travel_started, travel_encounter_resolved, and town_reached', async ({ page, serverUrl, request }) => {
    await page.goto(`${serverUrl}/app`);
    await sendWsAction(page, serverUrl, { type: 'reset_game' });
    await page.waitForTimeout(500);

    // Travel to broken_engine
    await sendWsAction(page, serverUrl, { type: 'travel', targetId: 'broken_engine' });
    await page.waitForTimeout(600);

    const res1 = await request.get(`${serverUrl}/api/action-log`);
    expect(res1.ok()).toBeTruthy();
    const log1 = await res1.json();

    const travelStarted = log1.events.find((e: any) => e.type === 'travel_started');
    expect(travelStarted).toBeTruthy();
    expect(travelStarted.category).toBe('overworld');
    expect(travelStarted.payload.from).toBe('the_reach');
    expect(travelStarted.payload.to).toBe('broken_engine');

    // Resolve any non-combat travel encounters
    let encounterCount = 0;
    for (let i = 0; i < 5; i++) {
      const encounterVisible = await page.locator('.travel-encounter-overlay').isVisible().catch(() => false);
      if (encounterVisible) {
        encounterCount++;
        await page.locator('.travel-action-btn').first().click();
        await page.waitForTimeout(500);
      } else {
        break;
      }
    }

    // Travel back to the_reach
    await sendWsAction(page, serverUrl, { type: 'travel', targetId: 'the_reach' });
    await page.waitForTimeout(600);

    // Resolve any non-combat travel encounters
    for (let i = 0; i < 5; i++) {
      const encounterVisible = await page.locator('.travel-encounter-overlay').isVisible().catch(() => false);
      if (encounterVisible) {
        encounterCount++;
        await page.locator('.travel-action-btn').first().click();
        await page.waitForTimeout(500);
      } else {
        break;
      }
    }

    const res2 = await request.get(`${serverUrl}/api/action-log`);
    expect(res2.ok()).toBeTruthy();
    const log2 = await res2.json();

    const encounterResolved = log2.events.filter((e: any) => e.type === 'travel_encounter_resolved');
    expect(encounterResolved.length).toBeGreaterThanOrEqual(encounterCount);

    const townReached = log2.events.find((e: any) => e.type === 'town_reached');
    expect(townReached).toBeTruthy();
    expect(townReached.category).toBe('overworld');
    expect(townReached.payload.townId).toBe('the_reach');
  });
});
