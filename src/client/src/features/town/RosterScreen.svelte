<script lang="ts">
  import type { GameState, PartyMember, BenchMember } from '$shared/types/game';
  import { modal } from '$shared/actions/modal';
  import type { UiIntent } from '$shared/actions/uiIntent';

  interface Props {
    gameState: GameState | null;
    onIntent: (intent: UiIntent) => void;
  }

  let { gameState, onIntent }: Props = $props();

  const ACTIVE_SLOTS = 6;

  type SortKey = 'slot' | 'class' | 'level' | 'hp' | 'branch';

  /** A unified roster entry covering both active party members and bench members. */
  interface RosterEntry {
    id: string;
    name: string;
    className: string;
    color: string;
    level: number;
    hp: number;
    maxHp: number;
    alive: boolean;
    branch: string | null;
    location: 'active' | 'bench';
    /** Active party slot (0-5); undefined for bench members. */
    slot?: number;
  }

  let filterClass = $state<string>('all');
  let sortKey = $state<SortKey>('slot');
  let pendingActivateId = $state<string | null>(null);
  let confirmDismiss = $state<RosterEntry | null>(null);

  const activeMembers = $derived<PartyMember[]>(gameState?.party ?? []);
  const benchMembers = $derived<BenchMember[]>(gameState?.bench ?? []);
  const rosterInfo = $derived(gameState?.rosterInfo);
  const maxRosterSize = $derived(rosterInfo?.maxRosterSize ?? 12);

  function formatBranch(branchId: string | null | undefined): string | null {
    if (!branchId) return null;
    return branchId
      .split('_')
      .map((w) => w.charAt(0).toUpperCase() + w.slice(1))
      .join(' ');
  }

  const entries = $derived<RosterEntry[]>([
    ...activeMembers.map((m) => ({
      id: m.id,
      name: m.name,
      className: m.className,
      color: m.color,
      level: m.level,
      hp: m.hp,
      maxHp: m.maxHp,
      alive: m.alive,
      branch: formatBranch(m.branchLevel6 ?? m.branchChoice),
      location: 'active' as const,
      slot: m.slot,
    })),
    ...benchMembers.map((b) => ({
      id: b.id,
      name: b.name,
      className: b.className,
      color: b.color,
      level: b.level,
      hp: b.hp,
      maxHp: b.maxHp,
      alive: b.alive,
      branch: formatBranch(b.branchChoice),
      location: 'bench' as const,
    })),
  ]);

  const classOptions = $derived(
    Array.from(new Set(entries.map((e) => e.className))).sort()
  );

  const filtered = $derived(
    filterClass === 'all'
      ? entries
      : entries.filter((e) => e.className === filterClass)
  );

  const sorted = $derived(
    [...filtered].sort((a, b) => {
      switch (sortKey) {
        case 'class':
          return a.className.localeCompare(b.className) || a.name.localeCompare(b.name);
        case 'level':
          return b.level - a.level || a.name.localeCompare(b.name);
        case 'hp':
          return b.hp - a.hp || a.name.localeCompare(b.name);
        case 'branch':
          return (a.branch ?? '~').localeCompare(b.branch ?? '~') || a.name.localeCompare(b.name);
        case 'slot':
        default:
          // Active (by slot) first, then bench in given order.
          if (a.location !== b.location) return a.location === 'active' ? -1 : 1;
          if (a.location === 'active' && b.location === 'active') {
            return (a.slot ?? 0) - (b.slot ?? 0);
          }
          return 0;
      }
    })
  );

  /** Empty placeholder slots fill the grid up to the roster cap (only when unfiltered). */
  const emptySlotCount = $derived(
    filterClass === 'all' ? Math.max(0, maxRosterSize - entries.length) : 0
  );

  /** Lowest active slot (0-5) not occupied by an active member, or null when full. */
  const firstEmptyActiveSlot = $derived.by(() => {
    for (let s = 0; s < ACTIVE_SLOTS; s++) {
      if (!activeMembers.some((m) => m.slot === s)) return s;
    }
    return null;
  });

  function benchOut(entry: RosterEntry) {
    if (entry.slot === undefined) return;
    onIntent({ kind: 'swapActiveBench', activeSlot: entry.slot });
  }

  function activate(entry: RosterEntry) {
    const empty = firstEmptyActiveSlot;
    if (empty !== null) {
      onIntent({ kind: 'swapActiveBench', activeSlot: empty, benchCharacterId: entry.id });
      pendingActivateId = null;
      return;
    }
    // Active party full — ask the player which active member to swap out.
    pendingActivateId = entry.id;
  }

  function swapInto(activeEntry: RosterEntry) {
    if (pendingActivateId === null || activeEntry.slot === undefined) return;
    onIntent({
      kind: 'swapActiveBench',
      activeSlot: activeEntry.slot,
      benchCharacterId: pendingActivateId,
    });
    pendingActivateId = null;
  }

  function requestDismiss(entry: RosterEntry) {
    confirmDismiss = entry;
  }

  function performDismiss() {
    if (!confirmDismiss) return;
    onIntent({ kind: 'dismissCharacter', characterId: confirmDismiss.id });
    confirmDismiss = null;
  }
</script>

<div class="roster-screen">
  <div class="roster-toolbar">
    <h2 class="roster-title">Roster</h2>
    <span class="roster-capacity">
      {entries.length} / {maxRosterSize}
    </span>
    <div class="roster-controls">
      <label class="roster-control">
        <span>Class</span>
        <select class="roster-filter-class" bind:value={filterClass}>
          <option value="all">All</option>
          {#each classOptions as cls}
            <option value={cls}>{cls}</option>
          {/each}
        </select>
      </label>
      <label class="roster-control">
        <span>Sort</span>
        <select class="roster-sort" bind:value={sortKey}>
          <option value="slot">Default</option>
          <option value="class">Class</option>
          <option value="level">Level</option>
          <option value="hp">HP</option>
          <option value="branch">Branch</option>
        </select>
      </label>
    </div>
  </div>

  {#if pendingActivateId}
    <div class="roster-pending-banner" role="status">
      Active party is full — select an active member to swap out.
      <button type="button" class="roster-pending-cancel" onclick={() => (pendingActivateId = null)}>
        Cancel
      </button>
    </div>
  {/if}

  <div class="roster-grid" role="list" aria-label="Roster slots">
    {#each sorted as entry (entry.id)}
      <div
        class="roster-card"
        class:active={entry.location === 'active'}
        class:bench={entry.location === 'bench'}
        class:dead={!entry.alive}
        role="listitem"
      >
        <div class="roster-card-portrait" style="background-color: {entry.color}" aria-hidden="true">
          <span class="roster-card-initial">{entry.name.charAt(0)}</span>
        </div>
        <span class="roster-badge">{entry.location === 'active' ? 'Active' : 'Bench'}</span>
        <div class="roster-card-body">
          <div class="roster-card-name">{entry.name}</div>
          <div class="roster-card-class">Lv.{entry.level} {entry.className}</div>
          <div class="roster-card-branch">{entry.branch ?? 'No path'}</div>
          <div class="roster-card-hp">HP {entry.hp}/{entry.maxHp}</div>
        </div>
        <div class="roster-card-actions">
          {#if pendingActivateId && entry.location === 'active'}
            <button type="button" class="roster-swap-target-btn" onclick={() => swapInto(entry)}>
              Swap here
            </button>
          {:else if entry.location === 'active'}
            <button type="button" class="roster-bench-btn" onclick={() => benchOut(entry)}>
              Bench
            </button>
          {:else}
            <button type="button" class="roster-activate-btn" onclick={() => activate(entry)}>
              Activate
            </button>
          {/if}
          <button type="button" class="roster-dismiss-btn" onclick={() => requestDismiss(entry)}>
            Dismiss
          </button>
        </div>
      </div>
    {/each}

    {#each Array(emptySlotCount) as _, i (i)}
      <div class="roster-card roster-card-empty" role="listitem" aria-label="Empty roster slot">
        <span class="roster-empty-label">Empty</span>
      </div>
    {/each}
  </div>
</div>

{#if confirmDismiss}
  <div class="roster-confirm-overlay" role="dialog" aria-label="Confirm dismissal" aria-modal="true" tabindex="-1" use:modal>
    <div class="roster-confirm">
      <h3 class="roster-confirm-title">Dismiss {confirmDismiss.name}?</h3>
      <p class="roster-confirm-text">
        This permanently removes {confirmDismiss.name} (Lv.{confirmDismiss.level}
        {confirmDismiss.className}) from your roster. This cannot be undone.
      </p>
      <div class="roster-confirm-actions">
        <button type="button" class="roster-confirm-cancel" onclick={() => (confirmDismiss = null)}>
          Cancel
        </button>
        <button type="button" class="roster-confirm-yes" onclick={performDismiss}>
          Dismiss
        </button>
      </div>
    </div>
  </div>
{/if}

<style>
  .roster-screen {
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
    min-height: 0;
  }

  .roster-toolbar {
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    gap: 0.75rem;
  }

  .roster-title {
    margin: 0;
    font-size: clamp(0.875rem, 2vw, 1.1rem);
    color: #d4a84b;
  }

  .roster-capacity {
    padding: 0.2rem 0.5rem;
    background: rgba(212, 168, 75, 0.12);
    border: 0.0625em solid #d4a84b;
    border-radius: 0.25rem;
    color: #d4a84b;
    font-size: clamp(0.65rem, 1.2vw, 0.8rem);
  }

  .roster-controls {
    display: flex;
    gap: 0.75rem;
    margin-left: auto;
  }

  .roster-control {
    display: flex;
    flex-direction: column;
    gap: 0.15rem;
    font-size: clamp(0.55rem, 1vw, 0.65rem);
    color: #888;
    text-transform: uppercase;
    letter-spacing: 0.03em;
  }

  .roster-control select {
    background: rgba(20, 16, 12, 0.9);
    border: 0.0625em solid #444;
    border-radius: 0.25rem;
    color: #ccc;
    padding: 0.2rem 0.4rem;
    font-size: 0.75rem;
  }

  .roster-pending-banner {
    display: flex;
    align-items: center;
    gap: 0.75rem;
    padding: 0.4rem 0.75rem;
    background: rgba(212, 168, 75, 0.12);
    border: 0.0625em solid #d4a84b;
    border-radius: 0.25rem;
    color: #d4a84b;
    font-size: clamp(0.65rem, 1.2vw, 0.8rem);
  }

  .roster-pending-cancel {
    margin-left: auto;
    padding: 0.2rem 0.5rem;
    background: rgba(255, 255, 255, 0.05);
    border: 0.0625em solid #555;
    border-radius: 0.2rem;
    color: #ccc;
    cursor: pointer;
    font-size: 0.7rem;
  }

  .roster-grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(clamp(8rem, 16vw, 11rem), 1fr));
    gap: 0.5rem;
    overflow-y: auto;
    padding-right: 0.25rem;
    min-height: 0;
  }

  .roster-card {
    position: relative;
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 0.35rem;
    padding: 0.5rem;
    background: rgba(255, 255, 255, 0.05);
    border: 0.0625em solid #444;
    border-radius: 0.375rem;
  }

  .roster-card.active {
    border-color: #446644;
  }

  .roster-card.bench {
    border-color: #444466;
  }

  .roster-card.dead {
    opacity: 0.55;
  }

  .roster-card-empty {
    align-items: center;
    justify-content: center;
    min-height: 6rem;
    border-style: dashed;
    color: #555;
  }

  .roster-empty-label {
    font-size: 0.7rem;
    color: #555;
  }

  .roster-card-portrait {
    width: clamp(2rem, 4vw, 2.75rem);
    height: clamp(2rem, 4vw, 2.75rem);
    border-radius: 50%;
    border: 0.125em solid #666;
    display: flex;
    align-items: center;
    justify-content: center;
    flex-shrink: 0;
  }

  .roster-card-initial {
    font-size: clamp(0.9rem, 2vw, 1.25rem);
    font-weight: 700;
    color: rgba(255, 255, 255, 0.9);
    text-shadow: 0 1px 2px #000;
  }

  .roster-badge {
    font-size: clamp(0.5rem, 1vw, 0.6rem);
    text-transform: uppercase;
    letter-spacing: 0.05em;
    color: #aaa;
  }

  .roster-card.active .roster-badge {
    color: #88cc88;
  }

  .roster-card.bench .roster-badge {
    color: #8888cc;
  }

  .roster-card-body {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 0.1rem;
    text-align: center;
  }

  .roster-card-name {
    font-size: clamp(0.75rem, 1.5vw, 0.875rem);
    font-weight: bold;
    color: #eee;
  }

  .roster-card-class {
    font-size: clamp(0.6rem, 1.2vw, 0.7rem);
    color: #888;
  }

  .roster-card-branch {
    font-size: clamp(0.55rem, 1.1vw, 0.65rem);
    color: #88ccff;
  }

  .roster-card-hp {
    font-size: clamp(0.6rem, 1.2vw, 0.7rem);
    color: #aaa;
  }

  .roster-card-actions {
    display: flex;
    gap: 0.35rem;
    width: 100%;
    margin-top: 0.25rem;
  }

  .roster-card-actions button {
    flex: 1 1 0;
    padding: 0.25rem 0.4rem;
    border-radius: 0.2rem;
    font-size: clamp(0.55rem, 1vw, 0.65rem);
    cursor: pointer;
    transition: background 0.15s;
  }

  .roster-bench-btn,
  .roster-swap-target-btn {
    background: rgba(212, 168, 75, 0.12);
    border: 0.0625em solid #d4a84b;
    color: #d4a84b;
  }

  .roster-bench-btn:hover,
  .roster-swap-target-btn:hover {
    background: rgba(212, 168, 75, 0.28);
  }

  .roster-activate-btn {
    background: rgba(68, 170, 68, 0.15);
    border: 0.0625em solid #44aa44;
    color: #88cc88;
  }

  .roster-activate-btn:hover {
    background: rgba(68, 170, 68, 0.3);
  }

  .roster-dismiss-btn {
    background: rgba(204, 68, 68, 0.12);
    border: 0.0625em solid #cc4444;
    color: #cc8888;
  }

  .roster-dismiss-btn:hover {
    background: rgba(204, 68, 68, 0.28);
  }

  .roster-confirm-overlay {
    position: fixed;
    inset: 0;
    display: flex;
    align-items: center;
    justify-content: center;
    background: rgba(0, 0, 0, 0.85);
    z-index: 60;
  }

  .roster-confirm {
    background: #1a1a2e;
    border: 0.0625em solid #cc4444;
    border-radius: 0.5rem;
    padding: 1.5rem;
    min-width: 300px;
    max-width: 90vw;
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
  }

  .roster-confirm-title {
    margin: 0;
    font-size: 1.1rem;
    color: #cc8888;
  }

  .roster-confirm-text {
    margin: 0;
    color: #aaa;
    font-size: 0.85rem;
  }

  .roster-confirm-actions {
    display: flex;
    gap: 0.75rem;
    justify-content: flex-end;
  }

  .roster-confirm-cancel,
  .roster-confirm-yes {
    padding: 0.4rem 0.9rem;
    border-radius: 0.25rem;
    cursor: pointer;
    font-size: 0.85rem;
  }

  .roster-confirm-cancel {
    background: rgba(255, 255, 255, 0.05);
    border: 0.0625em solid #555;
    color: #ccc;
  }

  .roster-confirm-yes {
    background: rgba(204, 68, 68, 0.2);
    border: 0.0625em solid #cc4444;
    color: #ff8888;
  }

  .roster-confirm-yes:hover {
    background: rgba(204, 68, 68, 0.4);
  }
</style>
