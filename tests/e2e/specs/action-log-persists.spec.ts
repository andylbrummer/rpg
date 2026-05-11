import { test, expect } from './fixtures';
import { sendWsAction } from './helpers';

async function getGameState(page: any): Promise<any> {
  return page.evaluate(() => {
    return new Promise((resolve) => {
      const store = (window as any).gameStore;
      let resolved = false;
      const unsub = store.subscribe((s: any) => {
        if (s && !resolved) {
          resolved = true;
          resolve(s);
          unsub();
        }
      });
    });
  });
}

test.describe('Action Log Persists', () => {
  test('combat log events survive save and reload', async ({ page, serverUrl, request }) => {
    await page.goto(`${serverUrl}/app`);

    // Start fresh and wait for initial state
    await sendWsAction(page, serverUrl, { type: 'reset_game' });
    await page.waitForTimeout(500);

    // Enter dungeon and trigger combat
    await sendWsAction(page, serverUrl, { type: 'enter_dungeon', dungeonType: 'broken_engine' });
    await sendWsAction(page, serverUrl, { type: 'enter_combat' });
    await page.waitForTimeout(500);

    // Resolve combat by sending Attack actions via WebSocket
    let iterations = 0;
    while (iterations < 50) {
      const state = await getGameState(page);
      if (state.mode !== 'Combat') break;

      const combat = state.combat;
      if (!combat || combat.phase !== 'Turn') {
        await page.waitForTimeout(200);
        iterations++;
        continue;
      }

      const currentId = combat.initiativeOrder[combat.currentTurnIndex];
      const target = combat.combatants.find((c: any) => !c.isPlayer && c.alive);
      if (!target) break;

      await sendWsAction(page, serverUrl, {
        type: 'combat_action',
        action: {
          actorId: currentId,
          type: 'Attack',
          targetId: target.id,
        },
      });
      await page.waitForTimeout(300);
      iterations++;
    }

    // Ensure we returned to exploration
    await expect(page.locator('text=Return to Town')).toBeVisible();

    // Return to town to complete dungeon
    await sendWsAction(page, serverUrl, { type: 'return_to_town' });
    await page.waitForTimeout(300);

    // Save game
    await sendWsAction(page, serverUrl, { type: 'save_game' });
    await page.waitForTimeout(300);

    // Inspect action log via debug endpoint before reload
    const beforeReload = await request.get(`${serverUrl}/api/action-log`);
    expect(beforeReload.ok()).toBeTruthy();
    const logBefore = await beforeReload.json();
    expect(logBefore.events.length).toBeGreaterThanOrEqual(2);

    // Reload page (triggers new WebSocket connection and state sync)
    await page.reload();
    await page.waitForTimeout(1000);

    // Inspect action log after reload
    const afterReload = await request.get(`${serverUrl}/api/action-log`);
    expect(afterReload.ok()).toBeTruthy();
    const logAfter = await afterReload.json();

    // Verify event count preserved
    expect(logAfter.events.length).toBe(logBefore.events.length);

    // Verify encounter events have matching encounterId
    const started = logAfter.events.find((e: any) => e.type === 'encounter_started');
    const won = logAfter.events.find((e: any) => e.type === 'encounter_won');
    expect(started).toBeTruthy();
    expect(won).toBeTruthy();
    expect(started.payload.encounterId).toBe(won.payload.encounterId);

    // Verify dungeon events ordering
    const dungeonEntered = logAfter.events.find((e: any) => e.type === 'dungeon_entered');
    const dungeonCompleted = logAfter.events.find((e: any) => e.type === 'dungeon_completed');
    expect(dungeonEntered).toBeTruthy();
    expect(dungeonCompleted).toBeTruthy();
    expect(dungeonEntered.turn).toBeLessThan(dungeonCompleted.turn);
  });
});
