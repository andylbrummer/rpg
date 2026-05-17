<script lang="ts">
  import type { GameState, PartyMember } from '$shared/types/game';

  interface Props {
    gameState: GameState | null;
    onSwapRow: (slot: number) => void;
    onViewSheet: (member: PartyMember) => void;
  }

  let { gameState, onSwapRow, onViewSheet }: Props = $props();

  let selectedCharacter = $state<string | null>(null);
  let dragSlot = $state<number | null>(null);

  const partyMembers = $derived(gameState?.party ?? []);
  const partyGold = $derived(gameState?.partyGold ?? 0);
  const heat = $derived(gameState?.heat);

  function selectCharacter(name: string) {
    selectedCharacter = selectedCharacter === name ? null : name;
  }

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
</script>

      <div class="party-header-row">
        <h2>Your Party</h2>
        <div class="header-badges">
          <span class="gold-badge">{partyGold}g</span>
          {#if heat && heat.value > 0}
            <span class="heat-badge" class:heat-low={heat.value <= 20} class:heat-med={heat.value > 20 && heat.value <= 40} class:heat-high={heat.value > 40 && heat.value <= 60} class:heat-severe={heat.value > 60 && heat.value <= 80} class:heat-lockdown={heat.value > 80}>
              Heat: {heat.value}
            </span>
          {/if}
        </div>
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
        {#each partyMembers as member (member.name)}
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
                onclick={(e) => { e.stopPropagation(); onViewSheet(member); }}
              >
                Details
              </button>
            </div>
          </div>
        {/each}
      </div>

<style>
  h2 {
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
  .party-header-row {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 0.5rem;
  }

  .header-badges {
    display: flex;
    gap: 0.5rem;
    align-items: center;
  }

  .gold-badge {
    padding: 0.2rem 0.5rem;
    background: rgba(212, 168, 75, 0.15);
    border: 0.0625em solid #d4a84b;
    border-radius: 0.25rem;
    color: #d4a84b;
  }

  .heat-badge {
    padding: 0.2rem 0.5rem;
    border-radius: 0.25rem;
    font-size: clamp(0.6rem, 1.2vw, 0.75rem);
    font-weight: bold;
  }

  .heat-low {
    background: rgba(68, 170, 68, 0.15);
    border: 0.0625em solid #44aa44;
    color: #88cc88;
  }

  .heat-med {
    background: rgba(212, 168, 75, 0.15);
    border: 0.0625em solid #d4a84b;
    color: #d4a84b;
  }

  .heat-high {
    background: rgba(212, 120, 60, 0.15);
    border: 0.0625em solid #d4783c;
    color: #e8a060;
  }

  .heat-severe {
    background: rgba(204, 68, 68, 0.15);
    border: 0.0625em solid #cc4444;
    color: #cc8888;
  }

  .heat-lockdown {
    background: rgba(140, 40, 40, 0.25);
    border: 0.0625em solid #cc4444;
    color: #ff6666;
    font-size: clamp(0.65rem, 1.3vw, 0.75rem);
    font-weight: bold;
  }
</style>
