# Localization (i18n) — Design Spec
Date: 2026-05-10
Status: design — Phase 2 deliverable; Phase 1 ships English-only with extraction-friendly patterns
Depends on: content-pipeline spec, settings spec, dialogue spec
Scope: string registry, extraction, runtime lookup, pluralization, locale selection, content vs UI strings. Phase 1 prep so Phase 2 add-language is a translation effort, not a refactor.

## 1. String taxonomy

| Class | Source | Locale-bound | Examples |
|---|---|---|---|
| **UI chrome** | client code | yes | "Save Game", "Inventory", "Level Up" |
| **Game terms** | content + code | yes | "Bone Fragments", "Cauterist", "Stillness" |
| **Mission text** | content | yes | mission title + lore + objectives |
| **Dialogue text** | content | yes | NPC lines + choice labels |
| **Lore documents** | content | yes | full lore text |
| **System errors** | code | yes | "Save failed", "Connection lost" |
| **Debug / logs** | code | no | always English |
| **IDs / keys** | code + content | no | "broken_engine", "bone_fragment" |

Ids never translated. Display names are.

## 2. Storage

### 2.1 UI strings

`src/client/src/i18n/en.json`:

```json
{
  "ui.button.save": "Save Game",
  "ui.button.reset": "Reset",
  "ui.modal.inventory.title": "Inventory",
  "ui.combat.attack": "Attack",
  "ui.combat.miss": "{actor} misses {target}",
  "ui.combat.crit_hit": "{actor} crits {target} for {damage}!",
  "ui.toast.save_success": "Saved.",
  "ui.toast.save_failed": "Save failed — {reason}",
  "ui.party.downed": "{name} is DOWN",
  "ui.tooltip.encumbrance": "You are carrying {count}/{max} items",
  "ui.error.network_lost": "Connection lost. Reconnecting…"
}
```

Hierarchical dot-keys. Source-of-truth = `en.json`. Other locales (`de.json`, `ja.json`) are translations.

### 2.2 Content strings

Content JSON keeps strings inline but with i18n awareness:

```json
{
  "id": "bone_spear",
  "name": "Bone Spear",
  "lore": "The point matters less than what is on the point.",
  "i18n": {
    "name": "items.bone_spear.name",
    "lore": "items.bone_spear.lore"
  }
}
```

Phase 1: `name` + `lore` used directly. Phase 2: if `i18n.<field>` present, look up that key in active locale; otherwise fall back to inline.

Extraction script walks content, produces `i18n/extracted/content.en.json` with all `name`/`lore`/`title`/etc fields as keys for translator.

### 2.3 Content extraction

`tools/i18n-extract/`:
- Reads all content files.
- For each translatable field, emits `<type>.<id>.<field>` keyed entry in `i18n/extracted/content.<locale>.json`.
- Diff against previous extraction = translation work delta.

Run on every content change in CI; PRs surface untranslated keys.

## 3. Runtime lookup

`src/client/src/i18n/I18n.ts`:

```ts
export const I18n = {
  locale: 'en',
  setLocale(loc: string) { ... reloads bundles, emits store update ... },
  t(key: string, params?: Record<string,unknown>): string,
  has(key: string): boolean,
  pluralize(key: string, count: number, params?: Record<string,unknown>): string,
};
```

Template syntax: `{paramName}`. No nested logic — keep simple.

Missing key behavior:
- Dev: render key inline + log warn (visible to dev).
- Release: render English fallback if present; else render key + ship telemetry.

Performance: bundles preloaded at boot for active locale + English fallback. Cache in memory.

## 4. Pluralization

Use ICU MessageFormat subset for plurals:

```json
{
  "ui.inventory.items_dropped": "{count, plural, one {1 item dropped} other {# items dropped}}"
}
```

Implementation: `@formatjs/intl-messageformat` (browser-friendly, ~25 KB gzipped).

```ts
I18n.t('ui.inventory.items_dropped', { count: 3 });
// "3 items dropped"
```

Locale-correct plural categories handled by `Intl.PluralRules` built into browser.

## 5. Number / date formatting

Use `Intl.NumberFormat` + `Intl.DateTimeFormat` for any user-facing numeric:

```ts
const fmt = new Intl.NumberFormat(I18n.locale);
fmt.format(12345);   // "12,345" en-US, "12.345" de-DE
```

Damage numbers, gold counts, XP — all run through `formatNumber()` helper.

Phase 1: en-US locale only; helper still used so Phase 2 lights up automatically.

## 6. Direction (LTR/RTL)

Reserve in design:
- CSS uses logical properties (`margin-inline-start`, not `margin-left`).
- Existing `app.css` already does `inset-inline` / `border-inline` — good.
- `<html dir="ltr|rtl">` bound to locale.

Phase 2 may add Arabic / Hebrew. Audit all CSS for direction-safe properties on locale add.

## 7. Locale negotiation

Boot sequence:
1. Read `settings.display.locale` from KDL (Phase 2 setting).
2. Else use `navigator.language`.
3. Else default `en`.
4. If chosen locale bundle missing, fall back chain: `xx-YY` → `xx` → `en`.

`settings.display.locale` accepts BCP 47 tags: `"en"`, `"en-US"`, `"de"`, `"ja"`.

## 8. Server-side strings

Server emits **keys**, not localized text, where possible:

```json
{ "kind":"event", "type":"toast",
  "payload":{ "key":"ui.toast.save_failed", "params":{"reason":"disk_full"} } }
```

Client resolves with current locale. This keeps server locale-agnostic.

Exception: dialogue text is too cumbersome to key per line. Dialogue content's text fields are translated via content extraction (§2.3). Server sends content id + node id; client looks up localized text from the content bundle.

## 9. Translator workflow

1. Designer adds English content.
2. CI extracts new/changed keys to `i18n/extracted/`.
3. Translator copies extracted file to `i18n/translations/<locale>/`, fills translations.
4. Translation bundle compiler merges `i18n/translations/<locale>/*.json` into single `<locale>.json` per content category.
5. Built into the RPK.

Tools for translators: simple JSON editor; future Phase 3 may integrate Crowdin / Weblate.

Quality gates:
- Untranslated keys in a bundle warn but don't block build (fall back to English).
- Translated keys with wrong placeholder count fail build.
- Translated string >300% length of English warns (UI overflow risk).

## 10. UI text fitness

Every UI text container should accommodate 150% the source string length. German particularly verbose; Asian languages may need different line heights. Spec'd in design-system §5.1 button sizes — `min-width: --s-7` ensures buttons grow with content.

Truncation: `text-overflow: ellipsis` with tooltip showing full text on hover/focus. Never silently cut critical info.

## 11. Testing

- Unit: `I18n.t` with missing key returns key + warns.
- Unit: pluralization across en/de/ja for `0/1/2/5/many`.
- Pseudo-localization mode (Phase 2): `'en-XA'` wraps all strings in `⟦`…`⟧` and doubles characters to test layout robustness. Toggle via debug setting.
- Visual: Playwright at en + de + ja for primary screens, check no overflow.

## 12. Migration plan

Phase 1 ships English-only but with:
- All UI strings already in `en.json` (refactor inline literals into keys).
- Content extraction tool functional.
- `I18n.t()` calls throughout codebase.
- Number formatting via helper.

Phase 2:
- Add second locale (likely German based on tester pool) end-to-end.
- Locale setting in settings modal.
- Validate workflow with one external translator.

Phase 3:
- Open translation contributions.
- Add RTL locale to validate direction handling.

## 13. Out of scope

- Voice acting in multiple languages.
- Cultural adaptation (e.g., different game balance per locale).
- Machine translation pipeline (translators are humans).
- Right-to-left for combat layouts that imply directionality (Phase 3 if pursued).
