import { test, expect } from '@playwright/test';

test.describe('Branch Choice Modal', () => {
  test('modal appears when character awaits branch choice', async ({ page }) => {
    await page.goto('/app');
    await page.click('.title-btn.primary');

    await page.evaluate(() => {
      (window as any).__rpc_enableTestHooks();
      const gs = (window as any).gameStore;
      gs.__testSetState({
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
            name: 'TestHero',
            classId: 'bonewarden',
            className: 'Bonewarden',
            color: '#8B7355',
            level: 3,
            xp: 0,
            hp: 30,
            maxHp: 30,
            row: 0,
            alive: true,
            awaitingBranchChoice: true,
            availableBranches: ['bone_shaper', 'death_weaver'],
            classAbilities: [
              { id: 'bone_spear', name: 'Bone Spear', branch: 'bone_shaper' },
              { id: 'death_pact', name: 'Death Pact', branch: 'death_weaver' }
            ],
            stats: { strength: 4, dexterity: 3, constitution: 5, intelligence: 4, willpower: 4, maxHp: 30, speed: 10, accuracy: 10, evade: 10, power: 10 },
            equipment: { mainHand: null, offHand: null, armor: null, accessory1: null, accessory2: null },
            knownAbilities: ['bone_spear']
          }
        ],
        town: {
          currentTownId: 'the_reach',
          availableMissions: [],
          vendorStock: [],
          factionVendors: [],
          factionContacts: [],
          tavernRoster: [],
          viewedMissions: [],
          questLog: []
        },
        overworld: { currentNodeId: 'the_reach', nodes: [], routes: [], turns: 0 },
        reputation: {},
        partyGold: 500,
        partyInventory: [],
        actionLog: []
      });
    });

    await expect(page.locator('.branch-modal-overlay')).toBeVisible();
    await expect(page.locator('.branch-modal-title')).toContainText('TestHero');
    await expect(page.locator('.branch-name')).toHaveCount(2);
  });

  test('modal blocks town UI until resolved', async ({ page }) => {
    await page.goto('/app');
    await page.click('.title-btn.primary');

    await page.evaluate(() => {
      (window as any).__rpc_enableTestHooks();
      const gs = (window as any).gameStore;
      gs.__testSetState({
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
            name: 'TestHero',
            classId: 'bonewarden',
            className: 'Bonewarden',
            color: '#8B7355',
            level: 3,
            xp: 0,
            hp: 30,
            maxHp: 30,
            row: 0,
            alive: true,
            awaitingBranchChoice: true,
            availableBranches: ['bone_shaper'],
            classAbilities: [],
            stats: { strength: 4, dexterity: 3, constitution: 5, intelligence: 4, willpower: 4, maxHp: 30, speed: 10, accuracy: 10, evade: 10, power: 10 },
            equipment: { mainHand: null, offHand: null, armor: null, accessory1: null, accessory2: null },
            knownAbilities: ['bone_spear']
          }
        ],
        town: {
          currentTownId: 'the_reach',
          availableMissions: [],
          vendorStock: [],
          factionVendors: [],
          factionContacts: [],
          tavernRoster: [],
          viewedMissions: [],
          questLog: []
        },
        overworld: { currentNodeId: 'the_reach', nodes: [], routes: [], turns: 0 },
        reputation: {},
        partyGold: 500,
        partyInventory: [],
        actionLog: []
      });
    });

    await expect(page.locator('.branch-modal-overlay')).toBeVisible();

    // Attempting to click town UI through the overlay should fail
    const partyCard = page.locator('.character-card').first();
    await expect(partyCard).toBeVisible();
    await expect(partyCard.click({ timeout: 1000 })).rejects.toThrow();

    // Modal remains visible
    await expect(page.locator('.branch-modal-overlay')).toBeVisible();
  });

  test('modal appears at level 6 for specialization', async ({ page }) => {
    await page.goto('/app');
    await page.click('.title-btn.primary');

    await page.evaluate(() => {
      (window as any).__rpc_enableTestHooks();
      const gs = (window as any).gameStore;
      gs.__testSetState({
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
            name: 'TestHero',
            classId: 'bonewarden',
            className: 'Bonewarden',
            color: '#8B7355',
            level: 6,
            xp: 0,
            hp: 30,
            maxHp: 30,
            row: 0,
            alive: true,
            branchChoice: 'bone_shaper',
            awaitingBranchChoice: true,
            availableBranches: ['soul_warden', 'bone_colossus'],
            classAbilities: [
              { id: 'soul_warden_ult', name: 'Soul Warden Ult', branch: 'soul_warden' },
              { id: 'bone_colossus_ult', name: 'Bone Colossus Ult', branch: 'bone_colossus' }
            ],
            stats: { strength: 4, dexterity: 3, constitution: 5, intelligence: 4, willpower: 4, maxHp: 30, speed: 10, accuracy: 10, evade: 10, power: 10 },
            equipment: { mainHand: null, offHand: null, armor: null, accessory1: null, accessory2: null },
            knownAbilities: ['bone_spear']
          }
        ],
        town: {
          currentTownId: 'the_reach',
          availableMissions: [],
          vendorStock: [],
          factionVendors: [],
          factionContacts: [],
          tavernRoster: [],
          viewedMissions: [],
          questLog: []
        },
        overworld: { currentNodeId: 'the_reach', nodes: [], routes: [], turns: 0 },
        reputation: {},
        partyGold: 500,
        partyInventory: [],
        actionLog: []
      });
    });

    await expect(page.locator('.branch-modal-overlay')).toBeVisible();
    await expect(page.locator('.branch-modal-title')).toContainText('Specialize');
    await expect(page.locator('.branch-name')).toHaveCount(2);
  });
});
