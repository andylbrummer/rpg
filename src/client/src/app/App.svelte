<script lang="ts">
  import { onMount } from 'svelte';
  import { modal } from '$shared/actions/modal';
  import { gameStore, sendAction, serverErrorStore, bootstrapGameStore, onTestSetState } from '$shared/stores/gameStore';
  import { GameClient } from '$shared/net/GameClient';
  import { TownMenu } from '$features/town';
  import type { PlayerAction } from '$shared/types/game';
  import { intentToAction, type UiIntent } from '$shared/actions/uiIntent';
  import { CombatOverlay } from '$features/combat';
  import { PartyStatusBar, CharacterSheet } from '$features/party';
  import { ExplorationHUD } from '$features/exploration';
  import { FieldNotesPanel } from '$features/field-notes';
  import { SettingsPanel } from '$features/settings';
  import { AnalyticsDashboard } from '$features/analytics';
  import { TitleScreen } from '$features/title';
  import { RendererHost } from '$renderer/RendererHost';
  import type { GameState } from '$shared/types/game';
  import { loadBindings } from '$config/keybindings';
  import type { DisplaySettings } from '$config/displaySettings';
  import { loadAccessibilitySettings, applyAccessibilityToDocument, type AccessibilitySettings } from '$config/accessibilitySettings';
  import { ALL_SYNERGIES } from '$shared/data/synergies';
  import { playSynergyChime } from '$renderer/UISounds';
  import { subtitles as sharedSubtitles } from '$renderer/SubtitleSystem';
  import type { SubtitleEntry } from '$renderer/SubtitleSystem';
  import { GamepadManager } from '$renderer/GamepadManager';
  import { MovementInputController, resolveKeyToAction } from '$shared/input/movementInput';
  import { createActionLogFeedback } from '$shared/stores/actionLogFeedback';

  let gameContainer: HTMLDivElement | undefined = $state(undefined);
  let host = $state<RendererHost | null>(null);
  let gameState = $state<GameState | null>(null);
  let serverError = $state<{ code: string; message: string; recoverable: boolean } | null>(null);
  let combatCancelSignal = $state(0);
  let subtitleEntries = $state<SubtitleEntry[]>([]);
  let showFieldNotes = $state(false);
  let replaySynergyId = $state<string | null>(null);
  let selectedMemberSlot = $state<number | null>(null);
  let showSettings = $state(false);
  let showTitleScreen = $state(true);
  let showStats = $state(false);
  let analyticsData = $state<import('$shared/types/game').AnalyticsData | null>(null);
  let showTelemetryPrompt = $state(false);
  const TELEMETRY_CONSENT_KEY = 'rpc_telemetry_consent';
  let keyBindings = $state(loadBindings());

  // Movement/input buffering is owned by a dedicated controller; sendAction is resolved
  // lazily so the controller always dispatches through the live, bootstrapped binding.
  const input = new MovementInputController((action) => sendAction(action));

  // Action-log interpretation (reputation toasts, faction notifications, synergy journal)
  // lives in a self-subscribing feedback store.
  const feedback = createActionLogFeedback();
  const { repToasts, factionNotifications, discoveredOrder, revealedSynergies, synergyFlashTargetId } = feedback;

  serverErrorStore.subscribe((err) => {
    serverError = err;
  });

  function requestStats() {
    const client = (window as any).gameClient as GameClient;
    client?.requestAnalytics();
    showStats = true;
  }

  function handleCancel() {
    combatCancelSignal++;
    input.cancel();
  }

  const unsubGameStore = gameStore.subscribe((s) => {
    const hadState = gameState !== null;
    gameState = s;

    // Auto-dismiss title screen on first server state (page refresh mid-game)
    if (!hadState && s !== null) {
      showTitleScreen = false;
    }

    // Each settled server state clears the in-flight gate and drains buffered input.
    input.notifyStateSettled();
  });

  $effect(() => {
    return () => {
      unsubGameStore();
      feedback.dispose();
      host?.dispose();
      host = null;
    };
  });

  // Apply persisted accessibility settings to the document once on mount.
  $effect(() => {
    applyAccessibilityToDocument(loadAccessibilitySettings());
  });

  // Renderer + audio lifecycle is isolated in the RendererHost; created once the
  // container element is bound, then driven by each game-state change.
  $effect(() => {
    if (gameContainer && !host) {
      host = new RendererHost(gameContainer);
    }
  });

  $effect(() => {
    if (host) {
      host.update(gameState);
      subtitleEntries = host.subtitleEntries;
    }
  });

  function applyDisplaySettings(d: DisplaySettings) {
    host?.applyDisplaySettings(d);
  }

  function applyAccessibilitySettings(a: AccessibilitySettings) {
    applyAccessibilityToDocument(a);
    host?.applyAccessibilitySettings(a);
  }

  $effect(() => {
    if (gameState?.campaignEnded) {
      const consent = localStorage.getItem(TELEMETRY_CONSENT_KEY);
      if (consent === null) {
        showTelemetryPrompt = true;
      }
    }
  });

  onMount(() => {
    // Explicit bootstrap: create client, wire store, connect
    const client = new GameClient();
    bootstrapGameStore(client);

    if (typeof window !== 'undefined') {
      // Expose test hooks unconditionally — e2e suite depends on window.gameClient
      (window as any).gameClient = client;
      (window as any).gameStore = gameStore;
      (window as any).__rpc_enableTestHooks = () => {};
      (window as any).__rpc_subtitles = sharedSubtitles;

      // Dev-only high-level automation harness (window.__rpg) for scripting test sequences.
      if (import.meta.env.DEV) {
        import('$shared/net/testHarness').then(({ installTestHarness }) => {
          installTestHarness(client, gameStore as any);
        });
      }
    }

    // Auto-hide title screen when e2e tests inject state
    const unsubTest = onTestSetState(() => {
      showTitleScreen = false;
    });

    gameStore.connect();

    client.onAnalytics((data) => {
      analyticsData = data;
    });

    const handleKeyDown = (e: KeyboardEvent) => {
      if (showSettings) {
        // Let SettingsPanel handle its own key capture
        if (e.key === 'Escape') {
          e.preventDefault();
          showSettings = false;
        }
        return;
      }

      if (e.key === 'Escape') {
        e.preventDefault();
        if (selectedMemberSlot !== null) {
          selectedMemberSlot = null;
          return;
        }
        if (showFieldNotes) {
          showFieldNotes = false;
          return;
        }
        if (replaySynergyId) {
          replaySynergyId = null;
          return;
        }
        handleCancel();
        return;
      }

      if ((e.key === 'j' || e.key === 'J') && (gameState?.mode === 'Menu' || gameState?.mode === 'Exploration')) {
        e.preventDefault();
        showFieldNotes = !showFieldNotes;
        return;
      }

      const action = resolveKeyToAction(keyBindings, e.key);
      if (!action) return;

      if (gameState?.mode !== 'Exploration') return;

      e.preventDefault();
      input.keyDown(e.key, action);
    };

    const handleKeyUp = (e: KeyboardEvent) => {
      input.keyUp(e.key);
    };

    const gamepadManager = new GamepadManager((action: PlayerAction) => {
      // Movement is buffered and only meaningful while exploring; other mapped buttons
      // (cancel, enter/return) dispatch through the normal path regardless of mode so they
      // aren't dead outside Exploration.
      const isMovement = action.type === 'move_forward' || action.type === 'move_back'
        || action.type === 'strafe_left' || action.type === 'strafe_right'
        || action.type === 'turn_left' || action.type === 'turn_right';
      if (isMovement) {
        if (gameState?.mode === 'Exploration') input.enqueue(action);
      } else {
        sendAction(action);
      }
    });

    window.addEventListener('keydown', handleKeyDown);
    window.addEventListener('keyup', handleKeyUp);
    return () => {
      window.removeEventListener('keydown', handleKeyDown);
      window.removeEventListener('keyup', handleKeyUp);
      input.dispose();
      unsubTest();
      gamepadManager.dispose();
    };
  });

  function handleEnterDungeon(type: string) {
    sendAction({ type: 'enter_dungeon', dungeonType: type });
  }

  // Single dispatch path: presentation components emit typed UI intents, the
  // adapter maps them to protocol PlayerActions, and everything flows through
  // the shared sendAction (pending/error handling lives there).
  function dispatchIntent(intent: UiIntent) {
    sendAction(intentToAction(intent));
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

  function handlePickup() {
    sendAction({ type: 'pickup_loot' });
  }

  function handleReset() {
    sendAction({ type: 'reset_game' });
  }

  function handleSwapRow(slot: number) {
    sendAction({ type: 'swap_row', slot });
  }

  function handleOpenInventory(slot: number) {
    selectedMemberSlot = slot;
  }

  function handleTransferToCache(itemId: string, count: number) {
    if (selectedMemberSlot !== null) {
      dispatchIntent({ kind: 'transferToCache', slot: selectedMemberSlot, itemId, count });
    }
  }

  function handleTransferFromCache(itemId: string, count: number) {
    if (selectedMemberSlot !== null) {
      dispatchIntent({ kind: 'transferFromCache', slot: selectedMemberSlot, itemId, count });
    }
  }

  function handleTavernRecruit(id: string) {
    dispatchIntent({ kind: 'tavernRecruit', recruitId: id });
  }

  function handleMissionAccept(id: string) {
    dispatchIntent({ kind: 'missionAccept', missionId: id });
  }

  function handleVendorPurchase(id: string) {
    dispatchIntent({ kind: 'vendorPurchase', itemId: id });
  }

  function handleTravel(targetId: string) {
    sendAction({ type: 'travel', targetId });
  }

  function handleResolveEncounter(choice: string) {
    sendAction({ type: 'resolve_travel_encounter', targetId: choice });
  }

  function turnColor(turns: number): string {
    if (turns >= 13) return '#c44';
    if (turns >= 10) return '#d4a84b';
    return '#ccc';
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
    {#each $repToasts as toast (toast.id)}
      <div class="rep-toast" role="status">
        <span class="rep-toast-faction">{toast.factionId}</span>
        <span class="rep-toast-delta" class:positive={toast.delta > 0} class:negative={toast.delta < 0}>
          {toast.delta > 0 ? '+' : ''}{toast.delta}
        </span>
        <span class="rep-toast-source">({toast.source})</span>
      </div>
    {/each}
    {#each $factionNotifications as note (note.id)}
      <div class="faction-notification" role="status">
        {note.text}
      </div>
    {/each}
    {#if subtitleEntries.length > 0}
      <div class="subtitle-overlay">
        {#each subtitleEntries as sub (sub.timestamp)}
          <div class="subtitle-line">{sub.text}</div>
        {/each}
      </div>
    {/if}
    {#if gameState?.mode !== 'Combat' && !showTitleScreen}
      <header class="top-bar">
        <!-- The app's only top-level heading; a div left the page with no h1 for a screen
             reader or outline to anchor on. -->
        <h1 class="game-title">The Reach</h1>
        <div class="game-info">
          <span class="mode-badge">{gameState?.mode || 'Menu'}</span>
          {#if gameState?.hasDungeon}
            <span class="dungeon-badge">{gameState.dungeonType ?? 'Dungeon'}</span>
          {/if}
          {#if gameState?.overworld != null}
            <span class="turn-counter" style="color: {turnColor(gameState.overworld.turns)}">
              Turn {gameState.overworld.turns}/15
            </span>
          {/if}
          {#if gameState?.rescueExpedition?.isActive}
            <span class="rescue-badge" title="Rescue expedition in progress. Reach the TPK location to recover equipment.">
              🛡️ Rescue
            </span>
          {/if}
          {#if gameState?.isFragileState}
            <span class="fragile-warning" title="Your roster is thin. A total party kill may end your campaign.">
              ⚠ Fragile
            </span>
          {/if}
          <button class="field-notes-toggle" onclick={() => showFieldNotes = true}>Field Notes</button>
          <button class="stats-toggle" onclick={requestStats}>Stats</button>
          <button class="settings-toggle" onclick={() => showSettings = true}>Settings</button>
        </div>
      </header>
    {/if}
    <section class="viewport">
      {#if gameState?.mode === 'Menu' && showTitleScreen}
        <TitleScreen
          onEnterTown={() => showTitleScreen = false}
          onOpenSettings={() => showSettings = true}
        />
      {/if}
      {#if gameState?.mode === 'Menu' && !showTitleScreen}
        <TownMenu
          gameState={gameState}
          onEnterDungeon={handleEnterDungeon}
          onSave={handleSave}
          onReset={handleReset}
          onSwapRow={handleSwapRow}
          onTavernRecruit={handleTavernRecruit}
          onMissionAccept={handleMissionAccept}
          onVendorPurchase={handleVendorPurchase}
          onTravel={handleTravel}
          onIntent={dispatchIntent}
          audioManager={host?.ambientAudio}
        />
      {/if}
      {#if gameState?.mode === 'Combat'}
        <CombatOverlay
          combat={gameState.combat ?? null}
          lastResult={gameState.combatResult ?? null}
          onIntent={dispatchIntent}
          cancelSignal={combatCancelSignal}
          synergyFlashTargetId={$synergyFlashTargetId}
          party={gameState.party ?? []}
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
          onPickup={handlePickup}
          onIntent={dispatchIntent}
        />
      {/if}
      {#if gameState?.travelEncounter && gameState?.mode === 'Menu'}
        <div class="travel-encounter-overlay" role="dialog" aria-label="Travel encounter" aria-modal="true" tabindex="-1" use:modal>
          <div class="travel-encounter-card">
            <h2 class="travel-encounter-title">{gameState.travelEncounter.name}</h2>
            {#if gameState.travelEncounter.resolutionType === 'stat_test'}
              <p class="travel-encounter-desc">Test: {gameState.travelEncounter.statName}</p>
              <button class="travel-action-btn" onclick={() => handleResolveEncounter('roll')}>Roll</button>
            {:else if gameState.travelEncounter.resolutionType === 'dialogue'}
              <p class="travel-encounter-desc">Diplomatic encounter</p>
              {#if gameState.travelEncounter.options}
                <div class="travel-options">
                  {#each gameState.travelEncounter.options as opt}
                    <button class="travel-action-btn" onclick={() => handleResolveEncounter(opt)}>{opt}</button>
                  {/each}
                </div>
              {/if}
            {:else}
              <p class="travel-encounter-desc">Unexpected encounter</p>
              <button class="travel-action-btn" onclick={() => handleResolveEncounter('continue')}>Continue</button>
            {/if}
          </div>
        </div>
      {/if}
      {#if gameState?.campaignEnded}
        <div class="campaign-end-overlay" role="dialog" aria-label="Campaign complete" aria-modal="true" tabindex="-1" use:modal>
          <div class="campaign-end-card">
            <h2 class="campaign-end-title">Campaign Complete</h2>
            <p class="campaign-end-turns">Final Turn: {gameState.overworld?.turns ?? 15}/15</p>
            {#if gameState.epilogue}
              <div class="campaign-epilogue">
                {#each gameState.epilogue.split('\n\n') as paragraph}
                  <p class="epilogue-paragraph">{paragraph}</p>
                {/each}
              </div>
            {:else}
              <p class="campaign-end-desc">Your journey has come to an end.</p>
            {/if}
            {#if showTelemetryPrompt}
              <div class="telemetry-prompt">
                <p>Help improve The Reach by sharing anonymized play data?</p>
                <div class="telemetry-buttons">
                  <button class="telemetry-yes" onclick={() => { localStorage.setItem(TELEMETRY_CONSENT_KEY, 'true'); showTelemetryPrompt = false; }}>Yes</button>
                  <button class="telemetry-no" onclick={() => { localStorage.setItem(TELEMETRY_CONSENT_KEY, 'false'); showTelemetryPrompt = false; }}>No</button>
                </div>
              </div>
            {/if}
            <button class="campaign-end-btn" onclick={() => handleReset()}>New Game</button>
          </div>
        </div>
      {/if}
      {#if showFieldNotes}
        <FieldNotesPanel
          discoveredOrder={$discoveredOrder}
          revealedIds={$revealedSynergies}
          onClose={() => showFieldNotes = false}
          onReplay={(id) => { replaySynergyId = id; playSynergyChime(); }}
        />
      {/if}
      {#if showSettings}
        <SettingsPanel
          open={showSettings}
          onClose={() => showSettings = false}
          onAudioToggle={(enabled) => host?.setAudioEnabled(enabled)}
          onDisplayChange={applyDisplaySettings}
          onAccessibilityChange={applyAccessibilitySettings}
        />
      {/if}
      {#if showStats}
        <div class="stats-overlay" role="dialog" aria-label="Your stats" aria-modal="true" tabindex="-1" use:modal>
          <div class="stats-card">
            <h2 class="stats-title">Your Stats</h2>
            {#if analyticsData}
              <AnalyticsDashboard data={analyticsData} />
              {#if analyticsData.classesPlayed.length > 0}
                <div class="stats-section">
                  <h3>Classes Played</h3>
                  <div class="stats-tags">
                    {#each analyticsData.classesPlayed as cls}
                      <span class="stat-tag">{cls}</span>
                    {/each}
                  </div>
                </div>
              {/if}
            {:else}
              <p class="stats-loading">Loading...</p>
            {/if}
            <button class="stats-close-btn" onclick={() => showStats = false}>Close</button>
          </div>
        </div>
      {/if}
      {#if replaySynergyId}
        <div class="replay-modal-overlay" role="dialog" aria-label="Synergy replay" aria-modal="true" tabindex="-1" use:modal>
          <div class="replay-modal-card">
            <h3 class="replay-title">{ALL_SYNERGIES.find(s => s.id === replaySynergyId)?.abilities.join(' + ') ?? 'Synergy'}</h3>
            <div class="replay-anim"></div>
            <button class="replay-close-btn" onclick={() => replaySynergyId = null}>Close</button>
          </div>
        </div>
      {/if}
    </section>
    {#if gameState?.mode !== 'Combat' && !showTitleScreen}
      <footer class="bottom-bar">
        <PartyStatusBar party={gameState?.party || []} onOpenInventory={handleOpenInventory} />
      </footer>
    {/if}
    {#if selectedMemberSlot !== null && gameState?.party}
      {@const member = gameState.party.find(m => m.slot === selectedMemberSlot)}
      {#if member}
        <CharacterSheet
          {member}
          onClose={() => selectedMemberSlot = null}
          onSwapRow={handleSwapRow}
          onTransferToCache={handleTransferToCache}
          onTransferFromCache={handleTransferFromCache}
          expeditionCache={gameState.expeditionCache ?? []}
        />
      {/if}
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
    /*
      minmax(0, 1fr) rather than 1fr. A bare 1fr is minmax(auto, 1fr), and the auto minimum is
      the track's min-content — so a wide child grows the track past the container instead of
      being made to fit. That is what pinned the app to a ~1000px floor on a narrow window: the
      grid track stayed at content width while the element itself was viewport width, and the
      overflow:hidden here quietly clipped the difference.
    */
    grid-template: minmax(0, 1fr) / minmax(0, 1fr);
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
    grid-template-rows: auto minmax(0, 1fr) auto;
    /* Single implicit column: keep it from being sized by its widest child (see .game). */
    grid-template-columns: minmax(0, 1fr);
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
    /* Title plus five status/menu controls do not fit one row on a narrow window; wrapping
       keeps them all reachable instead of pushing the document wider than the viewport. */
    flex-wrap: wrap;
    gap: 0.5rem;
    padding: clamp(0.375rem, 1.5vh, 0.75rem) clamp(0.5rem, 2vw, 1rem);
    background: rgba(0, 0, 0, 0.8);
    border-bottom: 0.0625em solid #333;
  }

  .game-title {
    margin: 0;
    font-size: clamp(1rem, 2.5vw, 1.5rem);
    font-weight: bold;
    color: #d4a84b;
  }

  .game-info {
    display: flex;
    flex-wrap: wrap;
    gap: 0.5rem;
    align-items: center;
  }

  .mode-badge,
  .dungeon-badge,
  .turn-counter {
    padding: 0.2rem 0.5rem;
    border-radius: 0.25rem;
    font-size: clamp(0.65rem, 1.5vw, 0.75rem);
    font-weight: bold;
  }

  .turn-counter {
    background: rgba(0, 0, 0, 0.4);
    border: 0.0625em solid #444;
  }

  .fragile-warning {
    padding: 0.2rem 0.5rem;
    background: rgba(204, 68, 68, 0.2);
    border: 1px solid #c44;
    border-radius: 0.25rem;
    color: #e88;
    font-size: clamp(0.6rem, 1.2vw, 0.7rem);
    font-weight: bold;
    cursor: help;
    animation: pulse-warning 2s infinite;
  }

  @keyframes pulse-warning {
    0%, 100% { opacity: 1; }
    50% { opacity: 0.6; }
  }

  .mode-badge {
    background: rgba(68, 170, 255, 0.2);
    color: #66aaff;
  }

  .dungeon-badge {
    background: rgba(212, 168, 75, 0.2);
    color: #d4a84b;
  }

  .rescue-badge {
    padding: 0.2rem 0.5rem;
    background: rgba(68, 136, 204, 0.2);
    border: 1px solid #4488cc;
    border-radius: 0.25rem;
    color: #88bbee;
    font-size: clamp(0.6rem, 1.2vw, 0.7rem);
    font-weight: bold;
    cursor: help;
  }

  .viewport {
    display: grid;
    grid-template: minmax(0, 1fr) / minmax(0, 1fr);
    min-height: 0;
    overflow: hidden;
  }

  .viewport > :global(*) {
    grid-row: 1 / -1;
    grid-column: 1 / -1;
    min-height: 0;
    /*
      Grid items default to min-width:auto, so a screen wider than the viewport could not
      shrink and instead stretched the whole document — the town screen forced ~1000px and its
      own overflow:hidden then clipped it, taking the tab strip and the actions rail off-screen
      with no way to reach them. The min-height counterpart was already here; the width axis
      was missing.
    */
    min-width: 0;
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

  .rep-toast {
    position: fixed;
    top: 1rem;
    right: 1rem;
    z-index: 100;
    background: rgba(0, 0, 0, 0.9);
    border: 1px solid #d4a84b;
    border-radius: 0.5rem;
    padding: 0.75em 1.25em;
    display: flex;
    gap: 0.5em;
    align-items: center;
    animation: fadeIn 0.2s ease-out;
    pointer-events: auto;
    font-size: 0.875rem;
  }

  .rep-toast-faction {
    color: #ccc;
    text-transform: capitalize;
    font-weight: bold;
  }

  .rep-toast-delta.positive {
    color: #4a4;
  }

  .rep-toast-delta.negative {
    color: #c44;
  }

  .rep-toast-source {
    color: #888;
    font-size: 0.75rem;
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

  .travel-encounter-overlay {
    position: fixed;
    inset: 0;
    display: flex;
    align-items: center;
    justify-content: center;
    background: rgba(0, 0, 0, 0.7);
    z-index: 50;
    pointer-events: auto;
  }

  .travel-encounter-card {
    background: #1a1a2e;
    border: 1px solid #444;
    border-radius: 0.5rem;
    padding: 1.5rem;
    min-width: 280px;
    max-width: 90vw;
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
  }

  .travel-encounter-title {
    margin: 0;
    font-size: 1.25rem;
    color: #d4a84b;
  }

  .travel-encounter-desc {
    margin: 0;
    color: #ccc;
    font-size: 0.875rem;
  }

  .travel-options {
    display: flex;
    flex-direction: column;
    gap: 0.5rem;
  }

  .travel-action-btn {
    background: #2a2a4e;
    border: 1px solid #555;
    color: #fff;
    padding: 0.5rem 1rem;
    border-radius: 0.25rem;
    cursor: pointer;
    font-size: 0.875rem;
  }

  .travel-action-btn:hover {
    background: #3a3a5e;
  }

  .campaign-end-overlay {
    position: fixed;
    inset: 0;
    display: flex;
    align-items: center;
    justify-content: center;
    background: rgba(0, 0, 0, 0.9);
    z-index: 60;
    pointer-events: auto;
  }

  .campaign-end-card {
    background: #1a1a2e;
    border: 1px solid #444;
    border-radius: 0.5rem;
    padding: 2rem;
    min-width: 280px;
    max-width: 90vw;
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
    align-items: center;
    text-align: center;
  }

  .campaign-end-title {
    margin: 0;
    font-size: 1.5rem;
    color: #d4a84b;
  }

  .campaign-end-turns {
    margin: 0;
    color: #c44;
    font-size: 1rem;
    font-weight: bold;
  }

  .campaign-end-desc {
    margin: 0;
    color: #888;
    font-size: 0.875rem;
  }

  .campaign-epilogue {
    max-height: 300px;
    overflow-y: auto;
    text-align: left;
    padding: 0.5rem;
    background: rgba(0, 0, 0, 0.3);
    border-radius: 0.25rem;
  }

  .epilogue-paragraph {
    margin: 0 0 0.75rem 0;
    color: #ccc;
    font-size: 0.875rem;
    line-height: 1.5;
  }

  .epilogue-paragraph:last-child {
    margin-bottom: 0;
  }

  .campaign-end-btn {
    margin-top: 0.5rem;
    padding: 0.5rem 1.5rem;
    background: rgba(68, 170, 68, 0.2);
    border: 1px solid #44aa44;
    border-radius: 0.25rem;
    color: #88cc88;
    cursor: pointer;
    font-size: 0.875rem;
  }

  .campaign-end-btn:hover {
    background: rgba(68, 170, 68, 0.35);
  }

  .telemetry-prompt {
    margin: 0.75rem 0;
    padding: 0.75rem;
    background: rgba(68, 136, 204, 0.1);
    border: 1px solid #4488cc;
    border-radius: 0.375rem;
    text-align: center;
  }

  .telemetry-prompt p {
    margin: 0 0 0.5rem;
    font-size: 0.875rem;
    color: #88bbee;
  }

  .telemetry-buttons {
    display: flex;
    gap: 0.5rem;
    justify-content: center;
  }

  .telemetry-yes, .telemetry-no {
    padding: 0.3rem 0.75rem;
    border-radius: 0.25rem;
    cursor: pointer;
    font-size: 0.8rem;
    border: 1px solid;
  }

  .telemetry-yes {
    background: rgba(68, 170, 68, 0.15);
    border-color: #44aa44;
    color: #88cc88;
  }

  .telemetry-yes:hover {
    background: rgba(68, 170, 68, 0.3);
  }

  .telemetry-no {
    background: rgba(170, 68, 68, 0.15);
    border-color: #aa4444;
    color: #cc8888;
  }

  .telemetry-no:hover {
    background: rgba(170, 68, 68, 0.3);
  }

  @keyframes fadeIn {
    from { opacity: 0; transform: translateY(-0.5em); }
    to { opacity: 1; transform: translateY(0); }
  }

  .field-notes-toggle {
    padding: 0.2rem 0.5rem;
    background: rgba(212, 168, 75, 0.15);
    border: 1px solid #d4a84b;
    border-radius: 0.25rem;
    color: #d4a84b;
    cursor: pointer;
    font-size: clamp(0.6rem, 1.2vw, 0.75rem);
    font-weight: bold;
  }

  .field-notes-toggle:hover {
    background: rgba(212, 168, 75, 0.3);
  }

  .settings-toggle {
    padding: 0.2rem 0.5rem;
    background: rgba(120, 160, 200, 0.15);
    border: 1px solid #78a0c8;
    border-radius: 0.25rem;
    color: #78a0c8;
    cursor: pointer;
    font-size: clamp(0.6rem, 1.2vw, 0.75rem);
    font-weight: bold;
    margin-left: 0.5rem;
  }

  .settings-toggle:hover {
    background: rgba(120, 160, 200, 0.3);
  }

  .stats-toggle {
    padding: 0.2rem 0.5rem;
    background: rgba(160, 120, 200, 0.15);
    border: 1px solid #a078c8;
    border-radius: 0.25rem;
    color: #a078c8;
    cursor: pointer;
    font-size: clamp(0.6rem, 1.2vw, 0.75rem);
    font-weight: bold;
    margin-left: 0.5rem;
  }

  .stats-toggle:hover {
    background: rgba(160, 120, 200, 0.3);
  }

  .stats-overlay {
    position: fixed;
    inset: 0;
    display: flex;
    align-items: center;
    justify-content: center;
    background: rgba(0, 0, 0, 0.7);
    z-index: 100;
  }

  .stats-card {
    background: #1a1a2e;
    border: 1px solid #444;
    border-radius: 0.5rem;
    padding: 1.5rem;
    max-width: 400px;
    width: 90%;
    text-align: center;
  }

  .stats-title {
    margin: 0 0 1rem 0;
    color: #ddd;
    font-size: 1.25rem;
  }

  .stats-section {
    margin-top: 1rem;
    text-align: left;
  }

  .stats-section h3 {
    margin: 0 0 0.5rem 0;
    color: #aaa;
    font-size: 0.875rem;
  }

  .stats-tags {
    display: flex;
    flex-wrap: wrap;
    gap: 0.25rem;
  }

  .stat-tag {
    padding: 0.2rem 0.4rem;
    background: rgba(120, 160, 200, 0.2);
    border-radius: 0.25rem;
    font-size: 0.75rem;
    /* #78a0c8 measured 4.45:1 against its own tinted background — just under the 4.5:1 AA
       floor at this size. A slightly lighter blue clears it while keeping the same hue. */
    color: #8ab4dc;
  }

  .stats-loading {
    color: #888;
    font-size: 0.875rem;
  }

  .stats-close-btn {
    margin-top: 1rem;
    padding: 0.5rem 1.5rem;
    background: rgba(68, 170, 68, 0.2);
    border: 1px solid #44aa44;
    border-radius: 0.25rem;
    color: #88cc88;
    cursor: pointer;
  }

  .replay-modal-overlay {
    position: fixed;
    inset: 0;
    display: flex;
    align-items: center;
    justify-content: center;
    background: rgba(0, 0, 0, 0.9);
    z-index: 60;
    pointer-events: auto;
  }

  .replay-modal-card {
    background: #1a1a2e;
    border: 1px solid #d4a84b;
    border-radius: 0.5rem;
    padding: 2rem;
    min-width: 280px;
    max-width: 90vw;
    display: flex;
    flex-direction: column;
    gap: 1rem;
    align-items: center;
    text-align: center;
  }

  .replay-title {
    margin: 0;
    font-size: 1.25rem;
    color: #d4a84b;
  }

  .replay-anim {
    width: 120px;
    height: 120px;
    border-radius: 50%;
    border: 2px solid #444;
    background: rgba(212, 168, 75, 0.1);
    animation: synergyPulse 500ms ease-out;
  }

  @keyframes synergyPulse {
    0% {
      box-shadow: 0 0 0 0 rgba(212, 168, 75, 0.9);
      border-color: #d4a84b;
      transform: scale(1);
    }
    50% {
      box-shadow: 0 0 1em 0.5em rgba(212, 168, 75, 0.6);
      border-color: #ffdd77;
      transform: scale(1.1);
    }
    100% {
      box-shadow: 0 0 2em 1em rgba(212, 168, 75, 0);
      border-color: #444;
      transform: scale(1);
    }
  }

  .replay-close-btn {
    padding: 0.5rem 1.5rem;
    background: rgba(255, 255, 255, 0.05);
    border: 1px solid #666;
    border-radius: 0.25rem;
    color: #ccc;
    cursor: pointer;
    font-size: 0.875rem;
  }

  .replay-close-btn:hover {
    background: rgba(255, 255, 255, 0.15);
  }

  @keyframes fadeIn {
    from { opacity: 0; transform: translateY(-0.5em); }
    to { opacity: 1; transform: translateY(0); }
  }

  .subtitle-overlay {
    position: fixed;
    bottom: 2rem;
    left: 50%;
    transform: translateX(-50%);
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 0.25rem;
    z-index: 50;
    pointer-events: none;
  }

  .subtitle-line {
    background: rgba(0, 0, 0, 0.7);
    color: #aaa;
    font-size: 0.875rem;
    font-style: italic;
    padding: 0.25rem 0.75rem;
    border-radius: 0.25rem;
    animation: fadeIn 200ms ease-out;
  }

  .faction-notification {
    position: fixed;
    top: 5rem;
    right: 1rem;
    background: rgba(30, 15, 40, 0.9);
    border: 1px solid #aa44aa;
    border-radius: 0.25rem;
    color: #d4a84b;
    padding: 0.5rem 1rem;
    font-size: 0.875rem;
    z-index: 50;
    animation: fadeIn 300ms ease-out;
    max-width: 280px;
    pointer-events: none;
  }
</style>
