import { test, expect } from './fixtures';
import { enterCombat, enterDungeon, resetGame, resolveCombatByAttacking, sendWsAction } from './helpers';

test.describe('Action Log Persists', () => {
  test('combat log events survive save and reload', async ({ page, serverUrl, request }) => {
    await page.goto(`${serverUrl}/app`);

    // Start fresh and wait for initial state
    await resetGame(page, serverUrl);

    // Enter dungeon and trigger combat
    await enterDungeon(page, serverUrl, 'broken_engine');
    await enterCombat(page, serverUrl);
    await resolveCombatByAttacking(page, serverUrl);

    // Ensure we returned to exploration
    await expect(page.locator('text=Return to Town')).toBeVisible({ timeout: 10000 });

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
