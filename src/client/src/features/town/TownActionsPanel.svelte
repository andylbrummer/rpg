<script lang="ts">
  import type { GameState } from '$shared/types/game';

  interface Props {
    gameState: GameState | null;
    onEnterDungeon: (type: string) => void;
    onSave: () => void;
    onReset: () => void;
    onShowMap: () => void;
  }

  let { gameState, onEnterDungeon, onSave, onReset, onShowMap }: Props = $props();

  const dungeonMeta: Record<string, { name: string; level: number; desc: string }> = {
    broken_engine: { name: 'Broken Engine', level: 1, desc: 'Shallow caves infested with goblins.' },
    boneyard: { name: 'The Boneyard', level: 2, desc: 'Bone-sorting halls and tithe archives overrun by rogue constructs.' },
    sewers: { name: 'Sewer Warrens', level: 3, desc: 'Crumbling ruins of a lost civilization.' },
    settlement_gone_wrong: { name: 'Settlement Gone Wrong', level: 3, desc: 'A ruined town overtaken by bloom pockets and hostile survivors.' },
    bloom_site: { name: 'Bloom Site', level: 4, desc: 'A fungal infestation spreading through abandoned machinery.' },
    ossuary: { name: 'The Ossuary', level: 4, desc: 'Family vaults and memorial halls where ancestors do not rest quietly.' },
    crypt: { name: 'Crypt of Whispers', level: 5, desc: 'A volcanic lair of a fearsome dragon.' },
    sealed_vault: { name: 'Sealed Vault', level: 6, desc: 'Imperial wards and dead-language inscriptions guarding sealed horrors.' },
  };

  // Every authored dungeon template is directly enterable from town. (Overworld nodes still
  // drive travel on the map; this list is the quick-access roster of all playable dungeons.)
  const availableDungeons = $derived(
    Object.entries(dungeonMeta)
      .map(([id, m]) => ({ id, name: m.name, level: m.level, desc: m.desc }))
      .sort((a, b) => a.level - b.level)
  );

  const hasPendingBranches = $derived(
    (gameState?.party ?? []).some(m => m.awaitingBranchChoice)
  );
</script>

<div class="actions-panel">
  <h2>Actions</h2>

  {#if hasPendingBranches}
    <div class="pending-branches-banner">
      <span class="pending-branches-icon">⚠</span>
      <span class="pending-branches-text">Party members have pending branch choices. Resolve them in the Party tab before entering a dungeon.</span>
    </div>
  {/if}

  <div class="dungeon-list">
    {#each availableDungeons as dungeon}
      <button
        class="dungeon-btn"
        disabled={hasPendingBranches}
        onclick={() => onEnterDungeon(dungeon.id)}
      >
        <div class="dungeon-name">{dungeon.name}</div>
        <div class="dungeon-info">
          <span class="dungeon-level">Lv.{dungeon.level}</span>
          <span class="dungeon-desc">{dungeon.desc}</span>
        </div>
      </button>
    {:else}
      <div class="empty-state">No dungeons available.</div>
    {/each}
  </div>

  <div class="utility-actions">
    <button class="utility-btn" onclick={onShowMap}>Overworld Map</button>
    <button class="utility-btn save-btn" onclick={onSave}>Save Game</button>
    <button class="utility-btn reset-btn" onclick={onReset}>Reset Game</button>
  </div>
</div>

<style>
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

  .empty-state {
    padding: 1rem;
    text-align: center;
    color: #666;
    font-size: clamp(0.7rem, 1.5vw, 0.85rem);
    font-style: italic;
  }

  .pending-branches-banner {
    display: flex;
    align-items: flex-start;
    gap: 0.5rem;
    padding: 0.6rem 0.75rem;
    background: rgba(212, 168, 75, 0.1);
    border: 0.0625em solid rgba(212, 168, 75, 0.4);
    border-radius: 0.375rem;
    color: #d4a84b;
    font-size: clamp(0.65rem, 1.3vw, 0.8rem);
  }

  .pending-branches-icon {
    flex-shrink: 0;
    font-size: 1rem;
  }

  .pending-branches-text {
    line-height: 1.4;
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
