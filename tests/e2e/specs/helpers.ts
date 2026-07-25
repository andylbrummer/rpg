import { expect, type Page, type APIRequestContext } from '@playwright/test';

export async function sendWsAction(page: Page, _serverUrl: string, action: any): Promise<void> {
  await page.waitForFunction(() => Boolean((window as any).gameClient?.sendAction));
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

export async function waitForGameState(page: Page, predicate: (state: any) => boolean, timeout = 10000): Promise<any> {
  const deadline = Date.now() + timeout;
  while (Date.now() < deadline) {
    const state = await getGameState(page);
    if (predicate(state)) return state;
    await page.waitForTimeout(100);
  }

  throw new Error(`Timed out waiting for game state after ${timeout}ms`);
}

/**
 * Party the engine builds in InitializeDefaultParty, in slot order. Used as the marker that a
 * reset has actually landed.
 */
const DEFAULT_PARTY_ORDER = ['Kael', 'Sera', 'Mira', 'Vex', 'Nyx', 'Orin'];

export async function resetGame(page: Page, serverUrl: string): Promise<any> {
  await sendWsAction(page, serverUrl, { type: 'reset_game' });
  return waitForGameState(page, (state: any) =>
    state?.mode === 'Menu' &&
    state?.overworld?.turns === 0 &&
    state?.hasDungeon === false &&
    state?.party?.every((member: any) => member.level === 1) &&
    // Wait for the party to be back in its default slot order. Every other clause is already
    // true of the state from *before* the reset — a preceding test that left the game in town
    // at level 1 satisfies all of them — so without a marker that only a completed reset can
    // produce, this returned the previous test's snapshot. Rows alone are not that marker:
    // swapping two members exchanges their slots too, so the layout stays self-consistent.
    state?.party?.every((member: any, i: number) => member.name === DEFAULT_PARTY_ORDER[i]),
    20000);
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
