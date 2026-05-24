import { test, expect, type Page } from '@playwright/test';

async function readGameState(page: Page): Promise<any> {
  return page.evaluate(() => {
    let current: any = null;
    const unsubscribe = (window as any).gameStore.subscribe((state: any) => {
      current = state;
    });
    unsubscribe();
    return current;
  });
}

async function resetToTown(page: Page) {
  await page.goto('/app');
  await page.waitForFunction(() => Boolean((window as any).gameStore?.sendAction));
  await page.evaluate(() => {
    (window as any).gameStore.sendAction({ type: 'reset_game' });
  });
  await expect(page.locator('.mode-badge')).toContainText('Menu', { timeout: 10000 });
  await expect(page.locator('.town-menu')).toBeVisible();
}

async function enterDungeon(page: Page) {
  await resetToTown(page);
  await page.locator('.town-nav-btn').filter({ hasText: 'Dungeons' }).click();
  await page.locator('.dungeon-btn').first().click();
  await expect(page.locator('.mode-badge')).toContainText('Exploration', { timeout: 10000 });
  await expect(page.locator('.exploration-hud')).toBeVisible();
}

async function waitForState(page: Page, predicate: (state: any) => boolean, timeout = 10000) {
  const deadline = Date.now() + timeout;
  while (Date.now() < deadline) {
    const state = await readGameState(page);
    if (predicate(state)) return state;
    await page.waitForTimeout(100);
  }
  throw new Error(`Timed out waiting for state after ${timeout}ms`);
}

test.describe('Map and Combat Outcome QA', () => {
  test('overworld map shows route metadata and travel updates current node and turns', async ({ page }) => {
    await resetToTown(page);
    const before = await readGameState(page);
    const overworld = before.overworld;
    const currentId = overworld.currentNodeId;
    const route = overworld.routes.find((r: any) => r.status !== 'Blocked' && (r.from === currentId || r.to === currentId));
    expect(route).toBeTruthy();

    const targetId = route.from === currentId ? route.to : route.from;
    const target = overworld.nodes.find((n: any) => n.id === targetId);
    expect(target).toBeTruthy();

    await page.getByRole('button', { name: 'Overworld Map' }).click();
    const map = page.getByRole('dialog', { name: 'Overworld map' });
    await expect(map).toBeVisible();

    await map.locator(`[aria-label="Route danger ${route.dangerRating} status ${route.status}"]`).first().hover({ force: true });
    await expect(page.locator('.tooltip')).toContainText(`Distance: ${route.distance} turns`);
    await expect(page.locator('.tooltip')).toContainText(`Danger: ${route.dangerRating}`);
    await expect(page.locator('.tooltip')).toContainText(`Terrain: ${route.terrain}`);

    await map.getByRole('button', { name: target.name }).click();
    const confirm = page.getByRole('alertdialog', { name: 'Confirm travel' });
    await expect(confirm).toBeVisible();
    await expect(confirm).toContainText(`Cost: ${route.distance} turns`);
    await confirm.getByRole('button', { name: 'Travel' }).click();

    const after = await waitForState(page, (state: any) => state?.overworld?.currentNodeId === targetId);
    expect(after.overworld.turns).toBe(before.overworld.turns + route.distance);
  });

  test('automap renders explored tiles and player marker after dungeon entry', async ({ page }) => {
    await enterDungeon(page);
    const state = await readGameState(page);
    expect(state.explored.length).toBeGreaterThan(0);

    const pixels = await page.locator('.automap-container canvas').evaluate((canvas: HTMLCanvasElement) => {
      const ctx = canvas.getContext('2d');
      if (!ctx) return { painted: 0, playerBlue: 0 };
      const { data } = ctx.getImageData(0, 0, canvas.width, canvas.height);
      let painted = 0;
      let playerBlue = 0;
      for (let i = 0; i < data.length; i += 4) {
        const [r, g, b, a] = [data[i], data[i + 1], data[i + 2], data[i + 3]];
        if (a > 0 && (r > 0 || g > 0 || b > 0)) painted++;
        if (a > 0 && b > 160 && g > 100 && r < 120) playerBlue++;
      }
      return { painted, playerBlue };
    });

    expect(pixels.painted).toBeGreaterThan(0);
    expect(pixels.playerBlue).toBeGreaterThan(0);
  });

  test('combat targeting marks melee back-row enemies invalid before submit', async ({ page }) => {
    await resetToTown(page);
    await page.evaluate(() => {
      (window as any).gameStore.__testSetState({
        type: 'state',
        mode: 'Combat',
        player: { x: 0, y: 0, facing: 'North' },
        tiles: [],
        explored: [],
        hasDungeon: true,
        party: [],
        combat: {
          phase: 'Turn',
          round: 1,
          combatants: [
            {
              id: 'p1',
              name: 'Tester',
              isPlayer: true,
              hp: 20,
              maxHp: 20,
              speed: 10,
              row: 0,
              alive: true,
              isCurrent: true,
              abilities: [{ id: 'melee_test', name: 'Melee Test', range: 'melee', available: true }]
            },
            { id: 'e1', name: 'Front Enemy', isPlayer: false, hp: 10, maxHp: 10, speed: 1, row: 0, alive: true, isCurrent: false, abilities: [] },
            { id: 'e2', name: 'Back Enemy', isPlayer: false, hp: 10, maxHp: 10, speed: 1, row: 1, alive: true, isCurrent: false, abilities: [] }
          ],
          initiativeOrder: ['p1', 'e1', 'e2'],
          currentTurnIndex: 0,
          log: [],
          isFinished: false
        },
        overworld: { currentNodeId: 'the_reach', nodes: [], routes: [], turns: 0 }
      });
    });

    await page.locator('.action-btn', { hasText: 'Skill' }).click();
    await page.locator('.ability-btn', { hasText: 'Melee Test' }).click();

    await expect(page.locator('.enemy-side .front-band .combatant', { hasText: 'Front Enemy' })).toHaveClass(/valid-target/);
    const backEnemy = page.locator('.enemy-side .back-band .combatant', { hasText: 'Back Enemy' });
    await expect(backEnemy).toHaveClass(/invalid-target/);
    await expect(backEnemy).toBeDisabled();
  });

  test('real combat can resolve to a victory result and action-log outcome', async ({ page }) => {
    await enterDungeon(page);
    await page.evaluate(() => {
      (window as any).gameStore.sendAction({ type: 'enter_combat' });
    });
    await waitForState(page, (state: any) => state?.mode === 'Combat' && state?.combat?.phase === 'Turn');
    await expect(page.locator('.combat-overlay')).toBeVisible();

    for (let i = 0; i < 30; i++) {
      const state = await readGameState(page);
      if (state.mode !== 'Combat') break;
      const combat = state.combat;
      const actorId = combat.initiativeOrder[combat.currentTurnIndex];
      const actor = combat.combatants.find((c: any) => c.id === actorId);
      const target = combat.combatants.find((c: any) => !c.isPlayer && c.alive && c.hp > 0);
      if (actor?.isPlayer && target) {
        await page.evaluate(({ actorId, targetId }) => {
          (window as any).gameStore.sendAction({
            type: 'combat_action',
            action: { actorId, type: 'Attack', targetId }
          });
        }, { actorId, targetId: target.id });
      }
      await page.waitForTimeout(250);
    }

    const outcome = await waitForState(page, (state: any) => state?.mode === 'Exploration' && state?.combatResult?.victory === true, 15000);
    expect(outcome.combatResult.xpGained).toBeGreaterThan(0);
    expect(outcome.combatResult.roundCount).toBeGreaterThan(0);
    expect(outcome.actionLog.some((event: any) => event.type === 'encounter_won')).toBe(true);
  });
});
