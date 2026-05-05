<script lang="ts">
  import type { CombatState, CombatAction } from '../types/game';

  interface Props {
    combat: CombatState;
    onAction: (action: CombatAction) => void;
    onFlee: () => void;
  }

  let { combat, onAction, onFlee }: Props = $props();

  const players = $derived(combat.combatants.filter(c => c.isPlayer));
  const enemies = $derived(combat.combatants.filter(c => !c.isPlayer));
  const currentActor = $derived(combat.combatants.find(c => c.isCurrent));
  const isPlayerTurn = $derived(currentActor?.isPlayer ?? false);
  const recentLogs = $derived(combat.log.slice(-6).reverse());

  function sendAttack() {
    if (!currentActor) return;
    const target = enemies.find(e => e.alive);
    if (!target) return;
    onAction({
      actorId: currentActor.id,
      type: 'Attack',
      targetId: target.id
    });
  }

  function sendDefend() {
    if (!currentActor) return;
    onAction({
      actorId: currentActor.id,
      type: 'Defend'
    });
  }

  function sendWait() {
    if (!currentActor) return;
    onAction({
      actorId: currentActor.id,
      type: 'Wait'
    });
  }

  function hpColor(hp: number, maxHp: number): string {
    const ratio = hp / maxHp;
    if (ratio > 0.5) return '#4a4';
    if (ratio > 0.25) return '#aa4';
    return '#a44';
  }
</script>

<div class="combat-overlay">
  <div class="combat-header">
    <span class="round">Round {combat.round}</span>
    <span class="phase">{combat.phase}</span>
    {#if isPlayerTurn}
      <span class="turn-indicator">Your Turn</span>
    {:else if currentActor}
      <span class="turn-indicator ai">{currentActor.name}'s Turn</span>
    {/if}
  </div>

  <div class="combat-arena">
    <div class="side players">
      <h3>Party</h3>
      {#each players as c}
        <div class="combatant-card" class:current={c.isCurrent} class:dead={!c.alive}>
          <div class="name">{c.name}</div>
          <div class="hp-bar">
            <div class="hp-fill" style="width: {(c.hp / c.maxHp) * 100}%; background: {hpColor(c.hp, c.maxHp)}"></div>
          </div>
          <div class="hp-text">{c.hp}/{c.maxHp}</div>
          <div class="meta">Speed {c.speed} | Row {c.row === 0 ? 'Front' : 'Back'}</div>
        </div>
      {/each}
    </div>

    <div class="vs">VS</div>

    <div class="side enemies">
      <h3>Enemies</h3>
      {#each enemies as c}
        <div class="combatant-card" class:current={c.isCurrent} class:dead={!c.alive}>
          <div class="name">{c.name}</div>
          <div class="hp-bar">
            <div class="hp-fill" style="width: {(c.hp / c.maxHp) * 100}%; background: {hpColor(c.hp, c.maxHp)}"></div>
          </div>
          <div class="hp-text">{c.hp}/{c.maxHp}</div>
          <div class="meta">Speed {c.speed} | Row {c.row === 0 ? 'Front' : 'Back'}</div>
        </div>
      {/each}
    </div>
  </div>

  <div class="initiative-bar">
    {#each combat.initiativeOrder as id}
      {@const c = combat.combatants.find(x => x.id === id)}
      {#if c}
        <div class="init-token" class:current={c.isCurrent} class:dead={!c.alive}>
          {c.name}
        </div>
      {/if}
    {/each}
  </div>

  <div class="combat-log">
    {#each recentLogs as entry}
      <div class="log-entry">
        <span class="log-round">R{entry.round}</span>
        <span class="log-message">{entry.message}</span>
      </div>
    {/each}
  </div>

  <div class="action-bar">
    {#if isPlayerTurn}
      <button onclick={sendAttack} disabled={!enemies.some(e => e.alive)}>Attack</button>
      <button onclick={sendDefend}>Defend</button>
      <button onclick={sendWait}>Wait</button>
      <button onclick={onFlee} class="flee">Flee</button>
    {:else}
      <span class="waiting">Enemy acting...</span>
    {/if}
  </div>
</div>

<style>
  .combat-overlay {
    position: absolute;
    inset: 0;
    background: rgba(0, 0, 0, 0.92);
    color: #ddd;
    display: flex;
    flex-direction: column;
    padding: 1.5rem;
    font-family: system-ui, sans-serif;
    z-index: 100;
  }

  .combat-header {
    display: flex;
    gap: 1.5rem;
    align-items: center;
    justify-content: center;
    padding-bottom: 1rem;
    border-bottom: 1px solid #444;
  }

  .round {
    font-size: 1.25rem;
    font-weight: bold;
    color: #fff;
  }

  .phase {
    text-transform: uppercase;
    font-size: 0.75rem;
    color: #888;
    letter-spacing: 1px;
  }

  .turn-indicator {
    background: #2a4a2a;
    color: #4f4;
    padding: 0.25rem 0.75rem;
    border-radius: 4px;
    font-size: 0.875rem;
    font-weight: bold;
  }

  .turn-indicator.ai {
    background: #4a2a2a;
    color: #f44;
  }

  .combat-arena {
    display: flex;
    justify-content: center;
    align-items: flex-start;
    gap: 2rem;
    padding: 1.5rem 0;
    flex: 1;
  }

  .side {
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
    min-width: 200px;
  }

  .side h3 {
    margin: 0;
    text-align: center;
    font-size: 1rem;
    color: #888;
    text-transform: uppercase;
    letter-spacing: 2px;
  }

  .vs {
    font-size: 1.5rem;
    font-weight: bold;
    color: #666;
    align-self: center;
  }

  .combatant-card {
    background: #1a1a1a;
    border: 1px solid #333;
    border-radius: 6px;
    padding: 0.75rem;
    transition: border-color 0.2s;
  }

  .combatant-card.current {
    border-color: #48f;
    box-shadow: 0 0 8px rgba(68, 136, 255, 0.3);
  }

  .combatant-card.dead {
    opacity: 0.4;
    filter: grayscale(1);
  }

  .name {
    font-weight: bold;
    color: #fff;
    margin-bottom: 0.375rem;
  }

  .hp-bar {
    height: 8px;
    background: #333;
    border-radius: 4px;
    overflow: hidden;
    margin-bottom: 0.25rem;
  }

  .hp-fill {
    height: 100%;
    transition: width 0.3s;
  }

  .hp-text {
    font-size: 0.75rem;
    color: #888;
    margin-bottom: 0.25rem;
  }

  .meta {
    font-size: 0.6875rem;
    color: #666;
  }

  .initiative-bar {
    display: flex;
    gap: 0.5rem;
    justify-content: center;
    padding: 0.75rem 0;
    border-top: 1px solid #333;
    border-bottom: 1px solid #333;
    overflow-x: auto;
  }

  .init-token {
    background: #222;
    border: 1px solid #444;
    padding: 0.375rem 0.625rem;
    border-radius: 4px;
    font-size: 0.75rem;
    white-space: nowrap;
  }

  .init-token.current {
    border-color: #48f;
    background: #1a2a4a;
  }

  .init-token.dead {
    opacity: 0.4;
    text-decoration: line-through;
  }

  .combat-log {
    height: 120px;
    overflow-y: auto;
    padding: 0.75rem;
    background: #111;
    border-radius: 4px;
    margin: 0.75rem 0;
    font-size: 0.875rem;
    font-family: monospace;
  }

  .log-entry {
    display: flex;
    gap: 0.5rem;
    padding: 0.125rem 0;
  }

  .log-round {
    color: #666;
    min-width: 2rem;
  }

  .log-message {
    color: #ccc;
  }

  .action-bar {
    display: flex;
    gap: 0.75rem;
    justify-content: center;
    padding-top: 0.5rem;
  }

  .action-bar button {
    background: #2a2a2a;
    color: #fff;
    border: 1px solid #555;
    padding: 0.625rem 1.25rem;
    border-radius: 4px;
    cursor: pointer;
    font-size: 1rem;
    min-width: 100px;
  }

  .action-bar button:hover:not(:disabled) {
    background: #3a3a3a;
    border-color: #777;
  }

  .action-bar button:disabled {
    opacity: 0.4;
    cursor: not-allowed;
  }

  .action-bar button.flee {
    border-color: #844;
    color: #faa;
  }

  .action-bar button.flee:hover {
    background: #4a2222;
  }

  .waiting {
    color: #888;
    font-style: italic;
  }
</style>
