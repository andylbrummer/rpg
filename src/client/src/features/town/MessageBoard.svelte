<script lang="ts">
  import type { MissionOffer, TownRumor } from '$shared/types/game';

  interface Props {
    missions: MissionOffer[];
    rumors: TownRumor[];
    /** Mission ids already read, for the dog-eared/dimmed treatment. */
    viewedMissions?: string[];
    /** Current party level, to mark contracts above the party's weight. */
    partyLevel?: number;
    onAcceptMission: (id: string) => void;
    onVerifyRumor: (id: string) => void;
    resolveItemName?: (id: string) => string;
    resolveFactionName?: (id: string) => string;
  }

  let {
    missions,
    rumors,
    viewedMissions = [],
    partyLevel,
    onAcceptMission,
    onVerifyRumor,
    resolveItemName,
    resolveFactionName,
  }: Props = $props();

  const prettify = (id: string | null | undefined) =>
    !id ? '' : id.replace(/[_-]+/g, ' ').replace(/\b\w/g, (c) => c.toUpperCase());
  const itemName = (id: string) => resolveItemName?.(id) ?? prettify(id);

  const FACTION: Record<string, { name: string; tint: string }> = {
    bureau: { name: 'Bureau of Residual Affairs', tint: '#7fa0a8' },
    convocation: { name: 'The Convocation', tint: '#9a4a6b' },
    inkblood: { name: 'Ossuary Compact', tint: '#b04a3a' },
    stillness: { name: 'The Stillness', tint: '#8a7fa8' },
    cartography: { name: "Cartographers' Guild", tint: '#a89a5b' },
  };
  const factionName = (id: string) => resolveFactionName?.(id) ?? FACTION[id]?.name ?? prettify(id);
  const factionTint = (id: string) => FACTION[id]?.tint ?? '#9a7a34';

  const seen = (id: string) => viewedMissions.includes(id);
  const tooStrong = (m: MissionOffer) => partyLevel !== undefined && m.minLevel > partyLevel;

  // Deterministic slight tilt per notice so the board reads as physically pinned, not a CSS grid.
  const tilt = (id: string) => {
    let h = 0;
    for (const c of id) h = (h * 31 + c.charCodeAt(0)) | 0;
    return ((h % 5) - 2) * 0.5; // -1deg .. +1deg
  };

  type RumorTone = 'confirmed' | 'debunked' | 'unverified';
  const tone = (r: TownRumor): RumorTone =>
    !r.verified ? 'unverified' : r.verificationResult === true ? 'confirmed' : 'debunked';
</script>

<section class="board" aria-label="The notice board">
  <header class="masthead">
    <span class="rule" aria-hidden="true"></span>
    <h2>Notice Board</h2>
    <span class="seal" aria-hidden="true">✜</span>
    <span class="rule" aria-hidden="true"></span>
  </header>

  <div class="board-grid">
    <!-- CONTRACTS -->
    <div class="column contracts">
      <h3 class="col-title">Contracts</h3>
      {#if missions.length === 0}
        <p class="empty">No contracts posted. The board hangs bare.</p>
      {:else}
        <div class="notices">
          {#each missions as m, i (m.id)}
            <article
              class="notice"
              class:seen={seen(m.id)}
              class:locked={tooStrong(m)}
              style={`--tint:${factionTint(m.factionId)}; --rot:${tilt(m.id)}deg; --i:${i}`}
            >
              <span class="pin" aria-hidden="true"></span>
              <div class="notice-head">
                <h4>{m.title}</h4>
                <span class="wax" style={`--tint:${factionTint(m.factionId)}`} title={factionName(m.factionId)} aria-hidden="true"></span>
              </div>
              <p class="faction" style={`color:${factionTint(m.factionId)}`}>{factionName(m.factionId)}</p>
              <p class="desc">{m.description}</p>
              <div class="rewards">
                {#each m.rewards as rw}
                  <span class="reward">{itemName(rw)}</span>
                {/each}
                {#if m.repReward}
                  <span class="reward rep">+{m.repReward} standing</span>
                {/if}
              </div>
              <div class="notice-foot">
                <span class="req" class:short={tooStrong(m)}>Requires Lv.{m.minLevel}</span>
                <button class="accept" disabled={tooStrong(m)} onclick={() => onAcceptMission(m.id)}>
                  {tooStrong(m) ? 'Out of depth' : seen(m.id) ? 'Take again' : 'Take contract'}
                </button>
              </div>
            </article>
          {/each}
        </div>
      {/if}
    </div>

    <!-- WHISPERS -->
    <div class="column whispers">
      <h3 class="col-title">Whispers</h3>
      {#if rumors.length === 0}
        <p class="empty">The room is quiet. Nothing worth repeating.</p>
      {:else}
        <ul class="rumor-list">
          {#each rumors as r, i (r.id)}
            {@const t = tone(r)}
            <li class="rumor" data-tone={t} style={`--i:${i}`}>
              <p class="rumor-text">&ldquo;{r.text}&rdquo;</p>
              <div class="rumor-foot">
                {#if t === 'unverified'}
                  <span class="stamp unverified">unverified</span>
                  <button class="verify" onclick={() => onVerifyRumor(r.id)}>Run it down</button>
                {:else if t === 'confirmed'}
                  <span class="stamp confirmed">✓ confirmed</span>
                {:else}
                  <span class="stamp debunked">✗ debunked</span>
                {/if}
                {#if r.relatedFactionId}
                  <span class="rumor-faction" style={`color:${factionTint(r.relatedFactionId)}`}>— {factionName(r.relatedFactionId)}</span>
                {/if}
              </div>
            </li>
          {/each}
        </ul>
      {/if}
    </div>
  </div>
</section>

<style>
  .board {
    --ink: #e8dcc0; --ink-dim: #a9977a; --ink-faint: #7d6e55;
    --umber: #1c1611; --umber-2: #241c14; --panel: #2b2117; --panel-2: #322619;
    --gold: #c89b3c; --gold-soft: #9a7a34; --ember: #d4622a;
    --paper: #d9c79c; --paper-ink: #2a2014; /* notices read as light parchment on dark cork */
    --moss: #8a9a4f; --blood: #a8352a; --arcane: #5c8fb0;
    --font-display: 'Iowan Old Style', 'Palatino Linotype', 'Book Antiqua', Georgia, 'Times New Roman', serif;

    color: var(--ink);
    font-family: var(--font-display);
    /* dark cork/timber backing */
    background:
      radial-gradient(60% 40% at 20% 0%, rgba(200,155,60,0.05), transparent 60%),
      repeating-linear-gradient(90deg, rgba(0,0,0,0.06) 0 3px, transparent 3px 7px),
      linear-gradient(160deg, #251a12, #1a120c);
    border: 1px solid #000;
    box-shadow: 0 0 0 1px var(--gold-soft) inset, 0 0 0 3px #1a120c inset,
      0 0 0 4px rgba(200,155,60,0.2) inset, 0 24px 60px rgba(0,0,0,0.6);
    border-radius: 3px; padding: 1rem 1.1rem 1.2rem; width: 100%; max-width: 920px; position: relative;
  }
  .board::before {
    content: ''; position: absolute; inset: 0; pointer-events: none; opacity: 0.45; mix-blend-mode: overlay;
    background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='120' height='120'%3E%3Cfilter id='n'%3E%3CfeTurbulence type='fractalNoise' baseFrequency='0.85' numOctaves='2' stitchTiles='stitch'/%3E%3C/filter%3E%3Crect width='100%25' height='100%25' filter='url(%23n)' opacity='0.5'/%3E%3C/svg%3E");
  }

  .masthead { display: flex; align-items: center; gap: 0.75rem; margin-bottom: 1rem; }
  .masthead h2 { margin: 0; font-size: 1.5rem; letter-spacing: 0.3em; text-transform: uppercase; font-variant: small-caps; text-shadow: 0 1px 0 #000; white-space: nowrap; }
  .masthead .seal { color: var(--gold); }
  .masthead .rule { flex: 1; height: 2px; background: linear-gradient(90deg, transparent, var(--gold-soft), transparent); box-shadow: 0 1px 0 rgba(0,0,0,0.6); }

  .board-grid { display: grid; grid-template-columns: 1.4fr 1fr; gap: 1.1rem; position: relative; z-index: 1; }

  .col-title {
    margin: 0 0 0.7rem; font-size: 0.66rem; text-transform: uppercase; letter-spacing: 0.2em;
    color: var(--gold-soft); border-bottom: 1px dashed rgba(200,155,60,0.25); padding-bottom: 0.3rem;
  }
  .empty { color: var(--ink-faint); font-style: italic; font-size: 0.85rem; }

  /* — contracts: pinned parchment notices — */
  .notices { display: flex; flex-direction: column; gap: 0.9rem; }
  .notice {
    position: relative;
    background: linear-gradient(170deg, #e2d2a6, var(--paper));
    color: var(--paper-ink);
    border-radius: 1px;
    padding: 0.85rem 0.9rem 0.7rem;
    transform: rotate(var(--rot));
    box-shadow: 0 6px 14px rgba(0,0,0,0.5), inset 0 0 30px rgba(150,110,50,0.18);
    animation: pin 0.5s both; animation-delay: calc(var(--i) * 70ms);
  }
  /* torn/aged edges */
  .notice::after {
    content: ''; position: absolute; inset: 0; pointer-events: none; opacity: 0.35; mix-blend-mode: multiply;
    background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='90' height='90'%3E%3Cfilter id='m'%3E%3CfeTurbulence type='fractalNoise' baseFrequency='0.7' numOctaves='2'/%3E%3C/filter%3E%3Crect width='100%25' height='100%25' filter='url(%23m)' opacity='0.4'/%3E%3C/svg%3E");
  }
  .notice.seen { filter: saturate(0.8) brightness(0.94); }
  .notice.locked { opacity: 0.82; }
  .pin {
    position: absolute; top: -7px; left: 50%; transform: translateX(-50%);
    width: 13px; height: 13px; border-radius: 50%;
    background: radial-gradient(circle at 35% 30%, #e7b24a, #7a4a12 70%);
    box-shadow: 0 2px 4px rgba(0,0,0,0.6), inset 0 -2px 3px rgba(0,0,0,0.4);
    z-index: 2;
  }
  .notice-head { display: flex; align-items: flex-start; justify-content: space-between; gap: 0.5rem; }
  .notice h4 { margin: 0; font-size: 1.05rem; font-variant: small-caps; letter-spacing: 0.02em; color: #241a0e; }
  .wax {
    width: 20px; height: 20px; flex-shrink: 0; border-radius: 50%;
    background: radial-gradient(circle at 35% 30%, color-mix(in srgb, var(--tint) 75%, #fff 5%), color-mix(in srgb, var(--tint) 60%, #000));
    box-shadow: 0 1px 3px rgba(0,0,0,0.5), inset 0 0 4px rgba(0,0,0,0.4);
    border: 1px solid rgba(0,0,0,0.3);
  }
  .faction { margin: 0.05rem 0 0.45rem; font-size: 0.68rem; text-transform: uppercase; letter-spacing: 0.08em; font-weight: 700; }
  .desc { margin: 0 0 0.55rem; font-size: 0.82rem; line-height: 1.4; color: #3a2c19; }
  .rewards { display: flex; flex-wrap: wrap; gap: 0.3rem; margin-bottom: 0.6rem; }
  .reward {
    font-size: 0.68rem; color: #2a2014; background: rgba(120,90,40,0.18);
    border: 1px solid rgba(90,65,25,0.4); border-radius: 1px; padding: 0.12rem 0.4rem;
  }
  .reward.rep { color: #5a3a0c; border-color: rgba(150,110,40,0.6); }
  .notice-foot { display: flex; align-items: center; justify-content: space-between; }
  .req { font-size: 0.66rem; text-transform: uppercase; letter-spacing: 0.08em; color: #5a4528; }
  .req.short { color: var(--blood); font-weight: 700; }
  .accept {
    font: inherit; font-size: 0.76rem; font-variant: small-caps; letter-spacing: 0.04em;
    color: #f3e6c6; background: #3a2412;
    border: 1px solid #1d1208; border-radius: 1px; padding: 0.28rem 0.7rem; cursor: pointer;
    box-shadow: 0 1px 0 rgba(255,255,255,0.1) inset; transition: all 0.2s;
  }
  .accept:hover:not(:disabled) { background: #54331a; box-shadow: 0 0 10px rgba(212,98,42,0.5); }
  .accept:disabled { opacity: 0.5; cursor: not-allowed; }

  /* — whispers: chalk/ink scrawl on dark — */
  .rumor-list { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 0.6rem; }
  .rumor {
    padding: 0.55rem 0.65rem; border-left: 2px solid var(--gold-soft);
    background: linear-gradient(90deg, rgba(0,0,0,0.3), rgba(0,0,0,0.1));
    border-radius: 0 2px 2px 0; animation: fade 0.45s both; animation-delay: calc(var(--i) * 60ms);
  }
  .rumor[data-tone='confirmed'] { border-left-color: var(--moss); }
  .rumor[data-tone='debunked'] { border-left-color: var(--blood); }
  .rumor[data-tone='debunked'] .rumor-text { text-decoration: line-through; text-decoration-color: rgba(168,53,42,0.7); color: var(--ink-faint); }
  .rumor-text { margin: 0 0 0.4rem; font-size: 0.84rem; line-height: 1.35; color: var(--ink); font-style: italic; }
  .rumor-foot { display: flex; align-items: center; gap: 0.5rem; flex-wrap: wrap; }
  .stamp { font-size: 0.6rem; text-transform: uppercase; letter-spacing: 0.1em; padding: 0.12rem 0.4rem; border-radius: 1px; }
  .stamp.unverified { color: var(--ink-faint); border: 1px dashed rgba(125,110,85,0.6); }
  .stamp.confirmed { color: var(--moss); border: 1px solid rgba(138,154,79,0.6); }
  .stamp.debunked { color: var(--blood); border: 1px solid rgba(168,53,42,0.6); }
  .rumor-faction { font-size: 0.66rem; letter-spacing: 0.03em; }
  .verify {
    font: inherit; font-size: 0.72rem; font-variant: small-caps; letter-spacing: 0.04em;
    color: var(--ink); background: rgba(0,0,0,0.3); border: 1px solid var(--ember);
    border-radius: 1px; padding: 0.22rem 0.55rem; cursor: pointer; transition: all 0.2s;
  }
  .verify:hover { background: rgba(212,98,42,0.22); box-shadow: 0 0 8px rgba(212,98,42,0.45); }

  @keyframes pin { from { opacity: 0; transform: rotate(var(--rot)) translateY(-10px); } to { opacity: 1; transform: rotate(var(--rot)) translateY(0); } }
  @keyframes fade { from { opacity: 0; transform: translateX(-6px); } to { opacity: 1; transform: none; } }
  @media (prefers-reduced-motion: reduce) { .notice, .rumor { animation: none; } }

  @media (max-width: 640px) {
    .board-grid { grid-template-columns: 1fr; }
  }
</style>
