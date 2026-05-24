import { test, expect } from './fixtures';
import { getGameState, resolveTravelOutcomes, sendWsAction } from './helpers';

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

    await resolveTravelOutcomes(page, serverUrl);

    let log2: any = log1;
    for (let attempt = 0; attempt < 8; attempt++) {
      const state = await getGameState(page);
      const currentNodeId = state?.overworld?.currentNodeId;
      const targetId = currentNodeId === 'the_reach' ? 'broken_engine' : 'the_reach';

      await sendWsAction(page, serverUrl, { type: 'travel', targetId });
      await page.waitForTimeout(600);
      await resolveTravelOutcomes(page, serverUrl);

      const res2 = await request.get(`${serverUrl}/api/action-log`);
      expect(res2.ok()).toBeTruthy();
      log2 = await res2.json();

      const hasEncounterResolved = log2.events.some((e: any) => e.type === 'travel_encounter_resolved');
      const hasTownReached = log2.events.some((e: any) => e.type === 'town_reached');
      if (hasEncounterResolved && hasTownReached) {
        break;
      }
    }

    const encounterResolved = log2.events.filter((e: any) => e.type === 'travel_encounter_resolved');
    expect(encounterResolved.length).toBeGreaterThanOrEqual(1);

    const state = await getGameState(page);
    const townReached = log2.events.find((e: any) => e.type === 'town_reached');
    expect(townReached, JSON.stringify({ mode: state?.mode, overworld: state?.overworld, travelEncounter: state?.travelEncounter, events: log2.events }, null, 2)).toBeTruthy();
    expect(townReached).toBeTruthy();
    expect(townReached.category).toBe('overworld');
    expect(townReached.payload.townId).toBe('the_reach');
  });
});
