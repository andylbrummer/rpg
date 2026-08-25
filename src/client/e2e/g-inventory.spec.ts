import { test, expect, type Page } from '@playwright/test';
import { loadTown } from './townHarness';

const MEMBER_A = 'cccccccc-0000-0000-0000-000000000001';
const MEMBER_B = 'cccccccc-0000-0000-0000-000000000002';

function stack(itemId: string, count: number, overrides: Record<string, unknown> = {}) {
  return { itemId, count, maxStack: 99, name: itemId, type: 'component', equipSlot: null, ...overrides };
}

function member(slot: number, id: string, overrides: Record<string, unknown> = {}) {
  return {
    slot,
    id,
    name: slot === 0 ? 'Vesper' : 'Mira',
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

function inventoryState(extra: Record<string, unknown>) {
  return {
    type: 'state',
    mode: 'Menu',
    player: { x: 0, y: 0, facing: 'North' },
    tiles: [],
    explored: [],
    hasDungeon: false,
    party: [],
    bench: [],
    expeditionCache: [],
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

async function openInventory(page: Page) {
  await page.locator('.town-nav-btn', { hasText: 'Inventory' }).click();
  await expect(page.locator('.inventory-screen')).toBeVisible();
}

test.describe('Inventory UI (character bags + expedition cache)', () => {
  test('renders bag + cache grids with stack names and counts', async ({ page }) => {
    await loadTown(page);
    await setState(page, inventoryState({
      party: [member(0, MEMBER_A, {
        componentInventory: [stack('Bloom Dust', 5), stack('Iron Filing', 2)],
      })],
      expeditionCache: [stack('Ash Resin', 12)],
    }));
    await openInventory(page);

    // Bag shows two stacks + 6 empty placeholders (8-slot bag).
    const bag = page.locator('.inv-bag');
    await expect(bag.locator('.inv-stack:not(.inv-stack-empty)')).toHaveCount(2);
    await expect(bag.locator('.inv-stack-empty')).toHaveCount(6);
    await expect(bag).toContainText('Bloom Dust');
    await expect(bag.locator('.inv-stack-count').first()).toContainText('5/99');
    await expect(bag.locator('.inv-fill')).toContainText('2 / 8');

    // Cache shows one stack + 11 empty placeholders (12-slot cache).
    const cache = page.locator('.inv-cache');
    await expect(cache.locator('.inv-stack:not(.inv-stack-empty)')).toHaveCount(1);
    await expect(cache.locator('.inv-stack-empty')).toHaveCount(11);
    await expect(cache.locator('.inv-fill')).toContainText('1 / 12');
  });

  test('moving a stack to cache emits transfer_to_cache for the selected member slot', async ({ page }) => {
    await loadTown(page);
    await installActionCapture(page);
    await setState(page, inventoryState({
      party: [member(3, MEMBER_A, { componentInventory: [stack('Bloom Dust', 5)] })],
      expeditionCache: [],
    }));
    await openInventory(page);

    await page.locator('.inv-bag .inv-to-cache-btn').first().click();

    const actions = await getCaptured(page);
    // Default quantity is 1; slot is the member's party slot (3).
    expect(actions).toContainEqual({ type: 'transfer_to_cache', slot: 3, targetId: 'Bloom Dust', value: 1 });
  });

  test('quantity selector "All" splits the whole stack on move', async ({ page }) => {
    await loadTown(page);
    await installActionCapture(page);
    await setState(page, inventoryState({
      party: [member(0, MEMBER_A, { componentInventory: [stack('Bloom Dust', 7)] })],
      expeditionCache: [],
    }));
    await openInventory(page);

    await page.locator('.inv-qty-select').selectOption('all');
    await page.locator('.inv-bag .inv-to-cache-btn').first().click();

    const actions = await getCaptured(page);
    expect(actions).toContainEqual({ type: 'transfer_to_cache', slot: 0, targetId: 'Bloom Dust', value: 7 });
  });

  test('moving from cache emits transfer_from_cache to the selected member', async ({ page }) => {
    await loadTown(page);
    await installActionCapture(page);
    await setState(page, inventoryState({
      party: [member(0, MEMBER_A, { componentInventory: [] })],
      expeditionCache: [stack('Ash Resin', 4)],
    }));
    await openInventory(page);

    await page.locator('.inv-cache .inv-to-bag-btn').first().click();

    const actions = await getCaptured(page);
    expect(actions).toContainEqual({ type: 'transfer_from_cache', slot: 0, targetId: 'Ash Resin', value: 1 });
  });

  test('equippable bag item shows Equip and emits equip_item for the resolved slot', async ({ page }) => {
    await loadTown(page);
    await installActionCapture(page);
    await setState(page, inventoryState({
      party: [member(0, MEMBER_A, {
        componentInventory: [stack('Iron Sword', 1, { type: 'weapon', equipSlot: 'mainHand' })],
      })],
      expeditionCache: [],
    }));
    await openInventory(page);

    await expect(page.locator('.inv-bag .inv-equip-btn')).toBeVisible();
    await page.locator('.inv-bag .inv-equip-btn').click();

    const actions = await getCaptured(page);
    expect(actions).toContainEqual({
      type: 'equip_item',
      targetId: MEMBER_A,
      itemId: 'Iron Sword',
      equipSlot: 'mainHand',
    });
  });

  test('renders the town storage grid with its stacks while in town', async ({ page }) => {
    await loadTown(page);
    await setState(page, inventoryState({
      party: [member(0, MEMBER_A, { componentInventory: [stack('Bloom Dust', 5)] })],
      expeditionCache: [],
      townStorage: [stack('Bone Shard', 250), stack('Blood Vial', 40)],
    }));
    await openInventory(page);

    const storage = page.locator('.inv-storage');
    await expect(storage).toBeVisible();
    await expect(storage.locator('.inv-stack:not(.inv-stack-empty)')).toHaveCount(2);
    await expect(storage).toContainText('Bone Shard');
    await expect(storage.locator('.inv-fill')).toContainText('2 stacks');
  });

  test('moving a stack to town storage emits transfer_to_town_storage for the selected slot', async ({ page }) => {
    await loadTown(page);
    await installActionCapture(page);
    await setState(page, inventoryState({
      party: [member(2, MEMBER_A, { componentInventory: [stack('Bloom Dust', 5)] })],
      expeditionCache: [],
      townStorage: [],
    }));
    await openInventory(page);

    await page.locator('.inv-bag .inv-to-storage-btn').first().click();

    const actions = await getCaptured(page);
    expect(actions).toContainEqual({ type: 'transfer_to_town_storage', slot: 2, targetId: 'Bloom Dust', value: 1 });
  });

  test('moving from town storage emits transfer_from_town_storage to the selected member', async ({ page }) => {
    await loadTown(page);
    await installActionCapture(page);
    await setState(page, inventoryState({
      party: [member(0, MEMBER_A, { componentInventory: [] })],
      expeditionCache: [],
      townStorage: [stack('Bone Shard', 250)],
    }));
    await openInventory(page);

    await page.locator('.inv-storage .inv-from-storage-btn').first().click();

    const actions = await getCaptured(page);
    expect(actions).toContainEqual({ type: 'transfer_from_town_storage', slot: 0, targetId: 'Bone Shard', value: 1 });
  });

  test('switching member tab shows that member bag and routes transfers to their slot', async ({ page }) => {
    await loadTown(page);
    await installActionCapture(page);
    await setState(page, inventoryState({
      party: [
        member(0, MEMBER_A, { name: 'Vesper', componentInventory: [stack('Bloom Dust', 5)] }),
        member(1, MEMBER_B, { name: 'Mira', componentInventory: [stack('Ash Resin', 3)] }),
      ],
      expeditionCache: [],
    }));
    await openInventory(page);

    // Default selection is the first member.
    await expect(page.locator('.inv-bag')).toContainText('Bloom Dust');

    // Switch to the second member.
    await page.locator('.inv-member-tab', { hasText: 'Mira' }).click();
    await expect(page.locator('.inv-bag')).toContainText('Ash Resin');

    await page.locator('.inv-bag .inv-to-cache-btn').first().click();
    const actions = await getCaptured(page);
    expect(actions).toContainEqual({ type: 'transfer_to_cache', slot: 1, targetId: 'Ash Resin', value: 1 });
  });
});
