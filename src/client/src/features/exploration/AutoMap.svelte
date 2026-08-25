<script lang="ts">
  import type { GameState } from '$shared/types/game';

  interface Props {
    gameState: GameState | null;
  }

  let { gameState }: Props = $props();
  let canvas: HTMLCanvasElement | undefined = $state(undefined);
  let ctx: CanvasRenderingContext2D | null = null;
  let container: HTMLDivElement | undefined = $state(undefined);

  const BASE_CELL = 8;
  const BASE_MAP = 200;

  $effect(() => {
    if (!canvas || !gameState?.hasDungeon) return;
    ctx = canvas.getContext('2d');
    if (!ctx) return;
    renderMap(gameState);
  });

  function renderMap(gameState: GameState) {
    if (!ctx || !canvas || !container) return;

    // Scale canvas to container size
    const containerSize = Math.min(container.clientWidth, container.clientHeight);
    const scale = containerSize / BASE_MAP;
    const cellSize = BASE_CELL * scale;
    const mapSize = containerSize;

    canvas.width = mapSize;
    canvas.height = mapSize;

    ctx.clearRect(0, 0, mapSize, mapSize);

    if (!gameState.explored || gameState.explored.length === 0) return;

    let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
    for (const tile of gameState.explored) {
      minX = Math.min(minX, tile.x);
      minY = Math.min(minY, tile.y);
      maxX = Math.max(maxX, tile.x);
      maxY = Math.max(maxY, tile.y);
    }

    const width = maxX - minX + 1;
    const height = maxY - minY + 1;
    const offsetX = (mapSize - width * cellSize) / 2 - minX * cellSize;
    const offsetY = (mapSize - height * cellSize) / 2 - minY * cellSize;

    for (const tile of gameState.explored) {
      const x = tile.x * cellSize + offsetX;
      const y = tile.y * cellSize + offsetY;

      if (tile.type === 'Floor' || tile.type === 'StairsUp' || tile.type === 'StairsDown') {
        ctx.fillStyle = tile.type === 'StairsUp' ? '#aa8844' : tile.type === 'StairsDown' ? '#886644' : '#444';
        ctx.fillRect(x + scale, y + scale, cellSize - 2 * scale, cellSize - 2 * scale);
      }

      ctx.lineWidth = 1.5 * scale;

      if (tile.north === 'Wall' || tile.north === 'SecretDoor') {
        ctx.strokeStyle = tile.north === 'SecretDoor' ? '#887766' : '#666';
        ctx.beginPath();
        ctx.moveTo(x, y);
        ctx.lineTo(x + cellSize, y);
        ctx.stroke();
      } else if (tile.north === 'Door') {
        ctx.strokeStyle = '#8B4513';
        ctx.lineWidth = 2 * scale;
        ctx.beginPath();
        ctx.moveTo(x + scale, y);
        ctx.lineTo(x + cellSize - scale, y);
        ctx.stroke();
        ctx.lineWidth = 1.5 * scale;
      }

      if (tile.south === 'Wall' || tile.south === 'SecretDoor') {
        ctx.strokeStyle = tile.south === 'SecretDoor' ? '#887766' : '#666';
        ctx.beginPath();
        ctx.moveTo(x, y + cellSize);
        ctx.lineTo(x + cellSize, y + cellSize);
        ctx.stroke();
      } else if (tile.south === 'Door') {
        ctx.strokeStyle = '#8B4513';
        ctx.lineWidth = 2 * scale;
        ctx.beginPath();
        ctx.moveTo(x + scale, y + cellSize);
        ctx.lineTo(x + cellSize - scale, y + cellSize);
        ctx.stroke();
        ctx.lineWidth = 1.5 * scale;
      }

      if (tile.east === 'Wall' || tile.east === 'SecretDoor') {
        ctx.strokeStyle = tile.east === 'SecretDoor' ? '#887766' : '#666';
        ctx.beginPath();
        ctx.moveTo(x + cellSize, y);
        ctx.lineTo(x + cellSize, y + cellSize);
        ctx.stroke();
      } else if (tile.east === 'Door') {
        ctx.strokeStyle = '#8B4513';
        ctx.lineWidth = 2 * scale;
        ctx.beginPath();
        ctx.moveTo(x + cellSize, y + scale);
        ctx.lineTo(x + cellSize, y + cellSize - scale);
        ctx.stroke();
        ctx.lineWidth = 1.5 * scale;
      }

      if (tile.west === 'Wall' || tile.west === 'SecretDoor') {
        ctx.strokeStyle = tile.west === 'SecretDoor' ? '#887766' : '#666';
        ctx.beginPath();
        ctx.moveTo(x, y);
        ctx.lineTo(x, y + cellSize);
        ctx.stroke();
      } else if (tile.west === 'Door') {
        ctx.strokeStyle = '#8B4513';
        ctx.lineWidth = 2 * scale;
        ctx.beginPath();
        ctx.moveTo(x, y + scale);
        ctx.lineTo(x, y + cellSize - scale);
        ctx.stroke();
        ctx.lineWidth = 1.5 * scale;
      }
    }

    const px = gameState.player.x * cellSize + offsetX;
    const py = gameState.player.y * cellSize + offsetY;

    ctx.fillStyle = '#44aaff';
    ctx.beginPath();
    ctx.arc(px + cellSize / 2, py + cellSize / 2, cellSize / 2 - scale, 0, Math.PI * 2);
    ctx.fill();

    ctx.strokeStyle = '#44aaff';
    ctx.lineWidth = 1.5 * scale;
    ctx.beginPath();
    const cx = px + cellSize / 2;
    const cy = py + cellSize / 2;
    const dirLen = cellSize;

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

<div class="automap-container" bind:this={container}>
  <canvas bind:this={canvas}></canvas>
</div>

<style>
  .automap-container {
    background: rgba(0, 0, 0, 0.8);
    border: 0.0625em solid #444;
    border-radius: 0.25em;
    padding: 0.25em;
    width: min(13em, 25vw);
    height: min(13em, 25vw);
    overflow: hidden;
    box-sizing: border-box;
  }

  canvas {
    display: block;
    width: 100%;
    height: 100%;
  }
</style>
