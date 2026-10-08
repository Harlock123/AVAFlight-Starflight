# STARFLIGHT (1986, Binary Systems / Electronic Arts): mechanics research, IBM PC DOS focus

Accessed 2026-10-08. Everything below comes from sources I actually opened (see the Source Ledger, IDs S1–S14). The confidence tags mean:
- **[M]**: printed in the original manual (archive.org scan/OCR). This is the primary source.
- **[C]**: read directly from the s-macke/starflight-reverse decompiled DOS code or its data tables. "Derived" means I worked it out from the code, and the reasoning is shown.
- **[F]**: fan-site claim (starflt.com and others). Many of these are empirical measurements.
- **[D]**: 1984 Ambient Designs/Binary Systems design documents. These are **pre-release** and often differ from the shipped game.
- **[W]**: Wikipedia or a secondary article.

Where the decompiled code and the manual agree independently, I mark the item **VERIFIED x2**.

---

## 0. Version / platform notes

| Platform | Date | Source | Notes |
|---|---|---|---|
| IBM PC (DOS) | 15 Aug 1986 | [W] S1; "August 1986" [W] S4 | Original release used CGA. The IBM card in the manual offers Black/White, RGB, Color TV/Composite and Hercules (no EGA) [M] S3. |
| DOS "EGA" re-release | not dated ("a couple of years later") | Wikipedia Talk S2; search snippet | Wikipedia Talk editors describe album-box CGA/Hercules copies, a later sticker adding Tandy/CGA, and a conventional box with EGA/Tandy. They report missing animations in EGA mode (title planet, docking-bay doors) and possible gameplay differences. The decompiled build's display menu includes **"5. EGA"** [C], so the reverse-engineered build is an EGA-capable revision (probably the GOG files; the repo does not state which build). |
| Tandy | unclear | S2 | Tandy support offers "RGB" or "Color TV or Composite". Whether Tandy support existed at launch is unresolved. |
| Amiga, Commodore 64 | 1989 | [W] S1 | The multi-platform manual has Amiga/Atari ST/Mac mouse options and C64 joystick notes [M] S3b. |
| Atari ST, Mac | 1990 | [W] S1 | |
| Sega Genesis | NA 7 Oct 1991, EU 9 Oct 1991 | [W] S1 | archive.org also holds "Starflight Rev 1 (1991)". |

Version gameplay differences reported by fans [F] S7 (Wirsz walkthrough):
- Amiga and Genesis have no launch code wheel.
- Amiga has auto mineral pickup.
- "Ancient ruins stocked with fuel" on Arth-system planets is described as "PC version only".
- In the Amiga version you can talk your way past the Gazurtoid at 68,66 with obsequious posture. The PC version tends to end in combat.
- Walkthrough: "If you are running the EGA version… recommend [123,101] for colonization (35,000 credits)."

Team (sources disagree on some roles; see Disagreements):
- Rod McConnell (founder, Binary Systems / Ambient Design).
- Greg Johnson (lead designer).
- Alec Kercso (programming; starport module per S4).
- Tim Lee (graphics/programming; globe rendering and low-level code).
- Bob Gonsalves: "sound" per Wikipedia, "planetside exploration module" programmer per Digital Antiquarian.
- Also per S4: Dave Boulton (early Forth programmer, fractal planets) and Paul Reiche III (conversation-system consultant).
- Producer Joe Ybarra (EA) [W].
- Written in Forth [C][W].

---

## 1. Starting state at Starport (Arth)

- **Starting money: 12,000 MU** ("monetary units", mu/M.U.).
  - VERIFIED x2: the manual cover letter says "Your initial allotment of 12,000 mu's is enough to allow you to buy four cargo pods, and to gather and train a crew" [M] S3, S3b, S3c.
  - The first in-game Operations notice text in the instance data: "IN YOUR BANK ACCOUNT YOU WILL FIND THE AMOUNT OF 12,000 MONETARY UNITS (M.U.)" [C] S5.
  - The 1986-era review S9 also says 12,000. **The commonly cited 10,000 is wrong.**
- **Initially owned:** "one Class 1 engine and 20 cubic meters of endurium". The ship has no name, and you must name it ("Unchristened ships may not leave Starport") [M].
  - No crew, no pods, no armor, shields or weapons. That follows from "won't need to do anything… except name it" and from the CHKFLIGHT strings [C].
  - Code ship naming prefixes "ISS " ("SHIP NAME: ISS ") [C].
- **Pre-flight check** (CHKFLIGHT-OV strings) [C] blocks launch until you have:
  - assigned crew ("REPORT TO CREW ASSIGNMENT")
  - christened the ship
  - purchased engines ("REPORT TO SHIP-CONFIGURATION")
  - purchased fuel ("REPORT TO TRADE DEPOT TO PURCHASE FUEL")
  - reported to Operations for evaluation.
- **Starport layout:** six modules plus the docking bay, around an "amphitheater". You walk a character onto a module's entry mat and press a key [M].
  1. **Operations**: Notices and Evaluation (colony recommendations, fines).
  2. **Trade Depot**: Buy, Sell, Analyze. Artifact analysis costs **500 MU** ("ANALYSIS IS 500 MU") [C]. The manual says analysis is "for a fee".
  3. **Personnel**: Create, Train, Delete. Training money is not refunded on delete.
  4. **Crew Assignment**.
  5. **Bank**: last 10 transactions and balance. No credit purchases allowed.
  6. **Ship Configuration**: Buy, Sell, Repair, Name.
  7. **Docking Bay**: launch. The security code wheel prompt asks for location + artifact + race and you read a code off the wheel [M].
- **Starting date:** the game starts on stardate 0, which displays as **01-01-4620** (notices dated "Starport 01-01-4620" [F] S7). The manual cover letter is dated 30-10-4619 [M].
- **Initial notice** (verbatim, from the instance data [C]):
  - lists eight objectives
  - warns to avoid 135,84
  - mentions minerals in the mountains of the innermost Arth-system planet
  - mentions ruins at 17N x 162E on planet 2 of the neighboring K-class system
  - mentions alien activity at 175,94.
- **Later notices** include:
  - the sun-instability notice, which enables the stellar-condition sensor and colony-recommendation drones
  - flux link pairs 128,105–146,112; 126,87–173,88; 148,166–170,93; 104,82–118,107
  - Endurium price rises to **1,500** then **2,000 MU/m³**
  - joke notices (Borno/Xenon).
- Fan dates for the price rises: 20-02-4620 (1000→1500) and 15-05-4620 (1500→2000) [F] S6. Other notice dates [F] S7: 02-01-4620 (sun unstable), 03-01-4620 (androids), 18-02-4620 (ship lost at 192,152).
- **Story gating:** notices appear over time. Mechan cooperation requires a Human aboard plus correct answers to their code questions [F] S7. Win reward is "500,000 richer" [F] S8. There is no hard lock on exploration.

## 2. Crew

**Six posts:** Captain, Science Officer, Navigator, Engineer, Communications, Doctor [M][C].

**Aptitude → skill table** [M], VERIFIED by fan arithmetic:

| Aptitude | Initial | Max |
|---|---|---|
| Excellent | 50 | 250 |
| Good | 30 | 200 |
| Average | 10 | 150 |
| Poor | 0 | 100 |

**Race table** [M] (Sci / Nav / Eng / Comm / Med):

| Race | Durability | Learning rate | Sci | Nav | Eng | Comm | Med |
|---|---|---|---|---|---|---|---|
| Human | 6 | 9 | Exc | Good | Good | Good | Good |
| Velox | 8 | 6 | Good | Exc | Exc | Poor | Poor |
| Thrynn | 6 | 7 | Good | Good | Good | Exc | Poor |
| Elowan | 2 | 10 | Avg | Good | Avg | Exc | Exc |
| Android | 10 | 0 | fixed 50 | fixed 150 | fixed 100 | fixed 0 | fixed 20 |

- Androids can't be trained ("ANDROIDS CAN'T BE TRAINED") [C][M].
- Don't mix Elowan and Thrynn: having one aboard blocks useful communication with the other race [M].
- **Training:** each session adds skill equal to the race's learning rate [M]. Cost is **300 MU per session** ("COST: 300 MU/SESSION") [C].
  - Fan totals match exactly: Human science 50→250 = 23 sessions = 6,900. Velox navigation 50→250 = 34 sessions = 10,200 [F] S7.
  - A cap message appears at max ("MAXIMUM TRAINING LEVEL HAS ALREADY BEEN ATTAINED").
- **Skill effects** [M]:
  - **Science.** Sensor and analysis completeness/accuracy. Above 150, detects aliens at long range.
  - **Navigation.**
    - Above 150, sees continuum fluxes; at 150 or below cannot see them, and may hit one by accident.
    - Determines the time to re-fix position after a flux.
    - Sets weapon accuracy (the Navigator fires weapons).
    - Below 200, the terrain vehicle can get lost in storms.
  - **Engineering.** Repair speed/efficiency. Longer repairs are more likely to require repair minerals.
  - **Communications.** Percentage of alien speech translated. +25 if any crew member is of the alien's race, +50 if the comms officer is.
  - **Medicine.** Treatment speed. Natural healing also scales with the Doctor's skill.
  - **Captain.** Combined skills raise the ship's "apparent power" (up to double). If a crew member dies, the next most capable crew member takes over the post.
- Fan model [F] S6, speculative ("from game testing"): efficiency = skill/250 (0→0%, 50→20% … 250→100%). Science: chance each analysis field is filled in, otherwise "NOT CERTAIN". Navigation: per-hour chance to re-fix position after a flux.
- **Crew records** show Vitality % and Durability [C].
  - Doctor status strings: Dead, Critically, Heavily, Moderately, Slightly, Not wounded.
  - Storms injure or kill crew (STORM-OV "IS INJURED"/"KILLED").
  - All crew dead → "!!CREW DECEASED!! **GAME OVER**" [C].
  - Replacement means creating new crew files at Personnel. Dead crew stay on file marked "A DEAD CREWMEMBER" [C].

## 3. Ship configuration

**Prices** [M], VERIFIED x2. CONFIG-OV holds a price table (×100 MU, class 5 down to class 1): 1000,400,200,80,10 | 1250,700,320,120,40 | 250,125,62,31,15 | 2000,1200,600,280,120 | 1500,900,540,200,80 [C]. Assigning the rows to parts is my inference: each row's endpoints match the manual exactly, and the class-4 engine price matches the fan figure of 40,000.

| Part | Class 1 | Class 2 | Class 3 | Class 4 | Class 5 | Notes |
|---|---|---|---|---|---|---|
| Engines | 1,000 | 8,000 | 20,000 | 40,000 | 100,000 | Higher class = better fuel economy and acceleration [M] |
| Shields | 4,000 | 12,000 | 32,000 | 70,000 | 125,000 | Use energy; don't work in nebulas; repairable; recharge during encounters [M] |
| Armor | 1,500 | 3,100 | 6,200 | 12,500 | 25,000 | Heavy; slows ship; can't be repaired, must be replaced; works in nebulas [M] |
| Missile launcher | 12,000 | 28,000 | 60,000 | 120,000 | 200,000 | Long range, can be dodged, 3× laser damage, 5× laser energy [M] |
| Laser cannon | 8,000 | 20,000 | 54,000 | 90,000 | 150,000 | Short range, cannot be dodged [M] |

(The manual gives only the class 1 and class 5 prices; the middle columns come from the code table [C].)

- **Cargo pods:** 50 m³ each, **500 MU** [M]. Up to **16** pods ("No. of pods (0-16)" [D]; fans buy "16 cargo pods (8000 credits)" [F]).
  - The terrain-vehicle hold is 50 m³ [M].
  - Selling: depreciation starts at purchase, so resale is always lower, "with the possible exception of cargo pods" [M]. The repair cost string is "COST TO REPAIR ENTIRE SHIP" [C].
- **Mass and acceleration** [C, derived from SHIPGRPH-OV `(.MASS)` and `(.ACC)`]:
  - **mass (tons) = 50 + 50·[engine present] + 9·(armor class)² + 10·(number of pods) + 5·(shield>0) + 5·(missile>0) + 5·(laser>0)**
  - **acceleration (G) = engine class × 500 / mass** (integer)
  - Starting ship: 100 t, 5 G. Maximum loadout: 50+50+225+160+15 = **500 t**.
  - This matches the 1984 design ranges "Mass 100-500 tons, Accel 1-25 g" [D]. Mapping the record fields to armor, engine and pods is inferred from the formula's behavior, so treat it as high but not absolute confidence.
- **Fuel = Endurium**, in m³ (stored internally as tenths, `10*END`) [C].
  - **Hyperspace burn rate [C, derived]:** per-tick usage `E-USE = 7 − engine class` (6…2), halved if a specific artifact is held. Fans say the **Tesseract** "doubles fuel efficiency (after analysed)" [F] S6.
  - The manual gives **0.48 to 0.16 m³ per coordinate travelled (by engine class)** [M]. That is the same 3:1 ratio as 6:2, i.e. ≈0.08 × (7 − class). Fans measured **0.16 / 0.25 / 0.33 / 0.41 / 0.49** [F] S6.
  - Fuel use does **not** depend on mass in the code path I read: it uses the engine class only.
  - Other energy use [M]: laser shot 0.01 m³; missile shot 0.05 m³; shields up 0.1 m³ per star-hour; launch or land 0.25 m³ per G of planet gravity.
  - Low-fuel warnings fire when remaining fuel is under 150× and 75× the per-tick use [C, derived from `(-ENDURIUM)`].
- **Damage systems** [C]: Hull, Engines, Sensors, Comm, Shields, Missile launcher, Laser cannon.
  - Percent damage equals the percent chance the section fails outright [M].
  - The Engineer repairs in flight, and long repairs may need repair minerals [M]. Repair minerals: Cobalt, Molybdenum, Aluminum, Titanium, Promethium [M].
  - Dry-dock repair costs fan-claimed ~1.25× the part price; comm/sensors 10,000; hull 1,000 [F] S6b.
- **Ship's Status** (Science → Status) [M][C]: date (Day.Hour-Month-Year), damage, cargo % full, energy (endurium m³), shields up/down, weapons armed/unarmed.

## 4. Navigation

- **Map:** **270 star systems, 811 planets** [C], counted from the decompiled star table (S5 `data/starsystem.h`).
  - The instance tree also has 270 STARSYSTEM and 811 PLANET objects. Wikipedia says "270… total of 800" [W]. Digital Antiquarian says 811 [W].
  - Star coordinates span **x 2–249, y 0–217** [C], i.e. a grid of about 250×220.
  - 0–8 planets per system (distribution: 0:33, 1:34, 2:52, 3:46, 4:46, 5:26, 6:18, 7:7, 8:8).
  - Each star has 8 orbit slots [M].
  - Spectral mix: F 79, M 55, G 53, K 33, B 22, A 19, O 9 [C].
- **Arth** is at **125,100**, a G-class system with 5 planets [C]. Arth is planet 2 [F]; you can't land on it ("WE CAN'T LAND ON ARTH") [C]. Starport is a station in Arth orbit.
- **Display levels** [M]: Hyperspace, Star Approach (system), Planet Approach (orbit), Tactical (encounters).
  - Coordinates are shown above the main view.
  - Enter orbit by pressing a key over the planet's center. Leave via Navigator → Maneuver. Leave a system by flying past its edge.
- **Continuum fluxes:** paired wormholes. Fans list 32 pairs (64 endpoints); the instance tree has 64 FLUX-NODE objects [C][F] S6c.
  - After a jump you are "lost" until the navigator re-fixes position.
  - The Ring Device artifact reveals fluxes for weak navigators [M][F].
- **Nebulae:** 70 NEBULA objects [C]. Shields don't work in nebulas [M]; fans say the EGA build breaks this rule (S2).
- **Flares:** each star has a flare date (see §8). Upon entering a system, the stellar sensor reports one of [C]:
  - "STABLE" (or "(POST-FLARE)")
  - "SLIGHTLY UNSTABLE" (more than 60 days left)
  - "UNSTABLE – ESTIMATED TIME TO FLARE: n ARTH DAYS" (60 days or fewer).
  The analysis only applies if the flare is 1–399 days away.
- **Encounters** happen at any level, including hyperspace, and drop you into tactical. You exit by flying until the aliens are off-screen [M].
- **Out of fuel / distress:** Comms → Distress launches a drone. You are towed home in stasis for a stiff, distance-based fee [M]. Operations shows "TOWING CHARGES" [C].
- **Cruise control:** hold a cursor key plus Insert [M card].

## 5. Planetside / Terrain Vehicle

- **From orbit:**
  - Sensors give mass, bio %, mineral %, atmosphere, hydrosphere and lithosphere [M].
  - Analysis gives orbit number, predominant surface, gravity, atmosphere (and density), temperature range and global weather [M][C].
  - Landing on more than **8.0 G** crushes the hull [M].
- **Landing:** Captain → Land → Select Site (cursor on a Mercator/topographic map, with lat/long) → Descend (autopilot) [M]. Minerals are denser at high altitude; lifeforms cluster in temperate lowlands [M].
- **TV menu:** MAPS, MOVE, CARGO, LOOK, SCAN, WEAPON, ICONS [C][M].
  - Maps have three zoom levels.
  - Scan works on lifeforms only.
  - Weapons are a laser and a stunner.
  - TV hold is 50 m³.
  - The TV has a small reserve that lasts "5 to 25 steps" depending on terrain; efficiency drops with altitude [M].
  - If the TV runs out of fuel, the crew returns on foot ("OUT OF FUEL", "RETURNING TO SHIP ON FOOT") [C].
  - Re-entering the ship refuels the TV and moves its cargo to the ship [C].
  - A lost TV is charged at Operations ("LOSS OF TERRAIN VEHICLE") [C].
- **Lifeforms:** stun, then capture a specimen (stasis) or record bio-data [C][M].
  - Flying or floating lifeforms can't be captured [M].
  - Duplicates are rejected ("DUPLICATE SPECIMEN", "LIFEFORM ALREADY RECORDED") [C].
- **Ruins:** ancient and recent. Messages count as cargo [M]. Fans say ancient ruins hold endurium and restock when you leave orbit and return (PC) [F].
- **Mineral values (MU per m³)** [M], VERIFIED by the fan table:

| Mineral | Value | Mineral | Value |
|---|---|---|---|
| Lead | 40 | Chromium | 260 |
| Iron | 60 | Antimony | 280 |
| Cobalt\* | 80 | Promethium\* | 300 |
| Nickel | 100 | Mercury | 320 |
| Copper | 120 | Tungsten | 340 |
| Zinc | 140 | Silver | 360 |
| Molybdenum\* | 160 | Gold | 380 |
| Tin | 180 | Platinum | 400 |
| Magnesium | 200 | Plutonium | 420 |
| Aluminum\* | 220 | Rodnium | 440 |
| Titanium\* | 240 | Endurium | 1000 |

  \* repair minerals. Endurium's price later rises to 1500, then 2000 (notices).
- **Colony criteria** [M]:
  - some temperatures in the acceptable band
  - gravity below 2.0 G (0.7–1.3 optimal)
  - atmosphere containing oxygen
  - some free water
  - weather not violent or very violent.
  Fines apply for bad recommendations [M]. Fan reward figures are 35,000–55,000 per good world [F].

## 6. Combat

- **Real time:** "Starflight runs in real time" [M card]. Pressing + pauses messages for up to 2 minutes [M].
- **Steps:** Navigator raises shields and arms weapons (arming takes time: [D] says 6 s), then Combat → aim by rotating the nose and fire with Space [M].
  - The ship computer picks lasers or missiles by range.
  - Fans say lasers are used within about 6 squares and missiles beyond [F].
- **Aliens** can scan whether your shields are up and weapons armed, and read these as hostile [M]. Repeated combat with a race lowers the chance of friendly relations later.
  - Some ships are immune to a weapon type: Gazurtoid shrug off missiles [M appx D][C dialogue].
  - Aliens can surrender if you stop firing and hail [M].
  - Pick up debris afterwards via Captain → Cargo [M].
- **Fan measurements** [F] S6b, empirical:
  - player shields 500 points per class; armor about 250 per class, with base hull 250
  - damage reduction 0/0/20/40/60/80% for class 0–5
  - laser damage by class 90/210/360/510/660
  - missile damage by class 200/400/700/1000/1500
  - laser damage falls 5% per 0.5 unit of distance
  - alien shields 200 per class and don't regenerate
  - enemy shields scale up the more of that race you kill
  - Uhlek plasma bolts do 4000, are homing and move at double speed.

## 7. Economy

Money sources [M]:
- minerals (table above)
- artifacts, valued on analysis; sold artifacts can be bought back
- lifeform specimens or recordings. Specimens are worth more. Value scales with new species, intelligence, food-chain niche, size and distance from Arth. Duplicates aren't bought.
- colony recommendations, with fines for bad ones
- salvage/debris.

Prices and rates:
- **Bank interest: 12%** ("( INTEREST RATE: 12% )") [C].
  - Code (`INT%`) [C, derived]: balance × 12/100 × (days since last dock)/300, credited when you dock. That is simple interest at 12% per 300-day game year.
  - The 1986 review S9 confirms "the Bank (which pays interest!)".
- **Endurium price inflation** comes via notices: 1000 → 1500 → 2000 MU/m³ [C][F].
- **Artifact analysis:** 500 MU [C].
- **Thrynn** pay 6× Starport for artifacts, 12× after the second fuel-price rise [F].
- **Recruiting** costs nothing per the manual. Only training costs money (300/session).

## 8. Time, scoring, endings

- **Calendar** [C, derived from `.STARDATE`]:
  - the stardate is a day counter
  - year = 4620 + ⌊d/300⌋
  - month = ⌊(d mod 300)/30⌋ + 1
  - day = (d mod 30) + 1
  - plus an hour counter (STAR-HR).
  So a game year is **10 months × 30 days = 300 days**. Status displays it as Day.Hour-Month-Year [M].
- **Flare clock:**
  - 79 of 270 stars have negative flare dates, i.e. they flared before the game starts. All of them have x ≥ 137, matching the "dead zone coreward of x=200" lore.
  - 93 stars flare during days 0–299 and 98 at day 300 or later [C].
  - **Arth's system flare date = 300**, which by the formula is 01-01-4621, right after 30-10-4620.
  - The Elowan dialogue in the data says: "THE SUN OF THE PLANET THOU CALL'ST ARTH SHALL FLARE IN THE FINAL WEEK OF YOUR TEN-MONTH OF THIS YEAR" [C]. The CRPG Addict called this "plenty of time" [W].
- **Game-over states** [C]:
  1. Being in a system on its flare day: "STAR IN SYSTEM x,y FLARED ON STARDATE … THE ISS <name> AND CREW WERE INCINERATED. GAME OVER". The check is STARDATE == system flare date and not yet won (`?WIN`).
  2. All crew dead.
  3. Ship destroyed in combat [M].
  4. Hull melting near a star ("TEMPERATURE IS INCREASING!… IS MELTING!… AHHHHHH!").
  5. Crushing gravity on landing [M].
  6. Running out of fuel or money is listed as a disaster [M]. The Distress tow is the escape hatch.
  - Also "9. End Game" destroys the save [M card].
- **Winning:** destroy the Crystal Planet (192,152) with the Black Egg bomb plus the Crystal Orb [F].
  - The win sets `?WIN`, which disables the flare checks [C].
  - Operations shows "COMPLETION OF MISSION" [C].
  - Fans cite a 500,000 MU reward and say play continues afterwards [F][S9].

## 9. UI / controls

- **Ship console** has four areas [M]:
  - **Main View Screen** (space, tactical, orbit maps, comms)
  - **Auxiliary View Screen** (status, sensors, damage, system map)
  - **Control Panel** (crew-post buttons, then that post's functions)
  - **Text Window** (all messages)
  - plus a coordinate readout above the main view.
  The manual's Figure 2 OCR reads "AUXILIARY VIEW SCREEN / CONTROL PANEL / TEXT WINDOW". **I could not verify the exact placement** from text; it is commonly shown as main view left, aux view upper right, buttons right and text along the bottom.
- **Command menu by role** [M][C]:
  - **Captain:** Launch/Land, Disembark, Cargo, Log Planet, Ship's Log, Bridge
  - **Science:** Sensor, Analysis, Status
  - **Navigator:** Maneuver, Raise/Drop Shield, Arm/Disarm, Combat (the code also has a Starmap/MAP overlay showing position, destination, distance and fuel)
  - **Engineer:** Damage, Repair
  - **Comms:** Hail/Respond (postures Friendly, Hostile, Obsequious; Statement, Question on Themselves, Other Races, Old Empire, Ancients or General Info; Posture; Terminate), Distress
  - **Doctor:** Examine, Treat.
- **IBM keys** [M card]:
  - arrow keys or the numeric keypad as an 8-way "cursor diamond"
  - Enter to select or open
  - Space = Enter except in combat, where it fires
  - Insert + direction = cruise control
  - + pauses messages
  - Ctrl-S toggles sound
  - Esc opens the Game Options menu (1 Save, 2 Resume, 3 Change Display, 9 End).
  Joystick support was a 1984 design assumption [D]. The C64 manual uses a joystick, and the Amiga, ST and Mac versions use a mouse.
- **Display** [M card][C]: Black/White, RGB (CGA 4-color), Color TV/Composite (the manual says it looks best on composite; the 1986 review agrees), Hercules mono. The later build adds EGA. Tandy is via RGB/composite (S2).

---

## Source ledger

| ID | Title | Author/Org | URL | Accessed | Type | Platform | What I inspected | Claims supported | Confidence | License notes |
|---|---|---|---|---|---|---|---|---|---|---|
| S1 | Starflight (Wikipedia) | Wikipedia editors | https://en.wikipedia.org/wiki/Starflight | 2026-10-08 | Encyclopedia | All | Article summary via fetch | Release dates, team, 270/800, 4620, sales | Med-High | CC BY-SA |
| S2 | Talk:Starflight | Wikipedia editors (HunterZ, Mpb2) | https://en.wikipedia.org/wiki/Talk:Starflight | 2026-10-08 | Talk page | DOS variants | Version discussion | CGA/Hercules → Tandy → EGA boxes; EGA anomalies | Med (anecdotal) | CC BY-SA |
| S3 | Starflight Manual (1986) | Electronic Arts / Binary Systems (scan: Vendetta Preservation Group) | https://archive.org/details/starflight-manual | 2026-10-08 | Original manual (OCR djvu.txt) | DOS-era | Full OCR text | 12,000 MU; races; skills; prices; energy chart; minerals; UI; colony criteria | High | © EA 1986; facts only, no copying |
| S3b | STARFLIGHT manual (multi-platform/UK) | EA | https://archive.org/details/STARFLIGHT | 2026-10-08 | Manual OCR | Amiga/ST/Mac/C64/IBM | Key passages and mineral chart | Mineral values (full chart), platform options | High | © EA |
| S3c | advpak starflight manual (incl. IBM command card) | EA via VGMuseum | https://archive.org/details/vgmuseum_ea_advpak-starflight-manual | 2026-10-08 | Manual + reference card OCR | IBM, Amiga, ST, C64, Tandy, Apple | IBM card: display modes, keys, save, code wheel | Controls, real-time, display options | High | © EA 1986/1987 |
| S4 | Starflight (The Digital Antiquarian) | Jimmy Maher | https://www.filfre.net/2014/10/starflight/ | 2026-10-08 | History article | DOS | Fetched summary | Team roles, 811 planets, Forth, Aug 1986 | Med-High | © author |
| S5 | starflight-reverse | Sebastian Macke (s-macke) | https://github.com/s-macke/starflight-reverse | 2026-10-08 | Decompiled DOS Forth → C, data dumps | DOS (EGA-capable build; exact version not stated) | README; `starflt1-out/data/starsystem.h`, `instance.txt`; overlays CONFIG, SHIPGRPH, PERSONNEL, BANK, HYPER, HYPERMSG, MOVE, ANALYZE, SCIENCE, PORTMENU, CHKFLIGHT; all PRINT strings | 270/811, coordinates, prices table, 300 MU/session, 12% interest, mass/accel formula, fuel use, calendar, flare logic, notices, 12,000 | High (code); derived formulas Med-High | **No LICENSE** (GitHub API `license: null`), so all rights reserved by default. The output is derived from copyrighted EA/Binary Systems code. Use facts only; don't copy code. |
| S5b | webarchive/SFFiles (Tim Lee design docs) | Ambient Designs/Binary Systems (archived by s-macke) | https://github.com/s-macke/starflight-reverse/tree/master/webarchive | 2026-10-08 | 1984 design docs | Pre-release | manual.txt, gameflow.txt, shiprec.txt, priority, starship.txt (skim) | Design ranges (pods 0-16, mass 100-500, accel 1-25, skills 0-250), design timeline (flare 4623) | Med (pre-release) | Marked "Ambient Designs proprietary"; facts only |
| S6 | Starflight Resource Pages: Miscellaneous | starflt.com (Wayback 2011-10-01) | http://www.starflt.com/tables/index.php/starflight1/miscellaneous/ | 2026-10-08 | Fan tables | DOS | Charts, fuel, skill notes | Fuel 0.16–0.49, endurium price dates, skill-efficiency model | Med | © 2011 Starflight Resource Pages, all rights reserved |
| S6b | Starflight Resource Pages: Ships/Combat Analysis | starflt.com (Wayback) | http://www.starflt.com/tables/index.php/starflight1/ships-combat-analysis/ | 2026-10-08 | Fan measurements | DOS | Full page | Shield/armor/weapon damage numbers, repair costs | Med (empirical) | same |
| S6c | Starflight Resource Pages: Minerals, Continuum Fluxes, Artifacts | starflt.com (Wayback) | …/starflight1/minerals, /continuum-fluxes/, /artifacts/ | 2026-10-08 | Fan tables | DOS | Tables | Mineral values, 32 flux pairs, artifact list/effects, Thrynn 6× | Med | same |
| S7 | Starflight 1: A Walk-Through | Steven Wirsz (on starflt.com, 2007) | http://www.starflt.com/tables/index.php/starflight1/starflight-walkthrough/ | 2026-10-08 | Walkthrough | DOS + Amiga/Genesis notes | First ~12k chars + keyword grep | Training cost totals, notice dates, version differences, colony values | Med | same |
| S8 | Beat Game Steps | starflt.com (Wayback) | http://www.starflt.com/tables/index.php/starflight1/beat-game-steps | 2026-10-08 | Fan speedrun | DOS | Full | 500,000 reward, minimal win path | Med | same |
| S9 | STARFLIGHT review (textfiles.com) | unknown 1980s reviewer | https://mirror.cyberbits.eu/textfiles.com/games/REVIEWS/starflt.rev | 2026-10-08 | Contemporary review | IBM PC (+Amiga notes) | First 5k chars | 12,000 credits, bank interest, 2 disks, codewheel, composite vs CGA 4-color | Med | Unknown |
| S10 | CRPG Addict, Starflight label | Chester Bolingbroke | http://crpgaddict.blogspot.com/search/label/Starflight | 2026-10-08 | Blog playthrough | DOS (EGA) | First 100k chars, summarized by fetch | Elowan flare hint, "plenty of time", UI annoyances | Med | © author |

Sources I tried but could not open:
- GameFAQs (HTTP 403)
- MobyGames (403; Wayback rate-limited, 429)
- live starflt.com (Cloudflare)
- GameBanshee retrospective interview (403)

---

## Disagreements between sources

1. **Starting money:** 12,000 in the manual, the code notice and the review, against the "10,000" in the prompt. 12,000 is correct.
2. **Planets:** 811 in the code and Digital Antiquarian, against Wikipedia's "800" (a rounding).
3. **Hyperspace fuel:** manual ".48 to .16 m³/coordinate". Fans measured 0.16/0.25/0.33/0.41/0.49. The code ratio 6:2 implies 0.48/0.40/0.32/0.24/0.16 if linear. The fan class-4 value (0.25) doesn't fit that model; the difference may come from rounding or tick granularity.
4. **First expedition** in the briefing: the 1984 design doc says no ships returned and fuel lasted 3 years. The shipped manual says 2 of 13 returned with fuel for 1 year. The design timeline puts the Arth flare at 4623; the shipped data puts the Arth system flare at stardate 300 (start of 4621), and the Elowan say "ten-month of this year".
5. **Bob Gonsalves' role:** "sound" (Wikipedia) against "planetside module" programmer (Digital Antiquarian).
6. **EGA version behavior:** Talk-page editors claim gameplay differences, such as shields working in nebulas. This is unconfirmed.
7. **Arth flare day:** the data's flare date 300 decodes to 01-01-4621, while the dialogue says "final week of ten-month". This is consistent if the flare lands at the day-300 boundary, and assumes my stardate decoding is right.

## Open questions / gaps

- I didn't read the GameFAQs FAQs or MobyGames trivia/specs (blocked). Release revisions such as DOS 1.0 vs EGA re-release dates remain unverified.
- **Race skill tables in code:** not located in the decompiled overlays (likely a disk table). The race stats rest on the manual only.
- **Lifeform sale prices:** no numeric formula found. The manual gives criteria only.
- **Artifact sale values, colony reward and fine amounts:** fan figures only (35k–55k per colony world).
- **Arth flaring while you're away:** unverified. The code I read only triggers game over when you're inside the flaring system on that day. What happens at Starport after Arth's flare date needs checking.
- **Combat internals:** shield/armor point values, recharge rate and alien AI exist only as fan measurements. COMBAT-OV (4,170 lines) was not analyzed.
- **Planet generation:** fractal terrain (FRACT-OV), mineral and lifeform seeding, weather and storm damage formulas, and TV fuel capacity are not analyzed.
- **Exact screen pixel layout and palettes** per mode (CGA 4-color vs EGA 16) were not verified against screenshots.
- **Repair pricing formula** and trade-depot buy/sell spreads were not extracted from code.
- **Mass/accel field mapping** (armor = field 0x11, pods = 0x1f bitmask) is inferred. Confirm by checking CONFIG-OV's buy routine.
