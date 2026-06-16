import { test, expect } from '@playwright/test';
import { loadTown } from './townHarness';

const MEMBER_ID = '22222222-2222-2222-2222-222222222222';

function baseMember(overrides: Record<string, unknown> = {}) {
  return {
    slot: 0,
    id: MEMBER_ID,
    name: 'Vesper',
    classId: 'inkblood',
    className: 'Inkblood',
    color: '#7A4B6B',
    level: 2,
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
    ...overrides,
  };
}

function baseState(extra: Record<string, unknown>) {
  return {
    type: 'state',
    mode: 'Menu',
    player: { x: 0, y: 0, facing: 'North' },
    tiles: [],
    explored: [],
    hasDungeon: false,
    party: [baseMember()],
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

test.describe('Downtime action UI', () => {
  test('selecting an action shows its description, cost and faction-rep hint', async ({ page }) => {
    await loadTown(page);

    await page.evaluate((state) => {
      (window as any).__rpc_enableTestHooks();
      (window as any).gameStore.__testSetState(state);
    }, baseState({}));

    const select = page.locator('.downtime-select').first();
    await expect(select).toBeVisible();

    // No preview before a selection is made.
    await expect(page.locator('.downtime-preview')).toHaveCount(0);

    await select.selectOption('Network');

    const preview = page.locator('.downtime-preview');
    await expect(preview).toBeVisible();
    await expect(preview.locator('.downtime-desc')).toContainText('contacts');
    await expect(preview.locator('.downtime-tag.cost')).toContainText('1 downtime action');
    await expect(preview.locator('.downtime-tag.rep')).toContainText('Reputation +5');

    // Perform stays enabled once an action is chosen.
    await expect(page.locator('.downtime-perform')).toBeEnabled();
  });

  test('non-reputation action omits the faction-rep hint', async ({ page }) => {
    await loadTown(page);

    await page.evaluate((state) => {
      (window as any).__rpc_enableTestHooks();
      (window as any).gameStore.__testSetState(state);
    }, baseState({}));

    await page.locator('.downtime-select').first().selectOption('Rest');

    await expect(page.locator('.downtime-preview .downtime-desc')).toContainText('full HP');
    await expect(page.locator('.downtime-preview .downtime-tag.rep')).toHaveCount(0);
  });

  test('completed downtime shows the outcome message from the action log', async ({ page }) => {
    await loadTown(page);

    await page.evaluate((state) => {
      (window as any).__rpc_enableTestHooks();
      (window as any).gameStore.__testSetState(state);
    }, baseState({
      downtimeCompleted: [MEMBER_ID],
      actionLog: [
        {
          turn: 1,
          act: 1,
          category: 'downtime',
          type: 'network',
          payload: {
            characterId: MEMBER_ID,
            characterName: 'Vesper',
            action: 'Network',
            message: 'Networked with bureau. Reputation +5.',
          },
        },
      ],
    }));

    await expect(page.locator('.downtime-done')).toBeVisible();
    await expect(page.locator('.downtime-outcome')).toContainText('Networked with bureau');
    // Picker is hidden once the action is spent.
    await expect(page.locator('.downtime-select')).toHaveCount(0);
  });
});
