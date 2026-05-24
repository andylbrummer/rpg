import { expect, type Page, type APIRequestContext } from '@playwright/test';

export async function sendWsAction(page: Page, _serverUrl: string, action: any): Promise<void> {
  await page.evaluate((act: any) => {
    (window as any).gameClient?.sendAction(act);
  }, action);
  await page.waitForTimeout(600);
}

export async function getGameState(page: Page): Promise<any> {
  return page.evaluate(() => {
    const store = (window as any).gameStore;
    let current: any = null;
    const unsubscribe = store.subscribe((state: any) => {
      current = state;
    });
    unsubscribe();
    return current;
  });
}

export async function resolveCombatByAttacking(page: Page, serverUrl: string, maxActions = 80): Promise<void> {
  await expect(page.locator('.combat-overlay')).toBeVisible({ timeout: 10000 });

  for (let i = 0; i < maxActions; i++) {
    const state = await getGameState(page);
    if (state?.mode !== 'Combat') return;

    const combat = state.combat;
    if (!combat || combat.phase !== 'Turn') {
      await page.waitForTimeout(150);
      continue;
    }

    const currentId = combat.initiativeOrder[combat.currentTurnIndex];
    const actor = combat.combatants.find((c: any) => c.id === currentId);
    if (!actor?.isPlayer) {
      await page.waitForTimeout(150);
      continue;
    }

    const target = combat.combatants.find((c: any) => !c.isPlayer && c.alive);
    if (!target) return;

    await sendWsAction(page, serverUrl, {
      type: 'combat_action',
      action: {
        actorId: actor.id,
        type: 'Attack',
        targetId: target.id,
      },
    });
  }

  throw new Error(`Combat did not resolve after ${maxActions} attempted actions`);
}

export async function resolveTravelOutcomes(page: Page, serverUrl: string, maxSteps = 5): Promise<void> {
  for (let i = 0; i < maxSteps; i++) {
    const combatVisible = await page.locator('.combat-overlay').isVisible().catch(() => false);
    if (combatVisible) {
      await sendWsAction(page, serverUrl, { type: 'flee_combat' });
      await expect(page.locator('.combat-overlay')).not.toBeVisible({ timeout: 10000 });
      continue;
    }

    const encounterVisible = await page.locator('.travel-encounter-overlay').isVisible().catch(() => false);
    const state = await getGameState(page);
    if (!encounterVisible && !state?.travelEncounter) return;

    if (encounterVisible) {
      await page.locator('.travel-action-btn').first().click();
    } else {
      await sendWsAction(page, serverUrl, {
        type: 'resolve_travel_encounter',
        targetId: state.travelEncounter.options?.[0] ?? 'roll',
      });
    }
    await page.waitForTimeout(700);
  }

  const combatVisible = await page.locator('.combat-overlay').isVisible().catch(() => false);
  const encounterVisible = await page.locator('.travel-encounter-overlay').isVisible().catch(() => false);
  const state = await getGameState(page);
  if (combatVisible || encounterVisible || state?.travelEncounter) {
    throw new Error(`Travel outcomes did not clear after ${maxSteps} steps`);
  }
}

export async function getPositionText(page: Page): Promise<string> {
  const el = page.locator('.exploration-hud .position');
  try {
    await el.waitFor({ timeout: 10000 });
    return await el.textContent() ?? '';
  } catch {
    return '';
  }
}

export async function getMainJsUrl(request: APIRequestContext, base: string): Promise<string> {
  const pageRes = await request.get(`${base || ''}/app`);
  const html = await pageRes.text();
  const match = html.match(/src="\/assets\/([^"]+\.js)"/);
  if (!match) throw new Error('Could not find main JS bundle');
  return match[1];
}
