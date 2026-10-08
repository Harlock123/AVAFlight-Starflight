# AVAFlight

[![Publish](https://github.com/Harlock123/AVAFlight-Starflight/actions/workflows/publish.yml/badge.svg?branch=main)](https://github.com/Harlock123/AVAFlight-Starflight/actions/workflows/publish.yml)

**AVAFlight** is a recreation of *Starflight*, the 1986 space exploration RPG. It is built with **.NET 10** and **Avalonia 12** for Windows, Linux and macOS, and plays fully offline as a single-player game.

You fly from Starport on Arth to explore 270 star systems and 811 planets. Along the way you:

- train and manage a six-post crew
- outfit your ship
- mine planets and capture alien life
- negotiate with alien races
- uncover what is igniting the stars before Arth's sun flares

There are two presets:

- **Classic** follows the documented 1986 DOS behaviour.
- **Modern** keeps every rule and number the same, and adds conveniences: an automatic captain's log, waypoints, a fuel-range ring, tooltips, a pause hotkey and named saves.

> Unofficial fan project. Not affiliated with or endorsed by Electronic Arts or Binary Systems. All text, art and audio are newly created, and no original game data is included. See [docs/THIRD_PARTY.md](THIRD_PARTY.md).

![Main menu](screenshots/01_main_menu.png)

![AVAFlight gameplay: driving the terrain vehicle across a jungle world, with the ship, wildlife, crew status and the terrain-vehicle menu](screenshots/08_terrain_vehicle.png)

## Prerequisites

- **.NET SDK 10.0.400 or later.** `global.json` pins the 10.0.400 SDK with `rollForward: latestFeature`.
- **Linux:** an X11 or XWayland session with fontconfig. **macOS / Windows:** nothing extra.
- Audio and gamepad support (SDL3) are bundled. If they can't be initialised, the game runs silently.

## Build, run, test

Run all commands from the repository root.

```bash
dotnet build                                      # whole solution
dotnet run --project src/AVAFlight.Avalonia       # play (add -- --mute to disable audio)
dotnet test                                       # 85 headless tests (engine, infrastructure, UI)
tools/screenshots.sh                              # regenerate screenshots/*.png
```

## Publish (self-contained single file)

```bash
./publish.sh                    # win-x64 linux-x64 osx-x64 osx-arm64 -> publish/<rid>/AVAFlight[.exe]
./publish.sh linux-arm64        # any other runtime identifier
pwsh ./publish.ps1              # Windows / PowerShell equivalent
```

Each target can also be published individually:

```bash
dotnet publish src/AVAFlight.Avalonia -c Release -r win-x64   --self-contained -p:PublishSingleFile=true
dotnet publish src/AVAFlight.Avalonia -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true
dotnet publish src/AVAFlight.Avalonia -c Release -r osx-x64   --self-contained -p:PublishSingleFile=true
dotnet publish src/AVAFlight.Avalonia -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true
```

**CI:** `.github/workflows/publish.yml` runs the tests, publishes all four targets on their native GitHub runners (windows-latest, ubuntu-latest, macos-15-intel, macos-latest), smoke-tests each binary, and uploads the archives as artifacts. Pushing a `v*` tag attaches them to a GitHub release. If GitHub cannot allocate a macOS runner, `.github/workflows/retry-macos.yml` re-runs the cancelled jobs automatically (up to 3 attempts); real failures are never retried.

To verify a published binary, run `publish/<rid>/AVAFlight --smoke-test`. It prints `AVAFLIGHT_SMOKE_OK: main menu reached` and exits with code 0.

Verification so far:

- **All four required targets:** published and smoke-tested on native GitHub runners by the CI workflow.
- **linux-arm64:** also run and verified locally. See [docs/KNOWN_ISSUES.md](KNOWN_ISSUES.md).

## Documentation

| Document | Contents |
|---|---|
| [docs/GAMEPLAY.md](GAMEPLAY.md) | Player guide and controls |
| [docs/RESEARCH.md](RESEARCH.md) | Research dossier, source ledger, open questions |
| [docs/FIDELITY.md](FIDELITY.md) | Reference version, fidelity matrix, Classic vs Modern |
| [docs/ARCHITECTURE.md](ARCHITECTURE.md) | Projects, engine, time models, rendering, input, audio, publishing |
| [docs/SAVES.md](SAVES.md) | Save locations, format, versioning |
| [docs/THIRD_PARTY.md](THIRD_PARTY.md) | Licences and asset provenance |
| [docs/KNOWN_ISSUES.md](KNOWN_ISSUES.md) | Caveats and unverified items |
| [docs/MILESTONE_STATUS.md](MILESTONE_STATUS.md) | Milestone status and next tasks |

## Screenshots

| | |
|---|---|
| ![](screenshots/02_starport_hub.png) Starport | ![](screenshots/05_star_map.png) Star map |
| ![](screenshots/07_planet_survey.png) Planet survey | ![](screenshots/08_terrain_vehicle.png) Terrain vehicle |
| ![](screenshots/10_alien_contact.png) Alien contact | ![](screenshots/11_combat.png) Combat |

All 17 screenshots are in [screenshots](screenshots).

## License

MIT. See [LICENSE](../LICENSE). Bundled fonts are under the SIL Open Font License (`licenses/`); see THIRD_PARTY.md.
