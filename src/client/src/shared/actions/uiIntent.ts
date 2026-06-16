import type { PlayerAction } from '$shared/types/game';

/**
 * Typed UI intents emitted by presentation components.
 *
 * Components describe *what the user did* in domain terms and stay ignorant of
 * the wire protocol. The single adapter below (`intentToAction`) is the only
 * place that knows how a UI intent maps to a transport-level `PlayerAction`,
 * keeping protocol details out of the view layer and routing every action
 * through one dispatch path.
 */
export type UiIntent =
  | { kind: 'combatAbility'; actorId: string; abilityId: string; targetId?: string }
  | {
      kind: 'combatAction';
      actorId: string;
      action: 'Attack' | 'Defend' | 'Wait' | 'UseItem' | 'Flee';
      targetId?: string;
    }
  | { kind: 'useConsumable'; actorId: string; itemId: string; targetId?: string }
  | { kind: 'flee' }
  | { kind: 'downtime'; memberId: string; action: string }
  | { kind: 'wildcardAlliance'; choice: 'accept' | 'refuse' | 'ignore' }
  | { kind: 'resurrect'; characterId: string }
  | { kind: 'payTithe' }
  | { kind: 'readArchive'; archiveId: string }
  | { kind: 'verifyRumor'; rumorId: string }
  | { kind: 'rest' }
  | { kind: 'branchChoose'; characterId: string; branch: string }
  | { kind: 'transferToCache'; slot: number; itemId: string; count: number }
  | { kind: 'transferFromCache'; slot: number; itemId: string; count: number }
  | { kind: 'transferToTownStorage'; slot: number; itemId: string; count: number }
  | { kind: 'transferFromTownStorage'; slot: number; itemId: string; count: number }
  | { kind: 'tavernRecruit'; recruitId: string }
  | { kind: 'swapActiveBench'; activeSlot: number; benchCharacterId?: string }
  | { kind: 'dismissCharacter'; characterId: string }
  | { kind: 'missionAccept'; missionId: string }
  | { kind: 'vendorPurchase'; itemId: string }
  | { kind: 'equipItem'; characterId: string; itemId: string; slot: string }
  | { kind: 'unequipItem'; characterId: string; slot: string };

/**
 * Pure mapping from a typed UI intent to the protocol `PlayerAction`.
 * No side effects — dispatch happens via the adapter wired in the app shell.
 */
export function intentToAction(intent: UiIntent): PlayerAction {
  switch (intent.kind) {
    case 'combatAbility':
      return {
        type: 'combat_action',
        action: {
          actorId: intent.actorId,
          type: 'UseAbility',
          targetId: intent.targetId,
          abilityId: intent.abilityId,
        },
      };
    case 'combatAction':
      return {
        type: 'combat_action',
        action: { actorId: intent.actorId, type: intent.action, targetId: intent.targetId },
      };
    case 'useConsumable':
      return {
        type: 'use_consumable',
        action: {
          actorId: intent.actorId,
          type: 'UseItem',
          targetId: intent.targetId,
          itemId: intent.itemId,
        },
      };
    case 'flee':
      return { type: 'flee_combat' };
    case 'downtime':
      return { type: 'downtime_action', targetId: intent.memberId, downtimeAction: intent.action };
    case 'wildcardAlliance':
      return { type: 'wildcard_alliance', targetId: intent.choice };
    case 'resurrect':
      return { type: 'resurrect_character', targetId: intent.characterId };
    case 'payTithe':
      return { type: 'pay_tithe' };
    case 'readArchive':
      return { type: 'read_archive', targetId: intent.archiveId };
    case 'verifyRumor':
      return { type: 'rumor_verify', targetId: intent.rumorId, source: 'Firsthand' };
    case 'rest':
      return { type: 'rest' };
    case 'branchChoose':
      return { type: 'branch_choose', targetId: intent.characterId, branch: intent.branch };
    case 'transferToCache':
      return {
        type: 'transfer_to_cache',
        slot: intent.slot,
        targetId: intent.itemId,
        value: intent.count,
      };
    case 'transferFromCache':
      return {
        type: 'transfer_from_cache',
        slot: intent.slot,
        targetId: intent.itemId,
        value: intent.count,
      };
    case 'transferToTownStorage':
      return {
        type: 'transfer_to_town_storage',
        slot: intent.slot,
        targetId: intent.itemId,
        value: intent.count,
      };
    case 'transferFromTownStorage':
      return {
        type: 'transfer_from_town_storage',
        slot: intent.slot,
        targetId: intent.itemId,
        value: intent.count,
      };
    case 'tavernRecruit':
      return { type: 'tavern_recruit', targetId: intent.recruitId };
    case 'swapActiveBench':
      return {
        type: 'swap_active_bench',
        slot: intent.activeSlot,
        targetId: intent.benchCharacterId,
      };
    case 'dismissCharacter':
      return { type: 'dismiss_character', targetId: intent.characterId };
    case 'missionAccept':
      return { type: 'mission_accept', targetId: intent.missionId };
    case 'vendorPurchase':
      return { type: 'vendor_purchase', targetId: intent.itemId };
    case 'equipItem':
      return {
        type: 'equip_item',
        targetId: intent.characterId,
        itemId: intent.itemId,
        equipSlot: intent.slot,
      };
    case 'unequipItem':
      return { type: 'unequip_item', targetId: intent.characterId, equipSlot: intent.slot };
  }
}
