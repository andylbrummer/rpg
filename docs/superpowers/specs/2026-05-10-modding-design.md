# Modding — Design Spec
Date: 2026-05-10
Status: design — Phase 3 deliverable (deferred); spec lands hooks Phase 2 so it isn't a rewrite later
Depends on: content-pipeline spec, settings spec, asset-pipeline spec
Scope: data-only mods (no code execution), load order, conflict resolution, distribution, safety. No scripting Phase 3 — content overlays only.

## 1. Mod kinds (Phase 3)

| Kind | What it can add/change | Status |
|---|---|---|
| **Content** | items, abilities, enemies, encounters, segments, missions, dialogue, synergies | Phase 3 |
| **Audio** | replace cue files | Phase 3 |
| **Visual** | replace textures, icons, portraits, fonts | Phase 3 |
| **Balance** | tweak class stats, enemy stats, costs, drop tables | Phase 3 (via content overlay) |
| **Localization** | add or override locale strings | Phase 3 |
| **Code** | scripts, behaviors, new mechanics | **NOT supported.** Phase 4 if ever. |

Code mods deliberately out: sandbox cost + save-compatibility nightmare.

## 2. Mod package format

`.rpcmod` = ZIP file with required layout:

```
mymod.rpcmod (zip)
  mod.kdl                      # metadata
  content/                     # JSON content overrides
    items/myitem.json
    missions/my_mission.json
  assets/                      # optional asset overrides
    icons/myicon.svg
    audio/mytrack.ogg
  i18n/                        # optional locale strings
    en.json
    de.json
  README.md
  LICENSE
```

`mod.kdl`:

```kdl
id "my-mod"
name "Bigger Bonewardens"
version "1.0.0"
author "name"
homepage "https://..."

gameVersion {
    min "0.5.0"
    max "0.9.99"          // optional upper bound
}

depends "another-mod" "1.2.0"   // optional
conflicts "rival-mod"           // optional

loadAfter "base"                // implicit
description "Boosts Bonewarden HP +5 across all levels. Adds the Marrow Helm."

tags "balance" "items"
```

## 3. Load order

```
base content (shipped RPK)
  ├─ mod A
  ├─ mod B
  └─ mod C
final resolved content
```

Each mod's content is merged ID-by-ID:
- New IDs: added.
- Existing IDs: mod overrides base entry; later mods override earlier mods.
- Removed IDs: NOT supported. Mod can only override or add, never remove (would break saves).

Conflict resolution UI lists order with user-controllable reordering.

## 4. Validation

Same schema validation as base content (per content-pipeline spec §2):
- All references resolve (within base + dependency mods + this mod).
- All cross-references coherent (e.g., adding a class also requires its abilities and starter loadout).
- No id collisions across mods unless explicit override declared in `mod.kdl`:
  ```kdl
  overrides {
      item "bone_spear"
      enemy "rat"
  }
  ```
- Without `overrides` entry, attempted override = validation error. Forces mod authors to be explicit.

Validation runs at load time. Invalid mods skipped + listed in mod manager UI with reasons.

## 5. Storage

Mods live at `{appData}/rpc/mods/<mod-id>/`:

```
mods/
  bigger-bonewardens/
    mod.kdl
    content/
    ...
  another-mod/
    ...
```

Optional: bundled `.rpcmod` zip files in `mods/_archives/`. Mod manager auto-unzips into named folders.

User editing of installed mods discouraged; mod manager primary UI.

## 6. Mod manager (in-game UI)

Settings → Mods opens management screen:

```
┌─Installed Mods─────────────────────────────────────────────────┐
│ Load order (drag to reorder):                                  │
│ ☑ ▣ Bigger Bonewardens   v1.0.0    [Details] [Disable]        │
│ ☑ ▣ Whispering Crypt     v0.3.1    [Details] [Disable]        │
│ ☒ ▢ Old Rival           v2.0.0    (incompatible: game v0.6)   │
│                                                                │
│ [Install from file…]  [Browse Community (Phase 3)…]            │
└────────────────────────────────────────────────────────────────┘
```

Per-mod details: description, dependencies status, conflicts, version compatibility, file list.

Enable/disable: live, except changes that affect active save (added/removed content with references in save) require save reload.

## 7. Save compatibility

Save records list of active mods:

```csharp
public List<ModRef> ActiveMods { get; set; }
public record ModRef(string Id, string Version);
```

On load:
- Compare active mods at save time to currently installed.
- Missing mod referenced in save: degrade per content-pipeline §11 (unknown id placeholders) + warn.
- Mod version mismatch: load anyway, log compatibility check.
- User can disable mods after save — content references degrade gracefully.

## 8. Safety

Phase 3 protects against:
- Malicious content (no scripts, so attack surface = denial-of-service via huge content).
- Size caps: total mod content max 500 MB; single mod max 200 MB.
- Validation timeout: 30 s per mod; hung loader → skip.
- File-system traversal: zip extraction sanitizes paths (no `..`, no absolute).

Phase 3 community workshop (separate spec) adds:
- Signature verification of submitted mods.
- Crowdsourced trust scores.
- Curated featured list.

## 9. Mod authoring tools

Phase 3:
- `tools/mod-pack/` CLI: `rpc-modpack init mymod` scaffolds folder; `... build` validates + zips.
- Documentation: `docs/modding/` walkthroughs for each kind.
- Example mod repo seeded at release.

## 10. Localization mods

Override existing locale OR add new locale:

```
mymod/
  i18n/
    eo.json       # adds Esperanto
```

When player selects Esperanto in settings, base bundle falls back to English where mymod hasn't translated. Localization mods are first-class: lighter validation, no schema other than JSON.

## 11. Asset mods

Override texture / icon / audio by matching path:

```
mymod/
  assets/
    icons/sword.svg     # replaces base icons.svg#sword symbol
    portraits/kael.webp # replaces base portrait
    audio/sfx/hit_melee_01.ogg  # replaces cue file
```

Build-time mod packager combines into a single overlay manifest layered atop base. No re-encoding needed if formats match base.

Asset mods can blow visual / audio coherence — that's by design (player consent).

## 12. Phase 2 prep work

Phase 2 doesn't ship modding UI but lays groundwork:

- Content-pipeline supports layered RPK loading (base + mods folder).
- Save format reserves `ActiveMods` array (currently empty).
- Content registries support id-override mechanism (loaders read multiple RPKs, last write wins).
- Asset pipeline supports overlay manifests (deferred to Phase 3 to wire up).

This means Phase 3 modding is content + UI + tools work, not engine surgery.

## 13. Tests

- xUnit (Phase 3): load order conflict detection.
- xUnit: validation rejects collision-without-override.
- xUnit: save with active mods → loads with mods disabled gracefully.
- Integration: install a sample mod via UI, verify content appears in game.
- Manual: community workshop submission flow (Phase 3 separate spec).

## 14. Out of scope

- Scripting / code mods.
- Server-side enforcement (single-player game).
- DRM / mod signing for end users (community workshop handles trust).
- Mod monetization.
- In-game mod editor.
