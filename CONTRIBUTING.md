# Contributing to AVAFlight

Thanks for your interest in AVAFlight! Bug reports, fixes, fidelity research and new features are all welcome. This guide covers how to build the project, the rules for content, and how to send changes.

## Getting started

You need the **.NET SDK 10.0.400 or later**. `global.json` pins it, with `rollForward: latestFeature`. On Linux you also need an X11 or XWayland session to run the game. Tests run headlessly.

```bash
git clone https://github.com/Harlock123/AVAFlight-Starflight.git
cd AVAFlight-Starflight
dotnet build
dotnet test                                   # 85 headless tests
dotnet run --project src/AVAFlight.Avalonia   # play (add -- --mute for no audio)
```

Read [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) before making larger changes.

## Reporting bugs

Open a GitHub issue with:

- your OS and CPU architecture
- whether you ran a release binary or built from source
- the AVAFlight version (shown on the title screen)
- steps to reproduce, and what you expected instead

If a save file triggers the bug, attach it. [docs/SAVES.md](docs/SAVES.md) explains where saves are stored.

## Project rules

### Architecture

- **`AVAFlight.Core` stays UI-independent.** It must not reference Avalonia, audio, files or any platform API. Rules and state go in Core; devices and I/O go in `AVAFlight.Infrastructure`; screens go in `AVAFlight.Avalonia`.
- **All randomness goes through `IGameRandom`.** Never use `System.Random` or the clock inside game logic. Determinism and save/replay depend on it.
- **Game content is data.** Star systems, races, dialogue, equipment, minerals, artifacts, messages and notices live in `src/AVAFlight.Core/Data/Json/`. `GameData.Validate()` checks the files on load; extend it when you add new kinds of data.
- **If you change the save format**, bump `GameState.CurrentSchemaVersion` and add a migration in `SaveStore` (see [docs/SAVES.md](docs/SAVES.md)).

### Fidelity

AVAFlight recreates the **1986 IBM PC DOS** release. Behaviour from the Amiga, C64 or Genesis ports, or from Starflight 2, must not be presented as DOS behaviour.

- **Label every new rule or number** with its evidence level, in a code comment or the data's `evidence` field:
  - `documented`: manual, in-game text, or notices
  - `derived`: worked out from the decompiled code
  - `fan-measured`
  - `reconstruction`: no evidence, a designed value
- **Record any change to historical behaviour** in [docs/FIDELITY.md](docs/FIDELITY.md).
- **New research sources** go in the ledger in [docs/RESEARCH.md](docs/RESEARCH.md), with URL, access date, what you inspected, and confidence.
- **Classic and Modern share every rule and number.** Put a Modern-only convenience behind the `Policy` record (`src/AVAFlight.Core/Model/Rules.cs`). Never make it a balance change.
- **Modern mode must not add** quest markers, autopilot routing, crafting or skill trees, or online features.

### Legal: no original game material

Starflight is still copyrighted by Electronic Arts.

- **Do not copy** original code, data files, star maps, dialogue or text, artwork, music or sounds.
- The decompiled project `s-macke/starflight-reverse` has **no licence**. You may use it to learn facts, such as how a formula works. Do not copy its code, data tables or text.
- **Write new text** for dialogue, messages and notices, carrying the documented information in your own words.
- **Third-party code or assets** must be under a permissive, MIT-compatible licence. Record each one in [docs/THIRD_PARTY.md](docs/THIRD_PARTY.md) with its source, version, licence and how it is used. GPL code cannot be accepted.

### Code style

- Match the surrounding code: naming, file layout, comment density and idioms.
- The repo has nullable reference types enabled. The build should stay free of warnings.
- Prefer small, focused changes.

## Tests

- **Add or update tests** for any change in behaviour (`tests/AVAFlight.Tests`):
  - engine rules: `Core/`
  - saves, audio and input: `Infrastructure/`
  - screens and rendering: `UI/`, using `Avalonia.Headless`
- **Probabilistic tests** must use a seeded RNG and clearly labelled statistical bounds.
- **If you change how a screen looks**, regenerate the documentation screenshots with `tools/screenshots.sh`. Look at the results before committing them.
- **`dotnet test` must pass.** CI runs the tests, then publishes and smoke-tests all four platforms.

## Pull requests

1. Fork the repository and create a branch from `main`.
2. Make your change, with tests and any documentation updates.
3. Add an entry under **Unreleased** in [CHANGELOG.md](CHANGELOG.md).
4. Open a pull request that describes what changed and why. For fidelity changes, include the evidence.

Make sure the Publish workflow passes on your pull request. A failure caused only by GitHub being unable to allocate a macOS runner is retried automatically.

## Licence

AVAFlight is released under the [MIT licence](LICENSE). By contributing, you agree that your contributions are licensed under the same terms.
