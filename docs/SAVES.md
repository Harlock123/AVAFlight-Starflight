# Save files

## Locations

| OS | Directory |
|---|---|
| Windows | `%APPDATA%\AVAFlight\` |
| macOS | `~/Library/Application Support/AVAFlight/` |
| Linux | `$XDG_DATA_HOME/AVAFlight/` (default `~/.local/share/AVAFlight/`) |

- To override the directory (portable installs, tests), set the environment variable `AVAFLIGHT_HOME`.
- Saves are stored in `saves/<slot>.avasave`.
- Settings (volumes, display, accessibility, key bindings) are stored in `settings.json`. Settings are separate from saves.
- The app never writes next to its executable. Single-file builds may be installed in read-only locations.

## Slots

| Preset | Slots |
|---|---|
| Classic | One slot per ship (`classic-<shipname>`), as in the original's single save |
| Modern | Any number of named slots, created from **Options → Save game → New save slot…** |

- **F5** quick-saves to the current slot.
- You cannot save during an encounter.

## Format

A save is a UTF-8 JSON envelope:

```json
{
  "format": "AVAFlight.Save",
  "schemaVersion": 2,
  "savedAt": "2026-10-08T11:30:00+00:00",
  "displayName": "Before the Crystal Planet",
  "preset": "Modern",
  "starDate": "13-01-4620",
  "location": "Hyperspace 131,103",
  "state": { "...": "the complete GameState, camelCase properties, enums as strings" }
}
```

- `state` is the full canonical `GameState`:
  - RNG state, clock, credits, bank
  - ship, cargo, crew (dead crew remain dead), assignments, location
  - surface and encounter state
  - story flags, race relations, captain's log, waypoints
  - per-planet records: mined deposits, collected sites, destroyed worlds
- **The galaxy itself is not saved.** It is regenerated deterministically from `galaxySeed` and the game data.
- Writes are atomic: the file is written to `*.tmp`, then renamed.

## Versioning and migration

- `GameState.CurrentSchemaVersion` is currently **2**.
- **Older saves** are migrated step by step by the functions in `SaveStore.Migrations`. For example, v1 → v2 adds the captain's log.
- **Newer saves** (made by a later AVAFlight) are rejected with the message "Save was made by a newer AVAFlight…".
- **Corrupt or foreign files** are rejected with a clear message rather than a crash, and appear as "(unreadable save)" in the load list. The cases covered are:
  - invalid JSON
  - wrong format tag
  - missing state
  - invalid values, such as negative credits, out-of-range component classes, or assignments to unknown crew
- **When changing the format:** bump `CurrentSchemaVersion` and add a migration keyed by the old version. Tests cover migration, rejection and corruption (`tests/AVAFlight.Tests/Infrastructure/InfrastructureTests.cs`).
