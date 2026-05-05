<script lang="ts">
  import type { CombatResult } from '../types/game';

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

<style>
  .toast {
    position: absolute;
    top: 50%;
    left: 50%;
    transform: translate(-50%, -50%);
    background: rgba(0, 0, 0, 0.9);
    border: 2px solid #666;
    border-radius: 8px;
    padding: 1.5rem 2rem;
    text-align: center;
    z-index: 200;
    animation: fadeIn 0.3s ease-out;
  }

  .toast.victory {
    border-color: #4a4;
  }

  .toast.defeat {
    border-color: #a44;
  }

  .title {
    font-size: 1.75rem;
    font-weight: bold;
    margin-bottom: 0.5rem;
  }

  .victory .title {
    color: #4f4;
  }

  .defeat .title {
    color: #f44;
  }

  .details {
    display: flex;
    flex-direction: column;
    gap: 0.25rem;
    color: #ccc;
    font-size: 0.875rem;
  }

  .level-up {
    color: #fd4;
    font-weight: bold;
  }

  .rounds {
    color: #888;
    font-size: 0.75rem;
  }

  @keyframes fadeIn {
    from { opacity: 0; transform: translate(-50%, -40%); }
    to { opacity: 1; transform: translate(-50%, -50%); }
  }
</style>
