<script lang="ts">
  import {
    loadBindings,
    saveBindings,
    resetToDefaults,
    findConflicts,
    eventToChord,
    ACTION_LABELS,
    ACTIONS,
    ACTION_CONTEXT,
    CONTEXT_LABELS,
    CONTEXT_ORDER,
    DEFAULT_BINDINGS,
    type Keybinding,
    type KeybindingContext,
  } from '$config/keybindings';

  import {
    loadDisplaySettings,
    saveDisplaySettings,
    resetDisplaySettings,
    FOV_MIN,
    FOV_MAX,
    RESOLUTION_SCALES,
    type DisplaySettings,
  } from '$config/displaySettings';
  import {
    loadAccessibilitySettings,
    saveAccessibilitySettings,
    resetAccessibilitySettings,
    applyAccessibilityToDocument,
    COLORBLIND_MODES,
    COLORBLIND_LABELS,
    TEXT_SCALE_MIN,
    TEXT_SCALE_MAX,
    type AccessibilitySettings,
    type ColorblindMode,
  } from '$config/accessibilitySettings';
  import { modal } from '$shared/actions/modal';

  interface Props {
    open: boolean;
    onClose: () => void;
    onAudioToggle?: (enabled: boolean) => void;
    onDisplayChange?: (settings: DisplaySettings) => void;
    onAccessibilityChange?: (settings: AccessibilitySettings) => void;
    /**
     * Whether the current run is in ironman mode. Server-authoritative and saved with the run, so
     * it is read from game state rather than from local settings storage like the options above.
     */
    isIronman?: boolean;
    onIronmanChange?: (enabled: boolean) => void;
  }

  let {
    open,
    onClose,
    onAudioToggle,
    onDisplayChange,
    onAccessibilityChange,
    isIronman = false,
    onIronmanChange,
  }: Props = $props();

  let a11y = $state<AccessibilitySettings>(loadAccessibilitySettings());

  function applyA11y() {
    saveAccessibilitySettings(a11y);
    applyAccessibilityToDocument(a11y);
    onAccessibilityChange?.(a11y);
  }

  function setColorblind(mode: ColorblindMode) {
    a11y = { ...a11y, colorblindMode: mode };
    applyA11y();
  }

  function setTextScale(value: number) {
    a11y = { ...a11y, textScale: value };
    applyA11y();
  }

  function toggleReduceMotion() {
    a11y = { ...a11y, reduceMotion: !a11y.reduceMotion };
    applyA11y();
  }

  function toggleHighContrast() {
    a11y = { ...a11y, highContrast: !a11y.highContrast };
    applyA11y();
  }

  function resetA11y() {
    a11y = resetAccessibilitySettings();
    applyAccessibilityToDocument(a11y);
    onAccessibilityChange?.(a11y);
  }

  let display = $state<DisplaySettings>(loadDisplaySettings());
  let isFullscreen = $state(typeof document !== 'undefined' && !!document.fullscreenElement);

  function applyDisplay() {
    saveDisplaySettings(display);
    onDisplayChange?.(display);
  }

  function setFov(value: number) {
    display = { ...display, fov: value };
    applyDisplay();
  }

  function setResolutionScale(value: number) {
    display = { ...display, resolutionScale: value };
    applyDisplay();
  }

  function toggleVsync() {
    display = { ...display, vsync: !display.vsync };
    applyDisplay();
  }

  async function toggleFullscreen() {
    try {
      if (document.fullscreenElement) {
        await document.exitFullscreen();
      } else {
        await document.documentElement.requestFullscreen();
      }
    } catch {
      // Fullscreen may be unavailable (e.g. denied) — fall through and read ground truth.
    }
    // Derive state from the document so a denied/failed request is reflected accurately.
    isFullscreen = !!document.fullscreenElement;
    display = { ...display, fullscreen: isFullscreen };
    applyDisplay();
  }

  function resetDisplay() {
    display = resetDisplaySettings();
    onDisplayChange?.(display);
  }

  let bindings = $state<Keybinding[]>(loadBindings());
  let capturingAction = $state<string | null>(null);
  let conflictMap = $state<Map<string, string[]>>(new Map());
  let audioEnabled = $state(localStorage.getItem('rpc_audio_enabled') !== 'false');

  function toggleAudio() {
    audioEnabled = !audioEnabled;
    localStorage.setItem('rpc_audio_enabled', String(audioEnabled));
    onAudioToggle?.(audioEnabled);
  }

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

    if (event.key === 'Escape') {
      capturingAction = null;
      return;
    }
    // Ignore lone modifier presses — wait for the full chord.
    if (event.key === 'Control' || event.key === 'Alt' || event.key === 'Shift' || event.key === 'Meta') {
      return;
    }

    const key = eventToChord(event);
    const context = ACTION_CONTEXT[capturingAction] ?? 'global';

    // Remove existing binding(s) for this action, then add the captured chord in its context.
    bindings = bindings.filter(b => b.action !== capturingAction);
    bindings = [...bindings, { action: capturingAction, key, context }];
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

  function actionsForContext(ctx: KeybindingContext): string[] {
    return ACTIONS.filter(a => a.context === ctx).map(a => a.action);
  }

  // Conflict map is keyed "context|key"; build the lookup key for a given action's binding.
  function conflictKeyFor(action: string): string {
    return `${ACTION_CONTEXT[action] ?? 'global'}|${getBindingKey(action)}`;
  }

  const populatedContexts = CONTEXT_ORDER.filter(ctx => actionsForContext(ctx).length > 0);
</script>

{#if open}
  <!-- svelte-ignore a11y_click_events_have_key_events -->
  <!-- svelte-ignore a11y_no_static_element_interactions -->
  <div class="settings-overlay" onclick={onClose}>
    <div
      class="settings-panel"
      role="dialog"
      aria-modal="true"
      aria-labelledby="settings-title"
      onclick={(e) => e.stopPropagation()}
      tabindex="-1"
      onkeydown={handleKeyDown}
      use:modal
    >
      <div class="settings-header">
        <h2 id="settings-title">Settings</h2>
        <button class="close-btn" onclick={onClose} aria-label="Close settings">×</button>
      </div>

      <div class="settings-section">
        <h3>Audio</h3>
        <label class="audio-toggle">
          <input type="checkbox" checked={audioEnabled} onchange={toggleAudio} />
          <span>Ambient audio {audioEnabled ? 'ON' : 'OFF'}</span>
        </label>
      </div>

      <div class="settings-section">
        <h3>Gameplay</h3>
        <!-- Locked once taken, because the server will not turn it back off. Offering a control
             whose request is refused would also leave this checkbox wrong: a refused action
             changes nothing, so no state comes back to correct it. -->
        <label class="audio-toggle">
          <input
            type="checkbox"
            checked={isIronman}
            disabled={isIronman}
            onchange={(e) => onIronmanChange?.(e.currentTarget.checked)}
          />
          <span>Ironman {isIronman ? 'ON' : 'OFF'}</span>
        </label>
        <p class="setting-note">
          One save, written after every action. A total party kill ends the run and deletes it —
          the bench may attempt a rescue to recover equipment, but the dead stay dead.
          {#if isIronman}
            This run is committed; only a new campaign clears it.
          {:else}
            Once taken, it cannot be turned off for this run.
          {/if}
        </p>
      </div>

      <div class="settings-section">
        <h3>Display</h3>
        <div class="display-row">
          <label class="display-label" for="fov-slider">Field of View</label>
          <input
            id="fov-slider"
            class="slider fov-slider"
            type="range"
            min={FOV_MIN}
            max={FOV_MAX}
            value={display.fov}
            oninput={(e) => setFov(Number((e.target as HTMLInputElement).value))}
          />
          <span class="display-value">{display.fov}°</span>
        </div>

        <div class="display-row">
          <label class="display-label" for="res-scale">Resolution</label>
          <select
            id="res-scale"
            class="display-select"
            value={display.resolutionScale}
            onchange={(e) => setResolutionScale(Number((e.target as HTMLSelectElement).value))}
          >
            {#each RESOLUTION_SCALES as scale}
              <option value={scale}>{Math.round(scale * 100)}%</option>
            {/each}
          </select>
        </div>

        <label class="audio-toggle">
          <input type="checkbox" checked={display.vsync} onchange={toggleVsync} />
          <span>V-Sync {display.vsync ? 'ON' : 'OFF'}</span>
        </label>

        <div class="display-row">
          <button class="display-btn" onclick={toggleFullscreen}>
            {isFullscreen ? 'Exit Fullscreen' : 'Enter Fullscreen'}
          </button>
          <button class="reset-btn" onclick={resetDisplay}>Reset Display</button>
        </div>
      </div>

      <div class="settings-section">
        <h3>Accessibility</h3>
        <div class="display-row">
          <label class="display-label" for="colorblind-select">Colorblind</label>
          <select
            id="colorblind-select"
            class="a11y-select"
            value={a11y.colorblindMode}
            onchange={(e) => setColorblind((e.target as HTMLSelectElement).value as ColorblindMode)}
          >
            {#each COLORBLIND_MODES as mode}
              <option value={mode}>{COLORBLIND_LABELS[mode]}</option>
            {/each}
          </select>
        </div>

        <div class="display-row">
          <label class="display-label" for="text-scale">Text Size</label>
          <input
            id="text-scale"
            class="slider"
            type="range"
            min={TEXT_SCALE_MIN}
            max={TEXT_SCALE_MAX}
            step="0.05"
            value={a11y.textScale}
            oninput={(e) => setTextScale(Number((e.target as HTMLInputElement).value))}
          />
          <span class="a11y-value">{Math.round(a11y.textScale * 100)}%</span>
        </div>

        <label class="audio-toggle">
          <input type="checkbox" checked={a11y.reduceMotion} onchange={toggleReduceMotion} />
          <span>Reduce motion {a11y.reduceMotion ? 'ON' : 'OFF'}</span>
        </label>

        <label class="audio-toggle">
          <input type="checkbox" checked={a11y.highContrast} onchange={toggleHighContrast} />
          <span>High contrast {a11y.highContrast ? 'ON' : 'OFF'}</span>
        </label>

        <div class="display-row">
          <button class="reset-btn" onclick={resetA11y}>Reset Accessibility</button>
        </div>
      </div>

      <div class="settings-section">
        <h3>Key Bindings</h3>
        {#if conflictMap.size > 0}
          <div class="conflict-banner">
            ⚠️ Conflicts detected:
            {#each Array.from(conflictMap.entries()) as [bucket, actions]}
              {@const key = bucket.split('|').slice(1).join('|')}
              <span class="conflict-item">{formatKey(key)} → {actions.map(a => ACTION_LABELS[a]).join(', ')}</span>
            {/each}
          </div>
        {/if}
        {#each populatedContexts as ctx}
          <h4 class="binding-context-label">{CONTEXT_LABELS[ctx]}</h4>
          <div class="binding-list">
            {#each actionsForContext(ctx) as action}
              <div class="binding-row">
                <span class="binding-label">{ACTION_LABELS[action]}</span>
                <button
                  class="binding-key"
                  class:capturing={capturingAction === action}
                  class:conflict={conflictMap.has(conflictKeyFor(action))}
                  onclick={() => startCapture(action)}
                >
                  {#if capturingAction === action}
                    Press a key…
                  {:else}
                    {formatKey(getBindingKey(action)) || '—'}
                  {/if}
                </button>
                <button class="clear-btn" onclick={() => clearBinding(action)} aria-label="Clear binding">×</button>
              </div>
            {/each}
          </div>
        {/each}
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

  .display-row {
    display: flex;
    align-items: center;
    gap: 0.6rem;
    margin-bottom: 0.5rem;
  }

  .display-label {
    flex: 0 0 auto;
    min-width: 6rem;
    color: #ccc;
    font-size: 0.85rem;
  }

  .setting-note {
    margin: 0.35rem 0 0;
    color: #999;
    font-size: 0.78rem;
    line-height: 1.4;
  }

  .slider {
    flex: 1 1 auto;
  }

  .display-value,
  .a11y-value {
    flex: 0 0 auto;
    min-width: 2.5rem;
    text-align: right;
    color: #888;
    font-size: 0.8rem;
  }

  .display-select,
  .a11y-select,
  .display-btn {
    padding: 0.3rem 0.6rem;
    background: rgba(255, 255, 255, 0.06);
    border: 0.0625em solid #444;
    border-radius: 0.25rem;
    color: #ccc;
    cursor: pointer;
    font-size: 0.8rem;
  }

  .binding-context-label {
    margin: 0.6rem 0 0.3rem;
    font-size: 0.75rem;
    text-transform: uppercase;
    letter-spacing: 0.05em;
    color: #888;
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
    /*
      #888 measures 4.48:1 against this button's composited background — under the 4.5:1 AA
      floor for text this size, which is why axe flagged it. #9a9a9a clears it at ~5.6:1 and
      still reads as the muted, secondary control it is meant to be.
    */
    color: #9a9a9a;
    font-size: 0.8rem;
    cursor: pointer;
  }

  .reset-btn:hover {
    background: rgba(100, 100, 100, 0.3);
    color: #aaa;
  }
</style>
