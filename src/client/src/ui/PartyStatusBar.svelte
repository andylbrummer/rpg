<script lang="ts">
  import type { PartyMember } from '../types/game';

  interface Props {
    members: PartyMember[];
  }

  let { members }: Props = $props();

  function hpColor(percent: number): string {
    if (percent > 0.5) return '#44ff44';
    if (percent > 0.25) return '#ffcc00';
    return '#ff4444';
  }

  function classIcon(classId: string): string {
    const icons: Record<string, string> = {
      bonewarden: '💀',
      stillblade: '⚔️',
      cauterist: '🔥',
      hollow: '🗡️'
    };
    return icons[classId] ?? '❓';
  }
</script>

<div class="party-bar">
  {#each members as member}
    <div class="char-tile" class:dead={!member.alive}>
      <div class="char-header">
        <span class="class-icon">{classIcon(member.classId)}</span>
        <span class="name">{member.name}</span>
        <span class="level">L{member.level}</span>
      </div>
      <div class="hp-bar-track">
        <div
          class="hp-bar-fill"
          style="width: {(member.hp / member.maxHp) * 100}%; background: {hpColor(member.hp / member.maxHp)};"
        ></div>
        <span class="hp-text">{member.hp}/{member.maxHp}</span>
      </div>
      <div class="row-indicator">
        {member.row === 0 ? 'Front' : 'Back'}
      </div>
    </div>
  {/each}
</div>

<style>
  .party-bar {
    display: flex;
    gap: 0.5rem;
    background: rgba(0, 0, 0, 0.85);
    padding: 0.5rem 1rem;
    border-top: 1px solid #444;
    justify-content: center;
  }

  .char-tile {
    background: rgba(255, 255, 255, 0.05);
    border: 1px solid #444;
    border-radius: 4px;
    padding: 0.5rem 0.75rem;
    min-width: 140px;
    display: flex;
    flex-direction: column;
    gap: 0.25rem;
  }

  .char-tile.dead {
    opacity: 0.5;
    filter: grayscale(1);
  }

  .char-header {
    display: flex;
    align-items: center;
    gap: 0.375rem;
    font-size: 0.875rem;
    color: #fff;
  }

  .class-icon {
    font-size: 1rem;
  }

  .name {
    font-weight: 600;
    flex: 1;
  }

  .level {
    color: #aaa;
    font-size: 0.75rem;
  }

  .hp-bar-track {
    position: relative;
    height: 14px;
    background: #333;
    border-radius: 2px;
    overflow: hidden;
  }

  .hp-bar-fill {
    height: 100%;
    transition: width 0.2s ease, background 0.2s ease;
  }

  .hp-text {
    position: absolute;
    top: 0;
    left: 0;
    right: 0;
    bottom: 0;
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: 0.65rem;
    color: #000;
    font-weight: 700;
    text-shadow: none;
  }

  .row-indicator {
    font-size: 0.65rem;
    color: #888;
    text-align: right;
  }
</style>
