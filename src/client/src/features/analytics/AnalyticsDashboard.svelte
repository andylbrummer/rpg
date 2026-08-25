<script lang="ts">
  import type { AnalyticsData } from '$shared/types/game';
  import { ALL_SYNERGIES } from '$shared/data/synergies';

  interface Props {
    data: AnalyticsData;
  }

  let { data }: Props = $props();

  // ---- Campaign outcomes -------------------------------------------------
  const completionRate = $derived(
    data.campaignsStarted > 0
      ? Math.round((data.campaignsCompleted / data.campaignsStarted) * 100)
      : 0
  );
  const avgTurns = $derived(
    data.campaignsCompleted > 0
      ? Math.round(data.totalTurns / data.campaignsCompleted)
      : 0
  );
  const avgDeaths = $derived(
    data.campaignsCompleted > 0
      ? (data.totalDeaths / data.campaignsCompleted).toFixed(1)
      : '0.0'
  );

  // Outcome breakdown as proportions of completed campaigns, for the bars.
  // pct is clamped to 100 — a single campaign can record multiple outcome flags,
  // so a raw count could otherwise exceed campaignsCompleted.
  const outcomeBars = $derived.by(() => {
    const total = Math.max(data.campaignsCompleted, 1);
    const pct = (n: number) => Math.min(100, Math.round((n / total) * 100));
    return [
      { label: 'Masterminds Exposed', value: data.mastermindsExposed, pct: pct(data.mastermindsExposed), color: '#7ec8e3' },
      { label: 'Schemes Stopped', value: data.schemesStopped, pct: pct(data.schemesStopped), color: '#8fd694' },
      { label: 'Betrayals', value: data.betrayals, pct: pct(data.betrayals), color: '#e3927e' },
    ];
  });

  // ---- Synergy discovery rate -------------------------------------------
  const totalSynergies = ALL_SYNERGIES.length;
  const discoveredCount = $derived(data.synergiesDiscovered.length);
  const synergyRate = $derived(
    totalSynergies > 0 ? Math.round((discoveredCount / totalSynergies) * 100) : 0
  );

  // ---- Branch pick distribution -----------------------------------------
  // Stored as "classId:branch:level". Aggregate counts by classId:branch.
  const branchDistribution = $derived.by(() => {
    const counts = new Map<string, number>();
    for (const entry of data.branchesChosen) {
      const parts = entry.split(':');
      if (parts.length < 2) continue;
      const key = `${parts[0]}:${parts[1]}`;
      counts.set(key, (counts.get(key) ?? 0) + 1);
    }
    const rows = Array.from(counts.entries())
      .map(([key, count]) => {
        const [classId, branch] = key.split(':');
        return { classId, branch, count };
      })
      .sort((a, b) => b.count - a.count);
    const max = rows.reduce((m, r) => Math.max(m, r.count), 1);
    return rows.map((r) => ({ ...r, pct: Math.round((r.count / max) * 100) }));
  });

  // ---- Faction heatmap ---------------------------------------------------
  const FACTION_NAMES: Record<string, string> = {
    bureau: 'Bureau of Residual Affairs',
    cartography: "Reach Cartographers' Guild",
    convocation: 'The Convocation',
    inkblood: 'Ossuary Compact',
    stillness: 'The Stillness',
  };

  // Reputation roughly spans -100..100; bucket into a 5-tier heat color.
  function repColor(rep: number): string {
    if (rep <= -50) return '#7a2b24'; // hostile
    if (rep < 0) return '#a85b3e'; // wary
    if (rep === 0) return '#5a5a5a'; // neutral
    if (rep < 50) return '#4f7a4a'; // friendly
    return '#3a9d5d'; // allied
  }
  function repTier(rep: number): string {
    if (rep <= -50) return 'Hostile';
    if (rep < 0) return 'Wary';
    if (rep === 0) return 'Neutral';
    if (rep < 50) return 'Friendly';
    return 'Allied';
  }

  const factionCells = $derived.by(() => {
    const states = data.factionEndStates ?? {};
    const ids = Object.keys(FACTION_NAMES);
    return ids.map((id) => {
      const rep = states[id];
      const known = rep !== undefined;
      return {
        id,
        name: FACTION_NAMES[id],
        rep: rep ?? 0,
        known,
        color: known ? repColor(rep) : '#2a2a2a',
        tier: known ? repTier(rep) : 'No data',
      };
    });
  });
</script>

<div class="dashboard">
  <!-- Campaign outcomes -->
  <section class="panel" aria-label="Campaign outcomes">
    <h3 class="panel-title">Campaign Outcomes</h3>
    <div class="kpi-row">
      <div class="kpi"><span class="kpi-value">{data.campaignsStarted}</span><span class="kpi-label">Started</span></div>
      <div class="kpi"><span class="kpi-value">{data.campaignsCompleted}</span><span class="kpi-label">Completed</span></div>
      <div class="kpi"><span class="kpi-value">{completionRate}%</span><span class="kpi-label">Completion</span></div>
      <div class="kpi"><span class="kpi-value">{avgTurns}</span><span class="kpi-label">Avg Turns</span></div>
      <div class="kpi"><span class="kpi-value">{avgDeaths}</span><span class="kpi-label">Avg Deaths</span></div>
    </div>
    <div class="bars">
      {#each outcomeBars as bar}
        <div class="bar-row">
          <span class="bar-label">{bar.label}</span>
          <div class="bar-track">
            <div class="bar-fill" style="width: {bar.pct}%; background: {bar.color};"></div>
          </div>
          <span class="bar-value">{bar.value}</span>
        </div>
      {/each}
    </div>
  </section>

  <!-- Synergy discovery rate -->
  <section class="panel" aria-label="Synergy discovery rate">
    <h3 class="panel-title">Synergy Discovery</h3>
    <div class="rate-headline">
      <span class="rate-pct">{synergyRate}%</span>
      <span class="rate-detail">{discoveredCount} / {totalSynergies} synergies discovered</span>
    </div>
    <div class="bar-track tall">
      <div class="bar-fill" style="width: {synergyRate}%; background: linear-gradient(90deg, #6a5acd, #7ec8e3);"></div>
    </div>
  </section>

  <!-- Branch pick distribution -->
  <section class="panel" aria-label="Branch pick distribution">
    <h3 class="panel-title">Branch Picks</h3>
    {#if branchDistribution.length > 0}
      <div class="bars">
        {#each branchDistribution as row}
          <div class="bar-row">
            <span class="bar-label">{row.classId} · {row.branch}</span>
            <div class="bar-track">
              <div class="bar-fill" style="width: {row.pct}%; background: #c8a24a;"></div>
            </div>
            <span class="bar-value">{row.count}</span>
          </div>
        {/each}
      </div>
    {:else}
      <p class="empty">No branch choices recorded yet.</p>
    {/if}
  </section>

  <!-- Faction heatmap -->
  <section class="panel" aria-label="Faction standings heatmap">
    <h3 class="panel-title">Faction Standings</h3>
    <div class="heatmap">
      {#each factionCells as cell}
        <div class="heat-cell" style="background: {cell.color};" title="{cell.name}: {cell.tier}">
          <span class="heat-name">{cell.name}</span>
          <span class="heat-tier">{cell.tier}</span>
          {#if cell.known}
            <span class="heat-rep">{cell.rep > 0 ? '+' : ''}{cell.rep}</span>
          {/if}
        </div>
      {/each}
    </div>
  </section>
</div>

<style>
  .dashboard {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(260px, 1fr));
    gap: 1rem;
    width: 100%;
  }
  .panel {
    background: rgba(20, 22, 28, 0.6);
    border: 1px solid rgba(255, 255, 255, 0.08);
    border-radius: 8px;
    padding: 1rem;
  }
  .panel-title {
    margin: 0 0 0.75rem;
    font-size: 0.95rem;
    letter-spacing: 0.04em;
    text-transform: uppercase;
    color: #b8c0cc;
  }
  .kpi-row {
    display: flex;
    flex-wrap: wrap;
    gap: 0.75rem;
    margin-bottom: 0.9rem;
  }
  .kpi {
    display: flex;
    flex-direction: column;
    min-width: 60px;
  }
  .kpi-value {
    font-size: 1.4rem;
    font-weight: 700;
    color: #f0f2f5;
  }
  .kpi-label {
    font-size: 0.7rem;
    color: #8b93a0;
    text-transform: uppercase;
    letter-spacing: 0.03em;
  }
  .bars {
    display: flex;
    flex-direction: column;
    gap: 0.5rem;
  }
  .bar-row {
    display: grid;
    grid-template-columns: 1fr 2fr auto;
    align-items: center;
    gap: 0.5rem;
  }
  .bar-label {
    font-size: 0.78rem;
    color: #c4ccd6;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }
  .bar-track {
    background: rgba(255, 255, 255, 0.06);
    border-radius: 4px;
    height: 10px;
    overflow: hidden;
  }
  .bar-track.tall {
    height: 16px;
  }
  .bar-fill {
    height: 100%;
    border-radius: 4px;
    transition: width 0.3s ease;
  }
  .bar-value {
    font-size: 0.8rem;
    color: #e0e4ea;
    min-width: 1.5rem;
    text-align: right;
  }
  .rate-headline {
    display: flex;
    align-items: baseline;
    gap: 0.6rem;
    margin-bottom: 0.6rem;
  }
  .rate-pct {
    font-size: 2rem;
    font-weight: 700;
    color: #7ec8e3;
  }
  .rate-detail {
    font-size: 0.8rem;
    color: #8b93a0;
  }
  .heatmap {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(90px, 1fr));
    gap: 0.5rem;
  }
  .heat-cell {
    display: flex;
    flex-direction: column;
    gap: 0.2rem;
    padding: 0.5rem;
    border-radius: 6px;
    min-height: 64px;
    justify-content: center;
  }
  .heat-name {
    font-size: 0.7rem;
    color: rgba(255, 255, 255, 0.92);
    line-height: 1.1;
  }
  .heat-tier {
    font-size: 0.72rem;
    font-weight: 600;
    color: #fff;
  }
  .heat-rep {
    font-size: 0.7rem;
    color: rgba(255, 255, 255, 0.75);
  }
  .empty {
    font-size: 0.8rem;
    color: #8b93a0;
  }
</style>
