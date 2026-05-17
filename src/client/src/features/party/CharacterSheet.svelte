<script lang="ts">
  import type { PartyMember } from '$shared/types/game';

  interface Props {
    member: PartyMember;
    onClose: () => void;
    onSwapRow: (slot: number) => void;
    onTransferToCache: (itemId: string, count: number) => void;
    onTransferFromCache: (itemId: string, count: number) => void;
    expeditionCache: import('$shared/types/game').ComponentStack[];
  }

  let { member, onClose, onSwapRow, onTransferToCache, onTransferFromCache, expeditionCache }: Props = $props();

  function getStockColor(count: number): string {
    if (count <= 1) return '#ff4444';
    if (count <= 2) return '#ff8800';
    if (count <= 3) return '#ffcc00';
    return '#aaaaaa';
  }

  const statLabels: Record<string, string> = {
    strength: 'STR',
    dexterity: 'DEX',
    constitution: 'CON',
    intelligence: 'INT',
    willpower: 'WIL',
  };

  const equipmentSlots: { key: keyof PartyMember['equipment']; label: string }[] = [
    { key: 'mainHand', label: 'Main Hand' },
    { key: 'offHand', label: 'Off Hand' },
    { key: 'armor', label: 'Armor' },
    { key: 'accessory1', label: 'Accessory 1' },
    { key: 'accessory2', label: 'Accessory 2' },
  ];

  function getItemName(itemId: string | null): string {
    if (!itemId) return 'Empty';
    return itemId;
  }

  const xpThresholds = [0, 100, 250, 500, 1000, 2000];
  function getXpPercent(): number {
    if (member.level >= xpThresholds.length - 1) return 100;
    const current = xpThresholds[member.level - 1] || 0;
    const next = xpThresholds[member.level] || current + 1000;
    const gained = member.xp - current;
    const needed = next - current;
    return Math.min(100, Math.max(0, (gained / needed) * 100));
  }
</script>

<div
  class="sheet-backdrop"
  onclick={(e) => { if (e.target === e.currentTarget) onClose(); }}
  role="presentation"
>
  <div
    class="sheet-panel"
    role="dialog"
    aria-modal="true"
    aria-label="{member.name} — Character Sheet"
    onkeydown={(e) => { if (e.key === 'Escape') { e.preventDefault(); onClose(); } }}
    tabindex="-1"
  >
    <button class="close-btn" onclick={onClose} aria-label="Close character sheet">×</button>

    <div class="sheet-header">
      <div class="portrait" style="background-color: {member.color}" aria-hidden="true"></div>
      <div class="header-info">
        <div class="name" id="sheet-title-{member.slot}">{member.name}</div>
        <div class="class-line">{member.className} · Lv.{member.level}</div>
        <div class="row-line">
          Row: {member.row === 0 ? 'Front' : 'Back'}
          <button class="swap-btn" onclick={() => onSwapRow(member.slot)}>Swap</button>
        </div>
      </div>
    </div>

    <div class="xp-bar">
      <div class="xp-fill" style="width: {getXpPercent()}%"></div>
      <div class="xp-text">XP: {member.xp}</div>
    </div>

    <div class="sheet-section">
      <h3>Stats</h3>
      <div class="stats-grid">
        {#each Object.entries(statLabels) as [key, label]}
          <div class="stat-box">
            <span class="stat-label">{label}</span>
            <span class="stat-value">{member.stats[key as keyof PartyMember['stats']]}</span>
          </div>
        {/each}
      </div>
      <div class="derived-stats">
        <span>Speed: {member.stats.speed}</span>
        <span>Accuracy: {member.stats.accuracy}</span>
        <span>Evade: {member.stats.evade}</span>
        <span>Power: {member.stats.power}</span>
      </div>
    </div>

    <div class="sheet-section">
      <h3>Equipment</h3>
      <div class="equipment-list">
        {#each equipmentSlots as slot}
          <div class="equip-slot">
            <span class="equip-label">{slot.label}</span>
            <span class="equip-value">{getItemName(member.equipment[slot.key])}</span>
          </div>
        {/each}
      </div>
    </div>

    <div class="sheet-section">
      <h3>Abilities</h3>
      <div class="ability-list">
        {#each member.knownAbilities as ability}
          <div class="ability-tag">{ability}</div>
        {/each}
      </div>
    </div>

    <div class="sheet-section">
      <h3>Components</h3>
      {#if member.componentInventory.length === 0}
        <div class="empty-inventory">No components</div>
      {:else}
        <div class="component-list">
          {#each member.componentInventory as stack}
            <div class="component-row">
              <span class="component-name" style="color: {getStockColor(stack.count)}">{stack.itemId}</span>
              <span class="component-count" style="color: {getStockColor(stack.count)}">{stack.count}/{stack.maxStack}</span>
              <button class="transfer-btn" onclick={() => onTransferToCache(stack.itemId, Math.min(stack.count, 5))}>→ Cache</button>
            </div>
          {/each}
        </div>
      {/if}
    </div>

    {#if expeditionCache.length > 0}
      <div class="sheet-section">
        <h3>Expedition Cache</h3>
        <div class="component-list">
          {#each expeditionCache as stack}
            <div class="component-row">
              <span class="component-name">{stack.itemId}</span>
              <span class="component-count">{stack.count}/{stack.maxStack}</span>
              <button class="transfer-btn" onclick={() => onTransferFromCache(stack.itemId, Math.min(stack.count, 5))}>→ {member.name}</button>
            </div>
          {/each}
        </div>
      </div>
    {/if}
  </div>
</div>

<style>
  .sheet-backdrop {
    position: fixed;
    inset: 0;
    background: rgba(0, 0, 0, 0.7);
    display: grid;
    place-items: center;
    z-index: 100;
    padding: 1rem;
  }

  .sheet-panel {
    background: rgba(20, 20, 25, 0.95);
    border: 0.0625em solid #444;
    border-radius: 0.5rem;
    padding: 1.25rem;
    width: min(24rem, 90vw);
    max-height: 85vh;
    overflow-y: auto;
    position: relative;
    display: flex;
    flex-direction: column;
    gap: 1rem;
  }

  .close-btn {
    position: absolute;
    top: 0.5rem;
    right: 0.5rem;
    width: 1.75rem;
    height: 1.75rem;
    background: rgba(255, 255, 255, 0.1);
    border: 0.0625em solid #555;
    border-radius: 50%;
    color: #ccc;
    font-size: 1rem;
    cursor: pointer;
    display: flex;
    align-items: center;
    justify-content: center;
    line-height: 1;
  }

  .close-btn:hover {
    background: rgba(255, 100, 100, 0.3);
    border-color: #a44;
  }

  .sheet-header {
    display: flex;
    gap: 0.75rem;
    align-items: center;
  }

  .portrait {
    width: clamp(3rem, 8vw, 4rem);
    height: clamp(3rem, 8vw, 4rem);
    border-radius: 50%;
    border: 0.125em solid #666;
    flex-shrink: 0;
  }

  .header-info {
    display: flex;
    flex-direction: column;
    gap: 0.15rem;
  }

  .name {
    font-size: clamp(1rem, 2.5vw, 1.25rem);
    font-weight: bold;
    color: #eee;
  }

  .class-line {
    font-size: clamp(0.75rem, 1.8vw, 0.875rem);
    color: #aaa;
  }

  .row-line {
    font-size: clamp(0.7rem, 1.5vw, 0.8rem);
    color: #888;
    display: flex;
    align-items: center;
    gap: 0.5rem;
  }

  .swap-btn {
    padding: 0.15rem 0.4rem;
    background: rgba(68, 170, 255, 0.15);
    border: 0.0625em solid #4488aa;
    border-radius: 0.2rem;
    color: #88ccff;
    font-size: clamp(0.6rem, 1.2vw, 0.7rem);
    cursor: pointer;
  }

  .swap-btn:hover {
    background: rgba(68, 170, 255, 0.3);
  }

  .xp-bar {
    position: relative;
    height: clamp(0.75rem, 1.5vh, 1rem);
    background: rgba(0, 0, 0, 0.5);
    border-radius: 0.25rem;
    overflow: hidden;
  }

  .xp-fill {
    height: 100%;
    background: linear-gradient(90deg, #d4a84b, #e8c87a);
    transition: width 0.3s ease;
  }

  .xp-text {
    position: absolute;
    inset: 0;
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: clamp(0.6rem, 1.2vw, 0.7rem);
    color: #fff;
    text-shadow: 0 0 0.1em rgba(0, 0, 0, 0.8);
    pointer-events: none;
  }

  .sheet-section h3 {
    margin: 0 0 0.5rem;
    font-size: clamp(0.8rem, 1.8vw, 0.9rem);
    color: #ccc;
    border-bottom: 0.0625em solid #333;
    padding-bottom: 0.25rem;
  }

  .stats-grid {
    display: grid;
    grid-template-columns: repeat(5, 1fr);
    gap: 0.5rem;
  }

  .stat-box {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 0.1rem;
    padding: 0.375rem;
    background: rgba(255, 255, 255, 0.03);
    border-radius: 0.25rem;
  }

  .stat-label {
    font-size: clamp(0.55rem, 1vw, 0.65rem);
    color: #888;
  }

  .stat-value {
    font-size: clamp(0.85rem, 1.8vw, 1rem);
    font-weight: bold;
    color: #ddd;
  }

  .derived-stats {
    display: flex;
    flex-wrap: wrap;
    gap: 0.5rem;
    margin-top: 0.5rem;
    font-size: clamp(0.65rem, 1.3vw, 0.75rem);
    color: #888;
    justify-content: center;
  }

  .equipment-list {
    display: flex;
    flex-direction: column;
    gap: 0.25rem;
  }

  .equip-slot {
    display: flex;
    justify-content: space-between;
    padding: 0.3rem 0.5rem;
    background: rgba(255, 255, 255, 0.03);
    border-radius: 0.2rem;
    font-size: clamp(0.7rem, 1.5vw, 0.8rem);
  }

  .equip-label {
    color: #888;
  }

  .equip-value {
    color: #ccc;
    font-style: italic;
  }

  .ability-list {
    display: flex;
    flex-wrap: wrap;
    gap: 0.35rem;
  }

  .ability-tag {
    padding: 0.2rem 0.5rem;
    background: rgba(100, 100, 100, 0.2);
    border: 0.0625em solid #555;
    border-radius: 0.2rem;
    font-size: clamp(0.65rem, 1.3vw, 0.75rem);
    color: #bbb;
  }

  .empty-inventory {
    font-size: clamp(0.7rem, 1.5vw, 0.8rem);
    color: #666;
    font-style: italic;
    padding: 0.5rem;
    text-align: center;
  }

  .component-list {
    display: flex;
    flex-direction: column;
    gap: 0.25rem;
  }

  .component-row {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 0.3rem 0.5rem;
    background: rgba(255, 255, 255, 0.03);
    border-radius: 0.2rem;
    font-size: clamp(0.7rem, 1.5vw, 0.8rem);
    gap: 0.5rem;
  }

  .component-name {
    flex: 1;
    text-transform: capitalize;
  }

  .component-count {
    font-variant-numeric: tabular-nums;
  }

  .transfer-btn {
    padding: 0.15rem 0.4rem;
    background: rgba(68, 170, 255, 0.15);
    border: 0.0625em solid #4488aa;
    border-radius: 0.2rem;
    color: #88ccff;
    font-size: clamp(0.6rem, 1.2vw, 0.7rem);
    cursor: pointer;
    white-space: nowrap;
  }

  .transfer-btn:hover {
    background: rgba(68, 170, 255, 0.3);
  }
</style>
