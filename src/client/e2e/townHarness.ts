import { expect, type Page } from '@playwright/test';

/**
 * Minimal valid Menu-mode state. Injecting this via __testSetState renders the
 * town menu deterministically, independent of whatever mode the shared backend
 * happens to be in (a prior test run may have left it in Combat/Exploration).
 */
export const MENU_STATE = {
  type: 'state',
  mode: 'Menu',
  player: { x: 0, y: 0, facing: 'North' },
  tiles: [],
  explored: [],
  hasDungeon: false,
  party: [],
  town: {
    currentTownId: 'the_reach',
    availableMissions: [],
    vendorStock: [],
    factionVendors: [],
    factionContacts: [],
    tavernRoster: [],
    viewedMissions: [],
    questLog: [],
  },
  overworld: { currentNodeId: 'the_reach', nodes: [], routes: [], turns: 0 },
  reputation: {},
  partyGold: 0,
  partyInventory: [],
  downtimeCompleted: [],
  actionLog: [],
};

/**
 * Load /app and deterministically render the town menu.
 *
 * The earlier implementation waited for `.town-menu` to appear from the live
 * backend's pushed state, which only works when the (reused) backend is already
 * in `mode='Menu'`. Because the dotnet host is shared and reused across runs
 * (`reuseExistingServer` locally), a prior test that entered a dungeon or combat
 * left the backend in a non-Menu mode, so `.town-menu` never appeared and these
 * specs failed — both in a full run and in isolation. game.spec avoids this by
 * sending `reset_game` first.
 *
 * Here we instead inject a Menu state through `__testSetState`, which engages the
 * store's override gate (`testStateOverrideActive`) so subsequent backend pushes
 * are ignored. This gives full isolation from backend state and makes town-menu
 * rendering deterministic — exactly the pattern these UI specs rely on for their
 * own per-test state injection.
 */
export async function loadTown(page: Page) {
  await page.goto('/app');
  await page.waitForFunction(() => Boolean((window as any).gameStore?.__testSetState));
  await page.evaluate((state) => {
    (window as any).__rpc_enableTestHooks();
    (window as any).gameStore.__testSetState(state);
  }, MENU_STATE);
  await expect(page.locator('.town-menu')).toBeVisible({ timeout: 10000 });
}
