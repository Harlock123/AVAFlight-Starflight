# Third-party notices and asset provenance

AVAFlight's own code, data, text and procedurally generated assets are released under the **MIT licence** (`LICENSE`). Third-party components keep their own licences, listed below.

AVAFlight is an **unofficial fan recreation**. It is not affiliated with, endorsed by or published by Electronic Arts or Binary Systems. "Starflight" is used only to describe the game that inspired AVAFlight.

**No original Starflight code, data files, text, artwork, music or sound is included.**

| Original material | How AVAFlight treats it |
|---|---|
| Rules and numbers | Implemented from documented facts (the manual, published walkthroughs, public analyses) |
| Story locations | Coordinates stated in walkthroughs and in-game hints are used as facts |
| Star map | Generated, not copied |
| Dialogue, messages, notices and the briefing | Newly written paraphrases of the documented content |
| Graphics and audio | Created procedurally |

Starflight 1+2 is still sold by GOG (© Electronic Arts). AVAFlight does not need, read or redistribute any original game files.

## Dependencies (NuGet)

Versions are pinned in `Directory.Packages.props` and were verified on nuget.org on 2026-10-08.

| Package | Version | Licence | Use | Single-file notes |
|---|---|---|---|---|
| Avalonia, Avalonia.Desktop, Avalonia.Skia, Avalonia.Themes.Fluent | 12.1.3 | MIT | UI framework (pulls Avalonia.Win32 / X11 / Native / HarfBuzz) | Native libs bundled via IncludeNativeLibrariesForSelfExtract |
| SkiaSharp (+ NativeAssets) | 3.119.4 | MIT | 2D rendering | libSkiaSharp is bundled and extracted at first run |
| HarfBuzzSharp (+ NativeAssets) | 8.3.1.3 | MIT | Text shaping | libHarfBuzzSharp is bundled |
| ppy.SDL3-CS | 2026.1002.1 | Bindings MIT; SDL 3 zlib | Audio output and gamepad input | SDL3 native lib is bundled for win/linux/osx (x64 and arm64) |
| Avalonia.Headless, Avalonia.Headless.XUnit | 12.1.3 | MIT | Tests only | — |
| xunit.v3 | 3.2.2 | Apache-2.0 | Tests only | — |
| xunit.runner.visualstudio | 3.1.5 | Apache-2.0 | Tests only | — |
| Microsoft.NET.Test.Sdk | 18.10.1 | MIT | Tests only | — |

SDL is © Sam Lantinga and contributors, under the zlib licence. It is used unmodified.

## Fonts

All fonts are under the **SIL Open Font License 1.1**. The licence texts are in `licenses/` and are also embedded with the fonts. Downloaded from https://github.com/google/fonts (`main` branch) on 2026-10-08.

| Font | File | SHA-256 | Copyright | Use |
|---|---|---|---|---|
| VT323 | VT323-Regular.ttf | cf4de751ada78ceac033dbe16a687742939995b77bc2a052ae17a4957958594d | © 2011 The VT323 Project Authors | Data displays, menus, terminal text |
| Press Start 2P | PressStart2P-Regular.ttf | 034c77f1f05ec89421e4a63f0e3a4ca1ecf852cc6d2bf611f126f275728e017d | © 2012 The Press Start 2P Project Authors (Reserved Font Name "Press Start 2P") | Titles and headings |
| IBM Plex Sans | IBMPlexSans.ttf (variable) | 3b031aa4216174205bd8471f88a49b91f093169e9e87bd5262242bc5967fe2e3 | © 2017 IBM Corp. (Reserved Font Name "Plex") | Dialogue and story prose |
| IBM Plex Mono | IBMPlexMono-Regular.ttf | 6a3412f058c7d8dfd9170c41e85ade48e5156ecb89356110ca57a0a27734af46 | © 2017 IBM Corp. (Reserved Font Name "Plex") | Reserved for monospace UI |

We considered VileR's "Px437 IBM EGA" font (CC BY-SA 4.0, a closer match to the EGA character set). We chose OFL fonts to avoid ShareAlike obligations on derived atlases.

## Asset provenance

| Asset | Source | Licence |
|---|---|---|
| All music (9 cues) | Newly composed, synthesised at runtime (`Infrastructure/Audio/SoundBank.cs`) | AVAFlight (MIT, see LICENSE) |
| All sound effects (19) | Synthesised at runtime | AVAFlight (MIT, see LICENSE) |
| Star fields, planets, terrain, ships, alien portraits, UI art | Drawn procedurally with SkiaSharp (`Avalonia/Rendering/*`) | AVAFlight (MIT, see LICENSE) |
| Alien dialogue, messages, notices, briefing | Newly written (`Core/Data/Json/*.json`) | AVAFlight (MIT, see LICENSE) |
| Galaxy layout (non-story systems) | Generated from a fixed seed | AVAFlight (MIT, see LICENSE) |
| EGA 16-colour palette values | Standard IBM hardware palette (factual) | — |

**Sampled audio we considered but did not use:**

- CC0 tracks on OpenGameArt by wipics, centurionofwar, synth-thetic and yd, and Kenney's Digital SFX. See Appendix C of RESEARCH.md.
- The OpenGameArt "Sci-Fi Sound Effects Library" (CC-BY 3.0).

Procedural synthesis was simpler, deterministic and testable, and closer to the original's PC-speaker character.

## Reference projects (not used as code)

| Project | Licence | Status |
|---|---|---|
| s-macke/starflight-reverse | **No licence** (all rights reserved; contains EA material) | Consulted for **facts only**; no code or data copied |
| mherbold/starflight (Unity) | Unlicense | Not used |
| Starflight: The Lost Colony | GPL-3.0 | Not used: copying would require AVAFlight to be GPL |
