<script lang="ts">
  import type { PartyMember } from '../types/game';

  interface Props {
    party: PartyMember[];
    onEnterDungeon: () => void;
    onRest: () => void;
  }

  let { party, onEnterDungeon, onRest }: Props = $props();

  function hpColor(hp: number, maxHp: number): string {
    const ratio = hp / maxHp;
    if (ratio > 0.5) return '#4a4';
    if (ratio > 0.25) return '#aa4';
    return '#a44';
  }
</script>

<div class="town-menu">
  <div class="town-header">
    <h1>The Reach</h1>
    <p class="subtitle">A bone-shard town at the edge of the dying world.</p>
  </div>

  <div class="town-content">
    <div class="party-panel">
      <h2>Your Party</h2>
      <div class="party-grid">
        {#each party as member}
          <div class="member-card">
            <div class="member-name">{member.name}</div>
            <div class="member-class">{member.classId} · Lv.{member.level}</div>
            <div class="hp-bar">
              <div class="hp-fill" style="width: {(member.hp / member.maxHp) * 100}%; background: {hpColor(member.hp, member.maxHp)}"></div>
            </div>
            <div class="hp-text">{member.hp}/{member.maxHp}</div>
            <div class="member-row">{member.row === 0 ? 'Front Row' : 'Back Row'}</div>
          </div>
        {/each}
      </div>
    </div>

    <div class="actions-panel">
      <h2>Actions</h2>
      <button class="action-btn primary" onclick={onEnterDungeon}>
        <span class="btn-icon">⚔</span>
        Enter Dungeon
      </button>
      <button class="action-btn" onclick={onRest}>
        <span class="btn-icon">🏠</span>
        Rest at Inn
      </button>
    </div>
  </div>
</div>

<style>
  .town-menu {
    position: absolute;
    inset: 0;
    background: #0a0a0a;
    color: #ddd;
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    font-family: system-ui, sans-serif;
    z-index: 50;
  }

  .town-header {
    text-align: center;
    margin-bottom: 2rem;
  }

  h1 {
    font-size: 2.5rem;
    margin: 0;
    color: #fff;
    letter-spacing: 4px;
    text-transform: uppercase;
  }

  .subtitle {
    color: #666;
    margin: 0.5rem 0 0;
    font-style: italic;
  }

  .town-content {
    display: flex;
    gap: 3rem;
    align-items: flex-start;
  }

  .party-panel h2,
  .actions-panel h2 {
    font-size: 1rem;
    text-transform: uppercase;
    letter-spacing: 2px;
    color: #888;
    margin: 0 0 1rem;
  }

  .party-grid {
    display: grid;
    grid-template-columns: repeat(2, 1fr);
    gap: 0.75rem;
  }

  .member-card {
    background: #141414;
    border: 1px solid #333;
    border-radius: 6px;
    padding: 0.875rem;
    min-width: 160px;
  }

  .member-name {
    font-weight: bold;
    color: #fff;
    font-size: 1rem;
  }

  .member-class {
    font-size: 0.75rem;
    color: #888;
    text-transform: capitalize;
    margin-bottom: 0.5rem;
  }

  .hp-bar {
    height: 6px;
    background: #333;
    border-radius: 3px;
    overflow: hidden;
    margin-bottom: 0.25rem;
  }

  .hp-fill {
    height: 100%;
    transition: width 0.3s;
  }

  .hp-text {
    font-size: 0.6875rem;
    color: #888;
  }

  .member-row {
    font-size: 0.6875rem;
    color: #666;
    margin-top: 0.25rem;
  }

  .actions-panel {
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
    min-width: 200px;
  }

  .action-btn {
    background: #1a1a1a;
    border: 1px solid #444;
    color: #ddd;
    padding: 1rem 1.5rem;
    border-radius: 6px;
    cursor: pointer;
    font-size: 1rem;
    display: flex;
    align-items: center;
    gap: 0.75rem;
    transition: all 0.2s;
  }

  .action-btn:hover {
    background: #252525;
    border-color: #666;
  }

  .action-btn.primary {
    border-color: #484;
    color: #4f4;
  }

  .action-btn.primary:hover {
    background: #1a2a1a;
    border-color: #5a5;
  }

  .btn-icon {
    font-size: 1.25rem;
  }
</style>
