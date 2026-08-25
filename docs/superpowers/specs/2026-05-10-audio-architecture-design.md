# Audio Architecture — Design Spec
Date: 2026-05-10
Status: design — Phase 1.5 deliverable; Phase 1 leaves stub hooks
Depends on: `docs/design/09-mvp-phases.md` §Audio Direction; design-system spec `Fx.play()` stub
Scope: bus/mixer, event-driven sound, source pooling, atmospheric ducking, accessibility (captions), settings integration. Web Audio API in webview, Howler.js as wrapper.

## 1. Principles

- **Audio is information, not decoration.** Sound tells player what's coming (faction patrol audio before visual contact, bloom shift, Unaccounted's wrong-pitch absence).
- All audio routed through buses → master gain → output. No direct-to-destination plays.
- Server emits **logical audio events**; client maps to clips via authored cue table. Engine never names files.
- Phase 1 leaves stub call sites so Phase 1.5 audio engineer doesn't touch combat / movement / UI code.

## 2. Bus topology

```
            ┌─► music ────┐
            ├─► ambience ─┤
master ◄────┼─► sfx ──────┼─► output (AudioContext.destination)
            ├─► ui ───────┤
            └─► captions* ┘  (silent, drives caption stream)
```

Per-bus gain bound to `settings.audio.{master|music|sfx|ambience}`. UI bus shares `sfx` gain. Music auto-ducks during combat + dialog (–6dB) via per-bus envelope.

`muteOnFocusLoss`: window blur → master gain 0 ramped 200ms; focus → restore.

## 3. Cue table

Authored: `content/audio/cues.json` (also goes into RPK).

```json
{
  "ui.button.click":      { "files":["ui/click1.ogg","ui/click2.ogg"], "bus":"ui", "vol":0.5, "pickMode":"random" },
  "combat.hit.melee":     { "files":["sfx/hit_melee_01.ogg","sfx/hit_melee_02.ogg"], "bus":"sfx", "vol":0.8, "pitchRange":[0.95,1.05] },
  "combat.miss":          { "files":["sfx/miss_swish.ogg"], "bus":"sfx", "vol":0.6 },
  "combat.crit":          { "files":["sfx/crit_impact.ogg"], "bus":"sfx", "vol":1.0 },
  "combat.heal":          { "files":["sfx/heal_chime.ogg"], "bus":"sfx", "vol":0.7 },
  "combat.downed":        { "files":["sfx/downed_thud.ogg"], "bus":"sfx", "vol":0.9 },
  "combat.died":          { "files":["sfx/died_bell.ogg"], "bus":"sfx", "vol":1.0 },
  "combat.synergy":       { "files":["sfx/synergy_chord.ogg"], "bus":"sfx", "vol":1.0, "ducks":"music:6:600" },
  "exploration.step":     { "files":["sfx/step1.ogg","sfx/step2.ogg","sfx/step3.ogg"], "bus":"sfx", "vol":0.5, "pickMode":"cycle", "pitchRange":[0.95,1.08] },
  "exploration.turn":     { "files":["sfx/cloth_turn.ogg"], "bus":"sfx", "vol":0.4 },
  "exploration.pickup":   { "files":["sfx/pickup_pop.ogg"], "bus":"sfx", "vol":0.6 },
  "ambience.dungeon.broken_engine": { "files":["amb/engine_hum.ogg"], "bus":"ambience", "vol":0.6, "loop":true },
  "ambience.dungeon.sewers":        { "files":["amb/sewers_drip.ogg"], "bus":"ambience", "vol":0.5, "loop":true },
  "ambience.dungeon.crypt":         { "files":["amb/crypt_wind.ogg"], "bus":"ambience", "vol":0.5, "loop":true },
  "ambience.town":                  { "files":["amb/town_market.ogg"], "bus":"ambience", "vol":0.5, "loop":true },
  "music.town":           { "files":["mus/town_theme.ogg"], "bus":"music", "vol":0.7, "loop":true, "fadeIn":2000 },
  "music.combat":         { "files":["mus/combat_tense.ogg"], "bus":"music", "vol":0.7, "loop":true, "fadeIn":400 },
  "music.victory":        { "files":["mus/victory_sting.ogg"], "bus":"music", "vol":0.8 }
}
```

Cue keys are dotted namespaces — server emits these strings. Client never sees file paths.

### 3.1 Cue properties

| Prop | Meaning |
|---|---|
| `files[]` | available clips |
| `pickMode` | `random` (default), `cycle`, `first` |
| `bus` | one of `music\|ambience\|sfx\|ui` |
| `vol` | clip-relative 0..1 (multiplies bus + master) |
| `loop` | clip loops |
| `fadeIn` / `fadeOut` | ms |
| `pitchRange` | `[min,max]` randomize playback rate |
| `ducks` | `target:dB:durationMs` — temporarily ducks another bus |
| `priority` | int, default 0; higher cuts lower if voice cap reached |
| `cooldownMs` | suppress same cue within window |
| `caption` | string for caption track (also see §6) |

## 4. Source pool

`AudioContext` voice cap: 32 simultaneous sources. Allocation:
- Reserved 4 for music+ambience loops.
- Remaining 28 for sfx + ui.
- Voice manager LRU-evicts lowest-priority active voice when over cap.

Each playing voice = `{ source, gainNode, busNode, startedAt, priority, cueKey }`. Pool reuses `GainNode` instances; only `AudioBufferSourceNode` is per-play (single-use per Web Audio spec).

## 5. Event mapping

Server emits cue keys via `event:fx` payloads (see websocket-protocol spec §4.1). Examples:

| Server event | Cue key |
|---|---|
| `damage_number kind:physical` | `combat.hit.melee` (or `combat.hit.ranged` if origin band ≠ melee) |
| `damage_number crit:true` | `combat.crit` (in addition to hit) |
| `miss` | `combat.miss` |
| `heal` | `combat.heal` |
| `downed` | `combat.downed` |
| `died` | `combat.died` |
| `synergy_triggered` | `combat.synergy` |
| `tile_revealed` | `exploration.step` (suppressed if last step <120ms ago) |
| `encounter_incoming` | crossfade `music.combat` over `music.{current}` |
| `combat_end victory` | `music.victory` + restore prior music after sting |

Movement audio (step, turn) emitted client-side from input dispatcher (no need to round-trip server). All other audio server-driven.

## 6. Captions / accessibility

`settings.accessibility.captionCombatEvents` enables caption rendering. Each cue with a `caption` field, when played, emits a CaptionEvent to a Svelte store consumed by a `<CaptionLayer>` (atmos-layer z-band, fixed bottom):

```
[●] crit impact
[●] Sera misses
[●] Kael is downed
```

Last 3 captions visible, fade after 3s. Configurable size in accessibility settings.

`captions` bus exists conceptually (silent) so cues without audio file still get caption events. Used for status-applied events when sfx absent.

## 7. Client architecture

```
src/client/src/audio/
  AudioEngine.ts          // Howler wrapper, bus graph, voice pool
  CueTable.ts             // loads cues.json from RPK, exposes play(key, options?)
  CaptionStream.ts        // Svelte store
  index.ts                // exports `Audio` singleton + initialize()
```

API:

```ts
Audio.initialize();                    // call once on user-gesture (browser autoplay policy)
Audio.play(cueKey, { vol?, pitch?, pos? });
Audio.stop(cueKey | voiceId);
Audio.startMusic(cueKey);              // fades current → new
Audio.stopMusic();
Audio.startAmbience(cueKey);
Audio.duck(bus, dB, ms);
```

Audio bus initialized on first user gesture (button click, key press). Until then, calls are queued and flushed.

Wired to `FxBus`: an `FxBus → Audio` adapter maps server fx events → cue plays.

## 8. Server-side

Phase 1 keeps server agnostic. Server emits structured fx; cue mapping lives client-side. Server config exists only for music context switches (e.g., entering town → emit `music.context "town"`).

Phase 2 may add `audio_directive` event for special cases (Unaccounted reversed-audio cue triggered by AI).

## 9. Asset budget (Phase 1.5)

Initial cue files: 30 cues. Conservative size budget:
- UI: 6 cues × 30 KB = 180 KB
- Combat: 12 cues × 50 KB = 600 KB
- Exploration: 6 cues × 40 KB = 240 KB
- Ambience: 4 loops × 200 KB = 800 KB
- Music: 3 tracks × 800 KB = 2.4 MB

Total ~4.2 MB compressed (OGG Vorbis q=4). Phase 2 expansion estimated 12-15 MB.

Loading strategy: ambience + music streamed (Howler `html5:true` mode). SFX + UI preloaded on first user gesture into AudioBuffer pool.

## 10. Settings integration

Bound to `audio` node in settings-keybinds spec:
- `master`, `music`, `sfx`, `ambience` → live bus gain.
- `muteOnFocusLoss` → focus/blur handler.

Captions toggle in `accessibility.captionCombatEvents`.

## 11. Phase 1 stub

`src/client/src/audio/index.ts` initially:

```ts
export const Audio = {
  initialize: () => {},
  play: (_key: string) => {},
  startMusic: (_key: string) => {},
  stopMusic: () => {},
  startAmbience: (_key: string) => {},
  duck: () => {},
  stop: () => {},
};
```

Every UI button / FxBus → Audio mapping coded in Phase 1 against this no-op. Phase 1.5 replaces implementation; zero call site changes.

## 12. Tests

- Unit: cue picker (random, cycle, first); cooldown enforcement; voice eviction LRU; ducking envelope shape; pitch range bounds.
- Integration: focus-loss mutes within 200ms; settings change updates bus gain within 50ms; preload completes before first combat.
- Manual: audio engineer subjective pass — every cue distinct, no two cues confusable.

## 13. Out of scope

- 3D spatial audio (Phase 2 — design-system mentions Unaccounted wrong-direction sound, may need positional then).
- Procedural audio synthesis.
- Audio mods (Phase 3 modding spec).
- Voice acting / dialogue lines (Phase 3 if pursued).
