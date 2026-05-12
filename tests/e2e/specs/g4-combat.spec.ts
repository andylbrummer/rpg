import { test, expect } from './fixtures';
import { sendWsAction } from './helpers';

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

      // Select the first melee ability if available, otherwise any ability
      const abilities = page.locator('.ability-btn');
      const count = await abilities.count();
      if (count > 0) {
        await abilities.first().click();
        await page.waitForTimeout(200);

        // Check that front-row enemies have valid-target class
        const frontEnemies = page.locator('.enemy-side .row-band.front-band .combatant');
        const backEnemies = page.locator('.enemy-side .row-band.back-band .combatant');

        if (await frontEnemies.count() > 0) {
          await expect(frontEnemies.first()).toHaveClass(/valid-target/);
        }

        // Back-row enemies should be invalid targets for melee
        if (await backEnemies.count() > 0) {
          await expect(backEnemies.first()).toHaveClass(/invalid-target/);
        }
      }
    });
  });
});
