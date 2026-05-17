<script lang="ts">
  import type { CombatResult } from '$shared/types/game';

  interface Props {
    result: CombatResult;
    onDismiss: () => void;
  }

  let { result, onDismiss }: Props = $props();

  $effect(() => {
    const timer = setTimeout(onDismiss, 4000);
    return () => clearTimeout(timer);
  });
</script>

<div class="toast-backdrop">
  <div class="toast" class:victory={result.victory} class:defeat={!result.victory}>
    <div class="title">
      {result.victory ? 'Victory!' : 'Defeat...'}
    </div>
    <div class="details">
      {#if result.victory}
        <span>+{result.xpGained} XP</span>
        {#if result.levelUps.length > 0}
          <span class="level-up">{result.levelUps.join(', ')} leveled up!</span>
        {/if}
      {/if}
      <span class="rounds">{result.roundCount} rounds</span>
    </div>
  </div>
</div>

<style>
  .toast-backdrop {
    display: grid;
    place-items: center;
    width: 100%;
    height: 100%;
    background: rgba(0, 0, 0, 0.6);
    pointer-events: none;
  }

  .toast {
    background: rgba(0, 0, 0, 0.9);
    border: 0.125em solid #666;
    border-radius: 0.5em;
    padding: 1.5em 2em;
    text-align: center;
    animation: fadeIn 0.3s ease-out;
    pointer-events: auto;
  }

  .toast.victory {
    border-color: #4a4;
  }

  .toast.defeat {
    border-color: #a44;
  }

  .title {
    font-size: clamp(1.25rem, 3vw, 1.75rem);
    font-weight: bold;
    margin-bottom: 0.5em;
  }

  .victory .title {
    color: #4f4;
  }

  .defeat .title {
    color: #f44;
  }

  .details {
    display: grid;
    grid-template-rows: auto;
    gap: 0.25em;
    color: #ccc;
    font-size: clamp(0.8rem, 2vw, 0.875rem);
  }

  .level-up {
    color: #fd4;
    font-weight: bold;
  }

  .rounds {
    color: #888;
    font-size: 0.875em;
  }

  @keyframes fadeIn {
    from { opacity: 0; transform: translateY(-0.5em); }
    to { opacity: 1; transform: translateY(0); }
  }
</style>
