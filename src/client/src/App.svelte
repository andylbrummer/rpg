<script lang="ts">
  import { onMount } from 'svelte';
  import { gameStore, sendAction, serverErrorStore } from './stores/gameStore';
  import TownMenu from './ui/TownMenu.svelte';
  import type { PlayerAction } from './types/game';
  import CombatOverlay from './ui/CombatOverlay.svelte';
  import PartyStatusBar from './ui/PartyStatusBar.svelte';
  import ExplorationHUD from './ui/ExplorationHUD.svelte';
  import { DungeonRenderer } from './renderer/DungeonRenderer';
  import type { GameState } from './types/game';

  let gameContainer: HTMLDivElement | undefined = $state(undefined);
  let renderer: DungeonRenderer | null = null;
  let gameState = $state<GameState | null>(null);
  let serverError = $state<{ code: string; message: string; recoverable: boolean } | null>(null);
  let combatCancelSignal = $state(0);
  let repToasts = $state<Array<{ id: number; factionId: string; delta: number; source: string }>>([]);
  let lastActionLogTurn = $state(0);
  let nextToastId = 0;

  serverErrorStore.subscribe((err) => {
    serverError = err;
  });

  // Input buffer state
  const INPUT_BUFFER_SIZE = 2;
  const REPEAT_INITIAL_MS = 300;
  const REPEAT_INTERVAL_MS = 200;
  const PENDING_TIMEOUT_MS = 500;

  let inputBuffer: PlayerAction[] = [];
  let pendingAction: PlayerAction | null = null;
  let pendingTimer: ReturnType<typeof setTimeout> | null = null;
  let heldKeys = new Set<string>();
  let repeatTimers = new Map<string, ReturnType<typeof setTimeout>>();

  function clearPending() {
    if (pendingTimer) {
      clearTimeout(pendingTimer);
      pendingTimer = null;
    }
    pendingAction = null;
  }

  function drainBuffer() {
    if (pendingAction || inputBuffer.length === 0) return;
    const action = inputBuffer.shift()!;
    pendingAction = action;
    sendAction(action);
    pendingTimer = setTimeout(() => {
      pendingAction = null;
      pendingTimer = null;
      drainBuffer();
    }, PENDING_TIMEOUT_MS);
  }

  function enqueueAction(action: PlayerAction) {
    if (inputBuffer.length < INPUT_BUFFER_SIZE) {
      inputBuffer.push(action);
      drainBuffer();
    }
  }

  function startRepeat(key: string, action: PlayerAction) {
    if (repeatTimers.has(key)) return;
    const timer = setTimeout(() => {
      repeatTimers.delete(key);
      if (heldKeys.has(key)) {
        enqueueAction(action);
        const intervalTimer = setInterval(() => {
          if (!heldKeys.has(key)) {
            clearInterval(intervalTimer);
            return;
          }
          enqueueAction(action);
        }, REPEAT_INTERVAL_MS);
        repeatTimers.set(key, intervalTimer);
      }
    }, REPEAT_INITIAL_MS);
    repeatTimers.set(key, timer);
  }

  function stopRepeat(key: string) {
    const timer = repeatTimers.get(key);
    if (timer) {
      clearTimeout(timer);
      clearInterval(timer);
      repeatTimers.delete(key);
    }
    heldKeys.delete(key);
  }

  function stopAllRepeats() {
    for (const timer of repeatTimers.values()) {
      clearTimeout(timer);
      clearInterval(timer);
    }
    repeatTimers.clear();
    heldKeys.clear();
  }

  function handleCancel() {
    inputBuffer = [];
    clearPending();
    stopAllRepeats();
    combatCancelSignal++;
    sendAction({ type: 'cancel' });
  }

  function keyToAction(key: string): PlayerAction | null {
    switch (key) {
      case 'ArrowUp':
      case 'w':
      case 'W':
        return { type: 'move_forward' };
      case 'ArrowDown':
      case 's':
      case 'S':
        return { type: 'move_back' };
      case 'a':
      case 'A':
        return { type: 'strafe_left' };
      case 'd':
      case 'D':
        return { type: 'strafe_right' };
      case 'ArrowLeft':
      case 'q':
      case 'Q':
        return { type: 'turn_left' };
      case 'ArrowRight':
      case 'e':
      case 'E':
        return { type: 'turn_right' };
      default:
        return null;
    }
  }

  $effect(() => {
    const unsub = gameStore.subscribe((s) => {
      gameState = s;
      clearPending();
      drainBuffer();

      // Check for new reputation changes in action log
      const actionLog = s?.actionLog ?? [];
      const maxTurn = actionLog.length > 0 ? Math.max(...actionLog.map((e: any) => e.turn)) : 0;
      if (maxTurn > lastActionLogTurn) {
        const newEntries = actionLog.filter((e: any) => e.turn > lastActionLogTurn && e.type === 'rep_changed');
        for (const entry of newEntries) {
          const toast = {
            id: nextToastId++,
            factionId: entry.payload.factionId ?? 'unknown',
            delta: parseInt(entry.payload.delta ?? '0', 10),
            source: entry.payload.source ?? '',
          };
          repToasts = [...repToasts, toast];
          setTimeout(() => {
            repToasts = repToasts.filter((t) => t.id !== toast.id);
          }, 4000);
        }
        lastActionLogTurn = maxTurn;
      } else if (actionLog.length === 0 && lastActionLogTurn > 0) {
        // Reset detected — clear turn tracker so future toasts fire
        lastActionLogTurn = 0;
      }
    });
    return unsub;
  });

  $effect(() => {
    if (gameContainer && !renderer) {
      renderer = new DungeonRenderer(gameContainer);
    }
  });

  $effect(() => {
    if (renderer && gameState) {
      renderer.updateState(gameState);
    }
  });

  onMount(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        e.preventDefault();
        handleCancel();
        return;
      }

      const action = keyToAction(e.key);
      if (!action) return;

      if (gameState?.mode !== 'Exploration') return;

      e.preventDefault();

      if (!heldKeys.has(e.key)) {
        heldKeys.add(e.key);
        enqueueAction(action);
        startRepeat(e.key, action);
      }
    };

    const handleKeyUp = (e: KeyboardEvent) => {
      stopRepeat(e.key);
    };

    window.addEventListener('keydown', handleKeyDown);
    window.addEventListener('keyup', handleKeyUp);
    return () => {
      window.removeEventListener('keydown', handleKeyDown);
      window.removeEventListener('keyup', handleKeyUp);
      stopAllRepeats();
    };
  });

  function handleEnterDungeon(type: string) {
    sendAction({ type: 'enter_dungeon', dungeonType: type });
  }

  function handleCombatAction(actionType: string, targetId: string) {
    const combat = gameState?.combat;
    if (!combat || combat.currentTurnIndex < 0 || combat.currentTurnIndex >= combat.initiativeOrder.length) return;
    const actorId = combat.initiativeOrder[combat.currentTurnIndex];
    sendAction({
      type: 'combat_action',
      action: {
        actorId,
        type: actionType as any,
        targetId: targetId || undefined,
      },
    });
  }

  function handleFlee() {
    sendAction({ type: 'flee_combat' });
  }

  function handleMoveForward() {
    sendAction({ type: 'move_forward' });
  }

  function handleTurnLeft() {
    sendAction({ type: 'turn_left' });
  }

  function handleTurnRight() {
    sendAction({ type: 'turn_right' });
  }

  function handleReturnToTown() {
    sendAction({ type: 'return_to_town' });
  }

  function handleRest() {
    sendAction({ type: 'rest' });
  }

  function handleSave() {
    sendAction({ type: 'save_game' });
  }

  function handleReset() {
    sendAction({ type: 'reset_game' });
  }

  function handleSwapRow(slot: number) {
    sendAction({ type: 'swap_row', slot });
  }

  function handleTavernRecruit(id: string) {
    sendAction({ type: 'tavern_recruit', targetId: id });
  }

  function handleMissionAccept(id: string) {
    sendAction({ type: 'mission_accept', targetId: id });
  }

  function handleVendorPurchase(id: string) {
    sendAction({ type: 'vendor_purchase', targetId: id });
  }

  function handleTravel(targetId: string) {
    sendAction({ type: 'travel', targetId });
  }

  function handleResolveEncounter(choice: string) {
    sendAction({ type: 'resolve_travel_encounter', targetId: choice });
  }

  function turnColor(turns: number): string {
    if (turns >= 13) return '#c44';
    if (turns >= 10) return '#d4a84b';
    return '#ccc';
  }
</script>

<main class="game">
  <div bind:this={gameContainer} class="renderer"></div>
  <div class="ui-layer">
    {#if serverError}
      <div class="error-toast" role="alert">
        <span class="error-code">{serverError.code}</span>
        <span class="error-message">{serverError.message}</span>
      </div>
    {/if}
    {#each repToasts as toast (toast.id)}
      <div class="rep-toast" role="status">
        <span class="rep-toast-faction">{toast.factionId}</span>
        <span class="rep-toast-delta" class:positive={toast.delta > 0} class:negative={toast.delta < 0}>
          {toast.delta > 0 ? '+' : ''}{toast.delta}
        </span>
        <span class="rep-toast-source">({toast.source})</span>
      </div>
    {/each}
    {#if gameState?.mode !== 'Combat'}
      <header class="top-bar">
        <div class="game-title">The Reach</div>
        <div class="game-info">
          <span class="mode-badge">{gameState?.mode || 'Menu'}</span>
          {#if gameState?.hasDungeon}
            <span class="dungeon-badge">Dungeon</span>
          {/if}
          {#if gameState?.overworld != null}
            <span class="turn-counter" style="color: {turnColor(gameState.overworld.turns)}">
              Turn {gameState.overworld.turns}/15
            </span>
          {/if}
        </div>
      </header>
    {/if}
    <section class="viewport">
      {#if gameState?.mode === 'Menu'}
        <TownMenu
          gameState={gameState}
          onEnterDungeon={handleEnterDungeon}
          onSave={handleSave}
          onReset={handleReset}
          onSwapRow={handleSwapRow}
          onTavernRecruit={handleTavernRecruit}
          onMissionAccept={handleMissionAccept}
          onVendorPurchase={handleVendorPurchase}
          onTravel={handleTravel}
        />
      {/if}
      {#if gameState?.mode === 'Combat'}
        <CombatOverlay
          combat={gameState.combat ?? null}
          lastResult={gameState.combatResult ?? null}
          onCombatAction={handleCombatAction}
          onFlee={handleFlee}
          cancelSignal={combatCancelSignal}
        />
      {/if}
      {#if gameState?.mode === 'Exploration'}
        <ExplorationHUD
          gameState={gameState}
          onMoveForward={handleMoveForward}
          onTurnLeft={handleTurnLeft}
          onTurnRight={handleTurnRight}
          onReturnToTown={handleReturnToTown}
          onRest={handleRest}
          onSave={handleSave}
        />
      {/if}
      {#if gameState?.travelEncounter && gameState?.mode === 'Menu'}
        <div class="travel-encounter-overlay" role="dialog" aria-label="Travel encounter">
          <div class="travel-encounter-card">
            <h2 class="travel-encounter-title">{gameState.travelEncounter.name}</h2>
            {#if gameState.travelEncounter.resolutionType === 'stat_test'}
              <p class="travel-encounter-desc">Test: {gameState.travelEncounter.statName}</p>
              <button class="travel-action-btn" onclick={() => handleResolveEncounter('roll')}>Roll</button>
            {:else if gameState.travelEncounter.resolutionType === 'dialogue'}
              <p class="travel-encounter-desc">Diplomatic encounter</p>
              {#if gameState.travelEncounter.options}
                <div class="travel-options">
                  {#each gameState.travelEncounter.options as opt}
                    <button class="travel-action-btn" onclick={() => handleResolveEncounter(opt)}>{opt}</button>
                  {/each}
                </div>
              {/if}
            {:else}
              <p class="travel-encounter-desc">Unexpected encounter</p>
              <button class="travel-action-btn" onclick={() => handleResolveEncounter('continue')}>Continue</button>
            {/if}
          </div>
        </div>
      {/if}
      {#if gameState?.campaignEnded}
        <div class="campaign-end-overlay" role="dialog" aria-label="Campaign complete">
          <div class="campaign-end-card">
            <h2 class="campaign-end-title">Campaign Complete</h2>
            <p class="campaign-end-turns">Final Turn: {gameState.overworld?.turns ?? 15}/15</p>
            <p class="campaign-end-desc">Your journey has come to an end.</p>
            <button class="campaign-end-btn" onclick={() => handleReset()}>New Game</button>
          </div>
        </div>
      {/if}
    </section>
    {#if gameState?.mode !== 'Combat'}
      <footer class="bottom-bar">
        <PartyStatusBar party={gameState?.party || []} />
      </footer>
    {/if}
  </div>
</main>

<style>
  :global(html), :global(body) {
    margin: 0;
    padding: 0;
    width: 100%;
    height: 100%;
    overflow: hidden;
    background: #000;
  }

  :global(#app) {
    width: 100%;
    height: 100%;
  }

  .game {
    display: grid;
    grid-template: 1fr / 1fr;
    width: 100%;
    height: 100%;
    overflow: hidden;
  }

  .renderer,
  .ui-layer {
    grid-row: 1 / -1;
    grid-column: 1 / -1;
  }

  .renderer {
    z-index: 0;
    width: 100%;
    height: 100%;
  }

  .ui-layer {
    z-index: 1;
    display: grid;
    grid-template-rows: auto 1fr auto;
    pointer-events: none;
    width: 100%;
    height: 100%;
    overflow: hidden;
  }

  .ui-layer > * {
    pointer-events: auto;
  }

  .top-bar {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: clamp(0.375rem, 1.5vh, 0.75rem) clamp(0.5rem, 2vw, 1rem);
    background: rgba(0, 0, 0, 0.8);
    border-bottom: 0.0625em solid #333;
  }

  .game-title {
    font-size: clamp(1rem, 2.5vw, 1.5rem);
    font-weight: bold;
    color: #d4a84b;
  }

  .game-info {
    display: flex;
    gap: 0.5rem;
    align-items: center;
  }

  .mode-badge,
  .dungeon-badge,
  .turn-counter {
    padding: 0.2rem 0.5rem;
    border-radius: 0.25rem;
    font-size: clamp(0.65rem, 1.5vw, 0.75rem);
    font-weight: bold;
  }

  .turn-counter {
    background: rgba(0, 0, 0, 0.4);
    border: 0.0625em solid #444;
  }

  .mode-badge {
    background: rgba(68, 170, 255, 0.2);
    color: #66aaff;
  }

  .dungeon-badge {
    background: rgba(212, 168, 75, 0.2);
    color: #d4a84b;
  }

  .viewport {
    display: grid;
    grid-template: 1fr / 1fr;
    min-height: 0;
    overflow: hidden;
  }

  .viewport > :global(*) {
    grid-row: 1 / -1;
    grid-column: 1 / -1;
    min-height: 0;
  }

  .bottom-bar {
    background: rgba(0, 0, 0, 0.8);
  }

  .error-toast {
    position: fixed;
    top: 1rem;
    left: 50%;
    transform: translateX(-50%);
    z-index: 100;
    background: rgba(160, 40, 40, 0.95);
    border: 1px solid #c44;
    border-radius: 0.5rem;
    padding: 0.75em 1.25em;
    display: flex;
    gap: 0.75em;
    align-items: center;
    animation: fadeIn 0.2s ease-out;
    pointer-events: auto;
  }

  .rep-toast {
    position: fixed;
    top: 1rem;
    right: 1rem;
    z-index: 100;
    background: rgba(0, 0, 0, 0.9);
    border: 1px solid #d4a84b;
    border-radius: 0.5rem;
    padding: 0.75em 1.25em;
    display: flex;
    gap: 0.5em;
    align-items: center;
    animation: fadeIn 0.2s ease-out;
    pointer-events: auto;
    font-size: 0.875rem;
  }

  .rep-toast-faction {
    color: #ccc;
    text-transform: capitalize;
    font-weight: bold;
  }

  .rep-toast-delta.positive {
    color: #4a4;
  }

  .rep-toast-delta.negative {
    color: #c44;
  }

  .rep-toast-source {
    color: #888;
    font-size: 0.75rem;
  }

  .error-code {
    font-size: 0.75rem;
    font-weight: bold;
    text-transform: uppercase;
    letter-spacing: 0.05em;
    color: #fcc;
    background: rgba(0, 0, 0, 0.3);
    padding: 0.2em 0.5em;
    border-radius: 0.25em;
  }

  .error-message {
    font-size: 0.875rem;
    color: #fff;
  }

  .travel-encounter-overlay {
    position: fixed;
    inset: 0;
    display: flex;
    align-items: center;
    justify-content: center;
    background: rgba(0, 0, 0, 0.7);
    z-index: 50;
    pointer-events: auto;
  }

  .travel-encounter-card {
    background: #1a1a2e;
    border: 1px solid #444;
    border-radius: 0.5rem;
    padding: 1.5rem;
    min-width: 280px;
    max-width: 90vw;
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
  }

  .travel-encounter-title {
    margin: 0;
    font-size: 1.25rem;
    color: #d4a84b;
  }

  .travel-encounter-desc {
    margin: 0;
    color: #ccc;
    font-size: 0.875rem;
  }

  .travel-options {
    display: flex;
    flex-direction: column;
    gap: 0.5rem;
  }

  .travel-action-btn {
    background: #2a2a4e;
    border: 1px solid #555;
    color: #fff;
    padding: 0.5rem 1rem;
    border-radius: 0.25rem;
    cursor: pointer;
    font-size: 0.875rem;
  }

  .travel-action-btn:hover {
    background: #3a3a5e;
  }

  .campaign-end-overlay {
    position: fixed;
    inset: 0;
    display: flex;
    align-items: center;
    justify-content: center;
    background: rgba(0, 0, 0, 0.9);
    z-index: 60;
    pointer-events: auto;
  }

  .campaign-end-card {
    background: #1a1a2e;
    border: 1px solid #444;
    border-radius: 0.5rem;
    padding: 2rem;
    min-width: 280px;
    max-width: 90vw;
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
    align-items: center;
    text-align: center;
  }

  .campaign-end-title {
    margin: 0;
    font-size: 1.5rem;
    color: #d4a84b;
  }

  .campaign-end-turns {
    margin: 0;
    color: #c44;
    font-size: 1rem;
    font-weight: bold;
  }

  .campaign-end-desc {
    margin: 0;
    color: #888;
    font-size: 0.875rem;
  }

  .campaign-end-btn {
    margin-top: 0.5rem;
    padding: 0.5rem 1.5rem;
    background: rgba(68, 170, 68, 0.2);
    border: 1px solid #44aa44;
    border-radius: 0.25rem;
    color: #88cc88;
    cursor: pointer;
    font-size: 0.875rem;
  }

  .campaign-end-btn:hover {
    background: rgba(68, 170, 68, 0.35);
  }

  @keyframes fadeIn {
    from { opacity: 0; transform: translateY(-0.5em); }
    to { opacity: 1; transform: translateY(0); }
  }
</style>
