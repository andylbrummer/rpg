# Content Pipeline & Hot Reload — Design Spec
Date: 2026-05-10
Status: design — Phase 2 deliverable; Phase 1 already has RPK compiler (`src/tools/content-pack/`); this spec formalizes + extends
Scope: schema validation, dependency graph, hot reload during dev, content addressing, error surfacing. JSON sources → RPK binary; dev mode hot-swap.

## 1. Source layout

```
content/
  schemas/        JSON Schema for every content type
  segments/       room segments
  encounters/     encounter tables
  enemies/        enemy defs + ai blocks
  items/          weapons, armor, consumables, components
  classes/        class defs + ability lists
  abilities/      ability defs (referenced by classes + enemies)
  synergies/      ability-pair synergies (Phase 1.5)
  missions/       mission defs
  dungeons/       dungeon templates (segment selection + encounter wiring)
  npcs/           NPC defs (stats, faction, dialogue ref)
  dialogue/       per-NPC dialogue trees
  audio/          cues.json
  world/          clock.json, factions.json (Phase 2), rumors.json (Phase 2)
  loot/           loot tables
  strings/        i18n strings (Phase 2)
```

Each subfolder has one or more `*.json` files; the schema file in `schemas/` defines structure.

## 2. Schema validation

Every content file declares its `$schema`:

```json
{
  "$schema": "../schemas/item.schema.json",
  "id": "bone_spear",
  ...
}
```

Compiler runs Ajv (or Json.NET schema validator) over every file. Failures = compile error with file:line:path.

Schemas are versioned: `item.schema.json` has `$id: "rpc://schema/item/1"`. Bumping `1 → 2` requires a migration script under `content/migrations/`.

## 3. Reference resolution

Cross-file references use `id` strings. Compiler builds a global symbol table:

```
{
  "items.bone_spear": "content/items/weapons.json#bone_spear",
  "enemies.bone_archer": "content/enemies/bone_archer.json",
  "missions.foothold-broken-engine": "content/missions/foothold-broken-engine.json",
  ...
}
```

After load, every reference (e.g., enemy ability `"longbow"`, mission trigger `"engine_console"`) is validated against the symbol table. Unresolved references = compile error.

Cycle detection on `mission.unlocks` chain (toposort).

## 4. Output: RPK pack

`*.rpk` is a single binary file:

```
[8 bytes magic "RPK\0V01\0"]
[4 bytes manifest offset]
[content blocks, length-prefixed JSON or msgpack]
[manifest: { path → offset+length+hash }]
```

`RPC.Content` already reads RPK (per existing T16 implementation). Manifest format documented here.

Build command: `dotnet run --project src/tools/content-pack -- build content/ build/content.rpk`.

CI fails on any validation error. Hash of manifest emitted to `build/content.rpk.sha256`; mismatch invalidates client IndexedDB cache.

## 5. Hot reload (dev mode)

In dev (`DOTNET_ENVIRONMENT=Development`), `RPC.Host` watches `content/` with `FileSystemWatcher`. On any change:

1. Re-validate changed file.
2. If valid: rebuild affected RPK segment + emit `event:content_updated` over WebSocket.
3. Client receives event, refetches content via REST, invalidates IndexedDB entry, re-renders affected UI.
4. Active GameState NOT reset unless content change is structurally incompatible (e.g., currently-in-combat enemy id removed).

Compatibility check: if a referenced id is in active game state (party class, equipped item, active mission, current dungeon, ongoing combat), the system warns and prompts user "Reload current state? (Y/N)". Default N — keep playing with stale def.

### 5.1 Watch performance

Debounce file events 200ms. Skip files inside `.git`, `node_modules`, `obj`, `bin`. Throttle re-validate runs to single in-flight at a time.

### 5.2 Disabled in release

Release builds (`dotnet publish`) skip watcher + REST hot-content endpoint. Content baked in.

## 6. Dependency graph

Compiler emits `build/content-graph.json` with edges:

```
mission.foothold → dungeon.broken_engine
dungeon.broken_engine → segments.broken_engine_*
segments.broken_engine_control_room → encounter.engine_guardian
encounter.engine_guardian → enemy.engine_construct_alpha
enemy.engine_construct_alpha → ability.engine_slam
class.bonewarden → ability.bone_spear, ability.tithe_touch
synergy.tithe_link_pyre → ability.bone_link, ability.pyre
```

Used for:
- Orphan detection (any node with no inbound edge except known roots).
- Impact analysis (what content depends on this file).
- CI artifact for designer review.

## 7. Content addressing in code

Engine never references content by file path. Always by id:

```csharp
var item = ItemRegistry.Get("bone_spear");
var encounter = EncounterRegistry.Get("engine_guardian");
```

Registries loaded from RPK at boot. Missing id at runtime = throw + log; never silent.

## 8. ID conventions

- `snake_case` for content ids.
- Namespace prefix: `<type>_<kebab>` for items/abilities/etc; `<dungeon>_<segment>` for segments.
- All ids lowercase.
- Schema validation enforces regex `^[a-z][a-z0-9_-]*$`.

## 9. CI

`.github/workflows/content.yml`:

```yaml
- run: dotnet run --project src/tools/content-pack -- validate content/
- run: dotnet run --project src/tools/content-pack -- build content/ build/content.rpk
- run: dotnet run --project src/tools/content-pack -- graph content/ build/content-graph.json
- name: Upload graph
  uses: actions/upload-artifact@v4
  with: { name: content-graph, path: build/content-graph.json }
```

Lint rule (custom): no two content items with same human-facing `name` within the same type (prevents "Bone Spear" ambiguity).

## 10. Authoring conveniences

### 10.1 Live preview

Dev mode exposes `/content/preview/<id>` REST route returning the resolved view (after refs expanded). Authors hit this URL while editing JSON to verify resolution.

### 10.2 Schema-aware editor hints

`*.code-workspace` config + JSON `$schema` enables VS Code autocomplete and inline error squiggles on all content files. No additional tooling needed.

### 10.3 Templates

`content/_templates/` contains starter files per type (`item.template.json`, `mission.template.json`, etc.) ignored by compiler but referenced by docs.

## 11. Save compatibility

Save files carry references to content ids. If a referenced id is missing in current RPK:
- Item: replaced with "Unknown Item" placeholder; user warned + can drop.
- Class: character marked "Class missing — class data unavailable" until id restored.
- Mission: marked inactive + visible in journal as "Removed content".
- Enemy: encountered but missing → skipped in combat; logged.

Never crash. Always degrade visibly.

## 12. Modding hook (Phase 3)

Mods load as additional RPKs from `mods/<id>/content.rpk`. Layered over base; later RPK overrides same-id earlier. Mod metadata in `mods/<id>/mod.kdl`:

```kdl
id "myMod"
name "Bigger Bonewardens"
version "1.0.0"
gameVersionMin "0.4.0"
loadAfter "base"
```

Validation: mod cannot remove ids, only add or override. Removals not supported (would break saves).

Phase 3 spec: separate modding doc covers signing, sandboxing, marketplace.

## 13. Tests

- xUnit (tools): schema validation rejects malformed; symbol table catches dangling refs; cycle detection rejects loops.
- xUnit (host): watcher debounce; reload event emitted on file change; compatibility check on active state.
- Integration: build pipeline produces deterministic RPK given identical input (byte-identical except timestamp footer).

## 14. Out of scope

- Live multi-author collaboration (Git is the collaboration medium).
- Visual content editor (Phase 3 if pursued).
- Localization extraction (Phase 2 i18n spec).
- Content version migration UI (CLI only).
