<script lang="ts">
  import type { GameState, PartyMember, ComponentStack, Equipment } from '$shared/types/game';
  import type { UiIntent } from '$shared/actions/uiIntent';

  interface Props {
    gameState: GameState | null;
    onIntent: (intent: UiIntent) => void;
  }

  let { gameState, onIntent }: Props = $props();

  const BAG_SLOTS = 8;
  const CACHE_SLOTS = 12;

  type MoveQty = 'one' | 'five' | 'all';

  const members = $derived<PartyMember[]>(gameState?.party ?? []);
  const cache = $derived<ComponentStack[]>(gameState?.expeditionCache ?? []);

  let selectedSlot = $state<number | null>(null);
  let moveQty = $state<MoveQty>('one');

  /** The member whose bag is currently shown — defaults to the first party member. */
  const selected = $derived<PartyMember | null>(
    members.find((m) => m.slot === selectedSlot) ?? members[0] ?? null
  );

  const bag = $derived<ComponentStack[]>(selected?.componentInventory ?? []);

  function stockColor(count: number): string {
    if (count <= 1) return '#ff4444';
    if (count <= 2) return '#ff8800';
    if (count <= 3) return '#ffcc00';
    return '#aaaaaa';
  }

  function displayName(stack: ComponentStack): string {
    return stack.name ?? stack.itemId;
  }

  /** Amount to move for a given stack under the current quantity setting. */
  function moveAmount(stack: ComponentStack): number {
    if (moveQty === 'all') return stack.count;
    if (moveQty === 'five') return Math.min(5, stack.count);
    return 1;
  }

  function toCache(stack: ComponentStack) {
    if (!selected) return;
    onIntent({
      kind: 'transferToCache',
      slot: selected.slot,
      itemId: stack.itemId,
      count: moveAmount(stack),
    });
  }

  function toBag(stack: ComponentStack) {
    if (!selected) return;
    onIntent({
      kind: 'transferFromCache',
      slot: selected.slot,
      itemId: stack.itemId,
      count: moveAmount(stack),
    });
  }

  const equipmentSlots: { key: keyof Equipment; label: string }[] = [
    { key: 'mainHand', label: 'Main Hand' },
    { key: 'offHand', label: 'Off Hand' },
    { key: 'armor', label: 'Armor' },
    { key: 'accessory1', label: 'Accessory 1' },
    { key: 'accessory2', label: 'Accessory 2' },
  ];

  /**
   * Resolve the target equipment slot for a stack. The server reports the primary slot
   * (accessory1 for accessories); when that is occupied but the secondary accessory slot
   * is free, prefer the free one so a second accessory can be equipped.
   */
  function resolveEquipSlot(member: PartyMember, stack: ComponentStack): string | null {
    const primary = stack.equipSlot;
    if (!primary) return null;
    if (primary === 'accessory1' && member.equipment.accessory1 && !member.equipment.accessory2) {
      return 'accessory2';
    }
    return primary;
  }

  function equip(stack: ComponentStack) {
    if (!selected) return;
    const slot = resolveEquipSlot(selected, stack);
    if (!slot) return;
    onIntent({ kind: 'equipItem', characterId: selected.id, itemId: stack.itemId, slot });
  }

  function unequip(slotKey: string) {
    if (!selected) return;
    onIntent({ kind: 'unequipItem', characterId: selected.id, slot: slotKey });
  }

  const bagEmptyCount = $derived(Math.max(0, BAG_SLOTS - bag.length));
  const cacheEmptyCount = $derived(Math.max(0, CACHE_SLOTS - cache.length));
</script>

<div class="inventory-screen">
  <div class="inv-toolbar">
    <h2 class="inv-title">Inventory</h2>
    <label class="inv-qty">
      <span>Move</span>
      <select class="inv-qty-select" bind:value={moveQty}>
        <option value="one">1</option>
        <option value="five">5</option>
        <option value="all">All</option>
      </select>
    </label>
  </div>

  {#if members.length === 0}
    <div class="inv-empty-state">No party members.</div>
  {:else}
    <div class="inv-member-tabs" role="tablist" aria-label="Party member bags">
      {#each members as m (m.id)}
        <button
          type="button"
          class="inv-member-tab"
          class:selected={selected?.id === m.id}
          role="tab"
          aria-selected={selected?.id === m.id}
          onclick={() => (selectedSlot = m.slot)}
        >
          <span class="inv-member-dot" style="background-color: {m.color}" aria-hidden="true"></span>
          {m.name}
        </button>
      {/each}
    </div>

    <div class="inv-columns">
      <!-- Character bag -->
      <section class="inv-panel inv-bag" aria-label="Character bag">
        <header class="inv-panel-head">
          <h3>{selected?.name}'s Bag</h3>
          <span class="inv-fill">{bag.length} / {BAG_SLOTS}</span>
        </header>
        <div class="inv-grid">
          {#each bag as stack (stack.itemId)}
            <div class="inv-stack">
              <div class="inv-stack-info">
                <span class="inv-stack-name" style="color: {stockColor(stack.count)}">{displayName(stack)}</span>
                <span class="inv-stack-count" style="color: {stockColor(stack.count)}">{stack.count}/{stack.maxStack}</span>
              </div>
              <div class="inv-stack-actions">
                {#if stack.equipSlot && selected}
                  <button type="button" class="inv-equip-btn" onclick={() => equip(stack)}>Equip</button>
                {/if}
                <button type="button" class="inv-to-cache-btn" onclick={() => toCache(stack)}>→ Cache</button>
              </div>
            </div>
          {/each}
          {#each Array(bagEmptyCount) as _, i (i)}
            <div class="inv-stack inv-stack-empty" aria-label="Empty bag slot"><span>Empty</span></div>
          {/each}
        </div>

        {#if selected}
          <div class="inv-equipment">
            <h4>Equipped</h4>
            {#each equipmentSlots as eslot}
              <div class="inv-equip-slot">
                <span class="inv-equip-label">{eslot.label}</span>
                <span class="inv-equip-value">{selected.equipment[eslot.key] ?? 'Empty'}</span>
                {#if selected.equipment[eslot.key]}
                  <button type="button" class="inv-unequip-btn" onclick={() => unequip(eslot.key)}>Unequip</button>
                {/if}
              </div>
            {/each}
          </div>
        {/if}
      </section>

      <!-- Expedition cache -->
      <section class="inv-panel inv-cache" aria-label="Expedition cache">
        <header class="inv-panel-head">
          <h3>Expedition Cache</h3>
          <span class="inv-fill">{cache.length} / {CACHE_SLOTS}</span>
        </header>
        <div class="inv-grid">
          {#each cache as stack (stack.itemId)}
            <div class="inv-stack">
              <div class="inv-stack-info">
                <span class="inv-stack-name" style="color: {stockColor(stack.count)}">{displayName(stack)}</span>
                <span class="inv-stack-count" style="color: {stockColor(stack.count)}">{stack.count}/{stack.maxStack}</span>
              </div>
              <div class="inv-stack-actions">
                <button type="button" class="inv-to-bag-btn" onclick={() => toBag(stack)} disabled={!selected}>
                  → {selected?.name ?? 'Bag'}
                </button>
              </div>
            </div>
          {/each}
          {#each Array(cacheEmptyCount) as _, i (i)}
            <div class="inv-stack inv-stack-empty" aria-label="Empty cache slot"><span>Empty</span></div>
          {/each}
        </div>
      </section>
    </div>
  {/if}
</div>

<style>
  .inventory-screen {
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
    min-height: 0;
  }

  .inv-toolbar {
    display: flex;
    align-items: center;
    gap: 0.75rem;
  }

  .inv-title {
    margin: 0;
    font-size: clamp(0.875rem, 2vw, 1.1rem);
    color: #d4a84b;
  }

  .inv-qty {
    margin-left: auto;
    display: flex;
    align-items: center;
    gap: 0.35rem;
    font-size: clamp(0.55rem, 1vw, 0.7rem);
    color: #888;
    text-transform: uppercase;
    letter-spacing: 0.03em;
  }

  .inv-qty-select {
    background: rgba(20, 16, 12, 0.9);
    border: 0.0625em solid #444;
    border-radius: 0.25rem;
    color: #ccc;
    padding: 0.2rem 0.4rem;
    font-size: 0.75rem;
  }

  .inv-empty-state {
    color: #666;
    font-style: italic;
    padding: 1rem;
    text-align: center;
  }

  .inv-member-tabs {
    display: flex;
    flex-wrap: wrap;
    gap: 0.35rem;
  }

  .inv-member-tab {
    display: flex;
    align-items: center;
    gap: 0.35rem;
    padding: 0.25rem 0.6rem;
    background: rgba(255, 255, 255, 0.04);
    border: 0.0625em solid #444;
    border-radius: 0.3rem;
    color: #bbb;
    font-size: clamp(0.65rem, 1.3vw, 0.8rem);
    cursor: pointer;
  }

  .inv-member-tab.selected {
    border-color: #d4a84b;
    color: #f0d9a8;
    background: rgba(212, 168, 75, 0.12);
  }

  .inv-member-dot {
    width: 0.7rem;
    height: 0.7rem;
    border-radius: 50%;
    border: 0.0625em solid #666;
    flex-shrink: 0;
  }

  .inv-columns {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(clamp(14rem, 40vw, 22rem), 1fr));
    gap: 0.75rem;
    min-height: 0;
  }

  .inv-panel {
    display: flex;
    flex-direction: column;
    gap: 0.5rem;
    padding: 0.5rem;
    background: rgba(255, 255, 255, 0.03);
    border: 0.0625em solid #444;
    border-radius: 0.375rem;
  }

  .inv-cache {
    border-color: #446;
  }

  .inv-panel-head {
    display: flex;
    align-items: center;
    justify-content: space-between;
  }

  .inv-panel-head h3 {
    margin: 0;
    font-size: clamp(0.75rem, 1.6vw, 0.9rem);
    color: #ccc;
  }

  .inv-fill {
    font-size: clamp(0.6rem, 1.2vw, 0.75rem);
    color: #888;
    font-variant-numeric: tabular-nums;
  }

  .inv-grid {
    display: flex;
    flex-direction: column;
    gap: 0.25rem;
  }

  .inv-stack {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 0.5rem;
    padding: 0.3rem 0.5rem;
    background: rgba(255, 255, 255, 0.03);
    border-radius: 0.2rem;
    font-size: clamp(0.7rem, 1.5vw, 0.8rem);
  }

  .inv-stack-empty {
    justify-content: center;
    color: #555;
    font-style: italic;
    border: 0.0625em dashed #333;
    background: transparent;
  }

  .inv-stack-info {
    display: flex;
    align-items: center;
    gap: 0.5rem;
    min-width: 0;
  }

  .inv-stack-name {
    text-transform: capitalize;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .inv-stack-count {
    font-variant-numeric: tabular-nums;
    flex-shrink: 0;
  }

  .inv-stack-actions {
    display: flex;
    gap: 0.3rem;
    flex-shrink: 0;
  }

  .inv-stack-actions button,
  .inv-unequip-btn {
    padding: 0.15rem 0.4rem;
    background: rgba(68, 170, 255, 0.15);
    border: 0.0625em solid #4488aa;
    border-radius: 0.2rem;
    color: #88ccff;
    font-size: clamp(0.6rem, 1.2vw, 0.7rem);
    cursor: pointer;
    white-space: nowrap;
  }

  .inv-stack-actions button:hover,
  .inv-unequip-btn:hover {
    background: rgba(68, 170, 255, 0.3);
  }

  .inv-stack-actions button:disabled {
    opacity: 0.4;
    cursor: not-allowed;
  }

  .inv-equip-btn {
    background: rgba(68, 170, 68, 0.18) !important;
    border-color: #44aa44 !important;
    color: #88cc88 !important;
  }

  .inv-equipment {
    margin-top: 0.25rem;
    border-top: 0.0625em solid #333;
    padding-top: 0.5rem;
    display: flex;
    flex-direction: column;
    gap: 0.25rem;
  }

  .inv-equipment h4 {
    margin: 0 0 0.25rem;
    font-size: clamp(0.65rem, 1.3vw, 0.75rem);
    color: #999;
  }

  .inv-equip-slot {
    display: flex;
    align-items: center;
    gap: 0.5rem;
    padding: 0.25rem 0.4rem;
    background: rgba(255, 255, 255, 0.03);
    border-radius: 0.2rem;
    font-size: clamp(0.65rem, 1.3vw, 0.75rem);
  }

  .inv-equip-label {
    color: #888;
    flex-shrink: 0;
    min-width: 5.5rem;
  }

  .inv-equip-value {
    color: #ccc;
    font-style: italic;
    flex: 1;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .inv-unequip-btn {
    flex-shrink: 0;
  }
</style>
