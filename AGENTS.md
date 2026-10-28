# AGENTS.md

Notes for anyone, human or AI agent, changing this code. `README.md` covers playing the game;
`docs/ARCHITECTURE.md` explains the design in depth and `CONTRIBUTING.md` holds the project's rules for
content, fidelity and pull requests. This file is the short version.

## What this is

AVAFlight, an unofficial fan recreation of the 1986 IBM PC DOS game Starflight, in C# on .NET 10 and Avalonia 12,
drawn with SkiaSharp. Gamepads and audio go through SDL3 (`ppy.SDL3-CS`); all music and sound is synthesised at
run time. One self-contained single-file executable per platform. No original game material is used.

## Commands

```bash
dotnet build AVAFlight.slnx                       # SDK pinned in global.json (10.0.400, latestFeature)
dotnet test tests/AVAFlight.Tests                 # headless (Avalonia.Headless.XUnit): no window opens
dotnet run --project src/AVAFlight.Avalonia       # play; -- --mute for silence
tools/screenshots.sh                              # refresh docs/screenshots (AVAFLIGHT_SCREENSHOTS)
./publish.sh [rid ...]                            # single-file builds into publish/<rid>/ (publish.ps1 on Windows)
```

- Run arguments: `--mute` and `--smoke-test` (CI uses the latter to check a binary reaches the main menu).
- `AVAFLIGHT_HOME` overrides the data folder; `AVAFLIGHT_SMOKE_FILE` is where the smoke test writes its result.

## Layout

- `src/AVAFlight.Core/`: the engine, with no Avalonia, I/O or package references.
  - `Engine/GameSession.cs` and its services: Starport, Crew, Navigation, Planet, Comms, Combat, Story.
  - `Model/GameState.cs` (the whole serialisable state), `Model/Rules.cs` (`Policy`: Classic vs Modern).
  - `Galaxy/` generates the galaxy from the seed; `Random/GameRandom.cs` is `IGameRandom` (SplitMix64).
  - `Data/Json/*.json` is the game content, embedded and checked by `GameData.Validate()`.
- `src/AVAFlight.Infrastructure/`: `AppPaths`, `Persistence/SaveStore` (versioned saves, migration),
  `Settings/`, `Audio/` (synth, sound bank, SDL3 output), `Input/` (`InputMapper`, `GamepadService`).
- `src/AVAFlight.Avalonia/`: the app (assembly `AVAFlight`). `Screens/`, `Panels/`, `Rendering/` (`SkiaView` and
  the Skia views), `Ui/` (shared menu navigation), `Services/AppServices.cs` (composition root).
- `tests/AVAFlight.Tests/`: xUnit v3, split into `Core/`, `Infrastructure/` and `UI/`.
- `docs/`: architecture, saves, fidelity, research ledger, third-party notices and screenshots.

## Rules that keep it working

- **Dependencies point inward:** Avalonia -> Infrastructure -> Core. Nothing platform-bound goes into Core.
- **Determinism.** All randomness goes through `IGameRandom`, never `System.Random` or the clock. Its state is
  saved, and combat is replayed exactly from a seed in the tests. Probabilistic tests use a seeded RNG.
- **Time models differ by service.** Combat runs in fixed 1/30 s steps; navigation converts real seconds to game
  hours at a fixed rate; the planet surface advances one step per move. See `docs/ARCHITECTURE.md`.
- **Save format.** If you change it, bump `GameState.CurrentSchemaVersion` and add a migration in `SaveStore`
  (see `docs/SAVES.md`). Newer saves are rejected, not misread.
- **Player files.** Saves and `settings.json` live in `%APPDATA%\AVAFlight`, `~/Library/Application Support/AVAFlight`
  or `$XDG_DATA_HOME/AVAFlight`. Tests pass a temporary folder to `new AppPaths(...)`.
- **Input goes through `InputMapper`.** One-shot actions fire once per physical press; directions repeat.
- **Fidelity and legal rules** in `CONTRIBUTING.md` apply: label new numbers with their evidence level, record
  historical changes in `docs/FIDELITY.md`, and copy nothing from the original game or the unlicensed decompilation.

## Making a change

- Add or update tests in the matching folder. `dotnet test` must pass, with no new warnings.
- If a screen changes, run `tools/screenshots.sh` and look at the results.
- Add an entry under **Unreleased** in `CHANGELOG.md`, and update `README.md` or `docs/` to match.

## CI and releases

- `.github/workflows/publish.yml` runs on pushes to `main`, `v*` tags and pull requests.
  - It runs the tests, then publishes win-x64/arm64, linux-x64/arm64 and osx-x64/arm64 on native runners.
  - It checks each is a single file and smoke-tests it to the main menu.
- Pushes to `main` replace the assets of the `latest` pre-release; a `v*` tag makes a proper release.
- `retry-macos.yml` re-runs a Publish run that failed only because a macOS or ARM runner was not allocated.
- Dependabot opens weekly NuGet and Actions update PRs.
