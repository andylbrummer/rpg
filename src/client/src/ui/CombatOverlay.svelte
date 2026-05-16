<script lang="ts">
  import type { CombatState, Combatant } from '../types/game';
  import { sendAction } from '../stores/gameStore';
  import CombatResultToast from './CombatResultToast.svelte';

  interface Props {
    combat: CombatState | null;
    lastResult: { victory: boolean; xpGained: number; levelUps: string[]; roundCount: number } | null;
    onCombatAction: (action: string, targetId: string) => void;
    onFlee: () => void;
    cancelSignal?: number;
    synergyFlashTargetId?: string | null;
  }

  let { combat, lastResult, onCombatAction, onFlee, cancelSignal = 0, synergyFlashTargetId = null }: Props = $props();

  let flashingTargetId = $state<string | null>(null);
  let processedFlashTarget: string | null = null;

  $effect(() => {
    const target = synergyFlashTargetId;
    if (target && processedFlashTarget !== target) {
      processedFlashTarget = target;
      flashingTargetId = target;
      const timer = setTimeout(() => {
        flashingTargetId = null;
      }, 500);
      return () => clearTimeout(timer);
    }
  });

  let selectedTargetId = $state<string | null>(null);
  let selectedAction = $state('Attack');
  let selectedAbilityId = $state<string | null>(null);
  let showResult = $state(false);

  $effect(() => {
    if (lastResult) {
      showResult = true;
    }
  });

  function dismissResult() {
    showResult = false;
  }

  $effect(() => {
    if (cancelSignal > 0) {
      if (showResult) {
        dismissResult();
      } else {
        selectedTargetId = null;
      }
    }
  });

  function getActionName(action: string): string {
    const names: Record<string, string> = {
      Attack: 'Attack',
      Defend: 'Defend',
      Wait: 'Wait',
      UseAbility: 'Skill',
      UseItem: 'Item',
      Flee: 'Flee',
    };
    return names[action] || action;
  }

  function submitAction() {
    const currentActor = getCurrentActor();
    if (!currentActor) return;
    if (selectedAction === 'UseAbility' && !selectedAbilityId) return;
    if ((selectedAction === 'Attack' || selectedAction === 'UseAbility') && !selectedTargetId) return;

    if (selectedAction === 'UseAbility' && selectedAbilityId) {
      sendAction({
        type: 'combat_action',
        action: {
          actorId: currentActor.id,
          type: 'UseAbility',
          targetId: selectedTargetId || undefined,
          abilityId: selectedAbilityId,
        },
      });
      return;
    }

    const targetId = selectedAction === 'Attack' ? selectedTargetId! : '';
    onCombatAction(selectedAction, targetId);
  }

  function getCurrentActor(): Combatant | null {
    if (!combat || combat.currentTurnIndex < 0 || combat.currentTurnIndex >= combat.initiativeOrder.length) return null;
    const currentId = combat.initiativeOrder[combat.currentTurnIndex];
    return combat.combatants.find(c => c.id === currentId) || null;
  }

  function isPlayerTurn() {
    const actor = getCurrentActor();
    return actor?.isPlayer === true && combat?.phase === 'Turn';
  }

  function getInitiativeEntries() {
    if (!combat) return [];
    return combat.initiativeOrder.map(id => {
      const c = combat!.combatants.find(x => x.id === id);
      return c ? { id: c.id, name: c.name, isPlayer: c.isPlayer } : null;
    }).filter((e): e is { id: string; name: string; isPlayer: boolean } => e !== null);
  }

  function getEnemies(): Combatant[] {
    return combat?.combatants.filter(c => !c.isPlayer) || [];
  }

  function getParty(): Combatant[] {
    return combat?.combatants.filter(c => c.isPlayer) || [];
  }

  function getFrontRow(combatants: Combatant[]): Combatant[] {
    return combatants.filter(c => c.row === 0);
  }

  function getBackRow(combatants: Combatant[]): Combatant[] {
    return combatants.filter(c => c.row === 1);
  }

  function getCurrentAbilities(): { id: string; name: string; range?: string; available: boolean }[] {
    const actor = getCurrentActor();
    if (!actor || !actor.isPlayer) return [];
    return actor.abilities?.map(a => ({
      id: a.id,
      name: a.name,
      range: a.range,
      available: !a.requiredRow
        || (a.requiredRow === 'front' && actor.row === 0)
        || (a.requiredRow === 'back' && actor.row === 1)
    })) ?? [];
  }

  function isValidTarget(enemy: Combatant): boolean {
    if (!isPlayerTurn()) return false;
    if (selectedAction === 'Attack') return true;
    if (selectedAction !== 'UseAbility' || !selectedAbilityId) return false;
    const ability = getCurrentAbilities().find(a => a.id === selectedAbilityId);
    if (!ability) return false;
    if (ability.range === 'melee') return enemy.row === 0;
    return true;
  }

  function selectAction(action: string) {
    selectedAction = action;
    selectedAbilityId = null;
    selectedTargetId = null;
  }

  function selectAbility(abilityId: string) {
    selectedAbilityId = abilityId;
    selectedTargetId = null;
  }

  const actions = ['Attack', 'Defend', 'Wait', 'UseAbility', 'UseItem'];
</script>

<div class="combat-overlay" data-testid="combat-overlay" data-flash-target={flashingTargetId ?? ''}>
  {#if showResult && lastResult}
    <CombatResultToast result={lastResult} onDismiss={dismissResult} />
  {:else}
    <div class="combat-header">
      <div class="round-counter">
        Round {combat?.round ?? 0}
      </div>
      <div class="phase-indicator">
        {combat?.phase ?? 'Waiting...'}
      </div>
    </div>

    <div class="combat-body">
      <div class="initiative-bar">
        {#each getInitiativeEntries() as entry, i}
          <div
            class="initiative-entry"
            class:active={entry.isPlayer}
            class:current={i === combat?.currentTurnIndex}
          >
            <span class="init-name">{entry.name}</span>
          </div>
        {/each}
      </div>

      <div class="combat-arena">
        <div class="party-side">
          <h3>Party</h3>
          <div class="row-band front-band">
            <span class="band-label">Front</span>
            {#each getFrontRow(getParty()) as member}
              <div
                class="combatant"
                class:dead={member.hp <= 0}
                class:current-turn={member.isCurrent}
                class:synergy-flash={flashingTargetId === member.id}
              >
                <div class="combatant-header">
                  <span class="combatant-name">{member.name}</span>
                </div>
                <div class="hp-bar">
                  <div class="hp-fill" style="width: {(member.hp / member.maxHp) * 100}%"></div>
                  <div class="hp-text">{member.hp}/{member.maxHp}</div>
                </div>
              </div>
            {/each}
          </div>
          <div class="row-band back-band">
            <span class="band-label">Back</span>
            {#each getBackRow(getParty()) as member}
              <div
                class="combatant"
                class:dead={member.hp <= 0}
                class:current-turn={member.isCurrent}
                class:synergy-flash={flashingTargetId === member.id}
              >
                <div class="combatant-header">
                  <span class="combatant-name">{member.name}</span>
                </div>
                <div class="hp-bar">
                  <div class="hp-fill" style="width: {(member.hp / member.maxHp) * 100}%"></div>
                  <div class="hp-text">{member.hp}/{member.maxHp}</div>
                </div>
              </div>
            {/each}
          </div>
        </div>

        <div class="vs-divider">VS</div>

        <div class="enemy-side">
          <h3>Enemies</h3>
          <div class="row-band front-band">
            <span class="band-label">Front</span>
            {#each getFrontRow(getEnemies()) as enemy}
              <button
                type="button"
                class="combatant"
                class:dead={enemy.hp <= 0}
                class:selected={selectedTargetId === enemy.id && isPlayerTurn()}
                class:valid-target={isValidTarget(enemy)}
                class:invalid-target={!isValidTarget(enemy) && selectedAction === 'UseAbility' && selectedAbilityId !== null}
                class:current-turn={enemy.isCurrent}
                class:synergy-flash={flashingTargetId === enemy.id}
                onclick={() => { if (isPlayerTurn() && isValidTarget(enemy)) selectedTargetId = enemy.id; }}
                disabled={!isPlayerTurn() || !isValidTarget(enemy)}
              >
                <div class="combatant-header">
                  <span class="combatant-name">{enemy.name}</span>
                </div>
                <div class="hp-bar">
                  <div class="hp-fill" style="width: {(enemy.hp / enemy.maxHp) * 100}%"></div>
                  <div class="hp-text">{enemy.hp}/{enemy.maxHp}</div>
                </div>
              </button>
            {/each}
          </div>
          <div class="row-band back-band">
            <span class="band-label">Back</span>
            {#each getBackRow(getEnemies()) as enemy}
              <button
                type="button"
                class="combatant"
                class:dead={enemy.hp <= 0}
                class:selected={selectedTargetId === enemy.id && isPlayerTurn()}
                class:valid-target={isValidTarget(enemy)}
                class:invalid-target={!isValidTarget(enemy) && selectedAction === 'UseAbility' && selectedAbilityId !== null}
                class:current-turn={enemy.isCurrent}
                class:synergy-flash={flashingTargetId === enemy.id}
                onclick={() => { if (isPlayerTurn() && isValidTarget(enemy)) selectedTargetId = enemy.id; }}
                disabled={!isPlayerTurn() || !isValidTarget(enemy)}
              >
                <div class="combatant-header">
                  <span class="combatant-name">{enemy.name}</span>
                </div>
                <div class="hp-bar">
                  <div class="hp-fill" style="width: {(enemy.hp / enemy.maxHp) * 100}%"></div>
                  <div class="hp-text">{enemy.hp}/{enemy.maxHp}</div>
                </div>
              </button>
            {/each}
          </div>
        </div>
      </div>

      <div class="combat-log">
        {#each combat?.log?.slice(-6) || [] as entry}
          <div class="log-entry">
            {entry.message}
          </div>
        {/each}
      </div>
    </div>

    <div class="action-bar">
      {#if isPlayerTurn()}
        <div class="action-select">
          {#each actions as action}
            <button
              class="action-btn"
              class:selected={selectedAction === action}
              onclick={() => selectAction(action)}
            >
              {getActionName(action)}
            </button>
          {/each}
        </div>
        {#if selectedAction === 'UseAbility'}
          <div class="ability-select">
            {#each getCurrentAbilities() as ability}
              <button
                class="ability-btn"
                class:selected={selectedAbilityId === ability.id}
                class:disabled={!ability.available}
                onclick={() => ability.available && selectAbility(ability.id)}
                disabled={!ability.available}
                title={ability.available ? '' : 'Unavailable from this row'}
              >
                {ability.name}
              </button>
            {:else}
              <span class="no-abilities">No abilities available</span>
            {/each}
          </div>
        {/if}
        <div class="target-hint">
          {#if selectedAction === 'Attack'}
            Click an enemy to select target
          {:else if selectedAction === 'UseAbility'}
            {#if selectedAbilityId}
              Click a highlighted enemy to target
            {:else}
              Select an ability
            {/if}
          {:else}
            {getActionName(selectedAction)} selected
          {/if}
        </div>
        <div class="action-submit">
          <button class="submit-btn" onclick={submitAction}>Execute</button>
          <button class="flee-btn" onclick={onFlee}>Flee</button>
        </div>
      {:else}
        <div class="waiting-message">
          {#if combat?.phase === 'Resolve'}
            Resolving...
          {:else}
            Waiting for turn...
          {/if}
        </div>
      {/if}
    </div>
  {/if}
</div>

<style>
  .combat-overlay {
    display: flex;
    flex-direction: column;
    width: 100%;
    height: 100%;
    background: rgba(0, 0, 0, 0.92);
    padding: clamp(0.5rem, 2vh, 1rem);
    box-sizing: border-box;
    overflow: hidden;
  }

  .combat-header {
    flex: 0 0 auto;
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding-bottom: clamp(0.25rem, 1vh, 0.5rem);
    border-bottom: 0.0625em solid #444;
    margin-bottom: clamp(0.25rem, 1vh, 0.5rem);
  }

  .round-counter {
    font-size: clamp(0.8rem, 2vw, 1rem);
    color: #d4a84b;
    font-weight: bold;
  }

  .phase-indicator {
    font-size: clamp(0.7rem, 1.8vw, 0.875rem);
    color: #888;
    text-transform: uppercase;
  }

  .combat-body {
    flex: 1 1 auto;
    display: flex;
    flex-direction: column;
    overflow: hidden;
    min-height: 0;
    gap: clamp(0.25rem, 1vh, 0.5rem);
  }

  .initiative-bar {
    flex: 0 0 auto;
    display: flex;
    gap: 0.25rem;
    padding: 0.25rem 0;
  }

  .initiative-entry {
    display: flex;
    align-items: center;
    gap: 0.25rem;
    padding: 0.2rem 0.4rem;
    background: rgba(255, 255, 255, 0.05);
    border: 0.0625em solid #444;
    border-radius: 0.25rem;
    font-size: clamp(0.6rem, 1.2vw, 0.7rem);
    white-space: nowrap;
    flex: 1 1 0;
    min-width: 0;
    overflow: hidden;
  }

  .initiative-entry.active {
    border-color: #44aaff;
  }

  .initiative-entry.current {
    background: rgba(212, 168, 75, 0.2);
    border-color: #d4a84b;
  }

  .init-name {
    color: #ccc;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .combat-arena {
    flex: 1 1 auto;
    display: flex;
    justify-content: center;
    align-items: flex-start;
    gap: clamp(1rem, 3vw, 2rem);
    overflow: hidden;
    min-height: 0;
    padding: 0.5rem;
  }

  .party-side,
  .enemy-side {
    display: flex;
    flex-direction: column;
    gap: 0.5rem;
    min-width: clamp(12rem, 22vw, 18rem);
    max-width: min(22rem, 34vw);
    flex: 1 1 auto;
  }

  .row-band {
    display: flex;
    flex-direction: row;
    flex-wrap: wrap;
    gap: 0.35rem;
    padding: 0.35rem;
    border-radius: 0.25rem;
  }

  .row-band.front-band {
    background: rgba(68, 102, 68, 0.08);
    border: 0.0625em solid rgba(68, 102, 68, 0.25);
  }

  .row-band.back-band {
    background: rgba(68, 68, 102, 0.08);
    border: 0.0625em solid rgba(68, 68, 102, 0.25);
  }

  .band-label {
    font-size: clamp(0.55rem, 1vw, 0.65rem);
    color: #888;
    text-transform: uppercase;
    letter-spacing: 0.05em;
    text-align: center;
    width: 100%;
  }

  .party-side h3,
  .enemy-side h3 {
    margin: 0 0 0.25rem;
    font-size: clamp(0.75rem, 1.8vw, 0.9rem);
    color: #ccc;
    text-align: center;
  }

  .vs-divider {
    display: flex;
    align-items: center;
    font-size: clamp(1rem, 2.5vw, 1.25rem);
    font-weight: bold;
    color: #d4a84b;
    padding: 0 0.5rem;
  }

  .combatant {
    display: flex;
    flex-direction: column;
    gap: 0.25rem;
    padding: clamp(0.25rem, 0.8vh, 0.375rem);
    background: rgba(255, 255, 255, 0.05);
    border: 0.0625em solid #444;
    border-radius: 0.375rem;
    transition: border-color 0.15s;
    cursor: default;
    flex: 1 1 0;
    min-width: 0;
  }

  .combatant.dead {
    opacity: 0.4;
  }

  .combatant.current-turn {
    border-color: #d4a84b;
    box-shadow: 0 0 0.25em rgba(212, 168, 75, 0.3);
  }

  .enemy-side .combatant {
    cursor: pointer;
  }

  .enemy-side .combatant.selected {
    border-color: #44aaff;
    box-shadow: 0 0 0.25em rgba(68, 170, 255, 0.3);
  }

  .enemy-side .combatant.valid-target {
    border-color: #44aa44;
    box-shadow: 0 0 0.25em rgba(68, 170, 68, 0.3);
  }

  .enemy-side .combatant.invalid-target {
    opacity: 0.4;
    cursor: not-allowed;
  }

  .combatant-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
  }

  .combatant-name {
    font-size: clamp(0.7rem, 1.4vw, 0.875rem);
    font-weight: bold;
    color: #eee;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
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

  .enemy-side .hp-fill {
    background: linear-gradient(90deg, #a84444, #cc6666);
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

  .combat-log {
    flex: 0 0 auto;
    max-height: clamp(4rem, 12vh, 6rem);
    overflow-y: auto;
    padding: 0.375rem;
    background: rgba(0, 0, 0, 0.5);
    border: 0.0625em solid #333;
    border-radius: 0.25rem;
    font-size: clamp(0.65rem, 1.3vw, 0.75rem);
  }

  .log-entry {
    padding: 0.15rem 0;
    color: #ccc;
  }

  .action-bar {
    flex: 0 0 auto;
    display: flex;
    flex-direction: column;
    gap: 0.5rem;
    padding-top: clamp(0.25rem, 1vh, 0.5rem);
    border-top: 0.0625em solid #444;
  }

  .action-select {
    display: flex;
    gap: 0.5rem;
    justify-content: center;
    flex-wrap: wrap;
  }

  .action-btn {
    padding: clamp(0.3rem, 1vh, 0.4rem) clamp(0.6rem, 2vw, 1rem);
    background: rgba(255, 255, 255, 0.05);
    border: 0.0625em solid #444;
    border-radius: 0.25rem;
    color: #ccc;
    cursor: pointer;
    font-size: clamp(0.7rem, 1.5vw, 0.85rem);
    transition: background 0.15s, border-color 0.15s;
    min-width: clamp(4rem, 10vw, 5rem);
  }

  .action-btn:hover {
    background: rgba(100, 100, 100, 0.3);
  }

  .action-btn.selected {
    border-color: #d4a84b;
    background: rgba(212, 168, 75, 0.15);
  }

  .ability-select {
    display: flex;
    gap: 0.35rem;
    justify-content: center;
    flex-wrap: wrap;
  }

  .ability-btn {
    padding: clamp(0.2rem, 0.8vh, 0.3rem) clamp(0.5rem, 1.5vw, 0.75rem);
    background: rgba(100, 68, 170, 0.1);
    border: 0.0625em solid #6644aa;
    border-radius: 0.25rem;
    color: #aa88cc;
    cursor: pointer;
    font-size: clamp(0.65rem, 1.3vw, 0.8rem);
    transition: background 0.15s, border-color 0.15s;
  }

  .ability-btn:hover {
    background: rgba(100, 68, 170, 0.25);
  }

  .ability-btn.selected {
    border-color: #d4a84b;
    background: rgba(212, 168, 75, 0.15);
  }

  .ability-btn.disabled {
    opacity: 0.4;
    cursor: not-allowed;
    border-color: #444;
    color: #666;
  }

  .no-abilities {
    font-size: clamp(0.6rem, 1.2vw, 0.75rem);
    color: #666;
    font-style: italic;
  }

  .target-hint {
    text-align: center;
    font-size: clamp(0.6rem, 1.2vw, 0.75rem);
    color: #888;
  }

  .action-submit {
    display: flex;
    gap: 0.5rem;
    justify-content: center;
  }

  .submit-btn,
  .flee-btn {
    padding: clamp(0.3rem, 1vh, 0.5rem) clamp(1rem, 3vw, 2rem);
    border: 0.0625em solid;
    border-radius: 0.25rem;
    cursor: pointer;
    font-size: clamp(0.75rem, 1.8vw, 0.9rem);
    transition: background 0.15s;
  }

  .submit-btn {
    background: rgba(68, 170, 68, 0.2);
    border-color: #44aa44;
    color: #88cc88;
  }

  .submit-btn:hover {
    background: rgba(68, 170, 68, 0.35);
  }

  .flee-btn {
    background: rgba(170, 68, 68, 0.2);
    border-color: #aa4444;
    color: #cc8888;
  }

  .flee-btn:hover {
    background: rgba(170, 68, 68, 0.35);
  }

  .waiting-message {
    text-align: center;
    font-size: clamp(0.8rem, 2vw, 1rem);
    color: #888;
    padding: 0.5rem;
  }

  .combatant.synergy-flash {
    animation: synergyPulse 500ms ease-out;
    z-index: 10;
  }

  @keyframes synergyPulse {
    0% {
      box-shadow: 0 0 0 0 rgba(212, 168, 75, 0.9);
      border-color: #d4a84b;
    }
    50% {
      box-shadow: 0 0 0.75em 0.35em rgba(212, 168, 75, 0.6);
      border-color: #ffdd77;
    }
    100% {
      box-shadow: 0 0 1.5em 0.75em rgba(212, 168, 75, 0);
      border-color: #444;
    }
  }
</style>
