<script lang="ts">
  import type { GameState, PartyMember } from '$shared/types/game';
  import { sendAction } from '$shared/stores/gameStore';

  interface Props {
    gameState: GameState | null;
    onTavernRecruit: (id: string) => void;
    onMissionAccept: (id: string) => void;
    onVendorPurchase: (id: string) => void;
  }

  let { gameState, onTavernRecruit, onMissionAccept, onVendorPurchase }: Props = $props();

  const classColors: Record<string, string> = {
    bonewarden: '#8B7355',
    stillblade: '#6B8E9F',
    cauterist: '#B85C38',
    hollow: '#6B6B6B',
    fieldwright: '#7A8B69',
    inkblood: '#7A4B6B',
    marcher: '#5A7A5A',
    ashmouth: '#8B5A3C',
  };

  const factionDialogue: Record<string, { dismissive: string; rumor: string; hostility: string }> = {
    bureau: {
      dismissive: '"The Bureau has no business with you."',
      rumor: '"Rumor: A patrol went missing near the sewers."',
      hostility: '"You are not welcome here. Leave, or be removed."'
    },
    convocation: {
      dismissive: '"The Convocation does not suffer fools."',
      rumor: '"Rumor: The Bloom whispers differently tonight."',
      hostility: '"Your presence offends the Convocation. Depart before we act."'
    },
    cartography: {
      dismissive: '"The Collective does not guide those who cannot read their own path."',
      rumor: '"Rumor: A new passage has opened beneath the eastern quarter."',
      hostility: '"You are lost to us. Turn back before you lead others astray."'
    },
    inkblood: {
      dismissive: '"The Scribes record many things. Your name is not among them."',
      rumor: '"Rumor: A forgotten text in the deep archives speaks of what is to come."',
      hostility: '"Your story ends here. We will ensure it is properly documented."'
    },
    stillness: {
      dismissive: '"The Null has no use for noise. Be silent or be gone."',
      rumor: '"Rumor: The silence between heartbeats grows longer each night."',
      hostility: '"You disrupt the equilibrium. We will restore it."'
    }
  };

  function dismissiveLineForFaction(factionId: string): string {
    return factionDialogue[factionId]?.dismissive ?? '"We have nothing to discuss."';
  }

  function rumorForFaction(factionId: string): string {
    return factionDialogue[factionId]?.rumor ?? '"Rumor: Something stirs in the dark."';
  }

  function hostilityLineForFaction(factionId: string): string {
    return factionDialogue[factionId]?.hostility ?? '"You are not welcome here."';
  }

  const factionColors: Record<string, string> = {
    bureau: '#4488aa',
    convocation: '#aa44aa',
    cartography: '#b8860b',
    inkblood: '#8b2222',
    stillness: '#2a2a3a',
  };

  function greetingForRep(rep: number): string {
    if (rep < 0) return '"What do you want?"';
    if (rep >= 30) return '"Good to see you again."';
    return '"Hello."';
  }

  function getFactionMissions(factionId: string) {
    return town?.availableMissions.filter(m => m.factionId === factionId) ?? [];
  }

  const town = $derived(gameState?.town);
  const reputation = $derived(gameState?.reputation ?? {});
  const partyGold = $derived(gameState?.partyGold ?? 0);
  const partyMembers = $derived(gameState?.party ?? []);
  const partyInventory = $derived(gameState?.partyInventory ?? []);
  const downtimeCompleted = $derived(gameState?.downtimeCompleted ?? []);

  const downtimeActions = [
    { value: 'Rest', label: 'Rest' },
    { value: 'Train', label: 'Train' },
    { value: 'Craft', label: 'Craft' },
    { value: 'Network', label: 'Network' },
    { value: 'Investigate', label: 'Investigate' },
    { value: 'LayLow', label: 'Lay Low' },
    { value: 'TendBlooms', label: 'Tend Blooms' },
  ];

  function isDowntimeDone(member: PartyMember): boolean {
    return downtimeCompleted.includes(member.id);
  }

  function sendDowntimeAction(memberId: string, action: string) {
    sendAction({ type: 'downtime_action', targetId: memberId, downtimeAction: action });
  }

  function restAll() {
    for (const member of partyMembers) {
      if (member.id && !isDowntimeDone(member)) {
        sendDowntimeAction(member.id, 'Rest');
      }
    }
  }
</script>

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

<h2>Rumors</h2>
<div class="service-list">
  {#each town?.rumors || [] as rumor (rumor.id)}
    <div class="service-item rumor-row">
      <div class="rumor-text">
        <span class="rumor-quote">&ldquo;{rumor.text}&rdquo;</span>
        {#if rumor.verified}
          <span class="rumor-badge" class:rumor-true={rumor.verificationResult === true} class:rumor-false={rumor.verificationResult === false}>
            {rumor.verificationResult === true ? 'Confirmed' : 'Debunked'}
          </span>
        {:else}
          <span class="rumor-unverified">Unverified</span>
        {/if}
      </div>
      {#if !rumor.verified}
        <button
          type="button"
          class="action-btn"
          onclick={() => sendAction({ type: 'rumor_verify', targetId: rumor.id, source: 'Firsthand' })}
        >
          Verify
        </button>
      {/if}
    </div>
  {:else}
    <div class="empty-state">No rumors circulating.</div>
  {/each}
</div>

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

{#if gameState?.wildCardAlliance?.status === 'offered'}
  <h2>Wild Card Alliance</h2>
  <div class="service-list">
    <div class="service-item alliance-offer">
      <div class="alliance-text">
        <span class="alliance-faction">The {gameState.wildCardAlliance.factionId}</span>
        <span class="alliance-desc">has offered an alliance. Their soldiers will assist you in combat, and their vendors will offer a 25% discount.</span>
      </div>
      <div class="alliance-buttons">
        <button type="button" class="action-btn accept" onclick={() => sendAction({ type: 'wildcard_alliance', targetId: 'accept' })}>Accept</button>
        <button type="button" class="action-btn refuse" onclick={() => sendAction({ type: 'wildcard_alliance', targetId: 'refuse' })}>Refuse</button>
        <button type="button" class="action-btn ignore" onclick={() => sendAction({ type: 'wildcard_alliance', targetId: 'ignore' })}>Ignore</button>
      </div>
    </div>
  </div>
{:else if gameState?.wildCardAlliance?.status === 'accepted'}
  <h2>Wild Card Alliance</h2>
  <div class="service-list">
    <div class="service-item alliance-active">
      <span class="alliance-faction">Allied with {gameState.wildCardAlliance.factionId}</span>
      <span class="alliance-benefits">Combat assistance active. Vendor discount active.</span>
    </div>
  </div>
{/if}

<h2>Bone Clerk</h2>
<div class="service-list">
  <div class="bone-clerk-info">
    <span class="tithe-tokens">Tithe Tokens: {gameState?.titheTokens ?? 0}</span>
    <span class="clerk-desc">The Bone Clerk can return the dead to life, for a price.</span>
  </div>
  {#each gameState?.deadCharacters || [] as dead (dead.id)}
    <div class="service-item dead-character-row">
      <div class="dead-char-info">
        <span class="dead-name">{dead.name}</span>
        <span class="dead-class">{dead.classId}</span>
        <span class="dead-level">Lv.{dead.level}</span>
        {#if dead.resurrectionAttempts >= 2}
          <span class="dead-permanent">Permanently Lost</span>
        {:else}
          <span class="dead-attempts">Attempts: {dead.resurrectionAttempts}/2</span>
        {/if}
      </div>
      {#if dead.resurrectionAttempts < 2}
        <button
          type="button"
          class="action-btn"
          disabled={partyGold < dead.resurrectionCost || (gameState?.titheTokens ?? 0) < dead.titheTokenCost}
          onclick={() => sendAction({ type: 'resurrect_character', targetId: dead.id })}
        >
          Resurrect ({dead.resurrectionCost}g, {dead.titheTokenCost} TT)
        </button>
      {/if}
    </div>
  {:else}
    <div class="empty-state">No dead to resurrect.</div>
  {/each}
</div>

<h2>Downtime</h2>
<div class="service-list">
  <div class="downtime-header">
    <span class="downtime-info">One action per character per town visit</span>
    <button type="button" class="action-btn" onclick={restAll}>Rest All</button>
  </div>
  {#each partyMembers as member (member.id)}
    {#if member.id}
      <div class="service-item downtime-row">
        <div class="downtime-char">
          <span class="downtime-name">{member.name}</span>
          <span class="downtime-class">{member.className}</span>
        </div>
        {#if isDowntimeDone(member)}
          <span class="downtime-done">Done</span>
        {:else}
          <select
            class="downtime-select"
            onchange={(e) => {
              const value = (e.target as HTMLSelectElement).value;
              if (value) sendDowntimeAction(member.id, value);
            }}
          >
            <option value="">Select action...</option>
            {#each downtimeActions as action}
              <option value={action.value}>{action.label}</option>
            {/each}
          </select>
        {/if}
      </div>
    {/if}
  {:else}
    <div class="empty-state">No characters in party.</div>
  {/each}
</div>

<style>
  h2 {
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

  .rumor-row {
    flex-direction: column;
    align-items: flex-start;
    gap: 0.25rem;
  }

  .rumor-text {
    display: flex;
    flex-wrap: wrap;
    gap: 0.375rem;
    align-items: center;
  }

  .rumor-quote {
    color: #ccc;
    font-style: italic;
  }

  .rumor-badge {
    padding: 0.1rem 0.35rem;
    border-radius: 0.2rem;
    font-size: clamp(0.55rem, 1vw, 0.65rem);
    font-weight: bold;
    text-transform: uppercase;
  }

  .rumor-true {
    background: rgba(68, 170, 68, 0.2);
    color: #88cc88;
    border: 0.0625em solid #44aa44;
  }

  .rumor-false {
    background: rgba(204, 68, 68, 0.2);
    color: #cc8888;
    border: 0.0625em solid #cc4444;
  }

  .rumor-unverified {
    color: #888;
    font-size: clamp(0.55rem, 1vw, 0.65rem);
    font-style: italic;
  }

  .empty-state {
    padding: 0.5rem;
    color: #666;
    font-style: italic;
    text-align: center;
    font-size: clamp(0.65rem, 1.3vw, 0.75rem);
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

  .downtime-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: 0.25rem 0.5rem;
    font-size: clamp(0.6rem, 1.2vw, 0.7rem);
  }

  .downtime-info {
    color: #888;
    font-style: italic;
  }

  .downtime-row {
    display: flex;
    justify-content: space-between;
    align-items: center;
    gap: 0.5rem;
  }

  .downtime-char {
    display: flex;
    flex-direction: column;
    flex: 1 1 auto;
    min-width: 0;
  }

  .downtime-name {
    color: #eee;
    font-weight: bold;
    font-size: clamp(0.65rem, 1.3vw, 0.75rem);
  }

  .downtime-class {
    color: #888;
    font-size: clamp(0.55rem, 1vw, 0.65rem);
  }

  .downtime-done {
    color: #44aa44;
    font-size: clamp(0.6rem, 1.2vw, 0.7rem);
    font-weight: bold;
    flex-shrink: 0;
  }

  .downtime-select {
    padding: 0.2rem 0.375rem;
    background: rgba(0, 0, 0, 0.3);
    border: 0.0625em solid #444;
    border-radius: 0.2rem;
    color: #ccc;
    font-size: clamp(0.6rem, 1.2vw, 0.7rem);
    cursor: pointer;
    flex-shrink: 0;
    min-width: 7rem;
  }

  .downtime-select:focus {
    outline: none;
    border-color: #d4a84b;
  }

  .downtime-select option {
    background: #1a1a2e;
    color: #ccc;
  }

  .bone-clerk-info {
    display: flex;
    flex-direction: column;
    gap: 0.25rem;
    padding: 0.25rem 0.5rem;
  }

  .tithe-tokens {
    color: #d4a84b;
    font-weight: bold;
    font-size: clamp(0.65rem, 1.3vw, 0.75rem);
  }

  .clerk-desc {
    color: #888;
    font-size: clamp(0.55rem, 1vw, 0.65rem);
    font-style: italic;
  }

  .dead-character-row {
    display: flex;
    justify-content: space-between;
    align-items: center;
    gap: 0.5rem;
  }

  .dead-char-info {
    display: flex;
    flex-direction: column;
    flex: 1 1 auto;
    min-width: 0;
  }

  .dead-name {
    color: #ccc;
    font-weight: bold;
    font-size: clamp(0.65rem, 1.3vw, 0.75rem);
  }

  .dead-class {
    color: #888;
    font-size: clamp(0.55rem, 1vw, 0.65rem);
    text-transform: capitalize;
  }

  .dead-level {
    color: #d4a84b;
    font-size: clamp(0.55rem, 1vw, 0.65rem);
  }

  .dead-attempts {
    color: #d4a84b;
    font-size: clamp(0.55rem, 1vw, 0.65rem);
  }

  .dead-permanent {
    color: #c44;
    font-size: clamp(0.55rem, 1vw, 0.65rem);
    font-weight: bold;
  }

  .action-btn:disabled {
    opacity: 0.4;
    cursor: not-allowed;
  }

  .alliance-offer {
    flex-direction: column;
    align-items: flex-start;
    gap: 0.5rem;
    background: rgba(212, 168, 75, 0.1);
    border: 1px solid rgba(212, 168, 75, 0.3);
    border-radius: 0.5rem;
    padding: 0.75rem;
  }

  .alliance-text {
    display: flex;
    flex-direction: column;
    gap: 0.25rem;
  }

  .alliance-faction {
    color: #d4a84b;
    font-weight: bold;
    text-transform: capitalize;
  }

  .alliance-desc {
    color: #ccc;
    font-size: clamp(0.65rem, 1.2vw, 0.8rem);
  }

  .alliance-buttons {
    display: flex;
    gap: 0.5rem;
  }

  .alliance-buttons .accept {
    background: #4a7c3f;
  }

  .alliance-buttons .refuse {
    background: #7c3f3f;
  }

  .alliance-buttons .ignore {
    background: #555;
  }

  .alliance-active {
    flex-direction: column;
    align-items: flex-start;
    gap: 0.25rem;
    background: rgba(74, 124, 63, 0.1);
    border: 1px solid rgba(74, 124, 63, 0.3);
    border-radius: 0.5rem;
    padding: 0.75rem;
  }

  .alliance-benefits {
    color: #888;
    font-size: clamp(0.65rem, 1.2vw, 0.8rem);
  }
</style>
