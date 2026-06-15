# Client source layout

The Svelte client is organized by gameplay feature rather than by technical
layer, so related UI, state adapters, and feature types live together and
cross-feature dependencies are easy to find.

## Folder taxonomy

- `app/` — application shell and entry point (`App.svelte`, `main.ts`). Wires
  features together; owns top-level lifecycle and routing between game modes.
- `features/<area>/` — one folder per gameplay feature. Each contains its
  Svelte components and a typed `*Adapter.ts` of pure `select*` functions that
  read from `GameState`. Current areas: `analytics`, `combat`, `exploration`,
  `field-notes`, `overworld`, `party`, `settings`, `title`, `town`.
- `shared/` — cross-cutting code with no feature ownership:
  - `net/` — protocol and network transport (`GameClient`, `testHarness`).
    Network code is kept independent of UI state and never imports stores.
  - `stores/` — UI/runtime state (`gameStore`).
  - `types/` — shared and generated protocol types (`game.ts`,
    `protocol.gen.ts`).
  - `data/` — derived content data loaded at build time (e.g. `synergies`).
- `renderer/` — the Three.js dungeon renderer and audio/input subsystems.
- `config/` — user preferences (keybindings, display, accessibility).
- `lib/`, `cache/`, `assets/` — small utilities, runtime caches, static assets.

## Import conventions

Use the path aliases (defined in `vite.config.ts` and `tsconfig.app.json`)
rather than long relative paths:

`$app`, `$features`, `$shared`, `$renderer`, `$config`.

### Feature barrels

Each feature that is consumed from outside its own folder exposes a small
public surface via `features/<area>/index.ts`. Import the public component or
adapter selectors from the barrel:

```ts
import { CombatOverlay } from '$features/combat';
import { selectPartyMembers } from '$features/party';
```

Barrels re-export only what is intentionally public. Components used only
within their own feature (e.g. `town/TavernHall.svelte`, `combat/CombatResultToast.svelte`)
are imported directly with a relative path and stay off the public surface.
This keeps cross-feature imports intentional and grep-able: a search for
`$features/<area>` shows every external consumer of a feature.
