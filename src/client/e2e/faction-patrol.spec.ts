import { test, expect } from '@playwright/test';
import { loadTown } from './townHarness';

function stateWithFactionPresence() {
  return {
    type: 'state',
    mode: 'Menu',
    player: { x: 0, y: 0, facing: 'North' },
    tiles: [],
    explored: [],
    hasDungeon: false,
    party: [
      {
        slot: 0,
        id: '11111111-1111-1111-1111-111111111111',
        name: 'Scout',
        classId: 'marcher',
        className: 'Marcher',
        color: '#5A7A5A',
        level: 1,
        xp: 0,
        hp: 20,
        maxHp: 20,
        row: 0,
        alive: true,
        awaitingBranchChoice: false,
        classAbilities: [],
        stats: { strength: 4, dexterity: 4, constitution: 4, intelligence: 4, willpower: 4, maxHp: 20, speed: 8, accuracy: 10, evade: 10, power: 10 },
        equipment: { mainHand: null, offHand: null, armor: null, accessory1: null, accessory2: null },
        knownAbilities: [],
      },
    ],
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
    overworld: {
      currentNodeId: 'the_reach',
      nodes: [
        { id: 'the_reach', name: 'The Reach', type: 'town', factionPresence: ['bureau'] },
        { id: 'broken_engine', name: 'Broken Engine', type: 'dungeon', dungeonTemplateId: 'broken_engine', factionPresence: ['convocation'] },
      ],
      routes: [
        { from: 'the_reach', to: 'broken_engine', distance: 2, dangerRating: 3, terrain: 'caves', status: 'Open' },
      ],
      turns: 0,
    },
    reputation: {},
    partyGold: 100,
    partyInventory: [],
    actionLog: [],
  };
}

test.describe('Faction patrol on overworld', () => {
  test('routes with faction presence render animated patrol sprites', async ({ page }) => {
    await loadTown(page);

    await page.evaluate((state) => {
      (window as any).__rpc_enableTestHooks();
      (window as any).gameStore.__testSetState(state);
    }, stateWithFactionPresence());

    await page.getByText('Overworld Map').click();
    await expect(page.locator('.map-panel')).toBeVisible();

    // The route touches bureau (the_reach) and convocation (broken_engine) presence,
    // so both faction patrol sprites should ride the route.
    const patrols = page.locator('.patrol-sprite');
    await expect(patrols).toHaveCount(2);
    await expect(page.locator('.patrol-sprite[data-faction="bureau"]')).toHaveCount(1);
    await expect(page.locator('.patrol-sprite[data-faction="convocation"]')).toHaveCount(1);

    // Each patrol sprite animates along the route.
    await expect(page.locator('.patrol-sprite animateMotion').first()).toBeAttached();
  });

  test('no faction presence means no patrol sprites', async ({ page }) => {
    await loadTown(page);

    const state = stateWithFactionPresence();
    state.overworld.nodes.forEach((n: any) => { n.factionPresence = []; });

    await page.evaluate((s) => {
      (window as any).__rpc_enableTestHooks();
      (window as any).gameStore.__testSetState(s);
    }, state);

    await page.getByText('Overworld Map').click();
    await expect(page.locator('.map-panel')).toBeVisible();
    await expect(page.locator('.patrol-sprite')).toHaveCount(0);
  });
});
