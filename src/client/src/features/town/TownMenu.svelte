<script lang="ts">
  import type { GameState, PartyMember } from '$shared/types/game';
  import { sendAction } from '$shared/stores/gameStore';
  import CharacterSheet from '$features/party/CharacterSheet.svelte';
  import OverworldMap from '$features/overworld/OverworldMap.svelte';
  import TownActionsPanel from './TownActionsPanel.svelte';
  import PartyPanel from './PartyPanel.svelte';
  import TownServicesPanel from './TownServicesPanel.svelte';

  interface Props {
    gameState: GameState | null;
    onEnterDungeon: (type: string) => void;
    onSave: () => void;
    onReset: () => void;
    onSwapRow: (slot: number) => void;
    onTavernRecruit: (id: string) => void;
    onMissionAccept: (id: string) => void;
    onVendorPurchase: (id: string) => void;
    onTravel: (targetId: string) => void;
  }

  let {
    gameState,
    onEnterDungeon,
    onSave,
    onReset,
    onSwapRow,
    onTavernRecruit,
    onMissionAccept,
    onVendorPurchase,
    onTravel
  }: Props = $props();

  let sheetMember = $state<PartyMember | null>(null);
  let showMap = $state(false);
  let currentTab = $state<'party' | 'tavern' | 'missions' | 'market' | 'dungeons' | 'clerk'>('party');

  const tabs = [
    { id: 'party' as const, label: 'Party', icon: 'M12 2L2 7l10 5 10-5-10-5zM2 17l10 5 10-5M2 12l10 5 10-5' },
    { id: 'tavern' as const, label: 'Tavern', icon: 'M7 2h10v4H7zM5 6h14v2H5zm-2 4h18v10H3zm4 2v6m10-6v6' },
    { id: 'missions' as const, label: 'Missions', icon: 'M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8zM14 2v6h6M16 13H8m8 4H8' },
    { id: 'market' as const, label: 'Market', icon: 'M3 3h2l.4 2M7 13h10l4-8H5.4M7 13L5.4 5M7 13l-2.3 2.3c-.4.4-.6 1-.3 1.5.3.5.8.8 1.4.8h12.4' },
    { id: 'dungeons' as const, label: 'Dungeons', icon: 'M12 22s-8-4.5-8-11.8A8 8 0 0 1 12 2a8 8 0 0 1 8 8.2c0 7.3-8 11.8-8 11.8zM12 6v10' },
    { id: 'clerk' as const, label: 'Clerk', icon: 'M12 2a5 5 0 0 0-5 5v2a2 2 0 0 0-2 2v7a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2v-7a2 2 0 0 0-2-2V7a5 5 0 0 0-5-5zm-3 5a3 3 0 0 1 6 0v2H9V7z' },
  ];

  const partyMembers = $derived(gameState?.party ?? []);

  const pendingBranchMembers = $derived(
    partyMembers.filter(m => m.awaitingBranchChoice && (m.availableBranches?.length ?? 0) > 0)
  );

  function formatBranchName(branchId: string): string {
    return branchId
      .split('_')
      .map(w => w.charAt(0).toUpperCase() + w.slice(1))
      .join(' ');
  }

  function getBranchAbilities(member: PartyMember, branch: string) {
    return (member.classAbilities ?? []).filter(a => a.branch === branch);
  }

  function chooseBranch(characterId: string, branch: string) {
    sendAction({ type: 'branch_choose', targetId: characterId, branch });
  }
</script>

<div class="town-menu">
  <div class="town-atmosphere">
    <div class="stars"></div>
    <div class="skyline">
      <div class="building-silhouette b1"></div>
      <div class="building-silhouette b2"></div>
      <div class="building-silhouette b3"></div>
      <div class="building-silhouette b4"></div>
      <div class="building-silhouette b5"></div>
      <div class="building-silhouette b6"></div>
      <div class="building-silhouette b7"></div>
      <div class="building-silhouette b8"></div>
    </div>
    <div class="fog"></div>
  </div>

  <nav class="town-nav" aria-label="Town menu">
    <div class="town-nav-beam">
      {#each tabs as tab}
        <button
          type="button"
          class="town-nav-btn"
          class:active={currentTab === tab.id}
          onclick={() => currentTab = tab.id}
          aria-current={currentTab === tab.id ? 'page' : undefined}
        >
          <svg class="town-nav-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <path d={tab.icon} />
          </svg>
          <span class="town-nav-label">{tab.label}</span>
        </button>
      {/each}
    </div>
  </nav>

  <div class="town-body">
    {#if currentTab === 'party'}
      <div class="tab-panel">
        <PartyPanel gameState={gameState} {onSwapRow} onViewSheet={(m) => sheetMember = m} />
        <div class="town-services">
          <TownServicesPanel gameState={gameState} {onTavernRecruit} {onMissionAccept} {onVendorPurchase} />
        </div>
      </div>

      <TownActionsPanel
        {onEnterDungeon}
        {onSave}
        {onReset}
        onShowMap={() => showMap = true}
      />
    {:else if currentTab === 'dungeons'}
      <div class="tab-panel">
        <TownActionsPanel
          {onEnterDungeon}
          {onSave}
          {onReset}
          onShowMap={() => showMap = true}
        />
      </div>
    {:else}
      <div class="tab-panel">
        <TownServicesPanel gameState={gameState} {onTavernRecruit} {onMissionAccept} {onVendorPurchase} />
      </div>
    {/if}
  </div>

  {#if pendingBranchMembers.length > 0}
    {@const member = pendingBranchMembers[0]}
    {@const isLevel6 = member.level >= 6 && member.branchChoice != null}
    <div class="branch-modal-overlay" role="dialog" aria-label="Choose branch">
      <div class="branch-modal-card">
        <h2 class="branch-modal-title">
          {member.name} — {isLevel6 ? 'Specialize' : 'Choose Path'}
        </h2>
        <p class="branch-modal-subtitle">
          Level {isLevel6 ? 6 : 3} {member.className}
          {#if isLevel6 && member.branchChoice}
            <span class="branch-parent">({formatBranchName(member.branchChoice)} path)</span>
          {/if}
        </p>
        <div class="branch-options">
          {#each (member.availableBranches ?? []) as branch}
            <div class="branch-option">
              <h3 class="branch-name">{formatBranchName(branch)}</h3>
              {#if !isLevel6 && member.branchWarnings?.includes(branch)}
                <p class="branch-warning">Warning: This path contains a faction-gated branch at level 6.</p>
              {/if}
              <div class="branch-abilities">
                {#each getBranchAbilities(member, branch) as ability}
                  <span class="branch-ability">{ability.name}</span>
                {:else}
                  <span class="branch-ability-none">No preview available</span>
                {/each}
              </div>
              <button
                type="button"
                class="branch-choose-btn"
                onclick={() => chooseBranch(member.id, branch)}
              >
                Choose
              </button>
            </div>
          {:else}
            <p class="branch-empty">No branches available for this class.</p>
          {/each}
        </div>
      </div>
    </div>
  {/if}

  {#if sheetMember}
    <CharacterSheet
      member={sheetMember}
      onClose={() => sheetMember = null}
      onSwapRow={onSwapRow}
      onTransferToCache={(itemId, count) => sendAction({ type: 'transfer_to_cache', slot: sheetMember!.slot, targetId: itemId, value: count })}
      onTransferFromCache={(itemId, count) => sendAction({ type: 'transfer_from_cache', slot: sheetMember!.slot, targetId: itemId, value: count })}
      expeditionCache={gameState?.expeditionCache ?? []}
    />
  {/if}

  {#if showMap && gameState?.overworld}
    <OverworldMap
      overworld={gameState.overworld}
      onTravel={onTravel}
      onClose={() => showMap = false}
    />
  {/if}
</div>

<style>
  .town-menu {
    display: flex;
    flex-direction: column;
    width: 100%;
    height: 100%;
    padding: 1rem;
    gap: 1rem;
    box-sizing: border-box;
    overflow: hidden;
    position: relative;
  }

  .town-atmosphere {
    position: absolute;
    inset: 0;
    pointer-events: none;
    z-index: 0;
    overflow: hidden;
  }

  .stars {
    position: absolute;
    inset: 0;
    background-image:
      radial-gradient(2px 2px at 10% 20%, rgba(255, 255, 230, 0.9) 50%, transparent 50%),
      radial-gradient(1.5px 1.5px at 20% 40%, rgba(255, 255, 255, 0.7) 50%, transparent 50%),
      radial-gradient(2px 2px at 30% 15%, rgba(255, 250, 220, 0.85) 50%, transparent 50%),
      radial-gradient(1.5px 1.5px at 45% 35%, rgba(255, 255, 255, 0.6) 50%, transparent 50%),
      radial-gradient(2px 2px at 55% 10%, rgba(255, 255, 230, 0.9) 50%, transparent 50%),
      radial-gradient(1.5px 1.5px at 65% 30%, rgba(255, 255, 255, 0.7) 50%, transparent 50%),
      radial-gradient(2px 2px at 75% 18%, rgba(255, 250, 220, 0.85) 50%, transparent 50%),
      radial-gradient(1.5px 1.5px at 85% 40%, rgba(255, 255, 255, 0.5) 50%, transparent 50%),
      radial-gradient(2px 2px at 90% 15%, rgba(255, 255, 230, 0.8) 50%, transparent 50%),
      radial-gradient(1.5px 1.5px at 15% 55%, rgba(255, 255, 255, 0.5) 50%, transparent 50%),
      radial-gradient(2px 2px at 40% 60%, rgba(255, 250, 220, 0.7) 50%, transparent 50%),
      radial-gradient(1.5px 1.5px at 60% 55%, rgba(255, 255, 255, 0.6) 50%, transparent 50%),
      radial-gradient(2px 2px at 80% 58%, rgba(255, 255, 230, 0.5) 50%, transparent 50%),
      radial-gradient(1.5px 1.5px at 5% 35%, rgba(255, 255, 255, 0.6) 50%, transparent 50%),
      radial-gradient(2px 2px at 95% 50%, rgba(255, 250, 220, 0.7) 50%, transparent 50%),
      radial-gradient(1.5px 1.5px at 50% 45%, rgba(255, 255, 255, 0.5) 50%, transparent 50%),
      radial-gradient(2px 2px at 35% 8%, rgba(255, 255, 230, 0.8) 50%, transparent 50%),
      radial-gradient(1.5px 1.5px at 70% 45%, rgba(255, 255, 255, 0.6) 50%, transparent 50%);
  }

  .skyline {
    position: absolute;
    bottom: 0;
    left: 0;
    right: 0;
    height: 40%;
    display: flex;
    align-items: flex-end;
    justify-content: space-around;
    padding: 0 3%;
    opacity: 0.7;
  }

  .building-silhouette {
    background: linear-gradient(180deg, #151820 0%, #0c0e14 60%, #07080c 100%);
    border-radius: 2px 2px 0 0;
    position: relative;
    box-shadow: inset 0 0 8px rgba(0, 0, 0, 0.5);
  }

  .building-silhouette::before {
    content: '';
    position: absolute;
    top: -6px;
    left: 50%;
    transform: translateX(-50%);
    width: 0;
    height: 0;
    border-left: 6px solid transparent;
    border-right: 6px solid transparent;
    border-bottom: 6px solid #151820;
  }

  /* Building windows — warm amber lights */
  .building-silhouette::after {
    content: '';
    position: absolute;
    inset: 8px 4px;
    background-image:
      radial-gradient(1.5px 2px at 25% 20%, rgba(212, 168, 75, 0.5) 50%, transparent 50%),
      radial-gradient(1.5px 2px at 75% 35%, rgba(212, 168, 75, 0.3) 50%, transparent 50%),
      radial-gradient(1.5px 2px at 50% 55%, rgba(212, 168, 75, 0.4) 50%, transparent 50%),
      radial-gradient(1.5px 2px at 30% 75%, rgba(212, 168, 75, 0.35) 50%, transparent 50%),
      radial-gradient(1.5px 2px at 70% 80%, rgba(212, 168, 75, 0.25) 50%, transparent 50%);
    opacity: 0.6;
  }

  .building-silhouette.b1 { width: 8%; height: 45%; }
  .building-silhouette.b2 { width: 6%; height: 65%; }
  .building-silhouette.b3 { width: 10%; height: 35%; }
  .building-silhouette.b4 { width: 7%; height: 55%; }
  .building-silhouette.b5 { width: 9%; height: 40%; }
  .building-silhouette.b6 { width: 5%; height: 70%; }
  .building-silhouette.b7 { width: 8%; height: 50%; }
  .building-silhouette.b8 { width: 6%; height: 60%; }

  .fog {
    position: absolute;
    bottom: 0;
    left: 0;
    right: 0;
    height: 30%;
    background: linear-gradient(180deg, transparent 0%, rgba(12, 14, 26, 0.5) 30%, rgba(12, 14, 26, 0.9) 70%, rgba(12, 14, 26, 0.98) 100%);
  }

  .town-nav {
    position: relative;
    z-index: 1;
    flex-shrink: 0;
    padding: 0 0.5rem;
  }

  .town-nav-beam {
    display: flex;
    gap: 0.5rem;
    justify-content: center;
    padding: 0.5rem 0.75rem;
    background: linear-gradient(180deg, rgba(60, 45, 30, 0.6) 0%, rgba(40, 30, 20, 0.8) 100%);
    border: 1px solid rgba(120, 90, 60, 0.3);
    border-radius: 0.375rem;
    box-shadow: inset 0 1px 0 rgba(255, 255, 255, 0.05), 0 4px 12px rgba(0, 0, 0, 0.4);
    overflow-x: auto;
  }

  .town-nav-btn {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 0.25rem;
    padding: 0.5rem 0.75rem;
    min-width: 4rem;
    background: linear-gradient(180deg, rgba(30, 25, 20, 0.8) 0%, rgba(20, 16, 12, 0.9) 100%);
    border: 1px solid rgba(80, 65, 50, 0.4);
    border-radius: 0.25rem 0.25rem 0.375rem 0.375rem;
    color: #887766;
    cursor: pointer;
    font-size: clamp(0.6rem, 1.2vw, 0.75rem);
    transition: all 0.2s ease;
    position: relative;
  }

  .town-nav-btn::before {
    content: '';
    position: absolute;
    top: -4px;
    left: 50%;
    transform: translateX(-50%);
    width: 0;
    height: 0;
    border-left: 5px solid transparent;
    border-right: 5px solid transparent;
    border-bottom: 4px solid rgba(80, 65, 50, 0.4);
    transition: border-bottom-color 0.2s;
  }

  .town-nav-btn:hover {
    background: linear-gradient(180deg, rgba(45, 35, 25, 0.85) 0%, rgba(30, 24, 18, 0.95) 100%);
    border-color: rgba(120, 100, 70, 0.5);
    color: #bbaa88;
    transform: translateY(-2px);
  }

  .town-nav-btn:hover::before {
    border-bottom-color: rgba(120, 100, 70, 0.5);
  }

  .town-nav-btn.active {
    background: linear-gradient(180deg, rgba(60, 45, 25, 0.7) 0%, rgba(40, 30, 15, 0.85) 100%);
    border-color: rgba(212, 168, 75, 0.5);
    color: #d4a84b;
    box-shadow: 0 0 12px rgba(212, 168, 75, 0.15), inset 0 1px 0 rgba(212, 168, 75, 0.1);
  }

  .town-nav-btn.active::before {
    border-bottom-color: rgba(212, 168, 75, 0.5);
  }

  .town-nav-icon {
    width: clamp(1rem, 2vw, 1.25rem);
    height: clamp(1rem, 2vw, 1.25rem);
    opacity: 0.7;
    transition: opacity 0.2s;
  }

  .town-nav-btn:hover .town-nav-icon,
  .town-nav-btn.active .town-nav-icon {
    opacity: 1;
  }

  .town-nav-label {
    letter-spacing: 0.03em;
    text-transform: uppercase;
  }

  .town-body {
    position: relative;
    z-index: 1;
    flex: 1 1 auto;
    display: flex;
    flex-direction: column;
    min-height: 0;
    overflow: hidden;
  }

  .tab-panel {
    position: relative;
    z-index: 1;
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
    overflow-y: auto;
    padding: 0.75rem;
    min-height: 0;
    background: rgba(8, 10, 18, 0.75);
    border: 1px solid rgba(255, 255, 255, 0.06);
    border-radius: 0.5rem;
    backdrop-filter: blur(4px);
  }

  .town-services {
    margin-top: 1rem;
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
    overflow-y: auto;
    padding-right: 0.25rem;
  }

  .branch-modal-overlay {
    position: fixed;
    inset: 0;
    display: flex;
    align-items: center;
    justify-content: center;
    background: rgba(0, 0, 0, 0.85);
    z-index: 50;
    pointer-events: auto;
  }

  .branch-modal-card {
    background: #1a1a2e;
    border: 1px solid #444;
    border-radius: 0.5rem;
    padding: 1.5rem;
    min-width: 320px;
    max-width: 90vw;
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
  }

  .branch-modal-title {
    margin: 0;
    font-size: 1.25rem;
    color: #d4a84b;
  }

  .branch-modal-subtitle {
    margin: 0;
    color: #888;
    font-size: 0.875rem;
  }

  .branch-parent {
    color: #aaa;
    font-style: italic;
  }

  .branch-warning {
    margin: 0.25rem 0;
    color: #d4a84b;
    font-size: 0.75rem;
    font-style: italic;
  }

  .branch-options {
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
  }

  .branch-option {
    display: flex;
    flex-direction: column;
    gap: 0.375rem;
    padding: 0.75rem;
    background: rgba(255, 255, 255, 0.03);
    border: 1px solid #333;
    border-radius: 0.25rem;
  }

  .branch-name {
    margin: 0;
    font-size: 1rem;
    color: #ccc;
  }

  .branch-abilities {
    display: flex;
    flex-wrap: wrap;
    gap: 0.25rem;
  }

  .branch-ability {
    padding: 0.15rem 0.375rem;
    background: rgba(68, 170, 255, 0.1);
    border: 1px solid #4488aa;
    border-radius: 0.2rem;
    color: #88ccff;
    font-size: 0.75rem;
  }

  .branch-ability-none {
    color: #666;
    font-size: 0.75rem;
    font-style: italic;
  }

  .branch-choose-btn {
    margin-top: 0.25rem;
    padding: 0.4rem 0.75rem;
    background: rgba(68, 170, 68, 0.15);
    border: 1px solid #44aa44;
    border-radius: 0.25rem;
    color: #88cc88;
    cursor: pointer;
    font-size: 0.875rem;
    transition: background 0.15s;
  }

  .branch-choose-btn:hover {
    background: rgba(68, 170, 68, 0.3);
  }

  .branch-empty {
    color: #666;
    font-style: italic;
    text-align: center;
    font-size: 0.875rem;
  }
</style>
