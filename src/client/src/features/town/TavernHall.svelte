<script lang="ts">
  import type { TavernRecruit } from '$shared/types/game';

  interface Props {
    recruits: TavernRecruit[];
    gold: number;
    onRecruit: (id: string) => void;
    /** Rest the party at the inn. Omit to hide the rest card. */
    onRest?: () => void;
    restCost?: number;
    resolveClassName?: (classId: string) => string;
  }

  let { recruits, gold, onRecruit, onRest, restCost = 0, resolveClassName }: Props = $props();

  const prettify = (id: string) => id.replace(/[_-]+/g, ' ').replace(/\b\w/g, (c) => c.toUpperCase());
  const className = (id: string) => resolveClassName?.(id) ?? prettify(id);

  // Class identity tints — kept inside the warm register, distinct enough to read at a glance.
  const CLASS_TINT: Record<string, string> = {
    bonewarden: '#b8a229', stillblade: '#7fa0a8', cauterist: '#c87a4a',
    hollow: '#8a7fa8', ashmouth: '#b04a3a', fieldwright: '#a89a5b',
    inkblood: '#9a4a6b', marcher: '#9a8a4a',
  };
  const tint = (id: string) => CLASS_TINT[id] ?? '#9a7a34';

  const STATS: { key: keyof TavernRecruit['baseStats']; abbr: string }[] = [
    { key: 'strength', abbr: 'STR' },
    { key: 'dexterity', abbr: 'DEX' },
    { key: 'constitution', abbr: 'CON' },
    { key: 'intelligence', abbr: 'INT' },
    { key: 'willpower', abbr: 'WIL' },
  ];

  const canAfford = (cost: number) => gold >= cost;
</script>

<section class="tavern" aria-label="The Tavern — hands for hire">
  <header class="masthead">
    <span class="rule" aria-hidden="true"></span>
    <h2>The Tavern</h2>
    <span class="seal" aria-hidden="true">⚱</span>
    <span class="rule" aria-hidden="true"></span>
  </header>

  <div class="purse-row">
    <span class="purse"><span class="coin" aria-hidden="true">◉</span> {gold} <span class="purse-label">coin</span></span>
    {#if onRest}
      <button class="rest" onclick={() => onRest?.()}>
        <span class="rest-glow" aria-hidden="true"></span>
        <span class="rest-title">Rest at the Inn</span>
        <span class="rest-sub">{restCost > 0 ? `${restCost} coin · ` : ''}mend wounds, clear the head</span>
      </button>
    {/if}
  </div>

  <h3 class="board-title">Hands for Hire</h3>

  {#if recruits.length === 0}
    <p class="empty">The benches are empty tonight. No one worth the coin.</p>
  {:else}
    <div class="roster">
      {#each recruits as r, i (r.id)}
        {@const afford = canAfford(r.cost)}
        <article class="hand" class:unaffordable={!afford} style={`--tint:${tint(r.classId)}; --i:${i}`}>
          <div class="hand-head">
            <div class="medallion" style={`--tint:${tint(r.classId)}`}>
              <span class="initial">{r.name.charAt(0)}</span>
            </div>
            <div class="hand-id">
              <span class="hand-name">{r.name}</span>
              <span class="hand-class" style={`color:${tint(r.classId)}`}>{className(r.classId)}</span>
              <span class="hand-lv">Lv.{r.level}</span>
            </div>
          </div>

          <dl class="statline">
            {#each STATS as s}
              <div><dt>{s.abbr}</dt><dd>{r.baseStats[s.key]}</dd></div>
            {/each}
          </dl>

          <div class="hand-foot">
            <span class="cost" class:short={!afford}><span class="coin" aria-hidden="true">◉</span> {r.cost}</span>
            <button class="hire" disabled={!afford} onclick={() => onRecruit(r.id)}>
              {afford ? 'Recruit' : 'Too dear'}
            </button>
          </div>
        </article>
      {/each}
    </div>
  {/if}
</section>

<style>
  .tavern {
    --ink: #e8dcc0; --ink-dim: #a9977a; --ink-faint: #7d6e55;
    --umber: #1c1611; --umber-2: #241c14; --panel: #2b2117; --panel-2: #322619;
    --gold: #c89b3c; --gold-soft: #9a7a34; --ember: #d4622a; --hearth: #e08a3c;
    --moss: #8a9a4f; --blood: #a8352a;
    --font-display: 'Iowan Old Style', 'Palatino Linotype', 'Book Antiqua', Georgia, 'Times New Roman', serif;

    color: var(--ink);
    font-family: var(--font-display);
    background:
      radial-gradient(90% 60% at 50% 120%, rgba(224, 138, 60, 0.10), transparent 60%),
      repeating-linear-gradient(0deg, rgba(0,0,0,0.10) 0 2px, transparent 2px 4px),
      linear-gradient(160deg, var(--umber-2), var(--umber));
    border: 1px solid #000;
    box-shadow:
      0 0 0 1px var(--gold-soft) inset, 0 0 0 3px var(--umber) inset,
      0 0 0 4px rgba(200, 155, 60, 0.22) inset, 0 24px 60px rgba(0,0,0,0.6);
    border-radius: 3px;
    padding: 1rem 1.1rem 1.2rem;
    width: 100%; max-width: 880px; position: relative;
  }
  .tavern::before {
    content: ''; position: absolute; inset: 0; pointer-events: none; opacity: 0.5; mix-blend-mode: overlay;
    background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='120' height='120'%3E%3Cfilter id='n'%3E%3CfeTurbulence type='fractalNoise' baseFrequency='0.9' numOctaves='2' stitchTiles='stitch'/%3E%3C/filter%3E%3Crect width='100%25' height='100%25' filter='url(%23n)' opacity='0.5'/%3E%3C/svg%3E");
  }

  .masthead { display: flex; align-items: center; gap: 0.75rem; margin-bottom: 0.9rem; }
  .masthead h2 { margin: 0; font-size: 1.5rem; letter-spacing: 0.32em; text-transform: uppercase; font-variant: small-caps; text-shadow: 0 1px 0 #000; white-space: nowrap; }
  .masthead .seal { color: var(--gold); font-size: 1rem; }
  .masthead .rule { flex: 1; height: 2px; background: linear-gradient(90deg, transparent, var(--gold-soft), transparent); box-shadow: 0 1px 0 rgba(0,0,0,0.6); }

  .purse-row { display: flex; align-items: stretch; gap: 0.8rem; margin-bottom: 1rem; position: relative; z-index: 1; }
  .purse {
    display: inline-flex; align-items: center; gap: 0.4rem;
    font-size: 1.1rem; color: var(--gold); letter-spacing: 0.04em;
    background: rgba(0,0,0,0.3); border: 1px solid var(--gold-soft); border-radius: 2px;
    padding: 0.5rem 0.9rem;
  }
  .purse-label { font-size: 0.62rem; text-transform: uppercase; letter-spacing: 0.12em; color: var(--ink-faint); }
  .coin { color: var(--gold); }

  .rest {
    flex: 1; text-align: left; position: relative; overflow: hidden;
    font: inherit; cursor: pointer;
    display: flex; flex-direction: column; gap: 0.1rem;
    background: linear-gradient(160deg, rgba(224,138,60,0.12), rgba(0,0,0,0.25));
    border: 1px solid rgba(224,138,60,0.4); border-radius: 2px;
    padding: 0.5rem 0.9rem; color: var(--ink); transition: all 0.2s;
  }
  .rest:hover { border-color: var(--hearth); box-shadow: inset 0 0 24px rgba(224,138,60,0.25); }
  .rest-glow { position: absolute; right: -30px; top: 50%; width: 120px; height: 120px; transform: translateY(-50%); background: radial-gradient(circle, rgba(224,138,60,0.35), transparent 70%); pointer-events: none; }
  .rest-title { font-size: 0.95rem; font-variant: small-caps; letter-spacing: 0.06em; }
  .rest-sub { font-size: 0.7rem; color: var(--ink-dim); }

  .board-title, h3.board-title {
    margin: 0 0 0.6rem; font-size: 0.66rem; text-transform: uppercase; letter-spacing: 0.18em;
    color: var(--gold-soft); border-bottom: 1px dashed rgba(200,155,60,0.25); padding-bottom: 0.3rem;
  }

  .empty { color: var(--ink-faint); font-style: italic; font-size: 0.85rem; }

  .roster {
    display: grid; grid-template-columns: repeat(auto-fill, minmax(210px, 1fr)); gap: 0.7rem;
    position: relative; z-index: 1;
  }

  .hand {
    background: radial-gradient(120% 100% at 0% 0%, color-mix(in srgb, var(--tint) 10%, transparent), transparent 55%),
      linear-gradient(165deg, var(--panel-2), var(--panel));
    border: 1px solid rgba(200,155,60,0.2); border-radius: 2px;
    padding: 0.7rem; box-shadow: inset 0 0 26px rgba(0,0,0,0.4);
    animation: rise 0.45s both; animation-delay: calc(var(--i) * 50ms);
  }
  .hand.unaffordable { opacity: 0.72; }

  .hand-head { display: flex; gap: 0.55rem; align-items: center; margin-bottom: 0.55rem; }
  .medallion {
    width: 42px; height: 42px; flex-shrink: 0; border-radius: 50%; display: grid; place-items: center;
    background: radial-gradient(circle at 32% 28%, color-mix(in srgb, var(--tint) 70%, #000), #100c08 78%);
    border: 2px solid var(--gold-soft); box-shadow: 0 0 0 1px #000, inset 0 0 8px rgba(0,0,0,0.7);
  }
  .initial { font-size: 1.15rem; font-weight: 700; color: var(--ink); text-shadow: 0 1px 2px #000; }
  .hand-id { display: flex; flex-direction: column; min-width: 0; }
  .hand-name { font-size: 0.98rem; letter-spacing: 0.02em; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
  .hand-class { font-size: 0.72rem; font-variant: small-caps; letter-spacing: 0.05em; }
  .hand-lv { font-size: 0.62rem; color: var(--ink-faint); text-transform: uppercase; letter-spacing: 0.1em; }

  .statline { display: grid; grid-template-columns: repeat(5, 1fr); gap: 0.25rem; margin: 0 0 0.6rem; }
  .statline div { display: flex; flex-direction: column; align-items: center; background: rgba(0,0,0,0.25); border: 1px solid rgba(200,155,60,0.12); border-radius: 2px; padding: 0.2rem 0; }
  .statline dt { font-size: 0.5rem; letter-spacing: 0.06em; color: var(--ink-faint); }
  .statline dd { margin: 0.05rem 0 0; font-size: 0.92rem; font-weight: 700; }

  .hand-foot { display: flex; align-items: center; justify-content: space-between; }
  .cost { font-size: 0.95rem; color: var(--gold); display: inline-flex; align-items: center; gap: 0.25rem; }
  .cost.short { color: var(--blood); }
  .hire {
    font: inherit; font-size: 0.78rem; letter-spacing: 0.04em; font-variant: small-caps;
    color: var(--ink); background: rgba(0,0,0,0.35);
    border: 1px solid var(--ember); border-radius: 1px; padding: 0.3rem 0.75rem; cursor: pointer; transition: all 0.2s;
  }
  .hire:hover:not(:disabled) { background: rgba(212,98,42,0.25); box-shadow: 0 0 10px rgba(212,98,42,0.5); }
  .hire:disabled { border-color: rgba(120,110,85,0.5); color: var(--ink-faint); cursor: not-allowed; }

  @keyframes rise { from { opacity: 0; transform: translateY(8px); } to { opacity: 1; transform: none; } }
  @media (prefers-reduced-motion: reduce) { .hand { animation: none; } }
</style>
