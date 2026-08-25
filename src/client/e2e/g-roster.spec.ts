import { test, expect, type Page } from '@playwright/test';
import { loadTown } from './townHarness';

const ACTIVE_A = 'aaaaaaaa-0000-0000-0000-000000000001';
const ACTIVE_B = 'aaaaaaaa-0000-0000-0000-000000000002';
const BENCH_A = 'bbbbbbbb-0000-0000-0000-000000000001';
const BENCH_B = 'bbbbbbbb-0000-0000-0000-000000000002';

function activeMember(slot: number, overrides: Record<string, unknown> = {}) {
  return {
    slot,
    id: ACTIVE_A,
    name: 'Vesper',
    classId: 'inkblood',
    className: 'Inkblood',
    color: '#7A4B6B',
    level: 4,
    xp: 0,
    hp: 18,
    maxHp: 24,
    row: 0,
    alive: true,
    awaitingBranchChoice: false,
    classAbilities: [],
    stats: { strength: 4, dexterity: 4, constitution: 4, intelligence: 5, willpower: 4, maxHp: 24, speed: 8, accuracy: 10, evade: 10, power: 10 },
    equipment: { mainHand: null, offHand: null, armor: null, accessory1: null, accessory2: null },
    knownAbilities: [],
    componentInventory: [],
    ...overrides,
  };
}

function benchMember(id: string, overrides: Record<string, unknown> = {}) {
  return {
    id,
    name: 'Cassia',
    classId: 'bonewarden',
    className: 'Bonewarden',
    color: '#8B7355',
    level: 2,
    xp: 0,
    hp: 30,
    maxHp: 30,
    alive: true,
    branchChoice: null,
    ...overrides,
  };
}

function rosterState(extra: Record<string, unknown>) {
  return {
    type: 'state',
    mode: 'Menu',
    player: { x: 0, y: 0, facing: 'North' },
    tiles: [],
    explored: [],
    hasDungeon: false,
    party: [],
    bench: [],
    rosterInfo: { activeCount: 0, benchCount: 0, rosterCount: 0, maxRosterSize: 12, atCap: false },
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
    partyGold: 100,
    partyInventory: [],
    downtimeCompleted: [],
    actionLog: [],
    ...extra,
  };
}

/** Capture actions reaching the client (the real intent->action->sendAction path). */
async function installActionCapture(page: Page) {
  await page.evaluate(() => {
    const client = (window as any).gameClient;
    const captured: unknown[] = [];
    (window as any).__capturedActions = captured;
    client.sendAction = (action: unknown) => {
      captured.push(action);
    };
  });
}

async function setState(page: Page, state: Record<string, unknown>) {
  await page.evaluate((s) => {
    (window as any).__rpc_enableTestHooks();
    (window as any).gameStore.__testSetState(s);
  }, state);
}

async function getCaptured(page: Page): Promise<any[]> {
  return page.evaluate(() => (window as any).__capturedActions ?? []);
}

async function openRoster(page: Page) {
  await page.locator('.town-nav-btn', { hasText: 'Roster' }).click();
  await expect(page.locator('.roster-screen')).toBeVisible();
}

test.describe('Roster management UI (T57)', () => {
  test('shows active + bench in a 12-slot view with character cards', async ({ page }) => {
    await loadTown(page);
    await setState(page, rosterState({
      party: [activeMember(0, { branchChoice: 'ink_scribe' })],
      bench: [benchMember(BENCH_A)],
      rosterInfo: { activeCount: 1, benchCount: 1, rosterCount: 2, maxRosterSize: 12, atCap: false },
    }));
    await openRoster(page);

    await expect(page.locator('.roster-capacity')).toContainText('2 / 12');

    const activeCard = page.locator('.roster-card.active');
    await expect(activeCard).toHaveCount(1);
    await expect(activeCard).toContainText('Vesper');
    await expect(activeCard).toContainText('Lv.4 Inkblood');
    await expect(activeCard).toContainText('Ink Scribe');
    await expect(activeCard).toContainText('HP 18/24');
    await expect(activeCard.locator('.roster-badge')).toContainText('Active');

    const benchCard = page.locator('.roster-card.bench');
    await expect(benchCard).toContainText('Cassia');
    await expect(benchCard.locator('.roster-badge')).toContainText('Bench');

    // 12-slot grid: 2 occupied + 10 empty placeholders.
    await expect(page.locator('.roster-card-empty')).toHaveCount(10);
  });

  test('benching an active member emits swapActiveBench; activating uses an empty slot', async ({ page }) => {
    await loadTown(page);
    await installActionCapture(page);
    await setState(page, rosterState({
      party: [activeMember(2)],
      bench: [benchMember(BENCH_A)],
      rosterInfo: { activeCount: 1, benchCount: 1, rosterCount: 2, maxRosterSize: 12, atCap: false },
    }));
    await openRoster(page);

    await page.locator('.roster-card.active .roster-bench-btn').click();
    await page.locator('.roster-card.bench .roster-activate-btn').click();

    const actions = await getCaptured(page);
    expect(actions).toContainEqual({ type: 'swap_active_bench', slot: 2, targetId: undefined });
    // First empty active slot is 0 (active member occupies slot 2).
    expect(actions).toContainEqual({ type: 'swap_active_bench', slot: 0, targetId: BENCH_A });
  });

  test('dismiss requires confirmation before emitting dismissCharacter', async ({ page }) => {
    await loadTown(page);
    await installActionCapture(page);
    await setState(page, rosterState({
      bench: [benchMember(BENCH_A)],
      rosterInfo: { activeCount: 0, benchCount: 1, rosterCount: 1, maxRosterSize: 12, atCap: false },
    }));
    await openRoster(page);

    await page.locator('.roster-card.bench .roster-dismiss-btn').click();
    await expect(page.locator('.roster-confirm')).toBeVisible();

    // Cancel does not emit.
    await page.locator('.roster-confirm-cancel').click();
    await expect(page.locator('.roster-confirm')).toHaveCount(0);
    expect(await getCaptured(page)).toEqual([]);

    // Confirm emits dismiss_character.
    await page.locator('.roster-card.bench .roster-dismiss-btn').click();
    await page.locator('.roster-confirm-yes').click();
    expect(await getCaptured(page)).toContainEqual({ type: 'dismiss_character', targetId: BENCH_A });
  });

  test('filter by class and sort by level reorder the visible cards', async ({ page }) => {
    await loadTown(page);
    await setState(page, rosterState({
      party: [activeMember(0, { className: 'Inkblood', level: 4 })],
      bench: [
        benchMember(BENCH_A, { name: 'Cassia', className: 'Bonewarden', level: 2 }),
        benchMember(BENCH_B, { name: 'Doran', className: 'Bonewarden', level: 7 }),
      ],
      rosterInfo: { activeCount: 1, benchCount: 2, rosterCount: 3, maxRosterSize: 12, atCap: false },
    }));
    await openRoster(page);

    // Filter to Bonewarden hides the Inkblood and the empty placeholders.
    await page.locator('.roster-filter-class').selectOption('Bonewarden');
    await expect(page.locator('.roster-card:not(.roster-card-empty)')).toHaveCount(2);
    await expect(page.locator('.roster-card-empty')).toHaveCount(0);

    // Sort by level (descending) -> Doran (7) before Cassia (2).
    await page.locator('.roster-sort').selectOption('level');
    const names = page.locator('.roster-card .roster-card-name');
    await expect(names.first()).toContainText('Doran');
    await expect(names.last()).toContainText('Cassia');
  });

  test('activating with a full active party prompts for a swap target', async ({ page }) => {
    await loadTown(page);
    await installActionCapture(page);
    await setState(page, rosterState({
      party: [
        activeMember(0, { id: ACTIVE_A, name: 'Vesper' }),
        activeMember(1, { id: ACTIVE_B, name: 'Mira' }),
        activeMember(2, { id: 'aaaaaaaa-0000-0000-0000-000000000003', name: 'Tess' }),
        activeMember(3, { id: 'aaaaaaaa-0000-0000-0000-000000000004', name: 'Orin' }),
        activeMember(4, { id: 'aaaaaaaa-0000-0000-0000-000000000005', name: 'Lyle' }),
        activeMember(5, { id: 'aaaaaaaa-0000-0000-0000-000000000006', name: 'Bram' }),
      ],
      bench: [benchMember(BENCH_A, { name: 'Cassia' })],
      rosterInfo: { activeCount: 6, benchCount: 1, rosterCount: 7, maxRosterSize: 12, atCap: false },
    }));
    await openRoster(page);

    await page.locator('.roster-card.bench .roster-activate-btn').click();
    await expect(page.locator('.roster-pending-banner')).toBeVisible();

    // Choose the first active member (slot 0) as the swap target.
    await page.locator('.roster-card.active .roster-swap-target-btn').first().click();

    const actions = await getCaptured(page);
    expect(actions).toContainEqual({ type: 'swap_active_bench', slot: 0, targetId: BENCH_A });
    await expect(page.locator('.roster-pending-banner')).toHaveCount(0);
  });
});
