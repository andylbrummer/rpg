<script lang="ts">
  import type { GameState, PartyMember } from '../types/game';
  import { sendAction } from '../stores/gameStore';
  import CharacterSheet from './CharacterSheet.svelte';
  import OverworldMap from './OverworldMap.svelte';

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

  let selectedCharacter = $state<string | null>(null);
  let sheetMember = $state<PartyMember | null>(null);
  let showMap = $state(false);

  const dungeonTypes = [
    { id: 'broken_engine', name: 'Broken Engine', level: 1, desc: 'Shallow caves infested with goblins.' },
    { id: 'sewers', name: 'Sewer Warrens', level: 3, desc: 'Crumbling ruins of a lost civilization.' },
    { id: 'crypt', name: 'Crypt of Whispers', level: 5, desc: 'A volcanic lair of a fearsome dragon.' },
  ];

  const classColors: Record<string, string> = {
    bonewarden: '#8B7355',
    stillblade: '#6B8E9F',
    cauterist: '#B85C38',
    hollow: '#6B6B6B',
    fieldwright: '#7A8B69',
    inkblood: '#7A4B6B',
  };

  function selectCharacter(name: string) {
    selectedCharacter = selectedCharacter === name ? null : name;
  }

  function getFactionMissions(factionId: string) {
    return town?.availableMissions.filter(m => m.factionId === factionId) ?? [];
  }

  function greetingForRep(rep: number): string {
    if (rep < 0) return '"What do you want?"';
    if (rep >= 30) return '"Good to see you again."';
    return '"Hello."';
  }

  function dismissiveLineForFaction(factionId: string): string {
    return factionId === 'bureau'
      ? '"The Bureau has no business with you."'
      : '"The Convocation does not suffer fools."';
  }

  function rumorForFaction(factionId: string): string {
    return factionId === 'bureau'
      ? '"Rumor: A patrol went missing near the sewers."'
      : '"Rumor: The Bloom whispers differently tonight."';
  }

  function hostilityLineForFaction(factionId: string): string {
    return factionId === 'bureau'
      ? '"You are not welcome here. Leave, or be removed."'
      : '"Your presence offends the Convocation. Depart before we act."';
  }

  const factionColors: Record<string, string> = {
    bureau: '#4488aa',
    convocation: '#aa44aa',
  };

  const town = $derived(gameState?.town);
  const reputation = $derived(gameState?.reputation ?? {});
  const partyGold = $derived(gameState?.partyGold ?? 0);
  const partyInventory = $derived(gameState?.partyInventory ?? []);

  const partyMembers = $derived(gameState?.party ?? []);
  const frontRow = $derived(partyMembers.filter(m => m.row === 0));
  const backRow = $derived(partyMembers.filter(m => m.row === 1));

  const pendingBranchMembers = $derived(
    partyMembers.filter(m => m.awaitingBranchChoice && (m.availableBranches?.length ?? 0) > 0)
  );

  let dragSlot = $state<number | null>(null);

  function handleDragStart(slot: number) {
    dragSlot = slot;
  }

  function handleDragEnd() {
    dragSlot = null;
  }

  function handleDragOver(e: DragEvent) {
    e.preventDefault();
  }

  function handleDrop(e: DragEvent, targetRow: number) {
    e.preventDefault();
    if (dragSlot === null) return;
    const member = partyMembers.find(m => m.slot === dragSlot);
    if (!member) return;
    if (member.row !== targetRow) {
      onSwapRow(member.slot);
    }
    dragSlot = null;
  }

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
  <div class="town-header">
    <h1>The Reach</h1>
    <p class="tagline">An Old-School Dungeon Crawler</p>
  </div>

  <div class="town-body">
    <div class="party-panel">
      <div class="party-header-row">
        <h2>Your Party</h2>
        <span class="gold-badge">{partyGold}g</span>
      </div>
      <div class="formation-grid">
        <div
          class="formation-row front-row"
          ondragover={handleDragOver}
          ondrop={(e) => handleDrop(e, 0)}
          role="list"
          aria-label="Front row"
        >
          <div class="row-icon">
            <svg viewBox="0 0 24 24" width="16" height="16" fill="currentColor">
              <path d="M12 1L3 5v6c0 5.55 3.84 10.74 9 12 5.16-1.26 9-6.45 9-12V5l-9-4z"/>
            </svg>
            <span>Front</span>
          </div>
          <div class="formation-slots">
            {#each [0, 1, 2] as slot}
              {@const member = partyMembers.find(m => m.slot === slot)}
              <div class="formation-slot" class:empty={!member}>
                {#if member}
                  <div
                    class="formation-card"
                    draggable="true"
                    ondragstart={() => handleDragStart(member.slot)}
                    ondragend={handleDragEnd}
                    role="button"
                    tabindex="0"
                    aria-grabbed={dragSlot === member.slot}
                  >
                    <div class="formation-portrait" style="background-color: {member.color}"></div>
                    <span class="formation-name">{member.name}</span>
                  </div>
                {/if}
              </div>
            {/each}
          </div>
        </div>

        <div
          class="formation-row back-row"
          ondragover={handleDragOver}
          ondrop={(e) => handleDrop(e, 1)}
          role="list"
          aria-label="Back row"
        >
          <div class="row-icon">
            <svg viewBox="0 0 24 24" width="16" height="16" fill="currentColor">
              <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8zm-1-13h2v6h-2zm0 8h2v2h-2z"/>
            </svg>
            <span>Back</span>
          </div>
          <div class="formation-slots">
            {#each [3, 4, 5] as slot}
              {@const member = partyMembers.find(m => m.slot === slot)}
              <div class="formation-slot" class:empty={!member}>
                {#if member}
                  <div
                    class="formation-card"
                    draggable="true"
                    ondragstart={() => handleDragStart(member.slot)}
                    ondragend={handleDragEnd}
                    role="button"
                    tabindex="0"
                    aria-grabbed={dragSlot === member.slot}
                  >
                    <div class="formation-portrait" style="background-color: {member.color}"></div>
                    <span class="formation-name">{member.name}</span>
                  </div>
                {/if}
              </div>
            {/each}
          </div>
        </div>
      </div>

      <div class="party-grid">
        {#each gameState?.party || [] as member (member.name)}
          <div
            role="button"
            tabindex="0"
            class="character-card"
            class:selected={selectedCharacter === member.name}
            onclick={() => selectCharacter(member.name)}
            onkeydown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); selectCharacter(member.name); } }}
          >
            <div class="card-header" style="background-color: {member.color}">
              <span class="card-level">Lv.{member.level}</span>
            </div>
            <div class="card-body">
              <div class="card-name">{member.name}</div>
              <div class="card-class">{member.className}</div>
              <div class="card-stats">
                <span>HP: {member.hp}/{member.maxHp}</span>
              </div>
              <button
                type="button"
                class="view-sheet-btn"
                onclick={(e) => { e.stopPropagation(); sheetMember = member; }}
              >
                Details
              </button>
            </div>
          </div>
        {/each}
      </div>

      <div class="town-services">
        <h2>Tavern</h2>
        <div class="service-list">
          {#each town?.tavernRoster || [] as recruit (recruit.id)}
            <div class="service-item">
              <div class="recruit-header">
                <span class="recruit-name">{recruit.name}</span>
                <span class="recruit-class" style="color: {classColors[recruit.classId] || '#888'}">
                  {recruit.classId}
                </span>
                <span class="recruit-level">Lv.{recruit.level}</span>
              </div>
              <div class="recruit-cost">{recruit.cost}g</div>
              <button
                type="button"
                class="action-btn"
                onclick={() => onTavernRecruit(recruit.id)}
              >
                Recruit
              </button>
            </div>
          {:else}
            <div class="empty-state">No recruits available.</div>
          {/each}
        </div>

        <h2>Missions</h2>
        <div class="service-list">
          {#each town?.availableMissions || [] as mission (mission.id)}
            <div class="service-item">
              <div class="mission-title">{mission.title}</div>
              <div class="mission-desc">{mission.description}</div>
              <button
                type="button"
                class="action-btn"
                onclick={() => onMissionAccept(mission.id)}
              >
                Accept
              </button>
            </div>
          {:else}
            <div class="empty-state">No missions available.</div>
          {/each}
        </div>

        <h2>Vendor</h2>
        <div class="service-list">
          {#each town?.vendorStock || [] as item (item.itemId)}
            <div class="service-item">
              <div class="item-name">{item.name}</div>
              <div class="item-price">{item.price}g (x{item.quantity})</div>
              <button
                type="button"
                class="action-btn"
                onclick={() => onVendorPurchase(item.itemId)}
              >
                Buy
              </button>
            </div>
          {:else}
            <div class="empty-state">No items in stock.</div>
          {/each}
        </div>

        {#each town?.factionVendors || [] as vendor (vendor.factionId)}
          {@const rep = reputation[vendor.factionId] || 0}
          {@const isVisible = rep > -25}
          {@const isUnlocked = rep >= vendor.threshold}
          {#if isVisible}
            <h2 class:locked-heading={!isUnlocked}>{vendor.name}</h2>
            <div class="service-list">
              {#each vendor.stock as item (item.itemId)}
                <div class="service-item" class:locked-item={!isUnlocked}>
                  <div class="item-name">{item.name}</div>
                  <div class="item-price">{item.price}g (x{item.quantity})</div>
                  {#if isUnlocked}
                    <button
                      type="button"
                      class="action-btn"
                      onclick={() => onVendorPurchase(item.itemId)}
                    >
                      Buy
                    </button>
                  {:else}
                    <span class="lock-text">Requires {vendor.threshold} {vendor.factionId} reputation</span>
                  {/if}
                </div>
              {:else}
                <div class="empty-state">No items in stock.</div>
              {/each}
            </div>
          {/if}
        {/each}

        {#if partyInventory.length > 0}
          <h2>Inventory</h2>
          <div class="service-list">
            {#each partyInventory as itemId}
              <div class="service-item">
                <div class="item-name">{itemId}</div>
              </div>
            {/each}
          </div>
        {/if}

        <h2>Faction Contacts</h2>
        <div class="service-list">
          {#each town?.factionContacts || [] as contact (contact.id)}
            <div class="contact-card">
              <div class="contact-header">
                <div class="contact-portrait" style="background-color: {factionColors[contact.factionId] || '#888'}"></div>
                <div class="contact-info">
                  <span class="contact-name">{contact.name}</span>
                  <span class="contact-faction" style="color: {factionColors[contact.factionId] || '#888'}">{contact.factionId}</span>
                </div>
                <div class="contact-rep">
                  <div class="rep-bar">
                    <div class="rep-fill" style="width: {Math.max(0, ((reputation[contact.factionId] || 0) + 100) / 2)}%"></div>
                  </div>
                  <span class="rep-value">{reputation[contact.factionId] || 0}</span>
                </div>
              </div>
              <div class="contact-dialogue">
                {#if (reputation[contact.factionId] || 0) <= -25}
                  <p class="dialogue-line hostile">{hostilityLineForFaction(contact.factionId)}</p>
                {:else}
                  <p class="dialogue-line greeting">{greetingForRep(reputation[contact.factionId] || 0)}</p>
                  {#if (reputation[contact.factionId] || 0) <= 0}
                    <p class="dialogue-line dismissive">{dismissiveLineForFaction(contact.factionId)}</p>
                  {/if}
                  {#if (reputation[contact.factionId] || 0) >= 10}
                    <p class="dialogue-line rumor">{rumorForFaction(contact.factionId)}</p>
                  {/if}
                  {#if (reputation[contact.factionId] || 0) >= 20}
                    {#each getFactionMissions(contact.factionId) as mission (mission.id)}
                      <div class="mission-offer">
                        <div class="mission-offer-text">
                          <span class="mission-offer-title">{mission.title}</span>
                          <span class="mission-offer-desc">{mission.description}</span>
                        </div>
                        <button
                          type="button"
                          class="action-btn"
                          onclick={() => onMissionAccept(mission.id)}
                        >
                          Accept
                        </button>
                      </div>
                    {:else}
                      <p class="dialogue-line empty">No missions currently available.</p>
                    {/each}
                  {/if}
                {/if}
              </div>
            </div>
          {:else}
            <div class="empty-state">No faction contacts.</div>
          {/each}
        </div>

        <h2>Quest Log</h2>
        <div class="service-list">
          {#each town?.questLog || [] as quest (quest.id)}
            <div class="service-item">
              <div class="quest-title">{quest.title}</div>
              <span class="quest-status" class:completed={quest.status === 'completed'}>{quest.status}</span>
            </div>
          {:else}
            <div class="empty-state">No active quests.</div>
          {/each}
        </div>
      </div>
    </div>

    <div class="actions-panel">
      <h2>Actions</h2>

      <div class="dungeon-list">
        {#each dungeonTypes as dungeon}
          <button
            class="dungeon-btn"
            onclick={() => onEnterDungeon(dungeon.id)}
          >
            <div class="dungeon-name">{dungeon.name}</div>
            <div class="dungeon-info">
              <span class="dungeon-level">Lv.{dungeon.level}</span>
              <span class="dungeon-desc">{dungeon.desc}</span>
            </div>
          </button>
        {/each}
      </div>

      <div class="utility-actions">
        <button class="utility-btn" onclick={() => showMap = true}>Overworld Map</button>
        <button class="utility-btn save-btn" onclick={onSave}>Save Game</button>
        <button class="utility-btn reset-btn" onclick={onReset}>Reset Game</button>
      </div>
    </div>
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
            {#if !isLevel6 && (member as any).branchWarnings?.includes(branch)}
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
  }

  .town-header {
    flex: 0 0 auto;
    text-align: center;
    padding: 0.5rem 0;
  }

  .town-header h1 {
    margin: 0;
    font-size: clamp(1.5rem, 4vw, 2.5rem);
    color: #d4a84b;
    text-shadow: 0 0 0.5em rgba(212, 168, 75, 0.3);
  }

  .tagline {
    margin: 0.25rem 0 0;
    color: #888;
    font-size: clamp(0.75rem, 1.8vw, 0.875rem);
  }

  .town-body {
    flex: 1 1 auto;
    display: flex;
    gap: 1rem;
    min-height: 0;
    overflow: hidden;
  }

  .party-panel {
    flex: 1 1 auto;
    display: flex;
    flex-direction: column;
    min-width: 0;
    overflow: hidden;
  }

  .party-panel h2 {
    margin: 0 0 0.5rem;
    font-size: clamp(0.875rem, 2vw, 1.1rem);
    color: #ccc;
  }

  .formation-grid {
    display: flex;
    flex-direction: column;
    gap: 0.5rem;
    margin-bottom: 1rem;
  }

  .formation-row {
    display: flex;
    align-items: center;
    gap: 0.5rem;
    padding: 0.5rem;
    background: rgba(255, 255, 255, 0.03);
    border: 0.0625em solid #333;
    border-radius: 0.375rem;
  }

  .formation-row.front-row {
    border-color: #446644;
  }

  .formation-row.back-row {
    border-color: #444466;
  }

  .row-icon {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 0.15rem;
    width: 2.5rem;
    font-size: clamp(0.55rem, 1vw, 0.65rem);
    color: #888;
    flex-shrink: 0;
  }

  .formation-slots {
    display: flex;
    gap: 0.5rem;
    flex: 1 1 auto;
  }

  .formation-slot {
    flex: 1 1 0;
    min-width: 0;
    aspect-ratio: 1;
    max-height: 4rem;
    display: flex;
    align-items: center;
    justify-content: center;
    background: rgba(0, 0, 0, 0.2);
    border: 0.0625em dashed #444;
    border-radius: 0.25rem;
  }

  .formation-slot.empty {
    background: rgba(0, 0, 0, 0.1);
  }

  .formation-card {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 0.15rem;
    padding: 0.25rem;
    cursor: grab;
    width: 100%;
    height: 100%;
    justify-content: center;
  }

  .formation-card:active {
    cursor: grabbing;
  }

  .formation-portrait {
    width: clamp(1.5rem, 3vw, 2rem);
    height: clamp(1.5rem, 3vw, 2rem);
    border-radius: 50%;
    border: 0.125em solid #666;
    flex-shrink: 0;
  }

  .formation-name {
    font-size: clamp(0.55rem, 1vw, 0.7rem);
    color: #ccc;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
    max-width: 100%;
  }

  .party-grid {
    display: flex;
    flex-wrap: wrap;
    gap: 0.5rem;
    overflow-y: auto;
    padding-right: 0.25rem;
  }

  .character-card {
    display: flex;
    flex-direction: column;
    background: rgba(255, 255, 255, 0.05);
    border: 0.0625em solid #444;
    border-radius: 0.375rem;
    overflow: hidden;
    cursor: pointer;
    transition: transform 0.15s, border-color 0.15s;
    min-width: clamp(7rem, 15vw, 9rem);
    flex: 0 0 auto;
  }

  .character-card:hover {
    transform: translateY(-0.125rem);
    border-color: #666;
  }

  .character-card.selected {
    border-color: #d4a84b;
    box-shadow: 0 0 0.5em rgba(212, 168, 75, 0.2);
  }

  .card-header {
    height: clamp(1.5rem, 3vh, 2rem);
    display: flex;
    align-items: center;
    justify-content: flex-end;
    padding: 0 0.375rem;
  }

  .card-level {
    font-size: clamp(0.6rem, 1.2vw, 0.7rem);
    color: rgba(255, 255, 255, 0.8);
    font-weight: bold;
  }

  .card-body {
    padding: 0.375rem;
    display: flex;
    flex-direction: column;
    gap: 0.15rem;
  }

  .card-name {
    font-size: clamp(0.75rem, 1.5vw, 0.875rem);
    font-weight: bold;
    color: #eee;
  }

  .card-class {
    font-size: clamp(0.6rem, 1.2vw, 0.7rem);
    color: #888;
  }

  .card-stats {
    font-size: clamp(0.6rem, 1.2vw, 0.7rem);
    color: #aaa;
  }

  .view-sheet-btn {
    margin-top: 0.25rem;
    padding: 0.2rem 0.5rem;
    background: rgba(68, 170, 255, 0.1);
    border: 0.0625em solid #4488aa;
    border-radius: 0.2rem;
    color: #88ccff;
    font-size: clamp(0.55rem, 1vw, 0.65rem);
    cursor: pointer;
    transition: background 0.15s;
    width: 100%;
  }

  .view-sheet-btn:hover {
    background: rgba(68, 170, 255, 0.25);
  }

  .town-services {
    margin-top: 1rem;
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
    overflow-y: auto;
    padding-right: 0.25rem;
  }

  .town-services h2 {
    margin: 0;
    font-size: clamp(0.875rem, 2vw, 1.1rem);
    color: #ccc;
  }

  .service-list {
    display: flex;
    flex-direction: column;
    gap: 0.375rem;
  }

  .service-item {
    display: flex;
    align-items: center;
    gap: 0.5rem;
    padding: 0.375rem 0.5rem;
    background: rgba(255, 255, 255, 0.03);
    border: 0.0625em solid #333;
    border-radius: 0.25rem;
    font-size: clamp(0.65rem, 1.3vw, 0.75rem);
    color: #aaa;
  }

  .recruit-header {
    display: flex;
    gap: 0.5rem;
    align-items: center;
    flex: 1 1 auto;
    min-width: 0;
  }

  .recruit-name {
    color: #eee;
    font-weight: bold;
  }

  .recruit-class {
    text-transform: capitalize;
  }

  .recruit-level {
    color: #d4a84b;
  }

  .recruit-cost,
  .item-price {
    color: #d4a84b;
    font-weight: bold;
    min-width: 3rem;
    text-align: right;
  }

  .party-header-row {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 0.5rem;
  }

  .gold-badge {
    padding: 0.2rem 0.5rem;
    background: rgba(212, 168, 75, 0.15);
    border: 0.0625em solid #d4a84b;
    border-radius: 0.25rem;
    color: #d4a84b;
    font-size: clamp(0.65rem, 1.3vw, 0.75rem);
    font-weight: bold;
  }

  .locked-heading {
    color: #666;
    font-style: italic;
  }

  .locked-item {
    opacity: 0.6;
    background: rgba(255, 255, 255, 0.01);
    border-color: #222;
  }

  .lock-text {
    color: #c44;
    font-size: clamp(0.55rem, 1vw, 0.65rem);
    font-style: italic;
    flex-shrink: 0;
  }

  .mission-title,
  .item-name {
    color: #eee;
    font-weight: bold;
    flex: 1 1 auto;
    min-width: 0;
  }

  .mission-desc {
    color: #888;
    flex: 1 1 auto;
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .action-btn {
    padding: 0.15rem 0.375rem;
    background: rgba(68, 170, 68, 0.15);
    border: 0.0625em solid #44aa44;
    border-radius: 0.2rem;
    color: #88cc88;
    font-size: clamp(0.55rem, 1vw, 0.65rem);
    cursor: pointer;
    transition: background 0.15s;
    flex-shrink: 0;
  }

  .action-btn:hover {
    background: rgba(68, 170, 68, 0.3);
  }

  .empty-state {
    padding: 0.5rem;
    color: #666;
    font-style: italic;
    text-align: center;
    font-size: clamp(0.65rem, 1.3vw, 0.75rem);
  }

  .actions-panel {
    flex: 0 0 auto;
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
    min-width: clamp(14rem, 22vw, 18rem);
    max-width: min(22rem, 30vw);
  }

  .actions-panel h2 {
    margin: 0 0 0.25rem;
    font-size: clamp(0.875rem, 2vw, 1.1rem);
    color: #ccc;
  }

  .dungeon-list {
    display: flex;
    flex-direction: column;
    gap: 0.5rem;
    flex: 1 1 auto;
    overflow-y: auto;
  }

  .dungeon-btn {
    display: flex;
    flex-direction: column;
    gap: 0.25rem;
    padding: 0.5rem 0.75rem;
    background: rgba(255, 255, 255, 0.05);
    border: 0.0625em solid #444;
    border-radius: 0.375rem;
    color: #ccc;
    cursor: pointer;
    text-align: left;
    transition: background 0.15s, border-color 0.15s;
    min-height: 0;
  }

  .dungeon-btn:hover:not(:disabled) {
    background: rgba(255, 255, 255, 0.1);
    border-color: #666;
  }

  .dungeon-btn:disabled {
    opacity: 0.4;
    cursor: not-allowed;
  }

  .dungeon-name {
    font-size: clamp(0.8rem, 1.8vw, 0.95rem);
    font-weight: bold;
    color: #d4a84b;
  }

  .dungeon-info {
    display: flex;
    flex-direction: column;
    gap: 0.1rem;
  }

  .dungeon-level {
    font-size: clamp(0.65rem, 1.3vw, 0.75rem);
    color: #44aaff;
    font-weight: bold;
  }

  .dungeon-desc {
    font-size: clamp(0.6rem, 1.2vw, 0.7rem);
    color: #888;
  }

  .utility-actions {
    display: flex;
    gap: 0.5rem;
    flex-wrap: wrap;
  }

  .utility-btn {
    flex: 1 1 auto;
    padding: 0.4rem 0.75rem;
    background: rgba(255, 255, 255, 0.05);
    border: 0.0625em solid #444;
    border-radius: 0.25rem;
    color: #ccc;
    cursor: pointer;
    font-size: clamp(0.7rem, 1.5vw, 0.8rem);
    transition: background 0.15s;
    min-width: 5rem;
  }

  .utility-btn:hover {
    background: rgba(100, 100, 100, 0.3);
  }

  .save-btn { border-color: #444466; color: #8888cc; }
  .reset-btn { border-color: #664444; color: #cc8888; }

  .contact-card {
    display: flex;
    flex-direction: column;
    gap: 0.375rem;
    padding: 0.5rem;
    background: rgba(255, 255, 255, 0.03);
    border: 0.0625em solid #333;
    border-radius: 0.25rem;
  }

  .contact-header {
    display: flex;
    align-items: center;
    gap: 0.5rem;
  }

  .contact-portrait {
    width: clamp(1.5rem, 3vw, 2rem);
    height: clamp(1.5rem, 3vw, 2rem);
    border-radius: 50%;
    border: 0.125em solid #666;
    flex-shrink: 0;
  }

  .contact-info {
    display: flex;
    flex-direction: column;
    flex: 1 1 auto;
    min-width: 0;
  }

  .contact-name {
    font-size: clamp(0.7rem, 1.4vw, 0.8rem);
    font-weight: bold;
    color: #eee;
  }

  .contact-faction {
    font-size: clamp(0.6rem, 1.2vw, 0.7rem);
    text-transform: capitalize;
  }

  .contact-rep {
    display: flex;
    flex-direction: column;
    align-items: flex-end;
    gap: 0.1rem;
    flex-shrink: 0;
  }

  .rep-bar {
    width: 3rem;
    height: 0.375rem;
    background: rgba(0, 0, 0, 0.4);
    border-radius: 0.2rem;
    overflow: hidden;
  }

  .rep-fill {
    height: 100%;
    background: #d4a84b;
    border-radius: 0.2rem;
    transition: width 0.3s;
  }

  .rep-value {
    font-size: clamp(0.55rem, 1vw, 0.65rem);
    color: #aaa;
  }

  .contact-dialogue {
    display: flex;
    flex-direction: column;
    gap: 0.25rem;
    padding-left: 0.25rem;
  }

  .dialogue-line {
    margin: 0;
    font-size: clamp(0.6rem, 1.2vw, 0.7rem);
    color: #aaa;
  }

  .dialogue-line.greeting {
    color: #ccc;
  }

  .dialogue-line.dismissive {
    color: #c44;
    font-style: italic;
  }

  .dialogue-line.hostile {
    color: #f44;
    font-weight: bold;
  }

  .dialogue-line.rumor {
    color: #88ccff;
    font-style: italic;
  }

  .mission-offer {
    display: flex;
    align-items: center;
    gap: 0.5rem;
    padding: 0.25rem 0;
  }

  .mission-offer-text {
    display: flex;
    flex-direction: column;
    flex: 1 1 auto;
    min-width: 0;
  }

  .mission-offer-title {
    font-size: clamp(0.65rem, 1.3vw, 0.75rem);
    color: #eee;
    font-weight: bold;
  }

  .mission-offer-desc {
    font-size: clamp(0.6rem, 1.2vw, 0.7rem);
    color: #888;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .quest-title {
    color: #eee;
    font-weight: bold;
    flex: 1 1 auto;
    min-width: 0;
  }

  .quest-status {
    font-size: clamp(0.6rem, 1.2vw, 0.7rem);
    color: #d4a84b;
    text-transform: capitalize;
    flex-shrink: 0;
  }

  .quest-status.completed {
    color: #44aa44;
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
