<script lang="ts">
  import type { PartyMember } from '$shared/types/game';

  interface Props {
    party: PartyMember[];
    /** Resolve an item id to a display name. Defaults to a prettified id. */
    resolveItemName?: (id: string) => string;
    /** Resolve an ability id to a display name. Defaults to a prettified id. */
    resolveAbilityName?: (id: string) => string;
    /** Swap a member between front and back rank. */
    onSwapRow?: (slot: number) => void;
    /** Commit a branch/specialization choice for a member awaiting one. */
    onChooseBranch?: (slot: number, branch: string) => void;
    /** Notify the host which member is selected. */
    onSelect?: (slot: number) => void;
    /** Open the full character sheet (inventory transfer etc.) for a member. */
    onOpenSheet?: (slot: number) => void;
  }

  let {
    party,
    resolveItemName,
    resolveAbilityName,
    onSwapRow,
    onChooseBranch,
    onSelect,
    onOpenSheet,
  }: Props = $props();

  const prettify = (id: string | null | undefined): string =>
    !id ? '—' : id.replace(/[_-]+/g, ' ').replace(/\b\w/g, (c) => c.toUpperCase());

  const itemName = (id: string | null) => (id ? (resolveItemName?.(id) ?? prettify(id)) : '—');
  const abilityName = (id: string) => resolveAbilityName?.(id) ?? prettify(id);

  // ---- selection --------------------------------------------------------
  const firstSlot = $derived(
    (party.find((m) => m.alive) ?? party[0])?.slot ?? -1
  );
  let selectedSlot = $state<number | null>(null);
  const activeSlot = $derived(selectedSlot ?? firstSlot);
  const selected = $derived(party.find((m) => m.slot === activeSlot) ?? null);

  function select(slot: number) {
    selectedSlot = slot;
    onSelect?.(slot);
  }

  function onRailKey(e: KeyboardEvent) {
    if (e.key !== 'ArrowDown' && e.key !== 'ArrowUp') return;
    e.preventDefault();
    const order = party.map((m) => m.slot);
    const i = order.indexOf(activeSlot);
    if (i < 0) return;
    const next = e.key === 'ArrowDown' ? (i + 1) % order.length : (i - 1 + order.length) % order.length;
    select(order[next]);
  }

  // ---- per-member status ------------------------------------------------
  const hpPct = (m: PartyMember) => (m.maxHp > 0 ? Math.max(0, Math.min(1, m.hp / m.maxHp)) : 0);
  const wounded = (m: PartyMember) => !m.alive || hpPct(m) <= 0.3;
  const lowSupplies = (m: PartyMember) =>
    (m.componentInventory ?? []).some((c) => c.count > 0 && c.count <= 3);
  const branchReady = (m: PartyMember) => m.awaitingBranchChoice === true;

  // Warm-dark HP tiering: moss when healthy, ochre when pressed, blood when failing.
  function hpTier(pct: number): 'hale' | 'pressed' | 'failing' {
    if (pct > 0.6) return 'hale';
    if (pct > 0.3) return 'pressed';
    return 'failing';
  }

  const branchLine = (m: PartyMember) => {
    const spec = m.branchChoice ? prettify(m.branchChoice) : null;
    return spec ? `${m.className} · ${spec}` : m.className;
  };

  const xpPct = (m: PartyMember) => {
    // No explicit next-level threshold in the model; show progress within the current 1000-xp band.
    const within = m.xp % 1000;
    return Math.max(0, Math.min(100, (within / 1000) * 100));
  };

  const STATS: { key: keyof PartyMember['stats']; label: string; abbr: string }[] = [
    { key: 'strength', label: 'Strength', abbr: 'STR' },
    { key: 'dexterity', label: 'Dexterity', abbr: 'DEX' },
    { key: 'constitution', label: 'Constitution', abbr: 'CON' },
    { key: 'intelligence', label: 'Intelligence', abbr: 'INT' },
    { key: 'willpower', label: 'Willpower', abbr: 'WIL' },
  ];
  const DERIVED: { key: keyof PartyMember['stats']; abbr: string }[] = [
    { key: 'speed', abbr: 'SPD' },
    { key: 'accuracy', abbr: 'ACC' },
    { key: 'evade', abbr: 'EVA' },
    { key: 'power', abbr: 'PWR' },
  ];
  const EQUIP: { key: keyof PartyMember['equipment']; label: string }[] = [
    { key: 'mainHand', label: 'Main Hand' },
    { key: 'offHand', label: 'Off Hand' },
    { key: 'armor', label: 'Armor' },
    { key: 'accessory1', label: 'Trinket' },
    { key: 'accessory2', label: 'Trinket' },
  ];

  const abilitiesOf = (m: PartyMember) =>
    (m.classAbilities && m.classAbilities.length > 0)
      ? m.classAbilities.map((a) => ({ id: a.id, name: a.name ?? abilityName(a.id), branch: a.branch }))
      : m.knownAbilities.map((id) => ({ id, name: abilityName(id), branch: undefined as string | undefined }));
</script>

<section class="broadsheet" aria-label="The Company — party roster">
  <header class="masthead">
    <span class="rule" aria-hidden="true"></span>
    <h2>The Company</h2>
    <span class="seal" aria-hidden="true">✶</span>
    <span class="rule" aria-hidden="true"></span>
  </header>

  <div class="body">
    <!-- ROSTER RAIL -->
    <ul
      class="roster"
      role="listbox"
      aria-label="Party members"
      aria-activedescendant={`roster-${activeSlot}`}
      tabindex="0"
      onkeydown={onRailKey}
    >
      {#each party as m, i (m.slot)}
        {@const pct = hpPct(m)}
        <li
          id={`roster-${m.slot}`}
          class="rail-row"
          class:selected={m.slot === activeSlot}
          class:dead={!m.alive}
          role="option"
          aria-selected={m.slot === activeSlot}
          style={`--accent:${m.color}; --i:${i}`}
          onclick={() => select(m.slot)}
          onkeydown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); select(m.slot); } }}
        >
          <div class="medallion" style={`--accent:${m.color}`}>
            <span class="initial">{m.name.charAt(0)}</span>
          </div>
          <div class="rail-info">
            <div class="rail-top">
              <span class="rail-name">{m.name}</span>
              <span class="flags">
                {#if branchReady(m)}<span class="flag ready" title="Branch ready">▲</span>{/if}
                {#if lowSupplies(m)}<span class="flag supply" title="Low supplies">!</span>{/if}
                {#if wounded(m)}<span class="flag hurt" title="Wounded">✚</span>{/if}
              </span>
            </div>
            <div class="rail-sub">Lv.{m.level} · {m.className}</div>
            <div class="sliver" data-tier={hpTier(pct)}>
              <span class="sliver-fill" style={`width:${pct * 100}%`}></span>
            </div>
          </div>
        </li>
      {/each}
      <li class="rank-legend" aria-hidden="true">
        <span>━ front</span><span>back ━</span>
      </li>
    </ul>

    <!-- DETAIL RECORD -->
    {#if selected}
      {@const m = selected}
      {@const pct = hpPct(m)}
      <article class="record" style={`--accent:${m.color}`}>
        <div class="rec-head">
          <div class="portrait" aria-hidden="true">
            <span class="portrait-initial">{m.name.charAt(0)}</span>
          </div>
          <div class="rec-id">
            <h3>{m.name}</h3>
            <p class="rec-class">{branchLine(m)} · <span class="lv">Lv.{m.level}</span></p>
            <div class="rank">
              <span class="rank-badge">{m.row === 0 ? 'Front Rank' : 'Back Rank'}</span>
              {#if onSwapRow}
                <button class="swap" onclick={() => onSwapRow?.(m.slot)}>⇄ Swap rank</button>
              {/if}
              {#if onOpenSheet}
                <button class="swap" onclick={() => onOpenSheet?.(m.slot)}>Full sheet</button>
              {/if}
            </div>
          </div>
        </div>

        <div class="vitals">
          <div class="vital">
            <span class="vital-label">Vitality</span>
            <div class="vbar" data-tier={hpTier(pct)}>
              <span class="vbar-fill" style={`width:${pct * 100}%`}></span>
              <span class="vbar-text">{m.hp} / {m.maxHp}</span>
            </div>
          </div>
          <div class="vital">
            <span class="vital-label">Experience</span>
            <div class="vbar xp">
              <span class="vbar-fill" style={`width:${xpPct(m)}%`}></span>
              <span class="vbar-text">{m.xp} xp</span>
            </div>
          </div>
        </div>

        <div class="columns">
          <div class="col">
            <h4>Attributes</h4>
            <dl class="stats">
              {#each STATS as s}
                <div class="stat">
                  <dt title={s.label}>{s.abbr}</dt>
                  <dd>{m.stats[s.key]}</dd>
                </div>
              {/each}
            </dl>
            <div class="derived">
              {#each DERIVED as d}
                <span class="der"><b>{d.abbr}</b> {m.stats[d.key]}</span>
              {/each}
            </div>
          </div>

          <div class="col">
            <h4>Wargear</h4>
            <ul class="equip">
              {#each EQUIP as e}
                <li class:empty={!m.equipment[e.key]}>
                  <span class="eq-slot">{e.label}</span>
                  <span class="eq-item">{itemName(m.equipment[e.key])}</span>
                </li>
              {/each}
            </ul>
          </div>
        </div>

        <div class="abilities">
          <h4>Abilities</h4>
          <div class="ability-row">
            {#each abilitiesOf(m) as a (a.id)}
              <span class="ability" title={a.name}>
                <span class="glyph" aria-hidden="true">◆</span>{a.name}
              </span>
            {/each}
            {#if (m.availableBranches?.length ?? 0) > 0 && !m.branchChoice}
              <span class="ability locked" title="Specialty locked until you choose a branch">
                <span class="glyph" aria-hidden="true">◇</span>specialty
              </span>
            {/if}
          </div>
        </div>

        {#if branchReady(m)}
          <div class="branch-call" role="group" aria-label="Branch ready">
            <p class="bc-head"><span class="bc-mark">▲</span> Branch ready — choose this warden's path</p>
            <div class="bc-options">
              {#each (m.availableBranches ?? []) as b}
                <button class="bc-btn" onclick={() => onChooseBranch?.(m.slot, b)}>{prettify(b)}</button>
              {/each}
            </div>
            {#each (m.branchWarnings ?? []) as w}
              <p class="bc-warn">{w}</p>
            {/each}
          </div>
        {/if}
      </article>
    {/if}
  </div>
</section>

<style>
  .broadsheet {
    /* — warm-dark muster-roll palette — */
    --ink: #e8dcc0;            /* aged cream text */
    --ink-dim: #a9977a;        /* faded ink */
    --ink-faint: #7d6e55;
    --umber: #1c1611;          /* charred wood base */
    --umber-2: #241c14;
    --panel: #2b2117;          /* parchment-in-shadow */
    --panel-2: #322619;
    --gold: #c89b3c;           /* engraved hairline / accent */
    --gold-soft: #9a7a34;
    --ember: #d4622a;          /* selection / readiness fire */
    --moss: #8a9a4f;           /* HP hale */
    --ochre: #c8923a;          /* HP pressed */
    --blood: #a8352a;          /* HP failing */
    --arcane: #5c8fb0;         /* cool — supernatural cue only */

    --font-display: 'Iowan Old Style', 'Palatino Linotype', 'Book Antiqua', Georgia, 'Times New Roman', serif;
    --font-label: 'Iowan Old Style', Georgia, serif;

    color: var(--ink);
    font-family: var(--font-display);
    background:
      radial-gradient(120% 80% at 50% -10%, rgba(200, 155, 60, 0.06), transparent 60%),
      repeating-linear-gradient(0deg, rgba(0,0,0,0.10) 0 2px, transparent 2px 4px),
      linear-gradient(160deg, var(--umber-2), var(--umber));
    border: 1px solid #000;
    box-shadow:
      0 0 0 1px var(--gold-soft) inset,
      0 0 0 3px var(--umber) inset,
      0 0 0 4px rgba(200, 155, 60, 0.25) inset,
      0 24px 60px rgba(0, 0, 0, 0.6);
    border-radius: 3px;
    padding: 1rem 1.1rem 1.2rem;
    width: 100%;
    max-width: 880px;
    position: relative;
  }
  /* faint paper grain */
  .broadsheet::before {
    content: '';
    position: absolute;
    inset: 0;
    pointer-events: none;
    opacity: 0.5;
    mix-blend-mode: overlay;
    background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='120' height='120'%3E%3Cfilter id='n'%3E%3CfeTurbulence type='fractalNoise' baseFrequency='0.9' numOctaves='2' stitchTiles='stitch'/%3E%3C/filter%3E%3Crect width='100%25' height='100%25' filter='url(%23n)' opacity='0.5'/%3E%3C/svg%3E");
  }

  /* — masthead — */
  .masthead {
    display: flex;
    align-items: center;
    gap: 0.75rem;
    margin-bottom: 0.9rem;
  }
  .masthead h2 {
    margin: 0;
    font-size: 1.5rem;
    letter-spacing: 0.32em;
    text-transform: uppercase;
    font-variant: small-caps;
    color: var(--ink);
    text-shadow: 0 1px 0 #000;
    white-space: nowrap;
  }
  .masthead .seal { color: var(--gold); font-size: 0.85rem; }
  .masthead .rule {
    flex: 1;
    height: 2px;
    background: linear-gradient(90deg, transparent, var(--gold-soft), transparent);
    box-shadow: 0 1px 0 rgba(0,0,0,0.6);
  }

  .body {
    display: grid;
    grid-template-columns: minmax(190px, 240px) 1fr;
    gap: 1.1rem;
    position: relative;
    z-index: 1;
  }

  /* — roster rail — */
  .roster {
    list-style: none;
    margin: 0;
    padding: 0.35rem;
    display: flex;
    flex-direction: column;
    gap: 0.3rem;
    background: linear-gradient(180deg, rgba(0,0,0,0.28), rgba(0,0,0,0.12));
    border: 1px solid rgba(200, 155, 60, 0.18);
    border-radius: 2px;
    outline: none;
  }
  .roster:focus-visible { box-shadow: 0 0 0 2px var(--ember); }

  .rail-row {
    display: flex;
    align-items: center;
    gap: 0.6rem;
    padding: 0.4rem 0.45rem;
    border: 1px solid transparent;
    border-radius: 2px;
    cursor: pointer;
    position: relative;
    animation: rise 0.5s both;
    animation-delay: calc(var(--i) * 55ms);
  }
  .rail-row:hover { background: rgba(212, 98, 42, 0.08); }
  .rail-row.selected {
    background: linear-gradient(90deg, rgba(212, 98, 42, 0.18), rgba(212, 98, 42, 0.03));
    border-color: rgba(200, 155, 60, 0.5);
    box-shadow: inset 0 0 14px rgba(212, 98, 42, 0.25);
  }
  .rail-row.selected::before {
    content: '';
    position: absolute;
    left: -0.35rem; top: 18%; bottom: 18%;
    width: 3px;
    background: var(--ember);
    box-shadow: 0 0 8px var(--ember);
  }
  .rail-row.dead { opacity: 0.4; filter: grayscale(0.7); }

  .medallion {
    width: 38px; height: 38px;
    flex-shrink: 0;
    border-radius: 50%;
    display: grid;
    place-items: center;
    background:
      radial-gradient(circle at 32% 28%, color-mix(in srgb, var(--accent) 70%, #000) , #100c08 78%);
    border: 2px solid var(--gold-soft);
    box-shadow: 0 0 0 1px #000, inset 0 0 8px rgba(0,0,0,0.7);
  }
  .initial {
    font-size: 1.05rem;
    font-weight: 700;
    color: var(--ink);
    text-shadow: 0 1px 2px #000;
  }

  .rail-info { min-width: 0; flex: 1; }
  .rail-top { display: flex; align-items: baseline; justify-content: space-between; gap: 0.3rem; }
  .rail-name {
    font-size: 0.95rem;
    letter-spacing: 0.02em;
    color: var(--ink);
    white-space: nowrap; overflow: hidden; text-overflow: ellipsis;
  }
  .rail-sub {
    font-size: 0.68rem;
    color: var(--ink-faint);
    text-transform: uppercase;
    letter-spacing: 0.08em;
    margin: 0.05rem 0 0.25rem;
  }
  .flags { display: inline-flex; gap: 0.2rem; flex-shrink: 0; }
  .flag { font-size: 0.72rem; line-height: 1; font-weight: 700; }
  .flag.ready { color: var(--arcane); text-shadow: 0 0 6px rgba(92, 143, 176, 0.7); }
  .flag.supply { color: var(--ochre); }
  .flag.hurt { color: var(--blood); }

  .sliver {
    height: 5px;
    background: rgba(0,0,0,0.55);
    border-radius: 3px;
    overflow: hidden;
    box-shadow: inset 0 0 3px rgba(0,0,0,0.8);
  }
  .sliver-fill, .vbar-fill { height: 100%; display: block; transition: width 0.35s ease; }
  .sliver[data-tier='hale'] .sliver-fill,
  .vbar[data-tier='hale'] .vbar-fill { background: linear-gradient(90deg, #5e6b34, var(--moss)); }
  .sliver[data-tier='pressed'] .sliver-fill,
  .vbar[data-tier='pressed'] .vbar-fill { background: linear-gradient(90deg, #8a5e22, var(--ochre)); }
  .sliver[data-tier='failing'] .sliver-fill,
  .vbar[data-tier='failing'] .vbar-fill { background: linear-gradient(90deg, #6e2018, var(--blood)); }

  .rank-legend {
    display: flex; justify-content: space-between;
    margin-top: 0.2rem; padding: 0.3rem 0.2rem 0;
    font-size: 0.6rem; letter-spacing: 0.12em; text-transform: uppercase;
    color: var(--ink-faint);
    border-top: 1px dashed rgba(200,155,60,0.2);
  }

  /* — detail record — */
  .record {
    background:
      radial-gradient(140% 120% at 100% 0%, rgba(200,155,60,0.05), transparent 55%),
      linear-gradient(165deg, var(--panel-2), var(--panel));
    border: 1px solid rgba(200,155,60,0.22);
    border-radius: 2px;
    padding: 1rem 1.1rem;
    box-shadow: inset 0 0 40px rgba(0,0,0,0.45);
    animation: fade 0.4s both;
  }

  .rec-head { display: flex; gap: 0.9rem; align-items: flex-start; }
  .portrait {
    width: 76px; height: 92px; flex-shrink: 0;
    border-radius: 2px;
    display: grid; place-items: center;
    background:
      radial-gradient(circle at 38% 30%, color-mix(in srgb, var(--accent) 65%, #000), #0d0a07 82%);
    border: 1px solid var(--gold-soft);
    box-shadow: 0 0 0 1px #000, inset 0 -18px 26px rgba(0,0,0,0.7);
    position: relative;
  }
  .portrait::after {
    content: ''; position: absolute; inset: 3px;
    border: 1px solid rgba(200,155,60,0.25);
  }
  .portrait-initial { font-size: 2.4rem; font-weight: 700; color: var(--ink); text-shadow: 0 2px 4px #000; }

  .rec-id h3 {
    margin: 0;
    font-size: 1.45rem;
    letter-spacing: 0.03em;
    font-variant: small-caps;
    color: var(--ink);
    text-shadow: 0 1px 0 #000;
  }
  .rec-class { margin: 0.1rem 0 0.5rem; color: var(--ink-dim); font-size: 0.85rem; letter-spacing: 0.04em; }
  .rec-class .lv { color: var(--gold); }
  .rank { display: flex; align-items: center; gap: 0.6rem; }
  .rank-badge {
    font-size: 0.62rem; text-transform: uppercase; letter-spacing: 0.14em;
    color: var(--ink); background: rgba(0,0,0,0.35);
    border: 1px solid var(--gold-soft); padding: 0.18rem 0.5rem; border-radius: 1px;
  }
  .swap {
    font: inherit; font-size: 0.72rem; letter-spacing: 0.05em;
    color: var(--ink-dim); background: transparent;
    border: 1px solid rgba(200,155,60,0.3); border-radius: 1px;
    padding: 0.2rem 0.55rem; cursor: pointer; transition: all 0.2s;
  }
  .swap:hover { color: var(--ink); border-color: var(--ember); box-shadow: 0 0 8px rgba(212,98,42,0.4); }

  .vitals { display: grid; grid-template-columns: 1fr 1fr; gap: 0.7rem; margin: 0.9rem 0; }
  .vital-label, .col h4, .abilities h4 {
    font-size: 0.62rem; text-transform: uppercase; letter-spacing: 0.16em;
    color: var(--gold-soft); margin: 0 0 0.3rem;
  }
  .vbar {
    position: relative; height: 18px;
    background: rgba(0,0,0,0.5); border: 1px solid rgba(0,0,0,0.6);
    border-radius: 2px; overflow: hidden; box-shadow: inset 0 0 6px rgba(0,0,0,0.7);
  }
  .vbar.xp .vbar-fill { background: linear-gradient(90deg, #4a3a8a, var(--arcane)); }
  .vbar-text {
    position: absolute; inset: 0; display: grid; place-items: center;
    font-size: 0.68rem; letter-spacing: 0.05em; color: var(--ink);
    text-shadow: 0 1px 2px #000;
  }

  .columns { display: grid; grid-template-columns: 1fr 1fr; gap: 1.1rem; margin-bottom: 0.9rem; }

  .stats { display: grid; grid-template-columns: repeat(5, 1fr); gap: 0.3rem; margin: 0; }
  .stat {
    display: flex; flex-direction: column; align-items: center;
    padding: 0.35rem 0.1rem;
    background: rgba(0,0,0,0.25);
    border: 1px solid rgba(200,155,60,0.14);
    border-radius: 2px;
  }
  .stat dt { font-size: 0.56rem; letter-spacing: 0.08em; color: var(--ink-faint); }
  .stat dd { margin: 0.1rem 0 0; font-size: 1.15rem; font-weight: 700; color: var(--ink); }
  .derived {
    display: flex; flex-wrap: wrap; gap: 0.15rem 0.9rem;
    margin-top: 0.5rem; font-size: 0.72rem; color: var(--ink-dim);
  }
  .derived b { color: var(--gold-soft); font-weight: 700; letter-spacing: 0.05em; }

  .equip { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 0.2rem; }
  .equip li {
    display: flex; justify-content: space-between; gap: 0.5rem;
    padding: 0.25rem 0.4rem;
    border-bottom: 1px dotted rgba(200,155,60,0.18);
    font-size: 0.78rem;
  }
  .equip .eq-slot { color: var(--ink-faint); text-transform: uppercase; font-size: 0.6rem; letter-spacing: 0.08em; align-self: center; }
  .equip .eq-item { color: var(--ink); text-align: right; }
  .equip li.empty .eq-item { color: var(--ink-faint); font-style: italic; }

  .ability-row { display: flex; flex-wrap: wrap; gap: 0.35rem; }
  .ability {
    display: inline-flex; align-items: center; gap: 0.3rem;
    font-size: 0.74rem; color: var(--ink);
    background: rgba(0,0,0,0.3);
    border: 1px solid rgba(200,155,60,0.22);
    border-radius: 1px; padding: 0.22rem 0.5rem;
  }
  .ability .glyph { color: var(--gold); font-size: 0.7rem; }
  .ability.locked { color: var(--ink-faint); font-style: italic; }
  .ability.locked .glyph { color: var(--ink-faint); }

  .branch-call {
    margin-top: 0.9rem;
    padding: 0.7rem 0.8rem;
    background: linear-gradient(160deg, rgba(92,143,176,0.14), rgba(92,143,176,0.04));
    border: 1px solid rgba(92,143,176,0.5);
    border-radius: 2px;
    box-shadow: inset 0 0 20px rgba(92,143,176,0.12);
    animation: pulse 2.4s ease-in-out infinite;
  }
  .bc-head { margin: 0 0 0.5rem; font-size: 0.85rem; color: var(--ink); letter-spacing: 0.03em; }
  .bc-mark { color: var(--arcane); text-shadow: 0 0 8px var(--arcane); margin-right: 0.3rem; }
  .bc-options { display: flex; flex-wrap: wrap; gap: 0.4rem; }
  .bc-btn {
    font: inherit; font-size: 0.8rem; letter-spacing: 0.04em;
    color: var(--ink); background: rgba(0,0,0,0.35);
    border: 1px solid var(--arcane); border-radius: 1px;
    padding: 0.35rem 0.8rem; cursor: pointer; transition: all 0.2s;
  }
  .bc-btn:hover { background: rgba(92,143,176,0.25); box-shadow: 0 0 10px rgba(92,143,176,0.5); }
  .bc-warn { margin: 0.4rem 0 0; font-size: 0.7rem; color: var(--ochre); }

  @keyframes rise { from { opacity: 0; transform: translateX(-8px); } to { opacity: 1; transform: none; } }
  @keyframes fade { from { opacity: 0; transform: translateY(6px); } to { opacity: 1; transform: none; } }
  @keyframes pulse {
    0%, 100% { box-shadow: inset 0 0 20px rgba(92,143,176,0.12); }
    50% { box-shadow: inset 0 0 28px rgba(92,143,176,0.28); }
  }

  @media (prefers-reduced-motion: reduce) {
    .rail-row, .record, .branch-call { animation: none; }
  }

  @media (max-width: 640px) {
    .body { grid-template-columns: 1fr; }
    .columns, .vitals { grid-template-columns: 1fr; }
    .stats { grid-template-columns: repeat(5, 1fr); }
  }
</style>
