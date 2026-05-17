<script lang="ts">
  interface Props {
    discoveredOrder: string[];
    revealedIds: Set<string>;
    onClose: () => void;
    onReplay: (synergyId: string) => void;
  }

  let { discoveredOrder, revealedIds, onClose, onReplay }: Props = $props();

  const ALL_SYNERGIES = [
    { id: 'bonewarden_cauterist_bone_link', abilities: ['bone_link', 'pyre'], hint: 'Bone-link channels pyre damage to all linked allies.', effect: '+4 bonus damage' },
    { id: 'stillblade_hollow_backstep', abilities: ['backstep', 'cheap_shot'], hint: 'A Stillblade feint creates the perfect opening for a Hollow cheap shot.', effect: '+6 bonus damage' },
    { id: 'cauterist_hollow_purify', abilities: ['purify', 'cheap_shot'], hint: 'Purification exposes wounds, letting the Hollow\'s cheap shot apply a lasting debuff.', effect: 'applies status (-3 potency)' },
    { id: 'fieldwright_inkblood_overcharge', abilities: ['overcharge', 'knowledge_bolt'], hint: 'Overcharged tools empower the Inkblood\'s knowledge bolt to strike farther.', effect: '+5 bonus damage' },
  ];

  function sortedSynergies() {
    const discovered = ALL_SYNERGIES.filter(s => discoveredOrder.includes(s.id));
    const undiscovered = ALL_SYNERGIES.filter(s => !discoveredOrder.includes(s.id));
    discovered.sort((a, b) => discoveredOrder.indexOf(a.id) - discoveredOrder.indexOf(b.id));
    return [...discovered, ...undiscovered];
  }

  const discoveredCount = $derived(discoveredOrder.length);
  const totalCount = ALL_SYNERGIES.length;

  function handleKeyDown(e: KeyboardEvent) {
    if (e.key === 'j' || e.key === 'J' || e.key === 'Escape') {
      e.preventDefault();
      onClose();
    }
  }
</script>

<svelte:window onkeydown={handleKeyDown} />

<div class="field-notes-overlay" role="dialog" aria-label="Field Notes">
  <div class="field-notes-card">
    <div class="field-notes-header">
      <h2 class="field-notes-title">Field Notes</h2>
      <span class="field-notes-count">{discoveredCount}/{totalCount} discovered</span>
      <button class="field-notes-close" onclick={onClose}>Close</button>
    </div>
    <div class="field-notes-list">
      {#each sortedSynergies() as synergy}
        {@const revealed = revealedIds.has(synergy.id)}
        <div class="field-note-entry" class:revealed>
          <div class="field-note-names">
            {#if revealed}
              {synergy.abilities.join(' + ')}
            {:else}
              ??? + ???
            {/if}
          </div>
          {#if revealed}
            <div class="field-note-hint">{synergy.hint}</div>
            <div class="field-note-effect">{synergy.effect}</div>
            <button class="replay-btn" onclick={() => onReplay(synergy.id)}>Replay</button>
          {:else}
            <div class="field-note-locked">Undiscovered synergy</div>
          {/if}
        </div>
      {/each}
    </div>
  </div>
</div>

<style>
  .field-notes-overlay {
    position: fixed;
    inset: 0;
    display: flex;
    align-items: center;
    justify-content: center;
    background: rgba(0, 0, 0, 0.85);
    z-index: 55;
    pointer-events: auto;
  }

  .field-notes-card {
    background: #1a1a2e;
    border: 1px solid #444;
    border-radius: 0.5rem;
    padding: 1.5rem;
    min-width: 300px;
    max-width: 90vw;
    max-height: 80vh;
    overflow-y: auto;
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
  }

  .field-notes-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    border-bottom: 1px solid #444;
    padding-bottom: 0.5rem;
    gap: 0.5rem;
  }

  .field-notes-title {
    margin: 0;
    font-size: 1.25rem;
    color: #d4a84b;
  }

  .field-notes-count {
    color: #888;
    font-size: 0.8rem;
    margin-left: auto;
  }

  .field-notes-close {
    background: transparent;
    border: 1px solid #666;
    border-radius: 0.25rem;
    color: #ccc;
    cursor: pointer;
    padding: 0.25rem 0.5rem;
    font-size: 0.75rem;
  }

  .field-notes-close:hover {
    border-color: #888;
  }

  .field-notes-list {
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
  }

  .field-note-entry {
    padding: 0.75rem;
    background: rgba(255, 255, 255, 0.03);
    border: 1px solid #333;
    border-radius: 0.375rem;
  }

  .field-note-entry.revealed {
    border-color: #d4a84b;
    background: rgba(212, 168, 75, 0.05);
  }

  .field-note-names {
    font-weight: bold;
    color: #eee;
    font-size: 0.9rem;
    margin-bottom: 0.25rem;
  }

  .field-note-hint {
    color: #aaa;
    font-size: 0.8rem;
    font-style: italic;
    margin-bottom: 0.25rem;
  }

  .field-note-effect {
    color: #88cc88;
    font-size: 0.8rem;
    margin-bottom: 0.5rem;
  }

  .field-note-locked {
    color: #666;
    font-size: 0.8rem;
    font-style: italic;
  }

  .replay-btn {
    padding: 0.25rem 0.75rem;
    background: rgba(68, 170, 255, 0.15);
    border: 1px solid #44aaff;
    border-radius: 0.25rem;
    color: #66aaff;
    cursor: pointer;
    font-size: 0.75rem;
  }

  .replay-btn:hover {
    background: rgba(68, 170, 255, 0.3);
  }
</style>
