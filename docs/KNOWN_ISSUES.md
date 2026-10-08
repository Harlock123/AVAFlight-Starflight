# Known issues, caveats and deferred items

## Verification limits (please read)

- **Build machine.** Everything was built and tested on **Linux arm64** (an Arch VM with a Wayland desktop). No x86-64 emulator or Windows/macOS machine was available.
  - **linux-arm64** single-file build: run and verified. `--smoke-test` reached the main menu and exited 0. The bundled `libSkiaSharp`, `libHarfBuzzSharp` and `libSDL3` were extracted and loaded. The debug build was also run interactively on the desktop, and PipeWire showed its SDL3 audio stream.
  - **win-x64, linux-x64, osx-x64, osx-arm64** single-file builds: **published and smoke-tested on native GitHub-hosted runners** (windows-latest, ubuntu-latest with Xvfb, macos-15-intel, macos-latest) by `.github/workflows/publish.yml`. Each reached the main menu, wrote the `AVAFLIGHT_SMOKE_OK` marker and exited 0 (run https://github.com/Harlock123/AVAFlight-Starflight/actions/runs/37804677552). CI runs with `--mute`, because runners have no audio device; interactive play with audio on Windows and macOS has not been checked by a person yet.
- **Screenshots.** The brief asks for screenshots captured on Windows. They were rendered on Linux by Avalonia's headless Skia renderer, which uses the same Skia drawing code as the desktop app. Fonts are embedded, so text looks the same on every OS. Window chrome is not included.
- **Gamepad.** The SDL3 gamepad code builds, initialises, and handles a missing gamepad, but **no physical gamepad was available for testing.**
- **Audio.** Audio output was confirmed only on Linux (PipeWire). The synthesis and mixer are covered by unit tests.

## macOS

- The output is a bare executable, not a `.app` bundle. It is not signed or notarised, so Gatekeeper will warn. Run it from Terminal, or use `xattr -d com.apple.quarantine AVAFlight`.
- A distributable `.app` (Info.plist, icon, codesign, notarisation) is deferred.

## Linux

- The UI runs through X11 (XWayland on Wayland desktops). The system needs the usual X11/fontconfig libraries. SDL3 is bundled.
- Tiling window managers ignore the 1024×640 minimum size. AVAFlight then scales the UI down uniformly, so very small tiles are readable but tiny.

## Historical fidelity gaps

See FIDELITY.md for the full matrix. The main **reconstructed** (not evidence-based) elements are:

- **Star map.** The non-story galaxy layout is generated, not copied from the original (a legal choice). As a result:
  - only 8 of the original's 32 flux pairs exist (the documented ones)
  - there are 18 nebulae instead of 70
  - per-star flare dates are generated, except Arth and the authored systems
- **Arth's flare.** Game over when Arth flares while you are elsewhere is a reconstruction. The original behaviour is unverified.
- **Travel pacing.** The game-hours-per-coordinate value is a reconstruction.
- **Planet terrain.** The fractal algorithm, lifeform generation, hostility rates, storm rates and lifeform prices are reconstructions.
- **Terrain vehicle timing.** The vehicle uses a step-based time model; the original is real time.
- **Combat AI.** Shield recharge and alien behaviour are reconstructed. Damage figures are fan measurements.
- **Fuel price.** The Trade Depot sells fuel at the current Endurium market price (reconstruction).
- **Simplifications:**
  - The Velox probe quiz and tribute demands are simplified.
  - The Black Box has no effect.
  - The binary-speaking ship is named "Unknown vessel".
- **Minor artifacts.** Small artifacts named in some FAQs ("bladed toy" etc.) are not in AVAFlight. Their DOS names could not be verified.
- **Deliberate omissions.** The copy-protection code wheel and the Interstel police stop are not implemented.
- **Starport.** The walk-around concourse is replaced by a module menu. This is a friction-removal change; all services and numbers are kept.

## Accessibility

- High contrast applies to text, menus and panels. The Skia scene views (space, terrain, combat) keep the EGA palette.
- The font scale applies to all text-based UI. Canvas labels inside Skia views do not scale.

## Project

- AVAFlight is released under the MIT licence (`LICENSE`). All third-party components are permissive (MIT, zlib, OFL, Apache-2.0); the bundled fonts keep their OFL licences (`licenses/`).

## Deferred

- Interactive play with audio on Windows and macOS (CI smoke tests cover start-up only).
- A macOS `.app` bundle and signing.
- A Windows icon and version resource.
- Gamepad button remapping. Keyboard remapping is implemented; gamepad bindings use defaults.
