<script lang="ts">
  import type { GameState, PartyMember } from '../types/game';
  import CharacterSheet from './CharacterSheet.svelte';

  interface Props {
    gameState: GameState | null;
    onEnterDungeon: (type: string) => void;
    onSave: () => void;
    onReset: () => void;
    onSwapRow: (slot: number) => void;
  }

  let { gameState, onEnterDungeon, onSave, onReset, onSwapRow }: Props = $props();

  let selectedCharacter = $state<string | null>(null);
  let sheetMember = $state<PartyMember | null>(null);

  const dungeonTypes = [
    { id: 'broken_engine', name: 'Broken Engine', level: 1, desc: 'Shallow caves infested with goblins.' },
    { id: 'sewers', name: 'Sewer Warrens', level: 3, desc: 'Crumbling ruins of a lost civilization.' },
    { id: 'crypt', name: 'Crypt of Whispers', level: 5, desc: 'A volcanic lair of a fearsome dragon.' },
  ];

  function selectCharacter(name: string) {
    selectedCharacter = selectedCharacter === name ? null : name;
  }
</script>

<div class="town-menu">
  <div class="town-header">
    <h1>The Reach</h1>
    <p class="tagline">An Old-School Dungeon Crawler</p>
  </div>

  <div class="town-body">
    <div class="party-panel">
      <h2>Your Party</h2>
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
        <button class="utility-btn save-btn" onclick={onSave}>Save Game</button>
        <button class="utility-btn reset-btn" onclick={onReset}>Reset Game</button>
      </div>
    </div>
  </div>

{#if sheetMember}
  <CharacterSheet
    member={sheetMember}
    onClose={() => sheetMember = null}
    onSwapRow={onSwapRow}
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
</style>
