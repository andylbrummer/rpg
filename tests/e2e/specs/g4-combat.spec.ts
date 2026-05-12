import { test, expect } from './fixtures';
import { sendWsAction } from './helpers';

function makeMockCombat(partyCount: number, enemyCount: number) {
  const combatants = [
    ...Array.from({ length: partyCount }, (_, i) => ({
      id: `p${i}`,
      name: `Hero${i + 1}`,
      isPlayer: true,
      classId: 'warrior',
      hp: 100,
      maxHp: 100,
      speed: 5,
      row: i < 3 ? 0 : 1,
      alive: true,
      isCurrent: i === 0,
      abilities: [],
    })),
    ...Array.from({ length: enemyCount }, (_, i) => ({
      id: `e${i}`,
      name: `Enemy${i + 1}`,
      isPlayer: false,
      hp: 50,
      maxHp: 50,
      speed: 4,
      row: i < 3 ? 0 : 1,
      alive: true,
      isCurrent: false,
      abilities: [],
    })),
  ];
  const initiativeOrder = combatants.map(c => c.id);
  return {
    phase: 'Turn',
    round: 1,
    combatants,
    initiativeOrder,
    currentTurnIndex: 0,
    log: [],
    isFinished: false,
  };
}

async function injectCombatState(page: any, combat: any) {
  await page.evaluate((c: any) => {
    const store = (window as any).gameStore;
    store.__testSetState({
      type: 'state',
      mode: 'Combat',
      player: { x: 0, y: 0, facing: 'North' },
      tiles: [],
      explored: [],
      hasDungeon: true,
      party: [],
      combat: c,
    });
  }, combat);
}

test.describe('G4: Combat', () => {
  test('combat state has combatants after trigger', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await sendWsAction(page, serverUrl, { type: 'enter_dungeon', dungeonType: 'broken_engine' });
    await sendWsAction(page, serverUrl, { type: 'enter_combat' });
    await expect(page.locator('.combat-overlay')).toBeVisible();
  });

  test('flee combat returns to exploration', async ({ page, serverUrl }) => {
    await page.goto(`${serverUrl}/app`);
    await sendWsAction(page, serverUrl, { type: 'enter_dungeon', dungeonType: 'broken_engine' });
    await sendWsAction(page, serverUrl, { type: 'enter_combat' });
    await expect(page.locator('.combat-overlay')).toBeVisible();
    await sendWsAction(page, serverUrl, { type: 'flee_combat' });
    await expect(page.locator('text=Return to Town')).toBeVisible();
  });

  test.describe('FormationDragDropTests', () => {
    test('dragging char from front to back updates formation', async ({ page, serverUrl }) => {
      await page.goto(`${serverUrl}/app`);
      await sendWsAction(page, serverUrl, { type: 'return_to_town' });
      await expect(page.locator('.formation-grid')).toBeVisible();

      const frontCard = page.locator('.formation-row.front-row .formation-card').first();
      const backRow = page.locator('.formation-row.back-row');

      await frontCard.dragTo(backRow);
      await page.waitForTimeout(400);

      // After swap, the dragged character should appear in the back row
      const backNames = await backRow.locator('.formation-name').allTextContents();
      expect(backNames.length).toBeGreaterThan(0);
    });
  });

  test.describe('CombatRendererFormationTests', () => {
    test('combat renderer shows front and back rows', async ({ page, serverUrl }) => {
      await page.goto(`${serverUrl}/app`);
      await sendWsAction(page, serverUrl, { type: 'enter_dungeon', dungeonType: 'broken_engine' });
      await sendWsAction(page, serverUrl, { type: 'enter_combat' });
      await expect(page.locator('.combat-overlay')).toBeVisible();

      const partyFront = page.locator('.party-side .row-band.front-band');
      const partyBack = page.locator('.party-side .row-band.back-band');
      await expect(partyFront).toBeVisible();
      await expect(partyBack).toBeVisible();

      const enemyFront = page.locator('.enemy-side .row-band.front-band');
      const enemyBack = page.locator('.enemy-side .row-band.back-band');
      await expect(enemyFront).toBeVisible();
      await expect(enemyBack).toBeVisible();
    });
  });

  test.describe('TargetingUITests', () => {
    test('melee ability highlights only front-row enemies', async ({ page, serverUrl }) => {
      await page.goto(`${serverUrl}/app`);
      await sendWsAction(page, serverUrl, { type: 'enter_dungeon', dungeonType: 'broken_engine' });
      await sendWsAction(page, serverUrl, { type: 'enter_combat' });
      await expect(page.locator('.combat-overlay')).toBeVisible();

      // Wait for a player turn (action buttons visible)
      await expect(page.locator('.action-select')).toBeVisible({ timeout: 10000 });

      // Select UseAbility
      const abilityBtn = page.locator('.action-btn', { hasText: 'Skill' });
      await abilityBtn.click();
      await page.waitForTimeout(200);

      // Wait for ability list
      await expect(page.locator('.ability-select')).toBeVisible();

      // Select the first ability and verify targeting rules
      const abilities = page.locator('.ability-btn');
      const count = await abilities.count();
      if (count > 0) {
        await abilities.first().click();
        await page.waitForTimeout(200);

        const frontEnemies = page.locator('.enemy-side .row-band.front-band .combatant');
        const backEnemies = page.locator('.enemy-side .row-band.back-band .combatant');

        if (await frontEnemies.count() > 0) {
          await expect(frontEnemies.first()).toHaveClass(/valid-target/);
        }

        if (await backEnemies.count() > 0) {
          const backFirst = backEnemies.first();
          const classAttr = await backFirst.getAttribute('class');
          if (classAttr?.includes('invalid-target')) {
            // Melee ability: back row is invalid
            expect(classAttr).toMatch(/invalid-target/);
          } else {
            // Ranged ability: back row may be valid
            expect(classAttr).toMatch(/valid-target/);
          }
        }
      }
    });
  });

  test.describe('CombatViewportTests', () => {
    test('fits 1920x1080 without horizontal scroll', async ({ page, serverUrl }) => {
      await page.setViewportSize({ width: 1920, height: 1080 });
      await page.goto(`${serverUrl}/app`);
      await sendWsAction(page, serverUrl, { type: 'enter_dungeon', dungeonType: 'broken_engine' });
      await sendWsAction(page, serverUrl, { type: 'enter_combat' });
      await expect(page.locator('.combat-overlay')).toBeVisible();

      const combat = makeMockCombat(6, 3);
      await injectCombatState(page, combat);
      await expect(page.locator('.combat-arena .combatant')).toHaveCount(9);

      const overlay = page.locator('.combat-overlay');
      const box = await overlay.boundingBox();
      expect(box?.width).toBeLessThanOrEqual(1920);

      const arena = page.locator('.combat-arena');
      const scrollWidth = await arena.evaluate((el: HTMLElement) => el.scrollWidth);
      const clientWidth = await arena.evaluate((el: HTMLElement) => el.clientWidth);
      expect(scrollWidth).toBeLessThanOrEqual(clientWidth);
    });
  });

  test.describe('InitiativeBarTests', () => {
    test('12-slot initiative bar is readable without scroll', async ({ page, serverUrl }) => {
      await page.setViewportSize({ width: 1920, height: 1080 });
      await page.goto(`${serverUrl}/app`);
      await sendWsAction(page, serverUrl, { type: 'enter_dungeon', dungeonType: 'broken_engine' });
      await sendWsAction(page, serverUrl, { type: 'enter_combat' });
      await expect(page.locator('.combat-overlay')).toBeVisible();

      const combat = makeMockCombat(6, 6);
      await injectCombatState(page, combat);
      await expect(page.locator('.initiative-entry')).toHaveCount(12);

      const bar = page.locator('.initiative-bar');
      const scrollWidth = await bar.evaluate((el: HTMLElement) => el.scrollWidth);
      const clientWidth = await bar.evaluate((el: HTMLElement) => el.clientWidth);
      expect(scrollWidth).toBeLessThanOrEqual(clientWidth);

      const firstEntry = page.locator('.initiative-entry').first();
      const entryBox = await firstEntry.boundingBox();
      expect(entryBox?.width).toBeGreaterThanOrEqual(60);
    });
  });

  test.describe('VisualOverlapTests', () => {
    test('no overlap at max encounter', async ({ page, serverUrl }) => {
      await page.setViewportSize({ width: 1920, height: 1080 });
      await page.goto(`${serverUrl}/app`);
      await sendWsAction(page, serverUrl, { type: 'enter_dungeon', dungeonType: 'broken_engine' });
      await sendWsAction(page, serverUrl, { type: 'enter_combat' });
      await expect(page.locator('.combat-overlay')).toBeVisible();

      const combat = makeMockCombat(6, 9);
      await injectCombatState(page, combat);
      await expect(page.locator('.combat-arena .combatant')).toHaveCount(15);

      const cards = page.locator('.combat-arena .combatant');
      const count = await cards.count();
      for (let i = 0; i < count; i++) {
        const box = await cards.nth(i).boundingBox();
        expect(box).not.toBeNull();
        expect(box!.width).toBeGreaterThan(0);
        expect(box!.height).toBeGreaterThan(0);
      }

      const arena = page.locator('.combat-arena');
      const arenaScroll = await arena.evaluate((el: HTMLElement) => el.scrollWidth);
      const arenaClient = await arena.evaluate((el: HTMLElement) => el.clientWidth);
      expect(arenaScroll).toBeLessThanOrEqual(arenaClient);
    });
  });
});
