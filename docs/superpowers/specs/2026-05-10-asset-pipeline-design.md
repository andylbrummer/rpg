# Asset Pipeline — Design Spec
Date: 2026-05-10
Status: design — Phase 2 deliverable; Phase 1 uses procedural textures + inline SVG only
Depends on: design-system spec (icon sprite + portrait references), audio spec (audio asset budget), content-pipeline spec (RPK reader)
Scope: textures, geometry, icons, portraits, fonts. Size budgets, formats, build steps, lazy load, cache invalidation.

## 1. Asset categories

| Category | Format | Budget per asset | Total Phase 1 | Total Phase 2 |
|---|---|---|---|---|
| Wall / floor textures | KTX2 (BasisU compressed) | 100 KB | 0 (procedural) | 2 MB |
| Tile geometry | glTF 2.0 binary (`.glb`) | 50 KB | 0 (cubes) | 500 KB |
| Item icons | SVG sprite single file | — | 60 KB | 200 KB |
| Status icons | SVG sprite (shared with item icons) | — | (above) | (above) |
| Portrait illustrations | WebP (or AVIF if mature) | 80 KB | placeholders 0 | 4 MB (50 portraits) |
| Enemy sprites / models | glTF / sprite atlas | 60 KB | 0 (cubes) | 3 MB |
| Audio | OGG Vorbis q4 | varies (see audio spec) | 4 MB | 15 MB |
| Fonts | WOFF2 subsets | 50 KB | 200 KB (2 families) | 200 KB |
| Cursor / decorative | PNG | 10 KB | 50 KB | 100 KB |

Phase 2 total ≈ 25 MB. Acceptable for desktop Photino app. Phase 1 currently <300 KB.

## 2. Source vs build

```
assets/                        # source files, NOT shipped
  src/
    textures/wall_brick.png    # 2048×2048 source
    geometry/door_arch.blend
    icons/sword.svg
    portraits/kael.png         # 1024×1024 source
    audio/raw/...
  build.config.kdl             # per-asset pipeline overrides

build/assets/                  # generated, shipped via RPK or static
  textures/wall_brick.ktx2     # 256×256 compressed
  geometry/door_arch.glb
  icons.svg                    # combined sprite
  portraits/kael.webp          # 512×512 webp
```

`assets/src/` is the workspace; `build/assets/` is generated. Source committed; build artifacts gitignored.

## 3. Build steps

`tools/asset-pipeline/`:

```
asset-pipeline/
  Cli.cs                     // orchestrates all steps
  steps/
    Textures.cs              // PNG → KTX2 via toktx
    Geometry.cs              // Blender export via headless blender CLI
    Icons.cs                 // SVG → single sprite via svgo + concat
    Portraits.cs             // PNG → WebP via cwebp
    Audio.cs                 // WAV → OGG via oggenc (referenced from audio spec)
    Fonts.cs                 // OTF → WOFF2 subset via fonttools pyftsubset
```

Each step:
- Checks source mtime vs build mtime; skips if up to date.
- Outputs to `build/assets/<category>/<name>.<ext>`.
- Records SHA-256 in `build/assets/manifest.json`.

Build command: `dotnet run --project tools/asset-pipeline -- build`. Force rebuild: `... -- build --force`.

External tool dependencies:
- `toktx` (KTX-Software) — texture compression
- `blender` (headless mode) — geometry export
- `svgo` — SVG optimization
- `cwebp` — WebP encoding
- `oggenc` — Vorbis encoding
- `pyftsubset` (fonttools) — font subsetting

CI installs these. Dev machines install via README script.

## 4. Format choices — rationale

- **KTX2 + BasisU** for textures: GPU-decompressed, fits in VRAM at ~25% of raw size, supported by Three.js via `KTX2Loader`.
- **glTF 2.0 binary**: PBR-ready, single-file, Three.js native via `GLTFLoader`.
- **SVG sprite**: vector icons scale with UI scale setting; single file = single HTTP request.
- **WebP** for portraits: better compression than PNG, supported in all modern webviews; AVIF deferred until Photino's Chromium baseline guarantees support.
- **OGG Vorbis** for audio: broad webview support, royalty-free, smaller than MP3 at perceptual quality.
- **WOFF2 with subsetting**: shipping only used codepoints cuts font size 70%.

## 5. Shipping

Phase 1: assets live next to client bundle at `src/client/public/assets/`; served by Vite + Photino static file handler.

Phase 2: assets packed into the RPK alongside content. Same hash-based cache invalidation as content. Client requests via REST endpoint `/assets/{path}` which serves from RPK manifest.

Mix mode: small/critical (icons, fonts) inline in client bundle; large/lazy (portraits, textures, audio) served on demand.

## 6. Lazy loading

Three.js side:

- Dungeon-template textures loaded when first encountered. Pre-fetched on dungeon enter via `fetch()` priority `low`.
- Combat models / sprites loaded on combat enter.
- Portraits loaded on character first appearance (party = preload at session start; enemies = on encounter).

Pattern:

```ts
const loader = new AssetLoader();
await loader.preload(['textures/wall_brick.ktx2', 'geometry/door_arch.glb']);
const tex = loader.get('textures/wall_brick.ktx2');
```

`AssetLoader`:
- LRU cache, 100 MB cap.
- Dedup concurrent requests.
- Reports progress events to a Svelte store → loading indicator UI.

## 7. Cache invalidation

Manifest hash present in URL: `/assets/textures/wall_brick.ktx2?v=abc123`. When asset changes, hash changes, URL changes, browser cache misses, fresh download.

IndexedDB cache mirrors REST responses by URL. Eviction at 200 MB or oldest unused.

Phase 1 doesn't ship binary assets so cache is small. Phase 2 design matters.

## 8. Icons — sprite spec

Single file `build/assets/icons.svg`:

```xml
<svg xmlns="http://www.w3.org/2000/svg" style="display:none">
  <symbol id="sword" viewBox="0 0 24 24">
    <path d="..." stroke="currentColor" stroke-width="1.5" fill="none"/>
  </symbol>
  <symbol id="shield" viewBox="0 0 24 24">...</symbol>
  ...
</svg>
```

Usage in Svelte:

```svelte
<svg class="icon"><use href="/assets/icons.svg#sword"/></svg>
```

Source: individual SVG per icon under `assets/src/icons/`. Build step concatenates with `<symbol>` wrappers and id = filename stem. SVGO optimizes each before concat.

Phase 1 icon set (40):
- Combat actions: attack, defend, cast, item, flee, wait, stabilize
- Status effects: bleed, burn, poison, stun, mark, shield, regen, slow
- Classes: bonewarden, stillblade, cauterist, hollow (+ 4 more Phase 2)
- Resources: bone, ink, charge, cautery, bloom, tithe, gold, xp
- Items: weapon, armor, consumable, material, quest, currency
- Navigation: arrow-up/down/left/right, compass, map
- System: save, settings, close, expand, collapse, search, filter, sort

## 9. Geometry — low-poly conventions

- ≤200 triangles per dungeon tile element.
- No textures > 256×256 on tile assets (KTX2 compresses to ~30 KB).
- Single-sided faces where back faces never visible.
- Y-up coordinate system (Three.js default).
- Unit: 1 unit = 1 tile = 2 m in-fiction (matches `tileSize=2` in `DungeonRenderer.ts:9`).

Source files in `.blend`; build step uses headless blender:

```
blender -b src.blend --python tools/asset-pipeline/blender-export.py -- src.blend out.glb
```

## 10. Fonts

Two families:
- Display (Cinzel) — headings, screen titles
- Body (EB Garamond) — lore, descriptions
- UI (Inter) — labels, numbers (variable font)
- Mono (system fallback) — damage numbers, coords

Subsetting: Latin-1 + extended Latin + punctuation only Phase 1. Phase 2 add Cyrillic / Greek when localization spec lands.

Total Phase 1: 4 WOFF2 files, ~50 KB each = 200 KB.

License: all chosen fonts have permissive open licenses (SIL OFL or similar). Documented in `assets/LICENSES.md`.

## 11. Tests

- xUnit (tools): pipeline produces deterministic bytes given same input.
- xUnit: manifest contains every output; no orphans.
- xUnit: asset reference in content must resolve via manifest (e.g., portrait `kael.webp` referenced in npc def must exist in build).
- Visual regression (Playwright): UI snapshots at 768/1024/1280; failures gated until human reviews.

## 12. CI

`.github/workflows/assets.yml`:
- Runs on changes under `assets/src/**` or `tools/asset-pipeline/**`.
- Installs external tools (cached layer).
- Builds, validates, uploads `build/assets/` as artifact.
- Surfaces size delta vs base branch in PR comment.

## 13. Manual asset workflow

Designer drops new source asset → adds entry in `build.config.kdl` if non-default settings needed → runs `dotnet run --project tools/asset-pipeline -- build` → commits both source + build (?? no, build is gitignored — commit source only).

CI rebuilds on merge to main. Release pipeline (separate spec) packages final RPK including build artifacts.

## 14. Phase rollout

- Phase 1: icons SVG sprite + fonts subset. No 3D models, no textures (procedural), no portraits.
- Phase 1.5: portraits (placeholder set), audio assets, sprite atlas for combat.
- Phase 2: full dungeon textures + glTF geometry per template.
- Phase 3: per-dungeon ambient sets, weather variations, bloom visual effects.

## 15. Out of scope

- Asset DRM / encryption.
- Texture streaming (full asset load is fine at projected sizes).
- Dynamic asset generation (every asset is authored).
- Asset marketplace.
