import { test, expect, type Page } from '@playwright/test';

async function resetToTown(page: Page) {
  await page.goto('/app');
  await expect(page.locator('.game')).toBeVisible({ timeout: 20000 });
  await page.waitForFunction(() => Boolean((window as any).gameStore?.sendAction));
  await page.evaluate(() => {
    (window as any).gameStore.sendAction({ type: 'reset_game' });
  });
  await expect(page.locator('.town-menu')).toBeVisible({ timeout: 20000 });
}

/**
 * Injects a deterministic combat where the acting party member ('p1') holds a
 * consumable in their componentInventory, then captures the dispatched
 * PlayerAction so the UI -> intent -> action chain can be asserted without a backend.
 */
async function injectCombatWithConsumable(page: Page) {
  await page.evaluate(() => {
    const store = (window as any).gameStore;
    store.__testSetState({
      type: 'state',
      mode: 'Combat',
      player: { x: 0, y: 0, facing: 'North' },
      tiles: [],
      explored: [],
      hasDungeon: true,
      party: [
        {
          slot: 0,
          id: 'p1',
          name: 'Tester',
          classId: 'test',
          className: 'Tester',
          color: '#fff',
          level: 1,
          xp: 0,
          hp: 12,
          maxHp: 30,
          row: 0,
          alive: true,
          stats: {},
          equipment: {},
          knownAbilities: [],
          componentInventory: [
            { itemId: 'small_salve', count: 3, maxStack: 5, name: 'Small Salve', type: 'consumable' },
          ],
        },
      ],
      combat: {
        phase: 'Turn',
        round: 1,
        combatants: [
          {
            id: 'p1',
            name: 'Tester',
            isPlayer: true,
            hp: 12,
            maxHp: 30,
            speed: 10,
            row: 0,
            alive: true,
            isCurrent: true,
            abilities: [],
          },
          { id: 'e1', name: 'Goblin', isPlayer: false, hp: 10, maxHp: 10, speed: 1, row: 0, alive: true, isCurrent: false, abilities: [] },
        ],
        initiativeOrder: ['p1', 'e1'],
        currentTurnIndex: 0,
        log: [],
        isFinished: false,
      },
      overworld: { currentNodeId: 'the_reach', nodes: [], routes: [], turns: 0 },
    });
  });
}

test.describe('Combat consumables', () => {
  test('surfaces the actor consumables and emits a use_consumable action', async ({ page }) => {
    // Capture outbound websocket frames before the connection opens during goto.
    const sentPayloads: any[] = [];
    page.on('websocket', (ws) => {
      ws.on('framesent', (frame) => {
        try {
          const env = JSON.parse(frame.payload as string);
          if (env?.type === 'action' && env?.payload) sentPayloads.push(env.payload);
        } catch {
          /* non-JSON frame */
        }
      });
    });

    await resetToTown(page);
    await injectCombatWithConsumable(page);

    await expect(page.locator('.combat-overlay')).toBeVisible();

    // Choose the Item action; the actor's consumable should be surfaced.
    await page.locator('.action-btn', { hasText: 'Item' }).click();
    const consumable = page.locator('[data-testid="consumable-btn"][data-item-id="small_salve"]');
    await expect(consumable).toBeVisible();
    await expect(consumable).toContainText('Small Salve');
    await consumable.click();

    // Target the actor (self-heal): the party combatant becomes a valid target.
    const ally = page.locator('.party-side .front-band .combatant', { hasText: 'Tester' });
    await expect(ally).toHaveClass(/valid-target/);
    await ally.click();

    await page.locator('.submit-btn', { hasText: 'Execute' }).click();

    await expect.poll(() => sentPayloads.some((a) => a.type === 'use_consumable')).toBe(true);
    const useConsumable = sentPayloads.find((a) => a.type === 'use_consumable');
    expect(useConsumable.action).toMatchObject({
      actorId: 'p1',
      type: 'UseItem',
      targetId: 'p1',
      itemId: 'small_salve',
    });
  });

  test('shows no-consumables hint when the actor carries none', async ({ page }) => {
    await resetToTown(page);
    await page.evaluate(() => {
      const store = (window as any).gameStore;
      store.__testSetState({
        type: 'state',
        mode: 'Combat',
        player: { x: 0, y: 0, facing: 'North' },
        tiles: [],
        explored: [],
        hasDungeon: true,
        party: [
          {
            slot: 0,
            id: 'p1',
            name: 'Tester',
            classId: 'test',
            className: 'Tester',
            color: '#fff',
            level: 1,
            xp: 0,
            hp: 12,
            maxHp: 30,
            row: 0,
            alive: true,
            stats: {},
            equipment: {},
            knownAbilities: [],
            componentInventory: [],
          },
        ],
        combat: {
          phase: 'Turn',
          round: 1,
          combatants: [
            { id: 'p1', name: 'Tester', isPlayer: true, hp: 12, maxHp: 30, speed: 10, row: 0, alive: true, isCurrent: true, abilities: [] },
            { id: 'e1', name: 'Goblin', isPlayer: false, hp: 10, maxHp: 10, speed: 1, row: 0, alive: true, isCurrent: false, abilities: [] },
          ],
          initiativeOrder: ['p1', 'e1'],
          currentTurnIndex: 0,
          log: [],
          isFinished: false,
        },
        overworld: { currentNodeId: 'the_reach', nodes: [], routes: [], turns: 0 },
      });
    });

    await expect(page.locator('.combat-overlay')).toBeVisible();
    await page.locator('.action-btn', { hasText: 'Item' }).click();
    await expect(page.locator('.no-consumables')).toContainText('No consumables');
  });
});
