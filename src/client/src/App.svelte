<script lang="ts">
  import { onMount, onDestroy } from 'svelte';
  import { GameClient } from './net/GameClient';
  import { DungeonRenderer } from './renderer/DungeonRenderer';
  import AutoMap from './ui/AutoMap.svelte';
  import PartyStatusBar from './ui/PartyStatusBar.svelte';
  import CombatOverlay from './ui/CombatOverlay.svelte';
  import type { GameState, CombatAction } from './types/game';

  let gameContainer: HTMLElement | undefined = $state(undefined);
  let renderer: DungeonRenderer | undefined = $state(undefined);
  let client: GameClient | undefined = $state(undefined);
  let connected = $state(false);
  let state: GameState | null = $state(null);
  let statusMessage = $state('Connecting...');

  onMount(() => {
    if (!gameContainer) return;
    
    // Initialize renderer
    renderer = new DungeonRenderer(gameContainer);

    // Initialize client
    client = new GameClient();
    
    client.onConnect(() => {
      connected = true;
      statusMessage = 'Connected';
      // Generate dungeon once connected
      client?.sendAction({ type: 'generate_dungeon' });
    });

    client.onDisconnect(() => {
      connected = false;
      statusMessage = 'Disconnected - check console (F12)';
    });

    client.onState((newState) => {
      state = newState;
      renderer?.updateState(newState);
    });

    client.connect();

    // Keyboard input
    const handleKeyDown = (e: KeyboardEvent) => {
      if (!connected || !client) return;
      if (state?.mode === 'Combat') return; // disable movement in combat
      
      switch (e.key) {
        case 'ArrowUp':
        case 'w':
        case 'W':
          client.sendAction({ type: 'move_forward' });
          break;
        case 'ArrowLeft':
        case 'a':
        case 'A':
          client.sendAction({ type: 'turn_left' });
          break;
        case 'ArrowRight':
        case 'd':
        case 'D':
          client.sendAction({ type: 'turn_right' });
          break;
      }
    };

    window.addEventListener('keydown', handleKeyDown);

    return () => {
      window.removeEventListener('keydown', handleKeyDown);
    };
  });

  onDestroy(() => {
    client?.disconnect();
    renderer?.dispose();
  });

  function generateDungeon() {
    client?.sendAction({ type: 'generate_dungeon' });
  }

  function sendCombatAction(action: CombatAction) {
    client?.sendAction({ type: 'combat_action', action });
  }

  function fleeCombat() {
    client?.sendAction({ type: 'flee_combat' });
  }
</script>

<main class="game-container">
  <div bind:this={gameContainer} class="renderer"></div>
  
  <div class="ui-overlay">
    <div class="status-bar">
      <span class="connection-status" class:connected>
        {statusMessage}
      </span>
      {#if state}
        <span class="position">
          Pos: ({state.player.x}, {state.player.y}) Facing: {state.player.facing}
        </span>
      {/if}
    </div>

    <div class="controls">
      <button onclick={generateDungeon}>New Dungeon</button>
    </div>

    <div class="instructions">
      <p>Use <kbd>↑</kbd>/<kbd>W</kbd> to move forward</p>
      <p>Use <kbd>←</kbd>/<kbd>A</kbd> and <kbd>→</kbd>/<kbd>D</kbd> to turn</p>
    </div>

    <div class="automap-wrapper">
      <AutoMap {state} />
    </div>

    {#if state?.party}
      <div class="party-wrapper">
        <PartyStatusBar members={state.party} />
      </div>
    {/if}

    {#if state?.mode === 'Combat' && state.combat}
      <CombatOverlay combat={state.combat} onAction={sendCombatAction} onFlee={fleeCombat} />
    {/if}
  </div>
</main>

<style>
  .game-container {
    width: 100vw;
    height: 100vh;
    min-width: 800px;
    min-height: 600px;
    position: relative;
    overflow: hidden;
  }

  .renderer {
    width: 100%;
    height: 100%;
  }

  .ui-overlay {
    position: absolute;
    top: 0;
    left: 0;
    right: 0;
    bottom: 0;
    pointer-events: none;
    display: flex;
    flex-direction: column;
    justify-content: space-between;
    padding: 1rem;
  }

  .ui-overlay > * {
    pointer-events: auto;
  }

  .status-bar {
    display: flex;
    gap: 1rem;
    background: rgba(0, 0, 0, 0.7);
    padding: 0.5rem 1rem;
    border-radius: 4px;
    color: #fff;
    font-family: monospace;
    font-size: 0.875rem;
  }

  .connection-status {
    color: #ff4444;
  }

  .connection-status.connected {
    color: #44ff44;
  }

  .controls {
    position: absolute;
    top: 1rem;
    right: 1rem;
  }

  button {
    background: #333;
    color: #fff;
    border: 1px solid #555;
    padding: 0.5rem 1rem;
    border-radius: 4px;
    cursor: pointer;
    font-family: inherit;
  }

  button:hover {
    background: #444;
  }

  .instructions {
    background: rgba(0, 0, 0, 0.7);
    padding: 1rem;
    border-radius: 4px;
    color: #ccc;
    font-size: 0.875rem;
    max-width: 300px;
  }

  .automap-wrapper {
    position: absolute;
    top: 1rem;
    right: 1rem;
  }

  .party-wrapper {
    position: absolute;
    bottom: 0;
    left: 0;
    right: 0;
  }

  .instructions p {
    margin: 0.25rem 0;
  }

  kbd {
    background: #333;
    padding: 0.125rem 0.375rem;
    border-radius: 3px;
    border: 1px solid #555;
    font-family: monospace;
    font-size: 0.75rem;
  }
</style>
