# Architecture

## Solution layout

```
AVAFlight.slnx
├─ src/AVAFlight.Core            UI-independent game engine (no Avalonia, no I/O)
│   ├─ Data/                     Data-file definitions, GameData loader and validator, embedded Json/*.json
│   ├─ Galaxy/                   Galaxy model, seeded GalaxyGenerator, PlanetSurface (terrain)
│   ├─ Model/                    GameState (canonical, serializable), encounter model, rules, Policy
│   ├─ Engine/                   GameSession and its services (see below)
│   ├─ Random/                   IGameRandom + SplitMix64 (injectable, serializable)
│   └─ Serialization/            Shared System.Text.Json options
├─ src/AVAFlight.Infrastructure  Platform services
│   ├─ Persistence/SaveStore     Versioned save envelopes, migration, corruption handling
│   ├─ Settings/                 User settings (JSON), clamped and fault-tolerant
│   ├─ Audio/                    Synth, SoundBank (procedural music and SFX), Mixer, SDL3 output
│   ├─ Input/                    Logical actions, InputMapper (de-duplication, repeat), SDL3 GamepadService
│   └─ AppPaths                  Per-OS data directories
├─ src/AVAFlight.Avalonia        Desktop app (assembly name: AVAFlight)
│   ├─ Screens/                  Title, Load, Settings, Game (console), End, Overlays
│   ├─ Panels/                   StarportPanel, SurveyPanel (native Avalonia controls)
│   ├─ Rendering/                SkiaView base + Skia views (space, star map, system, globe, terrain, combat, portraits)
│   ├─ Ui/                       Theme, MenuList, PageStack (shared keyboard, mouse and gamepad navigation)
│   └─ Services/AppServices      Composition root (settings, saves, audio, input, gamepad)
└─ tests/AVAFlight.Tests         xUnit v3: engine, infrastructure, headless UI and screenshots
```

Dependencies point inward only: Avalonia → Infrastructure → Core. Core has no package references at all.

## Engine

`GameSession` wraps one running game. It owns:

- the canonical **`GameState`**
- the immutable **`GameData`**, loaded from embedded JSON and validated on load
- the **`GalaxyMap`**, regenerated from the seed
- the injected **`IGameRandom`**
- the **`Policy`** for the current preset

Player actions go through these services:

| Service | Responsibility | Time model |
|---|---|---|
| `StarportService` | Bank, ship configuration, trade depot, analysis, colony evaluation, pre-flight checks, launch and dock, distress tow | Menu-driven |
| `CrewService` | Personnel, training, assignment, injury, healing, in-flight repair | Menu-driven, plus hourly ticks |
| `NavigationService` | Hyperspace and in-system flight, cruise, fluxes, nebulae, star heat, Crystal Planet defences, encounter rolls, orbit | **Time-stepped**: `Fly(dir, realSeconds)` converts real time to game hours at a fixed rate |
| `PlanetService` | Sensors, analysis, logging, landing, terrain vehicle, minerals, lifeforms, sites, Black Egg | **Step-based**: each vehicle move or wait advances the surface world one step |
| `CommsService` | Postures, statements, questions, translation, race protocols, trades | Menu-driven, with a patience budget per exchange |
| `CombatService` | Encounters, tactical combat, damage, surrender, debris | **Real-time**, simulated in fixed 1/30 s steps |
| `StoryService` | Clock, flare schedule, notices, story flags, log, victory | Driven by the other services |

**Determinism.** All randomness goes through `IGameRandom`. Its state is saved, so a reloaded game continues the identical random sequence. `Combat_IsDeterministic_ForSameSeedAndInputs` replays a fight exactly from the same seed and inputs.

**Events.** Services report results through `ActionResult`, write text-window lines through `Say`, and queue `GameEvent`s (sound cues, mode changes, game over, victory). The UI drains the events each frame. The engine never touches UI or audio.

**Classic vs Modern.** A single `Policy` record (`Model/Rules.cs`) holds every difference. Rules and numbers are shared, so no part of the engine is duplicated.

**Galaxy vs story.** The galaxy is a pure function of the seed and the data. Story progress lives only in `GameState.Flags`, `Relations` and `Planets` (per-planet records such as mined deposits, collected sites and destroyed worlds). Saves store only these deltas.

## Game-state model

`GameState` (`Model/GameState.cs`) holds:

- seed, preset and RNG state
- clock, credits and bank transactions
- ship: components, pods, Endurium, hull, armour, shields, damage per system, cargo
- roster and assignments
- location: mode, hyperspace and in-system coordinates, orbit, cruise, position lost
- surface state (vehicle, creatures, egg fuse) and encounter state (ships, projectiles, comms)
- flags, race relations, log, waypoints, visited systems, planet records, species, notices seen
- statistics, win flag and game-over reason

The whole object round-trips through `System.Text.Json` (see SAVES.md). After loading, `GameState.Validate()` sanity-checks it.

## Views

| View | Kind | File |
|---|---|---|
| Title / main menu | Avalonia controls + Skia star field | `Screens/TitleScreen.cs`, `Rendering/StarfieldView.cs` |
| Starport hub and modules | Native controls (`PageStack`, `MenuList`) | `Panels/StarportPanel.cs` |
| Star map (galaxy, cursor, course) | Skia overlay | `Rendering/SpaceViews.cs: StarMapView`, `Screens/Overlays.cs: StarMapOverlay` |
| Hyperspace view | Skia | `HyperspaceView` |
| In-system view | Skia | `SystemView` |
| Orbit: globe and planet data | Skia + native | `PlanetGlobeView`, `Panels/SurveyPanel.cs` |
| Landing-site map | Skia (Mercator) | `PlanetGlobeView` (LandingMap) via `LandingOverlay` |
| Terrain vehicle | Skia | `TerrainView` |
| Communication | Native + Skia portrait | `CommsOverlay`, `AlienPortraitView` |
| Combat | Skia | `CombatView` |
| Crew, ship, cargo, log panels | Native | `InfoOverlay` |
| Status (auxiliary view), control panel, text window | Native, always visible | `Screens/GameScreen.cs` |
| Settings, load, game over / victory | Native | `SettingsScreen`, `LoadScreen`, `EndScreen` |

**Console layout.** The in-game console follows the original's four areas:

- main view (left)
- auxiliary view (status, top right)
- control panel (crew posts, then their functions)
- text window (bottom)

## Rendering pipeline

Avalonia has no `SKCanvasControl`. `Rendering/SkiaView` is the equivalent:

1. `Render()` queues an `ICustomDrawOperation`.
2. On the render thread, that operation leases the live `SKCanvas` through `ISkiaSharpApiLeaseFeature`.
3. Views draw in logical units; the canvas transform already includes the HiDPI scale, so output is sharp on high-DPI displays.
4. Animated views redraw at about 60 Hz from a `DispatcherTimer`.

The same path works under `Avalonia.Headless` with real Skia rendering, which is how the documentation screenshots are produced.

**Small windows.** Tiling window managers can ignore the window's minimum size. When the window is smaller than 1024×640, `MainWindow` scales the whole UI down uniformly with a `LayoutTransformControl`. **Fullscreen** is borderless (F11 or Alt+Enter).

## Input

```
keyboard (Avalonia KeyDown/Up) ─┐
                                ├─► InputMapper ─► logical InputAction ─► Screen.HandleAction
gamepad (SDL3 polling, 60 Hz) ──┘        │
                                         └─► HeldInput (held directions + analog stick) ─► flight, vehicle, combat
```

- **`InputMapper`** handles duplicates and repeats:
  - Confirm, Back and the other one-shot actions fire **once per physical press**, so OS auto-repeat and held buttons are ignored.
  - Directions repeat after 350 ms, then every 90 ms.
  - All bindings are remappable from Settings and stored in `settings.json`.
  - Key names are normalised because some Avalonia `Key` values share a number (Enter and Return).
- **Gamepad.** Avalonia 12 has no gamepad API, so AVAFlight uses SDL3 through `ppy.SDL3-CS`, initialising only the gamepad subsystem. Devices are re-enumerated every second, which gives hot-plug support. The left stick has a radial deadzone (configurable) and also acts as a D-pad in menus. Default buttons:
  - South: confirm; East: back
  - Start: pause; Back: star map
  - Shoulder buttons: switch tabs
- **Mouse.** Every `MenuList` row is clickable and every list scrolls with the wheel.

## Audio

All audio is **synthesised at runtime**; no audio files ship. The chain:

1. `Synth` provides square, pulse, triangle, sine, saw and LFSR noise oscillators, envelopes and a low-pass filter.
2. `SoundBank` holds nine newly composed music loops (title, Starport, hyperspace, system, planet, comms, combat, game over, victory) and 19 effects.
3. `Mixer` cross-fades music over 0.8 s, mixes at most 16 effect voices (the oldest is dropped), and soft-clips the output.
4. `Sdl3AudioOutput` feeds an SDL3 audio stream (float32 stereo, 44.1 kHz) from a dedicated thread, keeping about 60 ms queued.

`AudioEngine.Create` never throws. If SDL3 or an audio device is missing, the game runs silently and the reason is shown in Settings (`--mute` forces silence). Music and effect volumes are independent.

## Single-file publishing

Native libraries are bundled with `IncludeNativeLibrariesForSelfExtract=true`. On first run the .NET host extracts them to `~/.net/AVAFlight/<hash>`, or `%TEMP%\.net` on Windows; set `DOTNET_BUNDLE_EXTRACT_BASE_DIR` to change this.

| Target | Bundled native libraries |
|---|---|
| win-x64, win-arm64 | libSkiaSharp, libHarfBuzzSharp, av_libglesv2 (ANGLE), SDL3 |
| linux-x64, linux-arm64 | libSkiaSharp, libHarfBuzzSharp, libSDL3 (X11 and Wayland come from the system) |
| osx-x64, osx-arm64 | libSkiaSharp, libHarfBuzzSharp, libAvaloniaNative, libSDL3 |

Native `.pdb` files from packages are stripped (`AvaFlightRemoveNativePdbs` target). Managed symbols are embedded. Each publish folder therefore contains exactly one file. See README.md for the commands and KNOWN_ISSUES.md for what has and has not been run.

## Tests

`tests/AVAFlight.Tests` uses xUnit v3 and `Avalonia.Headless.XUnit`. It contains 85 tests:

- **Core rules:** fuel, mass, calendar, flares, planet ranges, determinism.
- **Starport:** economy round trip, training, interest, artifacts, colony criteria.
- **Exploration:** cruise fuel, fuel exhaustion and tow, fluxes, a statistical sensor test, crushing gravity, vehicle capacity, walking home, ruins.
- **Diplomacy:** Spemin surrender, Velox postures, the Elowan/Thrynn feud, Mechan verification, translation bonuses, a statistical comm-skill test.
- **Combat:** destruction leads to game over, fleeing, Gazurtoid missile resistance, deterministic replay.
- **Story:** victory gating, Crystal Planet melt, Arth flare, the full scripted winning sequence, Classic vs Modern policy.
- **Infrastructure:** save round trip, permadeath across saves, migration, newer-schema rejection, corrupt saves, settings fallback, audio rendering and voice limits, no-device audio, input de-duplication and repeat, deadzone.
- **UI:**
  - all 17 documentation screenshots (each also a render smoke test)
  - screen navigation and window resizing
  - a real keyboard-event flow from the title to creating a crew member
  - held-Enter de-duplication
  - small-window scaling
