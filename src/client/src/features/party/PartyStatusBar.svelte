<script lang="ts">
  import type { PartyMember } from '$shared/types/game';

  interface Props {
    party: PartyMember[];
    onOpenInventory: (slot: number) => void;
  }

  let { party, onOpenInventory }: Props = $props();

  function getLowStockWarning(member: PartyMember): string | null {
    const low = member.componentInventory.filter(c => c.count > 0 && c.count <= 3);
    if (low.length === 0) return null;
    return `${low.length} low`;
  }
</script>

<div class="party-bar">
  {#each party as member (member.name)}
    <div class="party-member" onclick={() => onOpenInventory(member.slot)} role="button" tabindex="0" onkeydown={(e) => e.key === 'Enter' && onOpenInventory(member.slot)}>
      <div class="member-portrait" style="background-color: {member.color}">
        {#if member.componentInventory.length > 0}
          <div class="component-badge">{member.componentInventory.reduce((a, c) => a + c.count, 0)}</div>
        {/if}
      </div>
      <div class="member-info">
        <div class="member-name">{member.name}</div>
        <div class="member-level">Lv.{member.level} {member.className}</div>
        <div class="hp-bar">
          <div class="hp-fill" style="width: {(member.hp / member.maxHp) * 100}%"></div>
          <div class="hp-text">{member.hp}/{member.maxHp}</div>
        </div>
        {#if getLowStockWarning(member)}
          <div class="stock-warning">{getLowStockWarning(member)}</div>
        {/if}
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
    position: relative;
    cursor: pointer;
  }

  .component-badge {
    position: absolute;
    bottom: -0.2rem;
    right: -0.2rem;
    background: #4488aa;
    color: #fff;
    font-size: clamp(0.45rem, 0.9vw, 0.55rem);
    font-weight: bold;
    padding: 0.05rem 0.2rem;
    border-radius: 0.2rem;
    min-width: 1rem;
    text-align: center;
  }

  .stock-warning {
    font-size: clamp(0.5rem, 1vw, 0.6rem);
    color: #ffcc00;
    font-weight: bold;
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
