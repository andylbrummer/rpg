import { describe, it, expect } from 'vitest';
import { intentToAction, type UiIntent } from './uiIntent';
import type { PlayerAction } from '$shared/types/protocol.gen';

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

  it('maps a transfer-to-town-storage intent to transfer_to_town_storage', () => {
    expect(
      intentToAction({ kind: 'transferToTownStorage', slot: 1, itemId: 'bone_shard', count: 9 })
    ).toEqual({ type: 'transfer_to_town_storage', slot: 1, targetId: 'bone_shard', value: 9 });
  });

  it('maps a transfer-from-town-storage intent to transfer_from_town_storage', () => {
    expect(
      intentToAction({ kind: 'transferFromTownStorage', slot: 1, itemId: 'bone_shard', count: 2 })
    ).toEqual({ type: 'transfer_from_town_storage', slot: 1, targetId: 'bone_shard', value: 2 });
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

  it('maps a searchSecrets intent to search_secrets', () => {
    expect(intentToAction({ kind: 'searchSecrets' })).toEqual({ type: 'search_secrets' });
  });

  it('maps a breakWall intent to break_wall carrying the secret/wall targetId', () => {
    expect(intentToAction({ kind: 'breakWall', targetId: 'secret-7' })).toEqual({
      type: 'break_wall',
      targetId: 'secret-7',
    });
  });
});

/**
 * Protocol dispatch-completeness guard.
 *
 * The class of bug this catches: a player command exists server-side and in the
 * wire protocol, yet no client dispatch path reaches it, leaving the feature
 * unreachable from the UI (the original search_secrets / break_wall gap).
 *
 * DISPATCH classifies every PlayerAction wire name by how the client reaches it.
 * Because it is typed `Record<PlayerAction['type'], DispatchPath>`, adding or
 * removing a protocol action is a compile error until it is classified here —
 * no protocol action can silently lack a dispatch decision. The runtime tests
 * then verify the classification is honest: every action marked 'intent' is
 * actually produced by intentToAction, and intentToAction produces nothing else.
 *
 *  - 'intent'  — reached through intentToAction (the typed-intent adapter).
 *  - 'direct'  — emitted via sendAction({ type }) from App.svelte (movement,
 *                dungeon/town transitions, single-button utilities).
 *  - 'server'  — not player-UI-initiated by design: server-authoritative
 *                resolution, engine-side mission/faction lifecycle, or debug /
 *                test-harness inputs.
 */
describe('protocol dispatch completeness', () => {
  type DispatchPath = 'intent' | 'direct' | 'server';

  const DISPATCH: Record<PlayerAction['type'], DispatchPath> = {
    move_forward: 'direct',
    move_back: 'direct',
    strafe_left: 'direct',
    strafe_right: 'direct',
    turn_left: 'direct',
    turn_right: 'direct',
    cancel: 'direct',
    enter_combat: 'direct',
    enter_dungeon: 'direct',
    combat_action: 'intent',
    use_consumable: 'intent',
    flee_combat: 'intent',
    rest: 'intent',
    return_to_town: 'direct',
    save_game: 'direct',
    pickup_loot: 'direct',
    search_secrets: 'intent',
    break_wall: 'intent',
    reset_game: 'direct',
    swap_row: 'direct',
    tavern_recruit: 'intent',
    swap_active_bench: 'intent',
    dismiss_character: 'intent',
    mission_accept: 'intent',
    vendor_purchase: 'intent',
    travel: 'direct',
    resolve_travel_encounter: 'direct',
    set_reputation: 'server',
    complete_mission: 'server',
    fail_mission: 'server',
    abandon_mission: 'server',
    dialogue_choice: 'server',
    encounter_choice: 'server',
    branch_choose: 'intent',
    accuse_faction: 'server',
    read_archive: 'intent',
    transfer_to_cache: 'intent',
    transfer_from_cache: 'intent',
    transfer_to_town_storage: 'intent',
    transfer_from_town_storage: 'intent',
    downtime_action: 'intent',
    resurrect_character: 'intent',
    wildcard_alliance: 'intent',
    rumor_verify: 'intent',
    equip_item: 'intent',
    unequip_item: 'intent',
    pay_tithe: 'intent',
  };

  // One representative sample per UiIntent kind. Keeping this exhaustive is what
  // lets the test compute the full set of wire names intentToAction can produce.
  const ALL_INTENT_SAMPLES: UiIntent[] = [
    { kind: 'combatAbility', actorId: 'a', abilityId: 'b' },
    { kind: 'combatAction', actorId: 'a', action: 'Attack' },
    { kind: 'useConsumable', actorId: 'a', itemId: 'i' },
    { kind: 'flee' },
    { kind: 'downtime', memberId: '00000000-0000-0000-0000-000000000000', action: 'rest' },
    { kind: 'wildcardAlliance', choice: 'accept' },
    { kind: 'resurrect', characterId: 'c' },
    { kind: 'payTithe' },
    { kind: 'readArchive', archiveId: 'r' },
    { kind: 'verifyRumor', rumorId: 'r' },
    { kind: 'rest' },
    { kind: 'branchChoose', characterId: 'c', branch: 'b' },
    { kind: 'transferToCache', slot: 0, itemId: 'i', count: 1 },
    { kind: 'transferFromCache', slot: 0, itemId: 'i', count: 1 },
    { kind: 'transferToTownStorage', slot: 0, itemId: 'i', count: 1 },
    { kind: 'transferFromTownStorage', slot: 0, itemId: 'i', count: 1 },
    { kind: 'tavernRecruit', recruitId: 'r' },
    { kind: 'swapActiveBench', activeSlot: 0 },
    { kind: 'dismissCharacter', characterId: 'c' },
    { kind: 'missionAccept', missionId: 'm' },
    { kind: 'vendorPurchase', itemId: 'i' },
    { kind: 'equipItem', characterId: 'c', itemId: 'i', slot: 's' },
    { kind: 'unequipItem', characterId: 'c', slot: 's' },
    { kind: 'searchSecrets' },
    { kind: 'breakWall', targetId: 't' },
  ];

  const intentWireNames = new Set<string>(ALL_INTENT_SAMPLES.map((i) => intentToAction(i).type));

  it('every protocol action classified as intent-dispatched is produced by intentToAction', () => {
    const declaredIntent = (Object.keys(DISPATCH) as PlayerAction['type'][]).filter(
      (a) => DISPATCH[a] === 'intent',
    );
    const missing = declaredIntent.filter((a) => !intentWireNames.has(a));
    expect(missing).toEqual([]);
  });

  it('intentToAction produces only wire names classified as intent-dispatched (no orphans)', () => {
    const orphans = [...intentWireNames].filter(
      (name) => DISPATCH[name as PlayerAction['type']] !== 'intent',
    );
    expect(orphans).toEqual([]);
  });

  it('search_secrets and break_wall are reached through the typed-intent path', () => {
    expect(DISPATCH.search_secrets).toBe('intent');
    expect(DISPATCH.break_wall).toBe('intent');
    expect(intentWireNames.has('search_secrets')).toBe(true);
    expect(intentWireNames.has('break_wall')).toBe(true);
  });
});
