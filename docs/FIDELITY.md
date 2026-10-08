# Fidelity baseline and matrix

## Reference version

**Baseline: Starflight for IBM PC DOS, released 15 Aug 1986 by Binary Systems and Electronic Arts.**

- **Primary evidence.** The 1986 manual and reference card (CGA, Hercules and composite display era), plus the decompiled data of an EGA-capable DOS revision (source 1 in RESEARCH.md).
- **Mixing the two builds.** No source documents a gameplay difference between the original DOS build and the EGA revision. The talk-page anecdotes about shields working in nebulae in EGA are unconfirmed. We therefore take numbers from both and follow the 1986 manual wherever the two could differ.
- **Exact revision numbers are unknown** (see KNOWN_ISSUES.md).

**Not used as the baseline:**

| Version | Year | Why it is excluded |
|---|---|---|
| Amiga / C64 | 1989 | Mouse UI, auto mineral pickup, no Scan/Look on C64, no code wheel |
| Atari ST / Mac | 1990 | Later ports |
| Sega Genesis | 1991 | Developed by **BlueSky Software**. Redesigned UI and TV upgrades. |
| Starflight 2 | 1989 | A sequel: trade economy, Shyneum fuel, different races |

Where AVAFlight had to borrow a location from a Genesis guide to fill a DOS gap, the data entry says so. This applies to the City of the Ancients at 56,144 and the Dodecahedron at 118,146.

## Evidence labels

These labels are used in data files (`evidence` fields), code comments and this document.

| Label | Meaning |
|---|---|
| **documented** | Stated by the manual, an Interstel notice, in-game text, or the decompiled data |
| **derived** | Worked out from decompiled code (e.g. the mass formula) |
| **fan-measured** | Empirical measurements by the fan community |
| **reconstruction** | No evidence. A designed value chosen to fit the documented behaviour. |
| **modern addition** | A Modern-mode convenience. It does not exist in the original. |

## Fidelity matrix

| System | Original behaviour | Evidence | AVAFlight implementation | Classic | Modern | Verification | Uncertainty |
|---|---|---|---|---|---|---|---|
| Starting state | 12,000 M.U., class 1 engine, 20 m³ Endurium, unnamed ship, date 01-01-4620 | documented (manual + notice) | `GameSession.NewGame` | Same | Same | `NewGame_HasDocumentedStartingState` | — |
| Pre-flight check | Crew assigned, ship named, engines, fuel, Operations visit | documented (CHKFLIGHT texts) | `StarportService.PreflightProblems` | Same | Same | `Launch_IsBlockedUntilPreflightChecksPass` | — |
| Starport layout | You walk a figure between six modules and the docking bay | documented | A direct module menu replaces the walk-around | Menu | Menu + tooltips | Screenshot 02 | Interface change only (friction removal) |
| Crew races and skills | 5 races; aptitude start/max values; durability; learning rate | documented (manual table) | `crew.json` | Same | Same + skill tooltips | `GameDataTests` | Race skill tables were not found in the code, so this rests on the manual |
| Training | +learning rate per session, 300 M.U., capped; Androids untrainable | documented | `CrewService.Train` | Same | Same | `Training_*`, `Androids_CannotBeTrained` | — |
| Experience | No documented experience growth | absence of evidence | None (training only) | — | — | — | If experience gain existed, it is not modelled |
| Skill effects | Science: scan certainty. Nav: weapon accuracy and fluxes above 150. Comms: translation. Medicine: healing. Engineering: repairs | documented (manual) | `Skill()` and per-service uses, with efficiency = skill/250 (fan model) | Same | Same | `ScienceSkill_*`, `CommSkill_*` | The exact formulas are a fan model |
| Crew death | Permanent; the next most capable member covers the post; all dead = game over | documented | `CrewService.Injure`; `GameSession.Skill` fallback | Same | Same | `CrewDeath_PersistsAcrossSaveLoad` | — |
| Component prices | 5 types × 5 classes | documented (manual endpoints + code table) | `ship.json` | Same | Same | `EconomyRoundTrip_*` | Assigning the middle-class rows to part types is an inference (endpoints match) |
| Mass / acceleration | 50 + engine 50 + 9·armour² + 10·pods + 5 per system; accel = engine × 500 / mass | derived | `ShipRules.Mass/Acceleration` | Same | Same | `MassAndAcceleration_MatchDerivedFormula` | Field mapping inferred |
| Shields / armour / weapons strength | Shield 500/class, armour 250/class, laser 90–660, missile 200–1500 | fan-measured | `ship.json` strengths; `CombatService` | Same | Same | Combat tests | Medium |
| Resale value | Always lower than the price, except possibly pods | documented (qualitative) | 50% trade-in; pods 100% | Same | Same | Round-trip test | The 50% figure is a reconstruction |
| Repair cost | Dry dock: hull ~1,000, comms/sensors ~10,000, others ~1.25× price | fan-measured | `StarportService.RepairCost` | Same | Same | — | Medium |
| Hyperspace fuel | 0.48 → 0.16 m³ per coordinate by engine class; Tesseract halves it | documented / fan | `ShipRules.FuelPerCoordinate` | Same | Same + range ring, range row and low-fuel warning (**modern addition**) | `FuelPerCoordinate_*`, `FuelCost_ForKnownRoute_ArthToSol` | Fan measurements differ slightly (see RESEARCH §4) |
| Travel time | Real-time movement on the map; clock runs | documented (qualitative) | Time-stepped: 1–3 game hours per coordinate by acceleration | Same | Same | `Cruise_*` | Pacing is a **reconstruction** |
| Cruise / destination | Cursor-key flight with cruise control; MAP overlay shows distance and fuel | documented | Manual flight plus a straight-line cruise to a star-map cursor (no pathing) | Same | Same + waypoints and notes (**modern addition**) | `Cruise_*` | Straight cruise is the original cruise control, not an autopilot |
| Fluxes | Paired wormholes; visible with navigation > 150 or the Ring Device; position lost afterwards | documented | 8 documented pairs from notices and dialogue; position loss and re-fix | Same | Same | `Flux_TeleportsToPairedEndpoint` | The original had 32 pairs; the other 24 are not reproduced |
| Nebulae | Shields don't work inside | documented | 18 nebula discs; shields drop | Same | Same | — | The original had 70 nebulae; positions not copied |
| Star map | 270 systems / 811 planets, fixed for every game | documented | Story anchors at documented coordinates, plus a seeded generator to the same totals | Same | Same | `Galaxy_HasDocumentedTotals_AndIsDeterministic` | **Reconstruction:** the original layout is not copied (legal choice) |
| Flares | Per-star flare dates; dead zone already flared; inside a flaring system = death | documented / derived | `StoryService.ApplyFlareSchedule`; daily checks | Same | Same | `StellarFlareSchedule_*` | Per-star dates are reconstructed except Arth (day 300) and the authored systems |
| Arth flare | Arth's star flares at day 300 | documented (data + Elowan hint) | Game over at day 300 unless won, wherever you are | Same | Same | `ArthFlare_EndsGame_UnlessWon` | **Reconstruction:** the original's behaviour when you are away from Arth is unknown |
| Out of fuel | Distress call; towed home in stasis for a fee | documented | `StarportService.Distress` (1,000 + 150 per coordinate) | Same | Same | `FuelExhaustion_*` | The fee formula is a reconstruction |
| Orbit survey | Sensors and analysis; "not certain" at low science skill | documented | `PlanetService.Sensors/Analyze` | Same | Same | Screenshot 07 | Reveal probability is reconstructed |
| Landing | Choose a site on a map; 0.25 m³ per G each way; > 8 G crushes the ship | documented | `PlanetService.Land/Launch` | Same | Same | `Landing_OnCrushingGravity_IsFatal` | — |
| Terrain | Fractal planets; TV with 50 m³ hold; fuel per step, more at altitude; walk home if empty | documented | Fractal value-noise grid, 96×48 cells | Same | Same | TV tests | The terrain algorithm is **reconstructed**, not the original fractal |
| Terrain time model | Real time | documented | **Step-based** (one world step per move or wait) with held-key repeat | Same | Same | — | **Reconstruction** (keeps it deterministic) |
| Minerals | 21 minerals plus Endurium at manual prices; denser at altitude | documented | `minerals.json`; deposits weighted toward altitude | Same | Same | Round trip; carry-limit test | Rarity weights are reconstructed |
| Lifeforms | Stun, capture (not flyers), or record bio-data; duplicates rejected | documented | `PlanetService.Fire/Capture/ScanLife` | Same | Same | — | Value formula and hostility rates are reconstructed |
| Ruins | Ancient ruins hold Endurium and restock (PC) | fan | Ruin sites restock after 48 h | Same | Same | `Ruins_YieldEndurium` | Restock timing is reconstructed |
| Weather | Storms injure crew; navigation < 200 can get lost | documented | `PlanetService.Weather` | Same | Same | — | Rates are reconstructed |
| Alien roster | Elowan, Thrynn, Velox, Spemin, Mechan 9, Gazurtoid, Uhlek, Minstrels, Nomad, binary-speaking ship | documented (data) | `races.json` | Same | Same | Diplomacy tests | Name of the binary-speaking ship is unknown |
| Interstel police | Copy-protection stop (code wheel) | documented | **Not implemented.** It is copy protection, not gameplay. | — | — | — | Deliberate omission |
| Comms postures and topics | Friendly / Hostile / Obsequious; statements vs questions; 5 topics | documented | `CommsService` | Same | Same + attitude label and tooltips (**modern addition**) | Diplomacy tests | — |
| Dialogue | State-dependent, then random from a pool; translation by skill | documented | Pools with attitude, surrender and flag gating; random pick; word garbling | Same | Same | `CommSkill_*` | **All text newly written**; attitude weights are reconstructed |
| Race specifics | Elowan/Thrynn feud; Velox obsequious, tribute, Small Egg war; Spemin surrender under pressure; Mechan verification and human check; Gazurtoid missile-proof; Uhlek silent | documented | Implemented | Same | Same | Diplomacy and combat tests | Velox probe quiz and tribute demands are simplified |
| Trading | Thrynn buy artifacts (fan: 6×) and plutonium, and sell the "invulnerability device"; Elowan emergency fuel | documented / fan | `CommsService.Offers` | Same | Same | — | Black Box effect unknown (implemented as no effect) |
| Combat | Real time; nose-aimed weapons; computer picks laser or missile; shields and armour; flee off-screen; surrender | documented | `CombatService`, fixed 30 Hz steps | Same | Same | `ShipDestruction_*`, `Fleeing_*`, determinism | AI behaviour is reconstructed |
| Story | Orb → Uhlek mind → Cone → Egg at Nexus; McConnell's log; 500,000 bonus; play continues | documented | Flags plus `PlanetService.Detonate` and `StoryService.TryWin` | Same | Same + automatic captain's log (**modern addition**) | `FullWinningSequence_*`, `Victory_CannotBeTriggered_*` | Uhlek difficulty before the mind world falls is reconstructed |
| Bank | 12% simple interest per year, credited on docking; last 10 transactions | documented | `StarportService.CreditInterest` | Same | Same | `BankInterest_*` | — |
| Colony recommendation | Published criteria; rewards; fines | documented / fan | 35,000 + 5,000 × score; 5,000 fine | Same | Same | `ColonyCriteria_FollowManual` | Amounts are fan figures or reconstructed |
| Fuel purchase price | Unknown | none | Equal to the current Endurium price | Same | Same | — | **Reconstruction** |
| Saving | A single save per game; Esc menu | documented | One slot per ship | One slot | **Named multiple slots** (modern addition) | Save tests | — |
| Music and sound | PC-speaker theme (Jeff Lubeck) and effects | documented | **Newly composed and synthesised** chiptune per game state | Same | Same | Audio tests | Original audio deliberately not reproduced |
| Visuals | CGA / EGA, four-part console | documented | EGA palette and four-part console layout, at modern resolution | Same | Same | Screenshots | Exact pixel layout unknown |
| Code wheel | Copy protection at launch | documented | Not implemented | — | — | — | Deliberate omission |

## Classic vs Modern

Both presets run **the same engine, data, prices, fuel costs, alien behaviour and difficulty**. The preset can be changed during a game from the Options menu. All differences live in one place, `Policy` in `src/AVAFlight.Core/Model/Rules.cs`:

| Feature | Classic | Modern |
|---|---|---|
| Captain's log | Mission events and manually logged planets only | Records discoveries, contacts, artifacts and navigation automatically |
| Star-map waypoints and notes | — | ✓ (W to place with a note, X to remove) |
| Fuel range ring, range row, low-fuel warning, out-of-range warning | Distance, fuel and time readout only (as in the original MAP overlay) | ✓ |
| Tooltips on crew stats, components and status rows | — | ✓ |
| Pause hotkey (P) outside real-time combat | Options menu only (the original's Esc menu) | ✓ |
| Beginner hints (can be turned off permanently) | — | ✓ |
| Save slots | One per ship | Multiple named slots |

**Shared by both presets** (accessibility, not balance):

- adjustable dialogue text speed
- font scale
- high contrast
- remappable keys
- gamepad support
- borderless fullscreen

Modern mode deliberately **does not** add:

- quest markers or objective arrows
- autopilot routing
- crafting, weapon modding or skill trees
- online features
- any balance changes
