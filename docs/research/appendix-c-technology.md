# AVAFlight: Technical Research Report

Accessed 2026-10-08. All package versions were checked against nuget.org's flat-container index (`https://api.nuget.org/v3-flatcontainer/<id>/index.json`) and the nuspecs. API claims were checked against XML docs and binaries in the local NuGet cache (`~/.nuget/packages`), which already holds Avalonia 12.1.3.

---

## 0. Recommended package set (TL;DR)

| Purpose | Package ID | Version | License |
|---|---|---|---|
| UI framework | `Avalonia` | **12.1.3** (stable, 2026-09-22) | MIT |
| Desktop backends (Win32/X11/Native + Skia + HarfBuzz) | `Avalonia.Desktop` | 12.1.3 | MIT |
| Theme | `Avalonia.Themes.Fluent` or `Avalonia.Themes.Simple` | 12.1.3 | MIT |
| Skia drawing (transitive via Avalonia.Desktop) | `Avalonia.Skia` -> `SkiaSharp` **3.119.4**, `HarfBuzzSharp` 8.3.1.3 | 12.1.3 | MIT |
| Headless tests | `Avalonia.Headless.XUnit` (pulls `xunit.v3.extensibility.core` 3.2.2) | 12.1.3 | MIT |
| Audio + gamepad | `ppy.SDL3-CS` | **2026.1002.1** (bundles SDL 3.5.0, git ea7a2da) | bindings MIT; SDL3 native zlib |

Do NOT pin SkiaSharp explicitly to 4.x. SkiaSharp 4.153.x is on nuget, but Avalonia.Skia 12.1.3 depends on SkiaSharp 3.119.4. If you add a direct `SkiaSharp` reference, use **3.119.4**.

---

## 1. Avalonia

### 1.1 Which release is stable
- Avalonia **12.x is the current stable line.** 12.0.0 was published 2026-04-07, 12.1.0 on 2026-07-09, and **12.1.3 on 2026-09-22** (latest). The index shows no newer prerelease past 12.1.3.
- 11.3.x is still serviced (11.3.22, 2026-09-11) but is the maintenance line.
- Recommendation: **Avalonia 12.1.3** on `net10.0`. Avalonia 12 dropped .NET Framework and .NET Standard. It supports .NET 8+ and recommends .NET 10. The nuspec has `net8.0` and `net10.0` groups.

### 1.2 Avalonia 12 breaking changes that matter here
From https://docs.avaloniaui.net/docs/avalonia12-breaking-changes:
- **Skia is the only renderer.** Direct2D was removed.
- **Text shaping is now separate.** An app using `.UseSkia()` must also call **`.UseHarfBuzz()`**, which comes from the `Avalonia.HarfBuzz` package and is included via Avalonia.Desktop. `AppBuilder.Configure<App>().UsePlatformDetect()` covers this for desktop. Headless test builders need it explicitly.
- **Compiled bindings are on by default** (`AvaloniaUseCompiledBindingsByDefault=true`). `IBinding` was replaced by `BindingBase`. In C#, use `CompiledBinding`/`ReflectionBinding`.
- **TopLevel is not necessarily the visual root.** Use `TopLevel.GetTopLevel(visual)`. `IRenderRoot`, `IInputRoot` and `ILayoutRoot` were removed, and `GetPresentationSource()` was added.
- `SystemDecorations` became `WindowDecorations`, and `ExtendClientAreaChromeHints` was removed.
- `GotFocus/LostFocusEventArgs` became `FocusChangedEventArgs`. Gesture events moved to `InputElement`.
- **Animations stop on invisible controls** unless `Animation.PlaybackBehavior = Always`. This matters for game-loop-style animations.
- `Bitmap.CopyPixels()` no longer takes `AlphaFormat`, and `ILockedFramebuffer` has an `AlphaFormat` property. This matters if you blit through `WriteableBitmap`.
- `Avalonia.Diagnostics` was removed. Use `AvaloniaUI.DiagnosticsSupport` with `AttachDeveloperTools()`. The DevTools UI itself is part of the paid "Plus" tier (see 1.7). It is optional and not needed.
- **Headless testing now uses xUnit v3** (NUnit 4).
- Clipboard moved to `IAsyncDataTransfer`. Not relevant here.

### 1.3 SkiaSharp custom drawing in Avalonia 12
- **There is no `SKCanvasControl` in Avalonia.** That control belongs to `SkiaSharp.Views.*` for WPF/WinForms/MAUI. Avalonia core does not ship it. A string search of the Avalonia 12.1.3 assemblies and the official custom-rendering docs both confirm this.
- **Recommended pattern:** subclass `Control`, override `Render(DrawingContext)`, and call `context.Custom(new MyDrawOp(bounds, state))`. `MyDrawOp : ICustomDrawOperation` (namespace `Avalonia.Rendering.SceneGraph`) implements `Bounds`, `HitTest`, `Equals`, `Dispose` and `Render(ImmediateDrawingContext)`. Inside `Render`:
  ```csharp
  var lease = context.TryGetFeature<ISkiaSharpApiLeaseFeature>()?.Lease(); // Avalonia.Skia
  if (lease is null) return;               // non-Skia backend (e.g., UseHeadlessDrawing=true)
  using (lease) { var canvas = lease.SkCanvas; /* draw with SkiaSharp 3.x */ }
  ```
  To drive a game loop, call `InvalidateVisual()` from `TopLevel.RequestAnimationFrame(...)` or from a `DispatcherTimer`. Both `ISkiaSharpApiLeaseFeature` and `ISkiaSharpApiLease` exist in Avalonia.Skia 12.1.3. The API docs mark the lease as an "unstable API that may change in a future release".
- **Alternative (more portable and test-friendly):** render the game into an off-screen `SKBitmap`/`SKSurface` at native EGA resolution (e.g. 320x200), copy it into an Avalonia `WriteableBitmap` (`Lock()` -> memcpy), and draw that bitmap scaled with `RenderOptions.BitmapInterpolationMode="None"` for pixel-perfect output. This also works under `UseHeadlessDrawing=true`, and it keeps game rendering independent of Avalonia's rendering internals. **This hybrid is recommended for a retro 320x200 game:** draw the game into a fixed back-buffer, and let Avalonia handle only scaling and UI chrome.

### 1.4 Headless screenshot tests
- Packages: `Avalonia.Headless.XUnit` 12.1.3, which depends on `Avalonia.Headless` 12.1.3 and xunit v3. Add `Avalonia.Skia` and `Avalonia.HarfBuzz` 12.1.3, or `Avalonia.Desktop`, to the test project.
- Setup:
  ```csharp
  [assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]
  public class TestAppBuilder {
      public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
          .UseSkia().UseHarfBuzz()
          .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
  }
  // test
  [AvaloniaFact] public void Snap() { var w = new MainWindow(); w.Show();
      using var frame = w.CaptureRenderedFrame(); frame!.Save("out.png"); }
  ```
- XML docs confirm `HeadlessWindowExtensions.CaptureRenderedFrame(TopLevel)` ("Triggers a renderer timer tick and captures last rendered frame. Null, if nothing was rendered."). They also say about `UseHeadlessDrawing`: "Disable this option if you are using Avalonia.Skia or another drawing backend." With `UseHeadlessDrawing=true`, nothing renders and `ISkiaSharpApiLeaseFeature` is absent.
- xUnit v3 test projects are executables (`<OutputType>Exe</OutputType>`), and they use the xunit.v3 runner packages.

### 1.5 Single-file self-contained publish
These native libraries ship per RID, as checked in the local cache for Avalonia 12.1.3:

| RID | Native files |
|---|---|
| win-x64 | `libSkiaSharp.dll`, `libHarfBuzzSharp.dll`, `av_libglesv2.dll` (Avalonia.Angle.Windows.Natives 2.1.27548.20260419, via Avalonia.Win32) |
| linux-x64 | `libSkiaSharp.so`, `libHarfBuzzSharp.so` (X11 itself is a system dependency: libX11, libICE/libSM, fontconfig) |
| osx-x64 / osx-arm64 | `libSkiaSharp.dylib`, `libHarfBuzzSharp.dylib` (universal, `runtimes/osx`), `libAvaloniaNative.dylib` (Avalonia.Native, `runtimes/osx`) |
| + SDL3 | `SDL3.dll` / `libSDL3.so` / `libSDL3.dylib` from ppy.SDL3-CS |

- By default, a single-file publish bundles only managed DLLs and places native libraries **next to** the exe (Microsoft Learn, single-file overview). To get one file, set **`IncludeNativeLibrariesForSelfExtract=true`**. At startup the natives are extracted to `$DOTNET_BUNDLE_EXTRACT_BASE_DIR`, or `$HOME/.net` on Linux/macOS, or `%TEMP%\.net` on Windows, and they load from there. SkiaSharp, HarfBuzzSharp and SDL3-CS all use standard `DllImport`/`LibraryImport` probing, which finds the extracted files.
- Recommended csproj:
  ```xml
  <TargetFramework>net10.0</TargetFramework>
  <PublishSingleFile>true</PublishSingleFile>
  <SelfContained>true</SelfContained>
  <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
  <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile> <!-- optional; measure startup -->
  <DebugType>embedded</DebugType>
  <!-- avoid PublishTrimmed unless you add trimming roots; Avalonia + reflection bindings -->
  ```
  Publish with `dotnet publish -c Release -r <rid>` for win-x64, linux-x64, osx-x64 and osx-arm64.
- Caveats:
  - Under systemd or without `$HOME`, extraction fails unless `DOTNET_BUNDLE_EXTRACT_BASE_DIR` is set.
  - Do not use `Assembly.Location`. Use `AppContext.BaseDirectory`.
  - Load assets from embedded resources (`avares://`) so they don't depend on loose files.

### 1.6 macOS notes
- A bare single-file Mach-O works from the terminal. Without an `.app` bundle, though, the app gets no Dock icon or name, no Info.plist (no `NSHighResolutionCapable`), and it is awkward to launch from Finder.
- Gatekeeper quarantines unsigned downloaded binaries. Users must `xattr -dr com.apple.quarantine` the file or right-click and choose Open. Apple Silicon requires at least an ad-hoc signature (`codesign -s -`). The .NET SDK signs the apphost when building on macOS. Cross-built osx binaries from Linux/Windows may need `codesign` on a Mac.
- The dylibs extracted to `~/.net` are loaded unsigned. This works for ad-hoc and dev builds. Notarization needs a hardened runtime and signed natives, so a proper `.app` with natives in `Contents/MonoBundle` is the better route for distribution.
- `.app` bundling is **not required** for the stated goal. Note it as a later polish step. Tools include `dotnet-bundle` or a manual Info.plist layout per Avalonia's macOS deployment docs.

### 1.7 Licensing
- The packages above are all **MIT** (nuspec `<license type="expression">MIT</license>` on Avalonia 12.1.3, Avalonia.Skia, Avalonia.Headless.XUnit and Avalonia.Desktop).
- Commercial pieces, from avaloniaui.net/pricing:
  - **"Plus" tier** (~EUR 299/yr/seat): IDE tooling and Dev Tools. The Avalonia 12 DevTools replacement for `Avalonia.Diagnostics` lives here.
  - **"Pro" tier**: premium controls such as MediaPlayer, TreeDataGrid, Markdown, RichTextEditor, PDF and charts.
  - **Enterprise** and **Avalonia XPF**.
- AVAFlight needs none of these. The core framework remains MIT. `Avalonia.BuildServices` (a build-time dependency) collects anonymous build telemetry. Opt out with env var `AVALONIA_TELEMETRY_OPTOUT=1`. This is common knowledge for 11.x and was not re-verified for 12.

---

## 2. Audio options (.NET 10, cross-platform, single-file)

| Option | Package / version | Native RIDs shipped | License | Verdict |
|---|---|---|---|---|
| (a) OpenAL Soft via Silk.NET | `Silk.NET.OpenAL` 2.23.0 + `Silk.NET.OpenAL.Soft.Native` 1.23.1 | win-x64/x86/arm64 (`soft_oal.dll`), linux-x64/arm64/arm, osx-x64/arm64 | bindings MIT; **OpenAL Soft LGPL-2.0-or-later** (nuspec has `requireLicenseAcceptance=true`; upstream COPYING = LGPL v2 "Library GPL") | Works, but LGPL obligations (below). No gamepad. |
| (b1) SDL3 via ppy | **`ppy.SDL3-CS` 2026.1002.1** (SDL 3.5.0) | win-x64/x86/arm64, osx-x64, osx-arm64, linux-x64/x86/arm64/arm, ios, android-* | bindings **MIT**, SDL **zlib** | **Recommended.** Audio streams + gamepad + haptics. 609k downloads, used by osu!. |
| (b2) SDL3 via edwardgushchin | `SDL3-CS` 3.4.18 | none in the main package (no dependencies; natives are separate) | custom LICENSE file (check) | Alternative; more setup. |
| (b3) SDL2 via Silk.NET | `Silk.NET.SDL` 2.23.0 -> `Ultz.Native.SDL` 2.32.10 | multi-RID | MIT / zlib | SDL2 is legacy; Silk.NET 3 (SDL3) is not stable on nuget (latest stable 2.23.0). |
| (c) NAudio | `NAudio` 3.1.0 (MIT) | n/a | MIT | Output is Windows-only (WASAPI/WaveOut/DirectSound). `NAudio.Core` is useful only for mixing/sample providers. **Reject** for cross-platform. |
| (d) ManagedBass | `ManagedBass` 4.0.2 (wrapper) | you ship BASS yourself | BASS is **proprietary**: "free for non-commercial use", commercial EUR 950/product, shareware EUR 125 | **Reject.** Licensing friction, and no natives in the package. |
| (e) OpenTK audio | `OpenTK.Audio.OpenAL` 4.9.4 (stable); `OpenTK.Audio` 5.0.0-pre.16 only | no OpenAL native shipped (needs a system OpenAL or OpenAL Soft) | MIT bindings | Same LGPL issue as (a), plus you must source the natives. Reject. |

### LGPL obligations if OpenAL Soft is bundled (option a)
Under LGPL-2, you must allow users to relink or replace the library. With **dynamic linking** (a separate .so/.dll/.dylib), this is satisfied, but **single-file self-extract hides it inside the exe**. To stay compliant you would need to:
- ship the LGPL license text and an offer of the source (or a link to the exact OpenAL Soft version's source), and
- make replacement possible: exclude `soft_oal`/`libopenal` from the bundle with `ExcludeFromSingleFile` so it sits next to the exe, or document how users can override it. A bundled native library can be overridden by placing a library in the probing path, but this is fragile.

SDL3's zlib license has none of these obligations. It needs only no misrepresentation, and attribution is appreciated but not required in binaries.

### Native loading inside single-file
`ppy.SDL3-CS` uses `[LibraryImport("SDL3")]`. With `IncludeNativeLibrariesForSelfExtract=true`, the runtime extracts `SDL3.dll`/`libSDL3.so`/`libSDL3.dylib` to the extraction directory and the host's native-library probing finds it. No custom `NativeLibrary.SetDllImportResolver` is needed. Validate with a smoke test on each RID.

### Recommendation
Use **`ppy.SDL3-CS` 2026.1002.1**. Call `SDL_Init(SDL_INIT_AUDIO | SDL_INIT_GAMEPAD)` and **never** init `SDL_INIT_VIDEO`, because Avalonia owns the window. Open an `SDL_AudioStream` with `SDL_OpenAudioDeviceStream(SDL_AUDIO_DEVICE_DEFAULT_PLAYBACK, spec{F32, 2ch, 44100/48000}, callback)` and feed it from a managed mixer. Run SDL calls on a dedicated thread or the UI thread consistently.
- **Risk on macOS:** SDL's event and gamepad subsystem without video. Call `SDL_UpdateGamepads()`/`SDL_PumpEvents()` periodically, for example from the UI-thread game tick. Test on macOS early. If there are issues, set the hint `SDL_HINT_JOYSTICK_THREAD` (Windows) or poll on the main thread.
- ppy also publishes `ppy.SDL3_mixer-CS` (last seen 2026.x) if you want OGG/MP3 decoding and mixing. Otherwise decode OGG with a managed library such as NVorbis (MIT, not version-verified here) or synthesize PCM directly.

---

## 3. Gamepad
- **Avalonia 12 has no gamepad API.** Searching the Avalonia.Base 12.1.3 XML docs for "gamepad" finds only `KeyDeviceType.Gamepad` (a tag on key events the platform synthesizes) and `XYFocusNavigationModes.Gamepad` (D-pad focus navigation). There are no device, axis or button-polling APIs.
- **SDL3 gamepad (`SDL_OpenGamepad`, `SDL_GetGamepadAxis/Button`, `SDL_EVENT_GAMEPAD_ADDED/REMOVED`)** covers this, with built-in controller mappings (SDL_GameControllerDB). It is included in ppy.SDL3-CS. This is the recommended path, with one library for audio and input under the zlib license.

---

## 4. Fonts

| Font | License | Download URL (verified HTTP 200) | Notes |
|---|---|---|---|
| **Px437 IBM EGA 8x14** (VileR, Ultimate Oldschool PC Font Pack v2.2) | **CC BY-SA 4.0** (LICENSE.TXT in the zip = "Attribution-ShareAlike 4.0 International") | https://int10h.org/oldschool-pc-fonts/download/oldschool_pc_font_pack_v2.2_linux.zip -> `ttf - Px (pixel outline)/Px437_IBM_EGA_8x14.ttf` (also `PxPlus_IBM_EGA_8x14.ttf` with extended Unicode; `Mx437`/`Ac437` variants exist) | The most authentic EGA look. Attribution: credit "VileR" plus a link to https://int10h.org/oldschool-pc-fonts/. ShareAlike applies to the **font** and its modifications. Bundling the font unmodified in a game is generally treated as collection, not adaptation, so it does not relicense your code, but ship the font's LICENSE and keep it CC BY-SA. If you convert it to a bitmap atlas, that atlas is an adaptation and must stay CC BY-SA. |
| Press Start 2P | OFL 1.1 | https://raw.githubusercontent.com/google/fonts/main/ofl/pressstart2p/PressStart2P-Regular.ttf (+ `/OFL.txt`) | 8x8 arcade style |
| VT323 | OFL 1.1 | https://raw.githubusercontent.com/google/fonts/main/ofl/vt323/VT323-Regular.ttf (+ `/OFL.txt`) | DEC VT terminal; good for long text |
| Silkscreen | OFL 1.1 | https://raw.githubusercontent.com/google/fonts/main/ofl/silkscreen/Silkscreen-Regular.ttf, `Silkscreen-Bold.ttf` (+ `/OFL.txt`) | small caps-style pixel font |
| Pixelify Sans | OFL 1.1 | https://raw.githubusercontent.com/google/fonts/main/ofl/pixelifysans/PixelifySans%5Bwght%5D.ttf (+ `/OFL.txt`) | variable weight |
| IBM Plex Mono | OFL 1.1 | https://raw.githubusercontent.com/google/fonts/main/ofl/ibmplexmono/IBMPlexMono-Regular.ttf (+ `/OFL.txt`) | modern mono for debug/UI |

OFL obligations: ship OFL.txt with the font, don't sell the font by itself, and don't use the Reserved Font Name for modified versions. Embedding in a game is explicitly allowed.

Recommendation: for the cleanest licensing, use **VT323 or Press Start 2P (OFL)** as the default. Use Px437 IBM EGA 8x14 only if you accept CC BY-SA attribution and ShareAlike on the font file. Embed fonts as `AvaloniaResource` and reference them as `avares://AVAFlight/Assets/Fonts#Px437 IBM EGA 8x14`.

---

## 5. Audio assets (OpenGameArt, CC0 verified via each page's license field)

| Title | Author (OGA user) | Date | License | Page | Direct file |
|---|---|---|---|---|---|
| Outer Space Loop | wipics | 2020-06-03 | CC0 | https://opengameart.org/content/outer-space-loop | https://opengameart.org/sites/default/files/outer_space_2.mp3 (also outer_space_1.mp3) |
| Space Flight | wipics | 2020-06-03 | CC0 | https://opengameart.org/content/space-flight | https://opengameart.org/sites/default/files/space_flight_0.mp3 |
| Space Echo | centurionofwar (Centurion_of_war) | 2023-11-11 | CC0 | https://opengameart.org/content/space-echo | https://opengameart.org/sites/default/files/space_echo.ogg |
| Cosmic Navigation | synth-thetic | 2026-07-10 | CC0 | https://opengameart.org/content/cosmic-navigation | https://opengameart.org/sites/default/files/cosmic_navigation_loop.flac (+ cosmic_navigation.flac) |
| Space Music: Out There | yd | 2011-12-30 | CC0 | https://opengameart.org/content/space-music-out-there | https://opengameart.org/sites/default/files/OutThere_0.ogg |
| 63 Digital sound effects (lasers, phasers, space etc.) [SFX] | kenney | 2012-10-09 | CC0 | https://opengameart.org/content/63-digital-sound-effects-lasers-phasers-space-etc | https://opengameart.org/sites/default/files/Digital_SFX_Set.zip |

The "Sci-Fi Sound Effects Library" (little-robot-sound-factory) is **CC-BY 3.0, not CC0**. Excluded.

**Procedural vs. assets:** Starflight's original audio was PC-speaker beeps plus a short title theme, so procedural synthesis is simpler and more faithful.
- A ~200-line managed synth covers SFX: square/triangle/noise oscillators, ADSR, pitch sweeps for lasers, noise bursts for explosions, and blips for UI and scanners. Ambient drones are a few detuned sine/saw voices with slow LFO filters.
- This removes MP3/OGG/FLAC decoders (decoder licensing, size), asset attribution tracking, and repo bloat. It is also deterministic, which makes it unit-testable by hashing generated PCM.
- Recommendation: **synthesize everything procedurally**. Optionally add one or two CC0 ambient loops later (OGG via NVorbis). Note that the mp3/flac files above would need decoders.

---

## 6. Starflight-related projects and legal status

| Project | License | Contents | Reuse? |
|---|---|---|---|
| **s-macke/starflight-reverse** (GitHub, 236 stars, pushed 2026) | **No license file** (`license: null` via GitHub API), so all rights reserved by default | C disassembler/emulator (`src/`) for the Forth-based executable, plus **generated decompiled output** in `starflt1-out`/`starflt2-out`. It documents STARA.COM/STARB.COM directory formats (INSTANCE tree, VESSEL/ELEMENT/CREWMEMBER tables, etc.). Original game files are NOT included: `starflt1-in` holds only a placeholder, and users supply their own copies. README links to GOG for purchase. | **Do not copy code or output.** No license grant, and the decompiled output derives from EA's copyrighted code. You can **read it as documentation** of mechanics and formats. Facts and game rules are not copyrightable, but re-express them in your own code. A clean-room approach is advisable. |
| canadacow/starflight-reverse (fork), canadacow/StarflightUE | no license | fork / Unreal port | same as above |
| **mherbold/starflight** ("Starflight: The remaking of a legend", Unity, C#) | **Unlicense** (public-domain dedication) | Unity remake | Code is reusable under the Unlicense. Any assets in it that derive from EA's game (names, story text, art) are not covered by the author's dedication, so audit before reuse. |
| **Starflight: The Lost Colony** (Aeneas137/starflight-tlc; Starflight-The-Lost-Colony/tlc-remaster) | **GPL-3.0** | C++ (VC++ 2010, Allegro-era) fan sequel by Primeval Games (2010) | Copying its code into AVAFlight makes AVAFlight GPL-3.0. Use as reference only unless you go GPL. |
| henryjrobinson/starflight-clone (browser) | MIT | JS clone | reusable under MIT (verify assets) |
| Digitoxin1/Starflight2InteractiveMap | GPL-3.0 | SF2 star map | reference only |
| callmepartario/starflight-codex | no license | info codex | read-only reference |

**Original game data:** Starflight 1+2 is **sold on GOG** (https://www.gog.com/game/starflight_1_2, USD 5.99, publisher Electronic Arts, "(c) 1986, 2011 Electronic Arts Inc."). It is commercially available and copyrighted, so it is **not redistributable**. AVAFlight must not ship STARA/STARB/STARFLT.COM or extracted assets: graphics, alien portraits (CPIC), text and music. "Starflight" is also an EA mark, so use the name descriptively ("inspired by") rather than as branding. Optional legal route: an importer that reads a user-supplied GOG install at runtime, as starflight-reverse does.

---

## Source ledger

| URL | Accessed | What checked | License noted |
|---|---|---|---|
| https://api.nuget.org/v3-flatcontainer/avalonia/index.json (+ nuspec 12.1.3) | 2026-10-08 | latest 12.1.3; 11.3.22; deps | MIT |
| https://api.nuget.org/v3/registration5-gz-semver2/avalonia/index.json | 2026-10-08 | publish dates 12.0.0 (2026-04-07), 12.1.0 (2026-07-09), 12.1.3 (2026-09-22), 11.3.22 (2026-09-11) | - |
| https://api.nuget.org/v3-flatcontainer/avalonia.skia/12.1.3/avalonia.skia.nuspec | 2026-10-08 | SkiaSharp 3.119.4, HarfBuzzSharp 8.3.1.3 | MIT |
| https://api.nuget.org/v3-flatcontainer/avalonia.desktop/12.1.3/avalonia.desktop.nuspec | 2026-10-08 | Native/X11/Win32/HarfBuzz/Skia deps | MIT |
| https://api.nuget.org/v3-flatcontainer/avalonia.win32/12.1.3/avalonia.win32.nuspec | 2026-10-08 | Angle natives 2.1.27548.20260419 | MIT |
| https://api.nuget.org/v3-flatcontainer/avalonia.headless.xunit/12.1.3/avalonia.headless.xunit.nuspec | 2026-10-08 | xunit.v3.extensibility.core 3.2.2 | MIT |
| ~/.nuget/packages/avalonia*/12.1.3 (XML docs + binaries) | 2026-10-08 | ICustomDrawOperation, ISkiaSharpApiLeaseFeature, CaptureRenderedFrame, UseHeadlessDrawing remark, UseHarfBuzz, gamepad absence, native RIDs | - |
| https://api.nuget.org/v3-flatcontainer/skiasharp/index.json | 2026-10-08 | 4.153.1 exists; Avalonia uses 3.119.4 | MIT |
| https://docs.avaloniaui.net/docs/avalonia12-breaking-changes | 2026-10-08 | breaking changes list | - |
| https://docs.avaloniaui.net/docs/graphics-animation/custom-rendering | 2026-10-08 | ICustomDrawOperation + lease pattern; no SKCanvasControl (page samples still cite 11.2 package versions) | - |
| https://api-docs.avaloniaui.net/docs/T_Avalonia_Skia_ISkiaSharpApiLeaseFeature | 2026-10-08 (search snippet) | "unstable API" note | - |
| https://avaloniaui.net/pricing | 2026-10-08 | MIT core; Plus/Pro/Enterprise/XPF commercial | MIT + commercial add-ons |
| https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview | 2026-10-08 | IncludeNativeLibrariesForSelfExtract, extraction dirs, API incompatibilities | - |
| https://api.nuget.org/v3-flatcontainer/ppy.sdl3-cs/2026.1002.1/ (nuspec + nupkg) | 2026-10-08 | version, RIDs, bundled SDL 3.5.0 (ea7a2da) | MIT (bindings) |
| https://github.com/ppy/SDL3-CS | 2026-10-08 | scope, platforms, sister packages | MIT |
| https://github.com/libsdl-org/SDL/blob/main/LICENSE.txt | 2026-10-08 | SDL license | zlib |
| https://api.nuget.org/v3-flatcontainer/sdl3-cs/3.4.18/sdl3-cs.nuspec | 2026-10-08 | no deps/natives | LICENSE file |
| https://api.nuget.org/v3-flatcontainer/silk.net.sdl/2.23.0/silk.net.sdl.nuspec | 2026-10-08 | SDL2 via Ultz.Native.SDL 2.32.10 | MIT |
| https://api.nuget.org/v3-flatcontainer/silk.net.openal.soft.native/1.23.1/ (nuspec + nupkg) | 2026-10-08 | RIDs, requireLicenseAcceptance | LGPL-2.0-or-later |
| https://github.com/kcat/openal-soft/blob/master/COPYING | 2026-10-08 | LGPL v2 | LGPL-2 |
| https://api.nuget.org/v3-flatcontainer/opentk.audio.openal/index.json, opentk.audio | 2026-10-08 | 4.9.4 stable; 5.0.0-pre.16 | MIT |
| https://api.nuget.org/v3-flatcontainer/naudio/3.1.0/naudio.nuspec | 2026-10-08 | 3.1.0; Windows-specific output packages | MIT |
| https://api.nuget.org/v3-flatcontainer/managedbass/index.json | 2026-10-08 | 4.0.2 | wrapper LICENSE.md |
| https://www.un4seen.com/bass.html | 2026-10-08 | BASS licensing | proprietary; free non-commercial |
| https://int10h.org/oldschool-pc-fonts/readme/ + download zip v2.2 (LICENSE.TXT, file list) | 2026-10-08 | font names, attribution | CC BY-SA 4.0 |
| https://raw.githubusercontent.com/google/fonts/main/ofl/{pressstart2p,vt323,silkscreen,pixelifysans,ibmplexmono}/... | 2026-10-08 | TTF + OFL.txt URLs return 200 | OFL 1.1 |
| https://opengameart.org/content/outer-space-loop | 2026-10-08 | license field, file, author | CC0 |
| https://opengameart.org/content/space-flight | 2026-10-08 | same | CC0 |
| https://opengameart.org/content/space-echo | 2026-10-08 | same | CC0 |
| https://opengameart.org/content/cosmic-navigation | 2026-10-08 | same | CC0 |
| https://opengameart.org/content/space-music-out-there | 2026-10-08 | same | CC0 |
| https://opengameart.org/content/63-digital-sound-effects-lasers-phasers-space-etc | 2026-10-08 | same | CC0 |
| https://opengameart.org/content/sci-fi-sound-effects-library | 2026-10-08 | excluded | CC-BY 3.0 |
| https://github.com/s-macke/starflight-reverse (GitHub API repo + README + contents) | 2026-10-08 | no license; contents; GOG link | none (all rights reserved) |
| https://github.com/mherbold/starflight (API + README) | 2026-10-08 | Unity C# remake | Unlicense |
| https://github.com/Aeneas137/starflight-tlc, https://github.com/Starflight-The-Lost-Colony/tlc-remaster | 2026-10-08 | TLC source | GPL-3.0 |
| GitHub search "starflight" (gh search repos) | 2026-10-08 | other projects and licenses | various |
| https://www.gog.com/game/starflight_1_2 | 2026-10-08 | on sale, EA copyright | commercial (EA) |
