# Settings, Keybindings & Input

## Scope

Covers player preferences, input mapping, display options, and accessibility. Present from Phase 1 so the codebase has hooks from the start.

## Storage

- **Format:** KDL files on disk, mirrored to `localStorage` for synchronous reads at client startup.
- **Location:** `%APPDATA%/TheReach/settings.kdl` (Windows), `~/.config/TheReach/settings.kdl` (Linux), `~/Library/Application Support/TheReach/settings.kdl` (macOS).
- **Sync:** Host reads KDL at boot and pushes to client via initial WebSocket handshake. Client writes to `localStorage` mirror for instantaneous UI access. Host persists changes back to disk.

## Settings Categories

### Input

| Action | Default Binding | Rebindable |
|---|---|---|
| Move forward | Arrow Up / W | Yes |
| Move backward | Arrow Down / S | Yes |
| Strafe left | A | Yes |
| Strafe right | D | Yes |
| Turn left | Q | Yes |
| Turn right | E | Yes |
| Interact / confirm | Space / Enter | Yes |
| Cancel / back | Escape | Yes |
| Open inventory | I | Yes |
| Open automap | M | Yes |
| Open Field Notes | J | Yes (Phase 1.5+) |
| Toggle run/walk | Left Shift | Yes |
| Quick save (town only) | F5 | No |

- Bindings stored as `action: { key: string, modifiers?: ["shift","ctrl","alt"] }`.
- Conflicts detected at rebind time: two actions cannot share the same chord. UI warns and blocks.
- Gamepad support deferred to Phase 3 (analog stick for movement, face buttons for confirm/cancel/inventory/map).

### Display

| Setting | Default | Options |
|---|---|---|
| Resolution | Native desktop | 1280×720, 1920×1080, 2560×1440, native, windowed |
| Fullscreen | Windowed | Windowed, Borderless, Fullscreen |
| V-Sync | On | On, Off |
| Field of View | 75° | 60°–90° in 5° steps |
| Camera bob | On | On, Off |
| Camera shake | On | On, Reduced, Off |

- Resolution changes require Photino window resize + Three.js renderer resize.
- FOV changes are client-side only; server does not care about camera parameters.

### Accessibility

| Setting | Default | Effect |
|---|---|---|
| Colorblind mode | Off | Deuteranopia / Protanopia / Tritanopia presets. Shifts UI faction colors and status effect icons. |
| Text size | Medium | Small / Medium / Large. Scales Svelte UI root font-size (clamp). |
| Motion reduction | Off | When On, disables camera bob, reduces combat shake, replaces synergy flash with solid color overlay. |
| High contrast outlines | Off | Adds black outlines to enemies and interactables for low-light dungeon readability. |
| Subtitles | On | Text transcription of all audio cues (synergy sounds, Unaccounted audio warnings). |

- Accessibility settings are read at client startup and applied before the first frame renders.
- Subtitle system: engine tags audio events with `subtitle_id`; client displays the corresponding text overlay for 2–3 seconds.

## Input Architecture

The client owns input capture; the server owns validation.

```
Input Event → Client buffer → WebSocket → Server validates → State update → Client animates
```

- **Buffer:** Client maintains a 2-slot input queue. If the server hasn't confirmed the previous move, the second input is held. This prevents spam without feeling unresponsive.
- **Repeat delay:** Holding a movement key sends repeat actions at 200ms intervals after an initial 300ms delay.
- **Cancel:** If the server rejects an action (e.g., walking into a wall), the client snaps the camera back and clears the input buffer.

## Phase Tasks

| Phase | Task | Description |
|---|---|---|
| 1 | Settings data model | KDL schema, .NET settings loader, localStorage mirror |
| 1 | Keybindings (hardcoded) | Arrow keys + WASD movement. Settings UI panel lists bindings but editing is read-only |
| 1.5 | Rebindable keys | Full keybinding UI. Conflict detection. Persist to KDL |
| 2 | Display settings | Resolution, fullscreen, FOV |
| 2 | Accessibility settings | Colorblind mode, text size, motion reduction |
| 3 | Gamepad support | Controller detection, binding UI, analog deadzone config |
