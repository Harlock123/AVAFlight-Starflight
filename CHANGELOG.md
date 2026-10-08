# Changelog

All notable changes to AVAFlight are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- SECURITY.md, issue forms (bug, fidelity, feature request) and a pull request template.
- CODE_OF_CONDUCT.md: Contributor Covenant 2.1.
- CONTRIBUTING.md: build, project, fidelity, legal and pull-request guidelines.
- README: a full-size gameplay screenshot (terrain vehicle) near the top.
- README: a CI status badge for the Publish workflow.
- CI: `retry-macos.yml` automatically re-runs Publish runs that failed only because GitHub could not allocate a macOS runner. It makes up to 3 attempts. Real build, test and smoke-test failures are never retried.

## [1.0.0] - 2026-10-08

First release. Self-contained single-file builds for Windows x64, Linux x64, macOS x64 and macOS arm64.

### Added

**Starport**
- Operations (dated Interstel notices, mission briefing, colony evaluation), Personnel, Crew Assignment, Bank, Ship Configuration, Trade Depot and Docking Bay.
- The documented starting state: 12,000 M.U., a class 1 engine, 20 m³ of Endurium.

**Crew**
- Five races (Human, Velox, Thrynn, Elowan, Android) with the manual's skill ranges, durability and learning rates.
- Paid training: 300 M.U. per session.
- Six posts. When an officer dies, the next most capable crew member covers the post. Crew death is permanent.

**Ship**
- Engines, shields, armour, missiles and lasers in classes 1–5, at the documented prices.
- Up to 16 cargo pods.
- Mass and acceleration from the derived formula.
- In-flight repairs, which use repair minerals for heavy damage.

**Navigation**
- A 270-system, 811-planet galaxy, with story locations at their documented coordinates.
- Hyperspace and in-system flight with fuel use by engine class.
- A star map with course, distance, fuel and time readout.
- Continuum fluxes, nebulae, per-star flare schedules, star heat, and the distress tow.

**Planets**
- Orbital sensors and analysis, with certainty that depends on the Science Officer's skill.
- A Mercator landing map.
- A fractal-style terrain surface.
- The terrain vehicle: 50 m³ hold, fuel per step, walking home if it runs dry.
- Minerals, lifeforms (stun, capture, bio-data), ruins, messages and storms.

**Aliens**
- Elowan, Thrynn, Velox, Spemin, Mechan 9, Gazurtoid, Uhlek, Minstrels, Nomad probes, and an unidentified ship that speaks only in binary.
- Three postures, statements, and five question topics.
- Translation quality from the Communications skill, with race bonuses.
- Race-specific protocols and trades.
- Newly written dialogue.

**Combat**
- Real-time, simulated in fixed 30 Hz steps.
- Shields, armour and hull.
- Automatic laser/missile selection.
- Homing Uhlek plasma; Gazurtoid missile resistance.
- Escape, surrender, debris salvage.

**Story**
- The documented win path: Crystal Orb → Uhlek mind world → Crystal Cone → Black Egg at the Nexus of Control, with a 500,000 M.U. bonus.
- McConnell's log, and the Thrynn deception about Elan.
- Arth's flare deadline.

**Presets**
- **Classic** follows documented 1986 DOS behaviour.
- **Modern** keeps the same rules and adds an automatic captain's log, star-map waypoints with notes, a fuel-range ring and warnings, tooltips, a pause hotkey, dismissible hints and named save slots.

**Interface**
- The original's four-part console layout, drawn in the EGA palette at modern resolution.
- Keyboard, mouse and SDL3 gamepad input, with remappable keys and duplicate-press suppression.
- Font scaling, high contrast, adjustable dialogue text speed and borderless fullscreen.
- The UI scales down uniformly in small windows.

**Audio**
- Nine newly composed music cues and 19 sound effects, synthesised at runtime and played through SDL3.
- Independent music and effects volumes.
- Falls back to silence when no audio device is available.

**Saves**
- Versioned JSON saves with migration, atomic writes and clear errors for corrupt files.
- Per-OS data directories.

**Testing and CI**
- 85 headless tests covering the engine, infrastructure and UI, including the 17 documentation screenshots and a scripted full playthrough to victory.
- A GitHub Actions Publish workflow that tests, publishes and smoke-tests all four targets on native runners, and attaches archives to releases on `v*` tags.

**Documentation**
- Research dossier with source ledger, fidelity matrix, architecture, player guide, save format, third-party notices, known issues and milestone status.

**Licence**
- MIT. The bundled fonts are under the SIL Open Font License.

[Unreleased]: https://github.com/Harlock123/AVAFlight-Starflight/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/Harlock123/AVAFlight-Starflight/releases/tag/v1.0.0
