<script lang="ts">
  interface Props {
    onEnterTown: () => void;
    onOpenSettings: () => void;
  }

  let { onEnterTown, onOpenSettings }: Props = $props();
</script>

<div class="title-screen">
  <div class="title-vignette"></div>
  <div class="title-content">
    <h1 class="title">The Reach</h1>
    <p class="subtitle">An Old-School Dungeon Crawler</p>
    <div class="title-divider"></div>
    <nav class="title-actions" aria-label="Main menu">
      <button type="button" class="title-btn primary" onclick={onEnterTown}>
        Enter Town
      </button>
      <button type="button" class="title-btn" onclick={onOpenSettings}>
        Settings
      </button>
    </nav>
  </div>
</div>

<style>
  .title-screen {
    position: fixed;
    inset: 0;
    display: flex;
    align-items: center;
    justify-content: center;
    background: radial-gradient(ellipse at center, #1a1a2e 0%, #0a0a0f 70%);
    z-index: 20;
    pointer-events: auto;
  }

  .title-vignette {
    position: absolute;
    inset: 0;
    background: radial-gradient(ellipse at center, transparent 40%, rgba(0, 0, 0, 0.6) 100%);
    pointer-events: none;
  }

  .title-content {
    position: relative;
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 1rem;
    text-align: center;
    padding: 2rem;
    animation: fadeIn 1s ease-out;
  }

  .title {
    margin: 0;
    font-size: clamp(3rem, 10vw, 6rem);
    font-weight: 700;
    color: #d4a84b;
    letter-spacing: -0.05em;
    text-shadow:
      0 0 0.3em rgba(212, 168, 75, 0.4),
      0 0.1em 0.2em rgba(0, 0, 0, 0.8);
  }

  .subtitle {
    margin: 0;
    font-size: clamp(0.875rem, 2vw, 1.25rem);
    color: #888;
    letter-spacing: 0.15em;
    text-transform: uppercase;
  }

  .title-divider {
    width: 6rem;
    height: 0.125rem;
    background: linear-gradient(90deg, transparent, #d4a84b, transparent);
    margin: 0.5rem 0;
  }

  .title-actions {
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
    min-width: clamp(12rem, 25vw, 16rem);
    margin-top: 1rem;
  }

  .title-btn {
    padding: 0.75rem 1.5rem;
    background: rgba(255, 255, 255, 0.03);
    border: 0.0625em solid #444;
    border-radius: 0.375rem;
    color: #ccc;
    font-size: clamp(0.875rem, 1.8vw, 1rem);
    cursor: pointer;
    transition: background 0.2s, border-color 0.2s, color 0.2s, transform 0.15s;
    letter-spacing: 0.05em;
  }

  .title-btn:hover {
    background: rgba(255, 255, 255, 0.08);
    border-color: #666;
    color: #fff;
    transform: translateY(-0.125rem);
  }

  .title-btn.primary {
    background: rgba(212, 168, 75, 0.1);
    border-color: #d4a84b;
    color: #d4a84b;
    font-weight: 600;
  }

  .title-btn.primary:hover {
    background: rgba(212, 168, 75, 0.2);
    border-color: #e8c56a;
    color: #e8c56a;
  }

  @keyframes fadeIn {
    from { opacity: 0; transform: translateY(1rem); }
    to { opacity: 1; transform: translateY(0); }
  }
</style>
