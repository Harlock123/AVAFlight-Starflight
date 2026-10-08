# Milestone status

Status as of 2026-10-08. "Verified" means it was checked by running tests, the app, or a published binary on this build machine (Linux arm64).

| # | Milestone | Status | What was verified |
|---|---|---|---|
| 1 | Research and specification | **Done** | Research dossier, source ledger, fidelity matrix, reference version, open questions, and data design (RESEARCH.md, FIDELITY.md, appendices A–C) |
| 2 | Skeleton and navigation proof of concept | **Done** | Four-project solution; title screen with Skia star field (seen live on Wayland); headless state serialization test; star-map destination selection and travel; single-file publish (win-x64 built; linux-arm64 run) |
| 3 | Starport and ship / crew setup | **Done** | All seven modules; component tiers from the documented price tables; training; crew stats; credit tracking (economy round-trip test; screenshots 02–04) |
| 4 | Hyperspace navigation and in-system view | **Done** | Fuel per coordinate, travel time, fluxes, nebulae, flares and encounters; star map with zoom, cursor and course; in-system flight and orbit; out-of-fuel distress tow (tests; screenshots 05–06) |
| 5 | Planet survey and terrain vehicle | **Done** | Sensors and analysis with skill-based certainty; Mercator landing map; vehicle movement and fuel; minerals with the 50 m³ limit; lifeforms; storms; ruins; return and launch (tests; screenshots 07–09) |
| 6 | Alien diplomacy | **Done** | 10 speaking or encounterable entities; postures, statements and topics; translation by comms skill with race bonuses; race protocols; trades; attitudes that persist in saves (tests; screenshot 10) |
| 7 | Combat | **Done** | Real-time fixed-step combat; shields, armour and hull; laser and missile selection; Uhlek plasma; missile resistance; flee; surrender; debris; crew injury; engineer repairs (tests; screenshot 11) |
| 8 | Story and progression | **Done** | Story flags; Orb / Cone / Egg / Nexus win path (full scripted playthrough test); McConnell's log; Thrynn deception about Elan; captain's log; victory screen (screenshots 12, 13, 15) |
| 9 | Full audio | **Done (Linux verified)** | 9 synthesised music cues with cross-fades and 19 effects; independent volumes; silent fallback (tests; live PipeWire stream observed) |
| 10 | Modern mode, accessibility, polish | **Done, except gamepad hardware testing** | Policy-driven Modern features; settings screen; key rebinding; font scale; text speed; high contrast; named save slots (screenshots 16–17). The gamepad code has not been tested with a physical controller. |
| 11 | Single-file publishing and final docs | **Done** | `publish.sh` / `publish.ps1` produce one file per target. All four required targets are published and smoke-tested on native GitHub runners via `.github/workflows/publish.yml` (run https://github.com/Harlock123/AVAFlight-Starflight/actions/runs/37804677552); linux-arm64 is also verified locally. All 17 screenshots are current. All docs written. 85/85 tests passing. |

## Test suite

```
dotnet test tests/AVAFlight.Tests
Passed!  - Failed: 0, Passed: 85, Skipped: 0, Total: 85
```

## Next concrete tasks

1. Play briefly on Windows and macOS with audio on (download the CI artifacts), and record the results here. CI already verifies start-up on all four targets.
2. Test a physical gamepad (hot-plug, D-pad and stick menus, deadzone).
3. Optional fidelity work: reproduce the original's 32 flux pairs and 70 nebulae if their positions are confirmed as publishable facts; the Velox probe quiz; minor artifacts if their DOS names can be verified.
