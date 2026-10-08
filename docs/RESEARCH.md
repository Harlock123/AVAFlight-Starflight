# AVAFlight research dossier

Research was carried out on 2026-10-08. This file summarises it. The full working notes, each with its own detailed ledger, are in the appendices:

- [Appendix A: game mechanics](research/appendix-a-mechanics.md). Covers starting state, crew, ship, navigation, planets, combat, economy, time and UI.
- [Appendix B: aliens, communication, story, ports and history](research/appendix-b-races-story-ports.md)
- [Appendix C: technology and licensing](research/appendix-c-technology.md). Covers Avalonia, audio, gamepad, fonts, assets and open-source projects.

> **Honesty note.** We did not search "all resources". This document records what we actually opened. Several important sites were blocked from the research environment; see *Coverage gaps* below.

## 1. Summary of findings

- **Release and history**
  - Starflight was released for IBM PC DOS on **15 Aug 1986** by Binary Systems and Electronic Arts. It was written in Forth, with assembly routines.
  - The team was Rod McConnell (founder), Greg Johnson (lead design and story), Alec Kercso, Tim Lee and Bob Gonsalves. Joe Ybarra produced for EA, and Paul Reiche III advised on the conversation postures.
  - Development ran from late 1982 to 1986, about 3.5 years. The brief's "~5 years" is not supported by any source we found.
  - **No "Bob Mattes" is credited in any source we checked.** The name is probably a confusion with Bob Gonsalves.
- **Universe size.** One seed generated the universe, which the designers then tuned by hand. It has **270 star systems and 811 planets**, confirmed both by an interview and by the decompiled data. Coordinates run x 2–249 and y 0–217.
- **Starting state** (manual and in-game notice):
  - **12,000 M.U.**, not 10,000.
  - A class 1 engine and 20 m³ of Endurium.
  - An unnamed ship, with no crew, pods, armour or weapons.
  - The date is 01-01-4620.
  - You cannot launch until every crew post is filled, the ship is christened, engines and fuel are aboard, and you have visited Operations.
- **Crew**
  - There are five races (Human, Velox, Thrynn, Elowan, Android) and six posts.
  - Aptitude sets starting and maximum skill: Excellent 50/250, Good 30/200, Average 10/150, Poor 0/100.
  - Training costs **300 M.U. per session** and adds the race's learning rate. Androids cannot be trained.
  - **No source documents experience-based skill growth.** Skills improve only through training.
- **Ship.** Five component types (engine, shield, armour, missile, laser), each in classes 1–5.
  - Prices come from the manual's class 1 and class 5 endpoints plus the decompiled price table for the middle classes.
  - Cargo pods hold 50 m³, cost 500 M.U., and up to 16 can be fitted.
  - Mass and acceleration follow the decompiled formula: the starting ship is 100 t at 5 G, the maximum is 500 t.
  - Hyperspace fuel use is **0.48 down to 0.16 m³ per coordinate** depending on engine class (manual).
  - Landing or launch costs 0.25 m³ per G. Landing above 8 G crushes the ship.
- **Economy**
  - Mineral values run from Lead 40 to Rodnium 440 M.U./m³, with Endurium at 1,000 (manual).
  - Endurium rises to 1,500 and then 2,000 through dated notices.
  - The bank pays 12% simple interest per 300-day year. Artifact analysis costs 500 M.U.
  - Colony recommendations follow published criteria.
- **Time.** The calendar has ten 30-day months, so a year is 300 days. Each star has a flare date, and Arth's star flares on day 300. Being inside a system on its flare day is fatal.
- **Aliens**
  - The talking races are Elowan, Thrynn, Velox, Spemin and Mechan 9.
  - The hostile races are Gazurtoid and Uhlek; the Uhlek never talk.
  - The Minstrels speak in verse, Nomad probes transmit survey data, and one unnamed ship transmits only binary.
  - Communication uses three postures (Friendly, Hostile, Obsequious), statements, and five question topics.
  - Translation quality depends on the Communications officer, with +25 if any crew member shares the alien's race and +50 if the officer does.
  - Replies depend on the situation, then are picked at random from a pool.
- **Story**
  - The flares are caused by the Ancients' **Crystal Planet** at 192,152, which moves outward from the core.
  - The **Ancients are the Endurium itself.**
  - To win:
    1. Take the Crystal Orb from Sphexi (132,165).
    2. Destroy the Uhlek mind world at 55,32 with a Black Egg.
    3. Take the Crystal Cone from 20,198.
    4. Plant a second Black Egg at the Crystal Planet's Nexus of Control and leave.
  - The reward is 500,000 M.U.
- **Combat** runs in real time. The ship computer chooses lasers at short range and missiles at long range. Gazurtoid ships resist missiles. You escape by flying off-screen. The detailed numbers are fan measurements.
- **Audio.** The DOS version plays a PC-speaker theme credited to Jeff Lubeck, plus sound effects; Ctrl-S toggles sound.
- **Display.** The 1986 DOS release offered B/W, RGB (CGA), Composite and Hercules modes. EGA came in a later revision.

## 2. Reference version

See [FIDELITY.md](FIDELITY.md) for the decision and the full matrix.

In short:

- **Baseline:** the **IBM PC DOS release, using its CGA-era design.** Mechanics also draw on the EGA-capable revision that the decompiled data comes from, because no source documents a gameplay difference between the two builds.
- **Not used as the baseline:**
  - **Genesis (1991):** made by BlueSky Software, not Binary Systems as the brief suggested.
  - **Amiga / C64 (1989) and Atari ST / Mac (1990).**
  - **Starflight 2 (1989).**
- **Genesis-only details** are labelled in data and docs wherever they were used to fill a DOS gap: two artifact locations, and the City of the Ancients coordinates.

## 3. Consolidated source ledger

All sources were accessed on 2026-10-08. The *Confidence* column applies to the claims we draw from each source.

| # | Title | Author / org | URL | Type | Platform | What was inspected | Claims supported | Confidence | Licence / reuse |
|---|---|---|---|---|---|---|---|---|---|
| 1 | starflight-reverse | Sebastian Macke | https://github.com/s-macke/starflight-reverse | Decompiled DOS code and extracted data | DOS (EGA-capable build) | README; `instance.txt` (dialogue, systems, notices); star table; overlays CONFIG, SHIPGRPH, PERSONNEL, BANK, HYPER(MSG), COMM(SPEC), GAME, MUSIC, CHKFLIGHT | 270/811; prices; mass and acceleration; fuel; 300/session; 12% interest; calendar; flare logic; dialogue content; game-over texts | High for data; medium-high for derived formulas | **No licence (all rights reserved), and it contains EA's copyrighted text.** Used only as a reference for facts. No code, data or text is copied. |
| 2 | Starflight manual (1986) | EA / Binary Systems | https://archive.org/details/starflight-manual ; https://www.mocagh.org/ea/starflight-manual.pdf | Original manual scan | IBM PC | Full OCR | Starting money; races and skills; prices; energy chart; minerals; UI; comms rules; colony criteria; timeline | High | © EA. Facts only. |
| 3 | IBM reference card | EA | https://www.mocagh.org/ea/starflight-refcard.pdf | Card scan | IBM PC | Setup, display modes, keys, code wheel | Controls, real time, Ctrl-S sound, display modes | High | © EA |
| 4 | Multi-platform manual and Amiga cards | EA | https://archive.org/details/STARFLIGHT ; mocagh alt-manual / alt-refcard / advpak refcard | Scans | Amiga / ST / Mac / C64 / IBM | Platform notes, credits, mineral chart | Port differences; Jeff Lubeck theme credit | High | © EA |
| 5 | Starflight (Wikipedia, incl. talk page) | Wikipedia editors | https://en.wikipedia.org/wiki/Starflight | Encyclopedia | All | Article and talk page | Dates, team, Forth, Genesis by BlueSky, EGA re-release anecdotes | Medium-high | CC BY-SA |
| 6 | Starflight 2 (Wikipedia) | Wikipedia editors | https://en.wikipedia.org/wiki/Starflight_2:_Trade_Routes_of_the_Cloud_Nebula | Encyclopedia | DOS / Amiga / Mac | Summary | SF2-only content to exclude | Medium | CC BY-SA |
| 7 | The Digital Antiquarian: "Starflight" | Jimmy Maher | https://www.filfre.net/2014/10/starflight/ | Historical essay | DOS | Fetched summary | Team, 811 planets, Velox prefer obsequious address | Medium-high | © author |
| 8 | "The Making of Starflight" (oral history) | Time Extension, 2026 | https://www.timeextension.com/features/the-making-of-starflight-dont-let-me-die-until-this-game-is-out-an-oral-history-of-the-trailblazing-space-sandbox-sim | Interviews | DOS / C64 / Genesis | Fetched summary | Roles, timeline, EDL attitude variable, 270/811 | Medium-high | © publisher |
| 9 | Starflight Resource Pages (starflt.com, via Wayback) | Fan site | http://www.starflt.com/tables/index.php/starflight1/ | Fan tables | DOS | Misc, combat analysis, minerals, fluxes, artifacts, walkthrough (Wirsz), beat-game steps | Fuel measurements; combat numbers; notice dates; 500,000 reward; Thrynn 6× artifact price | Medium (empirical) | © site. Facts only. |
| 10 | Contemporary BBS review | Unknown | https://mirror.cyberbits.eu/textfiles.com/games/REVIEWS/starflt.rev | Review | IBM PC / Amiga | First pages | 12,000 credits; bank interest; code wheel | Medium | Unknown |
| 11 | CRPG Addict: Starflight | Chester Bolingbroke | http://crpgaddict.blogspot.com/search/label/Starflight | Playthrough blog | DOS (EGA) | Summary | Flare hint; UI friction | Medium | © author |
| 12 | Sega-16 Karpp interview and Genesis review | Sega-16 | https://www.sega-16.com/2005/03/interview-richard-karpp/ | Interview / review | Genesis | Summaries | Genesis developer (BlueSky); content parity | Medium | © publisher |
| 13 | RetroAchievements Starflight guide | Community | https://github-wiki-see.page/m/RetroAchievements/guides/wiki/Starflight | Fan guide | **Genesis** | Summary | Some artifact coordinates (labelled Genesis) | Medium; Genesis only | Unclear |
| 14 | rpggamers.com walkthrough | Bob Seljan | https://rpggamers.com/walkthrough/starflight | Walkthrough | Probably PC | Summary | Orb, Cone and Egg locations; Uhlek requirement | Medium | © author |
| 15 | nuget.org package indexes and nuspecs | Microsoft / NuGet | https://api.nuget.org/v3-flatcontainer/ | Package metadata | — | Version lists, dependencies, native RIDs | Pinned versions | High | Per package |
| 16 | Avalonia 12.1.3 XML docs and binaries | AvaloniaUI | ~/.nuget/packages/avalonia*/12.1.3 | API docs | — | Custom draw, Skia lease, headless capture, gamepad absence | Rendering approach | High | MIT |
| 17 | ppy/SDL3-CS and SDL 3 | ppy / libsdl.org | https://github.com/ppy/SDL3-CS | Bindings | — | Package contents, API | Audio and gamepad backend | High | MIT / zlib |
| 18 | Google Fonts repository | Google / font authors | https://github.com/google/fonts/tree/main/ofl | Fonts | — | TTF and OFL files | Fonts used | High | SIL OFL 1.1 |

Sources that were **tried but blocked or unavailable**:

- MobyGames (HTTP 403)
- GameFAQs (403)
- starflight.fandom.com (402)
- segaretro.org (403)
- GameBanshee retrospective interview (403)
- hardcoregaming101.net (404 at the URL tried)
- the live starflt.com site (Cloudflare)

Contemporary magazine reviews (CGW, Compute!, etc.) were **not read directly**. No longplay videos were watched: video was not accessible from the research environment. We found no ROM or binary analysis beyond source 1.

## 4. Disagreements between sources

1. **Starting money:** the manual, the in-game notice and a 1986 review all say 12,000. The brief says 10,000. **We use 12,000.**
2. **Planet count:** the data and an interview give 811; Wikipedia gives 800 (rounded). **We use 811.**
3. **Fuel per coordinate:** the manual gives 0.48–0.16. The code ratio (7 − class) predicts 0.48/0.40/0.32/0.24/0.16. Fans measured 0.16/0.25/0.33/0.41/0.49. **We use the manual-consistent linear form.**
4. **Noah 2 vs Noah 9:** the manual says Arth was settled by Noah 2. The game data says Heaven was the Noah 9 target. Wikipedia merges the two. **We keep them distinct.**
5. **Rod Device location:** DOS dialogue points to the pirate Harrison at 81,98. A Genesis guide says 180,124. **We use DOS (81,98).**
6. **Elan:** the Thrynn call it an Uhlek base; the Elowan call it their nursery. **We model the Thrynn claim as a lie with consequences.**
7. **Bob Gonsalves' role:** Wikipedia says sound; Time Extension says exploration and combat programming. Not relevant to gameplay.
8. **Arth flare day:** the data gives flare date 300, which decodes to 01-01-4621. The Elowan say "the final week of ten-month". These are consistent at the day-300 boundary.

## 5. Open questions

- The behaviour when Arth's star flares while the ship is elsewhere (we end the game; marked as a reconstruction).
- Exact combat internals: AI, shield recharge, and the damage model as implemented in the COMBAT overlay. We use fan measurements plus reconstruction.
- The planet and fractal generator, the lifeform generator, and storm damage formulas. These are reconstructed.
- The lifeform price formula, artifact sale values, and colony reward and fine amounts. Fan figures or reconstruction.
- The fuel purchase price at the Trade Depot. Reconstruction: the current Endurium market price.
- The full DOS minor-artifact name list, which is not in the plain-text data. Names such as "bladed toy" are unverified and **not used**.
- Whether "Mysterion" is the name of the binary-speaking ship. We call it "Unknown vessel".
- The exact console pixel layout per display mode.
- DOS revision numbers and dates.

## 6. Data-design decisions

AVAFlight's content data lives in `src/AVAFlight.Core/Data/Json/*.json` and is validated on load (`GameData.Validate`):

- `world.json`: documented anchor systems, race territories, documented fluxes, nebulae.
- `ship.json`: component tiers.
- `crew.json`: crew races.
- `minerals.json`
- `races.json`: alien races and newly written dialogue.
- `artifacts.json`
- `messages.json`: newly written surface messages.
- `notices.json`: dated notices.

Each entry carries an evidence tag. The rest of the galaxy (about 245 systems) is generated from a fixed seed to the documented totals, so the original star map is not copied.

Encounter tables are in code (`NavigationService.RollEncounter`) because their rates are reconstructions. Fleet sizes and ship statistics are per race in `races.json`.
