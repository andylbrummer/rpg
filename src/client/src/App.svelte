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

  serverErrorStore.subscribe((err) => {
    serverError = err;
  });

  $effect(() => {
    const unsub = gameStore.subscribe(s => { gameState = s; });
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
      if (gameState?.mode !== 'Exploration') return;

      switch (e.key) {
        case 'ArrowUp':
        case 'w':
        case 'W':
          e.preventDefault();
          sendAction({ type: 'move_forward' });
          break;
        case 'ArrowLeft':
        case 'a':
        case 'A':
          e.preventDefault();
          sendAction({ type: 'turn_left' });
          break;
        case 'ArrowRight':
        case 'd':
        case 'D':
          e.preventDefault();
          sendAction({ type: 'turn_right' });
          break;
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
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
      }
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
    {#if gameState?.mode !== 'Combat'}
      <header class="top-bar">
        <div class="game-title">The Reach</div>
        <div class="game-info">
          <span class="mode-badge">{gameState?.mode || 'Menu'}</span>
          {#if gameState?.hasDungeon}
            <span class="dungeon-badge">Dungeon</span>
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
        />
      {/if}
      {#if gameState?.mode === 'Combat'}
        <CombatOverlay
          combat={gameState.combat ?? null}
          lastResult={gameState.combatResult ?? null}
          onCombatAction={handleCombatAction}
          onFlee={handleFlee}
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
  .dungeon-badge {
    padding: 0.2rem 0.5rem;
    border-radius: 0.25rem;
    font-size: clamp(0.65rem, 1.5vw, 0.75rem);
    font-weight: bold;
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

  @keyframes fadeIn {
    from { opacity: 0; transform: translateY(-0.5em); }
    to { opacity: 1; transform: translateY(0); }
  }
</style>
