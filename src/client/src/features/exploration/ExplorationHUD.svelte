<script lang="ts">
  import AutoMap from './AutoMap.svelte';
  import type { GameState } from '$shared/types/game';
  import type { UiIntent } from '$shared/actions/uiIntent';

  interface Props {
    gameState: GameState | null;
    onMoveForward: () => void;
    onTurnLeft: () => void;
    onTurnRight: () => void;
    onReturnToTown: () => void;
    onRest: () => void;
    onSave: () => void;
    onPickup: () => void;
    onIntent: (intent: UiIntent) => void;
  }

  let {
    gameState,
    onMoveForward,
    onTurnLeft,
    onTurnRight,
    onReturnToTown,
    onRest,
    onSave,
    onPickup,
    onIntent
  }: Props = $props();

  const currentTile = $derived(
    gameState?.tiles?.find(
      t => t.x === gameState?.player?.x && t.y === gameState?.player?.y
    )
  );
  const currentTileHasLoot = $derived(!!currentTile?.hasLoot);
  const currentLootName = $derived(
    currentTile?.lootName
      ? currentTile.lootName
          .split(/[_-]/)
          .filter(Boolean)
          .map(w => w.charAt(0).toUpperCase() + w.slice(1))
          .join(' ')
      : 'item'
  );

  // Cartographer-detected secrets the player can act on are those on the tile
  // they currently occupy or an orthogonally adjacent tile (the wall is then
  // within reach). Each maps to a Break control that emits breakWall{id}.
  const reachableSecrets = $derived(
    (gameState?.detectedSecrets ?? []).filter(s => {
      const px = gameState?.player?.x;
      const py = gameState?.player?.y;
      if (px == null || py == null) return false;
      const dist = Math.abs(s.x - px) + Math.abs(s.y - py);
      return dist <= 1;
    })
  );

  function secretLabel(wall?: string | null): string {
    return wall ? `Break wall (${wall})` : 'Break wall';
  }
</script>

<div class="exploration-hud">
  <div class="hud-main">
    <div class="hud-left">
      <AutoMap {gameState} />
    </div>

    <div class="hud-center">
      <div class="compass">
        Facing: {gameState?.player?.facing}
      </div>
      <div class="position">
        Position: ({gameState?.player?.x}, {gameState?.player?.y})
      </div>
      {#if currentTileHasLoot}
        <button class="hud-btn take-btn" onclick={onPickup}>
          Take {currentLootName}
        </button>
      {/if}
      <button class="hud-btn search-btn" onclick={() => onIntent({ kind: 'searchSecrets' })}>
        Search
      </button>
      {#each reachableSecrets as secret (secret.id)}
        <button
          class="hud-btn break-btn"
          onclick={() => onIntent({ kind: 'breakWall', targetId: secret.id })}
        >
          {secretLabel(secret.wall)}
        </button>
      {/each}
    </div>

    <div class="hud-right">
      <button class="hud-btn return-btn" onclick={onReturnToTown}>
        Return to Town
      </button>
      <button class="hud-btn rest-btn" onclick={onRest}>
        Rest
      </button>
      <button class="hud-btn save-btn" onclick={onSave}>
        Save
      </button>
    </div>
  </div>

  <div class="movement-bar">
    <button class="move-btn turn-left" onclick={onTurnLeft}>←</button>
    <button class="move-btn move-forward" onclick={onMoveForward}>↑</button>
    <button class="move-btn turn-right" onclick={onTurnRight}>→</button>
  </div>
</div>

<style>
  .exploration-hud {
    display: flex;
    flex-direction: column;
    width: 100%;
    height: 100%;
    padding: 0.75rem;
    gap: 0.75rem;
    box-sizing: border-box;
    pointer-events: none;
  }

  .exploration-hud > * {
    pointer-events: auto;
  }

  .hud-main {
    flex: 1 1 auto;
    display: flex;
    justify-content: space-between;
    align-items: flex-start;
    gap: 1rem;
    min-height: 0;
    overflow: hidden;
  }

  .hud-left {
    display: flex;
    flex-direction: column;
    align-items: flex-start;
    gap: 0.5rem;
  }

  .hud-center {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 0.5rem;
    margin-top: 1rem;
  }

  .compass {
    background: rgba(0, 0, 0, 0.6);
    padding: 0.5rem 1rem;
    border-radius: 0.25rem;
    font-size: clamp(0.75rem, 1.8vw, 0.875rem);
    font-weight: bold;
    color: #44aaff;
  }

  .position {
    background: rgba(0, 0, 0, 0.6);
    padding: 0.5rem 1rem;
    border-radius: 0.25rem;
    font-size: clamp(0.65rem, 1.5vw, 0.8rem);
    color: #888;
  }

  .hud-right {
    display: flex;
    flex-direction: column;
    gap: 0.5rem;
  }

  .hud-btn {
    padding: 0.4rem 0.8rem;
    background: rgba(0, 0, 0, 0.6);
    border: 0.0625em solid #666;
    border-radius: 0.25rem;
    color: #ccc;
    cursor: pointer;
    font-size: clamp(0.7rem, 1.5vw, 0.8rem);
    transition: background 0.15s;
    min-width: 6rem;
  }

  .hud-btn:hover {
    background: rgba(100, 100, 100, 0.4);
  }

  .return-btn { border-color: #886644; color: #ccaa77; }
  .rest-btn { border-color: #446644; color: #88cc88; }
  .save-btn { border-color: #444466; color: #8888cc; }
  .take-btn { border-color: #d4a84b; color: #ffcc44; }
  .search-btn { border-color: #4488cc; color: #88bbee; }
  .break-btn { border-color: #aa6644; color: #dd9966; }

  .movement-bar {
    flex: 0 0 auto;
    display: flex;
    justify-content: center;
    gap: 0.5rem;
    padding: 0.5rem;
    background: rgba(0, 0, 0, 0.6);
    border-radius: 0.5rem;
  }

  .move-btn {
    width: clamp(3rem, 8vw, 4rem);
    height: clamp(2.5rem, 7vw, 3.5rem);
    background: rgba(0, 0, 0, 0.6);
    border: 0.0625em solid #666;
    border-radius: 0.375rem;
    color: #ccc;
    font-size: clamp(0.9rem, 2vw, 1.1rem);
    cursor: pointer;
    transition: background 0.15s;
  }

  .move-btn:hover {
    background: rgba(100, 100, 100, 0.5);
  }

  .move-forward {
    border-color: #446644;
    color: #88cc88;
  }

  .move-btn:active {
    transform: translateY(0.1rem);
  }
</style>
