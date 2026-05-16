<script lang="ts">
  import {
    loadBindings,
    saveBindings,
    resetToDefaults,
    findConflicts,
    ACTION_LABELS,
    DEFAULT_BINDINGS,
    type Keybinding,
  } from '../config/keybindings';

  interface Props {
    open: boolean;
    onClose: () => void;
  }

  let { open, onClose }: Props = $props();

  let bindings = $state<Keybinding[]>(loadBindings());
  let capturingAction = $state<string | null>(null);
  let conflictMap = $state<Map<string, string[]>>(new Map());

  function updateConflicts() {
    conflictMap = findConflicts(bindings);
  }

  function startCapture(action: string) {
    capturingAction = action;
  }

  function handleKeyDown(event: KeyboardEvent) {
    if (!capturingAction) return;
    event.preventDefault();
    event.stopPropagation();

    const key = event.key;
    if (key === 'Escape') {
      capturingAction = null;
      return;
    }

    // Remove existing binding for this action
    bindings = bindings.filter(b => b.action !== capturingAction);
    // Add new binding
    bindings = [...bindings, { action: capturingAction, key }];
    saveBindings(bindings);
    updateConflicts();
    capturingAction = null;
  }

  function clearBinding(action: string) {
    bindings = bindings.filter(b => b.action !== action);
    saveBindings(bindings);
    updateConflicts();
  }

  function resetAll() {
    bindings = resetToDefaults();
    updateConflicts();
  }

  function getBindingKey(action: string): string {
    const b = bindings.find(x => x.action === action);
    return b?.key ?? '';
  }

  function formatKey(key: string): string {
    if (key === ' ') return 'Space';
    if (key === 'ArrowUp') return '↑';
    if (key === 'ArrowDown') return '↓';
    if (key === 'ArrowLeft') return '←';
    if (key === 'ArrowRight') return '→';
    return key;
  }

  const actions = Object.keys(ACTION_LABELS);
</script>

{#if open}
  <!-- svelte-ignore a11y_click_events_have_key_events -->
  <!-- svelte-ignore a11y_no_static_element_interactions -->
  <div class="settings-overlay" onclick={onClose}>
    <div class="settings-panel" onclick={(e) => e.stopPropagation()} tabindex="-1" onkeydown={handleKeyDown}>
      <div class="settings-header">
        <h2>Settings</h2>
        <button class="close-btn" onclick={onClose} aria-label="Close settings">×</button>
      </div>

      <div class="settings-section">
        <h3>Key Bindings</h3>
        {#if conflictMap.size > 0}
          <div class="conflict-banner">
            ⚠️ Conflicts detected:
            {#each Array.from(conflictMap.entries()) as [key, actions]}
              <span class="conflict-item">{formatKey(key)} → {actions.map(a => ACTION_LABELS[a]).join(', ')}</span>
            {/each}
          </div>
        {/if}
        <div class="binding-list">
          {#each actions as action}
            <div class="binding-row">
              <span class="binding-label">{ACTION_LABELS[action]}</span>
              <button
                class="binding-key"
                class:capturing={capturingAction === action}
                class:conflict={conflictMap.has(getBindingKey(action))}
                onclick={() => startCapture(action)}
              >
                {#if capturingAction === action}
                  Press a key...
                {:else}
                  {formatKey(getBindingKey(action)) || '—'}
                {/if}
              </button>
              <button class="clear-btn" onclick={() => clearBinding(action)} aria-label="Clear binding">×</button>
            </div>
          {/each}
        </div>
        <div class="binding-actions">
          <button class="reset-btn" onclick={resetAll}>Reset to Defaults</button>
        </div>
      </div>
    </div>
  </div>
{/if}

<style>
  .settings-overlay {
    position: fixed;
    inset: 0;
    background: rgba(0, 0, 0, 0.6);
    display: flex;
    align-items: center;
    justify-content: center;
    z-index: 200;
  }

  .settings-panel {
    background: #1a1a24;
    border: 1px solid #333;
    border-radius: 0.5rem;
    width: min(28rem, 90vw);
    max-height: 80vh;
    overflow-y: auto;
    padding: 1rem;
    color: #ccc;
  }

  .settings-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 1rem;
    border-bottom: 1px solid #333;
    padding-bottom: 0.5rem;
  }

  .settings-header h2 {
    margin: 0;
    font-size: 1.1rem;
    color: #ddd;
  }

  .close-btn {
    background: none;
    border: none;
    color: #888;
    font-size: 1.5rem;
    cursor: pointer;
    line-height: 1;
  }

  .close-btn:hover {
    color: #fff;
  }

  .settings-section h3 {
    margin: 0 0 0.75rem 0;
    font-size: 0.95rem;
    color: #aa88cc;
  }

  .conflict-banner {
    background: rgba(200, 60, 60, 0.15);
    border: 1px solid #c84040;
    border-radius: 0.25rem;
    padding: 0.5rem;
    margin-bottom: 0.75rem;
    font-size: 0.8rem;
    color: #e08080;
  }

  .conflict-item {
    display: inline-block;
    margin-right: 0.75rem;
  }

  .binding-list {
    display: flex;
    flex-direction: column;
    gap: 0.4rem;
  }

  .binding-row {
    display: flex;
    align-items: center;
    gap: 0.5rem;
    padding: 0.35rem 0.25rem;
    border-radius: 0.25rem;
  }

  .binding-row:hover {
    background: rgba(255, 255, 255, 0.03);
  }

  .binding-label {
    flex: 1;
    font-size: 0.85rem;
  }

  .binding-key {
    min-width: 6rem;
    padding: 0.25rem 0.5rem;
    background: rgba(100, 68, 170, 0.15);
    border: 1px solid #6644aa;
    border-radius: 0.25rem;
    color: #aa88cc;
    font-size: 0.8rem;
    cursor: pointer;
    text-align: center;
    transition: background 0.15s;
  }

  .binding-key:hover {
    background: rgba(100, 68, 170, 0.3);
  }

  .binding-key.capturing {
    background: rgba(212, 168, 75, 0.2);
    border-color: #d4a84b;
    color: #d4a84b;
    animation: pulse 1s infinite;
  }

  .binding-key.conflict {
    border-color: #c84040;
    color: #e08080;
  }

  @keyframes pulse {
    0%, 100% { opacity: 1; }
    50% { opacity: 0.6; }
  }

  .clear-btn {
    width: 1.5rem;
    height: 1.5rem;
    background: none;
    border: none;
    color: #666;
    cursor: pointer;
    font-size: 0.9rem;
    line-height: 1;
  }

  .clear-btn:hover {
    color: #c84040;
  }

  .binding-actions {
    margin-top: 0.75rem;
    display: flex;
    justify-content: flex-end;
  }

  .reset-btn {
    padding: 0.35rem 0.75rem;
    background: rgba(100, 100, 100, 0.15);
    border: 1px solid #555;
    border-radius: 0.25rem;
    color: #888;
    font-size: 0.8rem;
    cursor: pointer;
  }

  .reset-btn:hover {
    background: rgba(100, 100, 100, 0.3);
    color: #aaa;
  }
</style>
