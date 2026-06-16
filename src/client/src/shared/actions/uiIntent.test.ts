import { describe, it, expect } from 'vitest';
import { intentToAction } from './uiIntent';

describe('intentToAction', () => {
  it('maps a combat ability intent to a combat_action UseAbility PlayerAction', () => {
    expect(
      intentToAction({
        kind: 'combatAbility',
        actorId: 'hero-1',
        abilityId: 'fireball',
        targetId: 'goblin-2',
      })
    ).toEqual({
      type: 'combat_action',
      action: {
        actorId: 'hero-1',
        type: 'UseAbility',
        targetId: 'goblin-2',
        abilityId: 'fireball',
      },
    });
  });

  it('omits targetId for a self-targeted combat ability', () => {
    expect(
      intentToAction({ kind: 'combatAbility', actorId: 'hero-1', abilityId: 'heal' })
    ).toEqual({
      type: 'combat_action',
      action: { actorId: 'hero-1', type: 'UseAbility', targetId: undefined, abilityId: 'heal' },
    });
  });

  it('maps a basic combat action intent (Attack) to combat_action', () => {
    expect(
      intentToAction({ kind: 'combatAction', actorId: 'hero-1', action: 'Attack', targetId: 'goblin-2' })
    ).toEqual({
      type: 'combat_action',
      action: { actorId: 'hero-1', type: 'Attack', targetId: 'goblin-2' },
    });
  });

  it('maps a defend intent with no target to combat_action with undefined targetId', () => {
    expect(intentToAction({ kind: 'combatAction', actorId: 'hero-1', action: 'Defend' })).toEqual({
      type: 'combat_action',
      action: { actorId: 'hero-1', type: 'Defend', targetId: undefined },
    });
  });

  it('maps a useConsumable intent to use_consumable with a UseItem CombatAction', () => {
    expect(
      intentToAction({ kind: 'useConsumable', actorId: 'hero-1', itemId: 'small_salve', targetId: 'hero-2' })
    ).toEqual({
      type: 'use_consumable',
      action: { actorId: 'hero-1', type: 'UseItem', targetId: 'hero-2', itemId: 'small_salve' },
    });
  });

  it('omits targetId for a self-targeted useConsumable', () => {
    expect(intentToAction({ kind: 'useConsumable', actorId: 'hero-1', itemId: 'small_salve' })).toEqual({
      type: 'use_consumable',
      action: { actorId: 'hero-1', type: 'UseItem', targetId: undefined, itemId: 'small_salve' },
    });
  });

  it('maps a flee intent to flee_combat', () => {
    expect(intentToAction({ kind: 'flee' })).toEqual({ type: 'flee_combat' });
  });

  it('maps an equipItem intent to equip_item', () => {
    expect(
      intentToAction({ kind: 'equipItem', characterId: 'hero-1', itemId: 'rusty_sword', slot: 'mainHand' })
    ).toEqual({ type: 'equip_item', targetId: 'hero-1', itemId: 'rusty_sword', equipSlot: 'mainHand' });
  });

  it('maps an unequipItem intent to unequip_item', () => {
    expect(
      intentToAction({ kind: 'unequipItem', characterId: 'hero-1', slot: 'mainHand' })
    ).toEqual({ type: 'unequip_item', targetId: 'hero-1', equipSlot: 'mainHand' });
  });

  it('maps a downtime intent to downtime_action', () => {
    expect(
      intentToAction({ kind: 'downtime', memberId: 'hero-3', action: 'train' })
    ).toEqual({ type: 'downtime_action', targetId: 'hero-3', downtimeAction: 'train' });
  });

  it('maps a wildcard alliance intent to wildcard_alliance', () => {
    expect(intentToAction({ kind: 'wildcardAlliance', choice: 'accept' })).toEqual({
      type: 'wildcard_alliance',
      targetId: 'accept',
    });
  });

  it('maps a resurrect intent to resurrect_character', () => {
    expect(intentToAction({ kind: 'resurrect', characterId: 'hero-4' })).toEqual({
      type: 'resurrect_character',
      targetId: 'hero-4',
    });
  });

  it('maps a payTithe intent to pay_tithe', () => {
    expect(intentToAction({ kind: 'payTithe' })).toEqual({ type: 'pay_tithe' });
  });

  it('maps a verify rumor intent to rumor_verify with Firsthand source', () => {
    expect(intentToAction({ kind: 'verifyRumor', rumorId: 'rumor-9' })).toEqual({
      type: 'rumor_verify',
      targetId: 'rumor-9',
      source: 'Firsthand',
    });
  });

  it('maps a rest intent to rest', () => {
    expect(intentToAction({ kind: 'rest' })).toEqual({ type: 'rest' });
  });

  it('maps a branch choose intent to branch_choose', () => {
    expect(
      intentToAction({ kind: 'branchChoose', characterId: 'hero-5', branch: 'arcane' })
    ).toEqual({ type: 'branch_choose', targetId: 'hero-5', branch: 'arcane' });
  });

  it('maps a transfer-to-cache intent to transfer_to_cache', () => {
    expect(
      intentToAction({ kind: 'transferToCache', slot: 2, itemId: 'potion', count: 3 })
    ).toEqual({ type: 'transfer_to_cache', slot: 2, targetId: 'potion', value: 3 });
  });

  it('maps a transfer-from-cache intent to transfer_from_cache', () => {
    expect(
      intentToAction({ kind: 'transferFromCache', slot: 2, itemId: 'potion', count: 1 })
    ).toEqual({ type: 'transfer_from_cache', slot: 2, targetId: 'potion', value: 1 });
  });

  it('maps a tavern recruit intent to tavern_recruit', () => {
    expect(intentToAction({ kind: 'tavernRecruit', recruitId: 'r-1' })).toEqual({
      type: 'tavern_recruit',
      targetId: 'r-1',
    });
  });

  it('maps a mission accept intent to mission_accept', () => {
    expect(intentToAction({ kind: 'missionAccept', missionId: 'm-1' })).toEqual({
      type: 'mission_accept',
      targetId: 'm-1',
    });
  });

  it('maps a vendor purchase intent to vendor_purchase', () => {
    expect(intentToAction({ kind: 'vendorPurchase', itemId: 'sword' })).toEqual({
      type: 'vendor_purchase',
      targetId: 'sword',
    });
  });

  it('maps a swapActiveBench intent to swap_active_bench', () => {
    expect(
      intentToAction({ kind: 'swapActiveBench', activeSlot: 2, benchCharacterId: 'c-9' }),
    ).toEqual({
      type: 'swap_active_bench',
      slot: 2,
      targetId: 'c-9',
    });
  });

  it('maps a swapActiveBench bench-out intent (no bench character) to swap_active_bench', () => {
    expect(intentToAction({ kind: 'swapActiveBench', activeSlot: 3 })).toEqual({
      type: 'swap_active_bench',
      slot: 3,
      targetId: undefined,
    });
  });

  it('maps a dismissCharacter intent to dismiss_character', () => {
    expect(intentToAction({ kind: 'dismissCharacter', characterId: 'c-1' })).toEqual({
      type: 'dismiss_character',
      targetId: 'c-1',
    });
  });
});
