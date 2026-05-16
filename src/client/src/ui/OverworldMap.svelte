<script lang="ts">
  import type { OverworldState } from '../types/game';

  interface Props {
    overworld: OverworldState;
    onTravel: (targetId: string) => void;
    onClose: () => void;
  }

  let { overworld, onTravel, onClose }: Props = $props();

  let confirmTarget = $state<string | null>(null);
  let tooltip = $state<{ x: number; y: number; text: string } | null>(null);

  function getNodePosition(index: number) {
    return { x: 80 + index * 240, y: 100 };
  }

  let nodePositions = $derived(
    new Map(overworld.nodes.map((node, i) => [node.id, getNodePosition(i)]))
  );

  function routeColor(danger: number, status: string): string {
    if (status === 'blocked') return '#444';
    if (status === 'bloomaffected') return '#a4a';
    if (status === 'contested') return '#a44';
    if (danger <= 2) return '#44aa44';
    if (danger === 3) return '#d4a84b';
    return '#c44';
  }

  function routeDashArray(status: string): string {
    if (status === 'blocked') return '4,4';
    if (status === 'contested') return '8,4';
    return 'none';
  }

  function getRouteBetween(a: string, b: string) {
    return overworld.routes.find(
      (r) => (r.from === a && r.to === b) || (r.to === a && r.from === b)
    );
  }

  function handleNodeClick(nodeId: string) {
    if (nodeId === overworld.currentNodeId) return;
    const r = getRouteBetween(nodeId, overworld.currentNodeId);
    if (!r || r.status === 'blocked') return;
    confirmTarget = nodeId;
  }

  function confirmTravel() {
    if (confirmTarget) {
      onTravel(confirmTarget);
      confirmTarget = null;
    }
  }

  function cancelTravel() {
    confirmTarget = null;
  }

  function handleRouteEnter(
    e: MouseEvent,
    r: { distance: number; dangerRating: number; terrain: string; status: string }
  ) {
    const statusLabel = r.status === 'bloomaffected' ? 'Bloom-Affected' : r.status;
    tooltip = {
      x: e.clientX + 12,
      y: e.clientY + 12,
      text: `Distance: ${r.distance} turns | Danger: ${r.dangerRating} | Terrain: ${r.terrain} | Status: ${statusLabel}`,
    };
  }

  function handleRouteMove(e: MouseEvent) {
    if (tooltip) {
      tooltip = { ...tooltip, x: e.clientX + 12, y: e.clientY + 12 };
    }
  }

  function handleRouteLeave() {
    tooltip = null;
  }
</script>

<div class="map-panel" role="dialog" aria-modal="true" aria-label="Overworld map">
  <button class="close-btn" onclick={onClose} aria-label="Close map">×</button>
  <h2 class="map-title">Overworld</h2>
  <svg viewBox="0 0 400 200" class="map-svg">
    {#each overworld.routes as route (route.from + '-' + route.to)}
      {@const fromPos = nodePositions.get(route.from)}
      {@const toPos = nodePositions.get(route.to)}
      {#if fromPos && toPos}
        <line
          x1={fromPos.x}
          y1={fromPos.y}
          x2={toPos.x}
          y2={toPos.y}
          stroke={routeColor(route.dangerRating, route.status)}
          stroke-width={route.status === 'blocked' ? 2 : 4}
          stroke-dasharray={routeDashArray(route.status)}
          opacity={route.status === 'blocked' ? 0.4 : 1}
          class="route-line"
          role="img"
          aria-label="Route danger {route.dangerRating} status {route.status}"
          onmouseenter={(e) => handleRouteEnter(e, route)}
          onmousemove={handleRouteMove}
          onmouseleave={handleRouteLeave}
        />
      {/if}
    {/each}
    {#each overworld.nodes as node (node.id)}
      {@const pos = nodePositions.get(node.id)}
      {#if pos}
        {@const isCurrent = node.id === overworld.currentNodeId}
        <g
          class="node-group"
          class:current={isCurrent}
          role="button"
          tabindex="0"
          aria-label={node.name}
          onclick={() => handleNodeClick(node.id)}
          onkeydown={(e) => {
            if (e.key === 'Enter' || e.key === ' ') {
              e.preventDefault();
              handleNodeClick(node.id);
            }
          }}
        >
          {#if isCurrent}
            <circle cx={pos.x} cy={pos.y} r="30" fill="none" stroke="#d4a84b" stroke-width="3" />
          {/if}
          <circle cx={pos.x} cy={pos.y} r="24" fill="#222" stroke="#666" stroke-width="2" />
          {#if node.type === 'town'}
            <g transform="translate({pos.x - 10},{pos.y - 10})">
              <path d="M12 3L2 12h3v8h6v-6h2v6h6v-8h3L12 3z" fill="#888" transform="scale(0.833)" />
            </g>
          {:else}
            <g transform="translate({pos.x - 10},{pos.y - 10})">
              <path d="M2 22l2-6h3l1.5-4h5L15 16h3l2 6H2zM12 2C8 2 5 5 5 9c0 2 1 4 2.5 5h9C18 13 19 11 19 9c0-4-3-7-7-7z" fill="#888" transform="scale(0.833)" />
            </g>
          {/if}
          <text x={pos.x} y={pos.y + 40} text-anchor="middle" fill="#ccc" font-size="10">{node.name}</text>
        </g>
      {/if}
    {/each}
  </svg>

  {#if confirmTarget}
    {@const targetNode = overworld.nodes.find((n) => n.id === confirmTarget)}
    {@const routeToTarget = getRouteBetween(confirmTarget, overworld.currentNodeId)}
    <div class="confirm-modal" role="alertdialog" aria-modal="true" aria-label="Confirm travel">
      <p>Travel to <strong>{targetNode?.name}</strong>?</p>
      <p class="cost">Cost: {routeToTarget?.distance ?? '?'} turns</p>
      {#if routeToTarget?.status === 'contested'}
        <p class="warning">Warning: Route is contested. Increased danger.</p>
      {:else if routeToTarget?.status === 'bloomaffected'}
        <p class="warning">Warning: Bloom-affected route. Unusual encounters possible.</p>
      {/if}
      <div class="confirm-actions">
        <button class="confirm-btn" onclick={confirmTravel}>Travel</button>
        <button class="cancel-btn" onclick={cancelTravel}>Cancel</button>
      </div>
    </div>
  {/if}

  {#if tooltip}
    <div class="tooltip" style="left: {tooltip.x}px; top: {tooltip.y}px;">
      {tooltip.text}
    </div>
  {/if}
</div>

<style>
  .map-panel {
    position: absolute;
    inset: 10%;
    background: rgba(0, 0, 0, 0.95);
    border: 1px solid #444;
    border-radius: 0.5rem;
    display: flex;
    flex-direction: column;
    align-items: center;
    padding: 1rem;
    z-index: 50;
  }

  .close-btn {
    position: absolute;
    top: 0.5rem;
    right: 0.5rem;
    background: none;
    border: none;
    color: #ccc;
    font-size: 1.5rem;
    cursor: pointer;
  }

  .map-title {
    margin: 0 0 0.5rem;
    color: #d4a84b;
    font-size: 1.25rem;
  }

  .map-svg {
    width: 100%;
    height: 100%;
    max-width: 600px;
  }

  .route-line {
    cursor: pointer;
  }

  .node-group {
    cursor: pointer;
  }

  .node-group.current {
    cursor: default;
  }

  .confirm-modal {
    position: absolute;
    top: 50%;
    left: 50%;
    transform: translate(-50%, -50%);
    background: #1a1a1a;
    border: 1px solid #555;
    border-radius: 0.375rem;
    padding: 1rem;
    display: flex;
    flex-direction: column;
    gap: 0.5rem;
    min-width: 12rem;
    color: #ccc;
  }

  .cost {
    color: #d4a84b;
    font-size: 0.875rem;
  }

  .confirm-actions {
    display: flex;
    gap: 0.5rem;
    justify-content: flex-end;
  }

  .confirm-btn,
  .cancel-btn {
    padding: 0.25rem 0.75rem;
    border-radius: 0.25rem;
    cursor: pointer;
    font-size: 0.875rem;
  }

  .confirm-btn {
    background: rgba(68, 170, 68, 0.2);
    border: 1px solid #44aa44;
    color: #88cc88;
  }

  .cancel-btn {
    background: rgba(170, 68, 68, 0.2);
    border: 1px solid #aa4444;
    color: #cc8888;
  }

  .warning {
    color: #d4a84b;
    font-size: 0.8rem;
    font-style: italic;
  }

  .tooltip {
    position: fixed;
    background: rgba(0, 0, 0, 0.9);
    border: 1px solid #555;
    border-radius: 0.25rem;
    padding: 0.375rem 0.5rem;
    color: #ccc;
    font-size: 0.75rem;
    pointer-events: none;
    z-index: 100;
    white-space: nowrap;
  }
</style>
