<script lang="ts">
  interface Props {
    onEnterDungeon: (type: string) => void;
    onSave: () => void;
    onReset: () => void;
    onShowMap: () => void;
  }

  let { onEnterDungeon, onSave, onReset, onShowMap }: Props = $props();

  const dungeonTypes = [
    { id: 'broken_engine', name: 'Broken Engine', level: 1, desc: 'Shallow caves infested with goblins.' },
    { id: 'sewers', name: 'Sewer Warrens', level: 3, desc: 'Crumbling ruins of a lost civilization.' },
    { id: 'crypt', name: 'Crypt of Whispers', level: 5, desc: 'A volcanic lair of a fearsome dragon.' },
    { id: 'bloom_site', name: 'Bloom Site', level: 4, desc: 'A fungal infestation spreading through abandoned machinery.' },
  ];
</script>

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
