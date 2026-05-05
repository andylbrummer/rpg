<script lang="ts">
  import type { GameState } from '../types/game';

  interface Props {
    gameState: GameState | null;
  }

  let { gameState }: Props = $props();
  let canvas: HTMLCanvasElement | undefined = $state(undefined);
  let ctx: CanvasRenderingContext2D | null = null;

  const CELL_SIZE = 8;
  const MAP_SIZE = 200;

  $effect(() => {
    if (!canvas || !gameState?.hasDungeon) return;
    ctx = canvas.getContext('2d');
    if (!ctx) return;
    renderMap(gameState);
  });

  function renderMap(gameState: GameState) {
    if (!ctx) return;
    
    ctx.clearRect(0, 0, MAP_SIZE, MAP_SIZE);
    
    if (!gameState.explored || gameState.explored.length === 0) return;

    // Find bounds
    let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
    for (const tile of gameState.explored) {
      minX = Math.min(minX, tile.x);
      minY = Math.min(minY, tile.y);
      maxX = Math.max(maxX, tile.x);
      maxY = Math.max(maxY, tile.y);
    }

    const width = maxX - minX + 1;
    const height = maxY - minY + 1;
    const offsetX = (MAP_SIZE - width * CELL_SIZE) / 2 - minX * CELL_SIZE;
    const offsetY = (MAP_SIZE - height * CELL_SIZE) / 2 - minY * CELL_SIZE;

    // Draw explored tiles
    for (const tile of gameState.explored) {
      const x = tile.x * CELL_SIZE + offsetX;
      const y = tile.y * CELL_SIZE + offsetY;
      
      switch (tile.type) {
        case 'Floor':
          ctx.fillStyle = '#444';
          ctx.fillRect(x, y, CELL_SIZE - 1, CELL_SIZE - 1);
          break;
        case 'Wall':
          ctx.fillStyle = '#666';
          ctx.fillRect(x, y, CELL_SIZE - 1, CELL_SIZE - 1);
          break;
        case 'Door':
          ctx.fillStyle = '#8B4513';
          ctx.fillRect(x, y, CELL_SIZE - 1, CELL_SIZE - 1);
          break;
        case 'SecretDoor':
          ctx.fillStyle = '#444';
          ctx.fillRect(x, y, CELL_SIZE - 1, CELL_SIZE - 1);
          break;
      }
    }

    // Draw player position
    const px = gameState.player.x * CELL_SIZE + offsetX;
    const py = gameState.player.y * CELL_SIZE + offsetY;
    
    ctx.fillStyle = '#44aaff';
    ctx.beginPath();
    ctx.arc(px + CELL_SIZE / 2, py + CELL_SIZE / 2, CELL_SIZE / 2 - 1, 0, Math.PI * 2);
    ctx.fill();

    // Draw facing direction
    ctx.strokeStyle = '#44aaff';
    ctx.lineWidth = 1.5;
    ctx.beginPath();
    const cx = px + CELL_SIZE / 2;
    const cy = py + CELL_SIZE / 2;
    const dirLen = CELL_SIZE;
    
    let dx = 0, dy = 0;
    switch (gameState.player.facing) {
      case 'North': dy = -1; break;
      case 'South': dy = 1; break;
      case 'East': dx = 1; break;
      case 'West': dx = -1; break;
    }
    
    ctx.moveTo(cx, cy);
    ctx.lineTo(cx + dx * dirLen, cy + dy * dirLen);
    ctx.stroke();
  }
</script>

<div class="automap-container">
  <canvas bind:this={canvas} width={MAP_SIZE} height={MAP_SIZE}></canvas>
</div>

<style>
  .automap-container {
    background: rgba(0, 0, 0, 0.8);
    border: 1px solid #444;
    border-radius: 4px;
    padding: 4px;
    width: fit-content;
  }

  canvas {
    display: block;
  }
</style>
