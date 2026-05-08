<script lang="ts">
  import type { Character } from '../types/game';

  interface Props {
    party: Character[];
  }

  let { party }: Props = $props();
</script>

<div class="party-bar">
  {#each party as member (member.name)}
    <div class="party-member">
      <div class="member-portrait" style="background-color: {member.color}"></div>
      <div class="member-info">
        <div class="member-name">{member.name}</div>
        <div class="member-level">Lv.{member.level} {member.className}</div>
        <div class="hp-bar">
          <div class="hp-fill" style="width: {(member.hp / member.maxHp) * 100}%"></div>
          <div class="hp-text">{member.hp}/{member.maxHp}</div>
        </div>
      </div>
    </div>
  {/each}
</div>

<style>
  .party-bar {
    display: flex;
    gap: 0.5rem;
    padding: 0.5rem;
    overflow-x: auto;
    background: rgba(0, 0, 0, 0.8);
    border-top: 0.0625em solid #333;
  }

  .party-member {
    display: flex;
    align-items: center;
    gap: 0.5rem;
    background: rgba(255, 255, 255, 0.05);
    border: 0.0625em solid #444;
    border-radius: 0.375rem;
    padding: 0.375rem;
    min-width: clamp(9rem, 18vw, 12rem);
    flex: 0 0 auto;
  }

  .member-portrait {
    width: clamp(2rem, 5vw, 2.5rem);
    height: clamp(2rem, 5vw, 2.5rem);
    border-radius: 50%;
    border: 0.125em solid #666;
    flex-shrink: 0;
  }

  .member-info {
    display: flex;
    flex-direction: column;
    gap: 0.15rem;
    min-width: 0;
    flex: 1 1 auto;
  }

  .member-name {
    font-size: clamp(0.7rem, 1.5vw, 0.8rem);
    font-weight: bold;
    color: #eee;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  .member-level {
    font-size: clamp(0.6rem, 1.2vw, 0.7rem);
    color: #888;
  }

  .hp-bar {
    position: relative;
    height: clamp(0.5rem, 1.2vh, 0.75rem);
    background: rgba(0, 0, 0, 0.5);
    border-radius: 0.25rem;
    overflow: hidden;
  }

  .hp-fill {
    height: 100%;
    background: linear-gradient(90deg, #44a844, #66cc66);
    transition: width 0.3s ease;
  }

  .hp-text {
    position: absolute;
    inset: 0;
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: clamp(0.5rem, 1vw, 0.65rem);
    color: #fff;
    text-shadow: 0 0 0.1em rgba(0, 0, 0, 0.8);
    pointer-events: none;
  }


</style>
