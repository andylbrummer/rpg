import type { GameState, PartyMember } from '$shared/types/game';

export function selectPartyMembers(state: GameState): PartyMember[] {
  return state.party ?? [];
}

export function selectExpeditionCache(state: GameState) {
  return state.expeditionCache ?? [];
}

export function selectActiveParty(state: GameState): PartyMember[] {
  return (state.party ?? []).filter((m) => m.alive);
}
