# STARFLIGHT (1986, Binary Systems / Electronic Arts): races, communication, story, ports

Research date: 2026-10-08. The focus is the original IBM PC DOS release. Each claim is tagged with ledger IDs ([S1]–[S16]) that point to the Source Ledger at the end.

**Evidence policy.** I recorded only what I actually read. Where a claim is an inference, I mark it **(inference)**. Where a claim comes only from a Genesis-version source, I mark it **(Genesis source)**. Dialogue is paraphrased; short quotes appear only as evidence.

**Primary-data note.** The strongest source here is the reverse-engineered DOS data in `s-macke/starflight-reverse` [S1]:
- `starflt1-out/data/instance.txt`: the game's instance tree, which holds all alien dialogue, star systems, planets, messages and notices.
- `strings.h`
- `directory.h`
- the decompiled overlays `COMM-OV.c`, `COMMSPEC-OV.c`, `HYPERMSG-OV.c`, `GAME-OV.c` and `MUSIC.c`.

I downloaded these files and inspected them locally. Copies are in `dl_sfrev/`, and `dialog_dump.txt` there is my index of all 543 phrase strings.

---

## 1. Alien race roster (Starflight 1)

### 1.1 What the DOS data proves exists

**Comm portraits in STARA.COM [S1].** `directory.h` lists portraits for these races:
- ELO (Elowan)
- GAZ (Gazurtoid)
- MEC (Mechan)
- MYS (Mysterion, by file prefix only)
- NOM (Nomad probe)
- SPE (Spemin)
- THR (Thrynn)
- VEL (Velox)
- VPR (Velox probe, which matches the `SA-VPROBE` word in COMMSPEC)
- MIN (Minstrel)
- COP (Interstel police, used for copy protection)

There is **no Uhlek portrait and no Ancients portrait.** Crew-race pictures (HUM, VEL, THR, ELO, AND) are separate files.

**Dialogue originators.** The dialogue tree has 15 ORIGINATOR blocks, each split into SUBJECT blocks, each holding a pool of PHRASECONTRL strings. I identified the originators from their text:

| # | Originator | Basis for identification |
|---|---|---|
| 1 | Interstel Corporate Police | "PULL OVER! … INTERGALACTIC SOFTWARE THEFT LAW" (copy protection) |
| 2 | Nomad probe (Old Empire survey drone) | "NOMAD PROBE REQUESTING PERMISSION TO TRANSMIT DATA"; sends planetary data records |
| 3 | Unidentified; strings are binary digit groups ("00001 01111 …") | **(inference)** probably the Mysterion ship. Veloxi say a strange ship "only communicating occasionally" that they cannot understand. |
| 4 | Gazurtoid | Religious "air breather" rhetoric |
| 5 | Thrynn | Hissing "SS/RR" orthography |
| 6 | Elowan | Archaic/Shakespearean English |
| 7 | Spemin | Blustering and grovelling |
| 8 | Mechan 9 (Old Empire androids) | "WE ARE MECHAN 9" |
| 9 | Unknown; only the string "RESPOND" | **(inference)** probably the Uhlek, who do not talk. Elowan: "THE UHLEK SPEAK ONLY WITH THEIR WEAPONS". |
| 10, 11 | Empty | — |
| 12 | The player's own transmissions | Statements and questions in each posture |
| 13 | Velox / Veloxi | Broken-English idiom |
| 14 | Minstrels (also called "Delasa'alia") | Rhyming verse, received telepathically ("receiving a message but there doesn't seem to be any transmission") |
| 15 | Distress beacon of the Empire colony ship *LASTHOPE* | Captain Smelenuf, dated 3-10-3480 |

**Not present in SF1 data.** I found no occurrences of "Dweller", "Humna", "Tandelou", "Umanu" or "Leghk" in SF1 `instance.txt`. Those names belong to Starflight 2 [S7]. The words "Mysterion" and "Ancients-as-speakers" never occur in SF1 text. "Mysterion" is known to me only from the `MYS-CPIC` filename. Numlox and Phlegmak are **extinct races mentioned in lore only** (the "First Wave", which began in 3000).

### 1.2 Per-race summary

All coordinates are game sector coordinates as written in-game. The raw `x,y` values in `instance.txt` are 8 times these: for example, Arth's system has raw x=1000, y=800, which is 125,100.

**Elowan** (sentient plants; hirable)
- *Disposition:* Peaceful, poetic and archaic in speech. They detest violence.
- *Hostility triggers:*
  - Thrynn aboard your ship. The COMMSPEC text: "WE DOST DETECT THE PRESENCE OF ACCURSED THRYNN ABOARD THY VESSEL" leads to no dealings.
  - Being judged friends of the Thrynn.
  - Destroying **Elan**, which makes them "now and forever mortal enemies".
  - The manual adds that the presence of either race in your crew blocks useful communication with the other [S2].
- *Homeworld/territory:*
  - Birth star "Eleran" (the Thrynn call it "Thoss") at 129,33. The Elowan lived on planet 2 and the Thrynn on planet 4.
  - Elowan dialogue calls **Elan** (148,63) their sacred island-world where their young dwell.
  - A Thrynn-voice line says their second homeworld was destroyed by a flare.
  - Their space is "downspin". The Hyperion veteran met Elowan scouts near 150,64 [S2].
- *Plot info they hold* [S1]:
  - Arth's sun will flare "in the final week of your ten-month of this year". The Thrynn sun flares in nine-month.
  - The Crystal Planet destroys life, unleashes a "mighty force" on approach, and is vulnerable only at the "Nexus of Control". The nexus requires the lost Crystal Cone.
  - The dead zone lies coreward, and the Crystal Planet orbits in the "zone intermediate".
  - The Four Seedlings constellation is made up of Akteron, Gaal, Iridani and Echt.
  - The Institute of the Old Empire knew most about the Ancients.
  - Lore on the Minstrels, Uhlek and Gazurtoid, including that the Uhlek mind lives in subterranean caverns.
  - An Elowan children's song about the Seedling suns falling.
- *Trade/aid:* The Elowan can give 15 units of fuel to friends in dire need, but refuse if you have 20 or more [S1, COMMSPEC]. They ask whether you have eaten **headfruit**, the Elowan seed-fruit that Thrynn steal.

**Thrynn** (reptiles; hirable)
- *Disposition:* Formal, protocol-obsessed, mercantile, "survival of the fittest". They despise races that grovel: "any race who does this deserves to die". This suggests the Obsequious posture backfires **(inference from text)**.
- *Hostility triggers:* Elowan aboard, being allied with the Elowan, or having destroyed Elowan ships. The last is a question they ask you.
- *Homeworld:* Thoss, planet 4 at 129,33. Their "homeworld territory" dialogue cites the "Transsentient-Inviolate-Region Treaty of 3210" [S1].
- *Plot info:* This is where most artifact rumours come from:
  - the Shimmering Ball in a Gazurtoid nebula, reached via the Cross Nexus at 98,79
  - the Tesseract (stolen by the Uhlek) on planet 5 of 18,50
  - a red cylindrical device on old outpost Koann-3 at 112,200, 59N x 64W
  - the Dodecahedron, an Ancient distress beacon that attracts ships
  - the Ring Device, which reveals continuum fluxes, on northern Mars
  - the Rod Device, a laser deflection screen once owned by the pirate Harrison of New Scotland
  - the Crystal Pearl, an automatic warp-out device, in the "city of the Ancients"
  - the whining Orb that translates Spemin
- *Deception:* They urge you to destroy **Elan** as the "secret base planet of the Uhlek" with a Black Egg. In the Elowan's own words, Elan is the Elowan nursery world **(cross-source inference: this is a Thrynn trick)**.
- *Trading* [S1, COMMSPEC]:
  - They buy plutonium for energy crystals.
  - They offer to buy artifacts.
  - They sell "a device that will make your ship invulnerable" for 30 energy crystals. A Genesis guide calls this the Black Box [S12, Genesis source].
  - An Interstel notice warns not to sell artifacts to the Thrynn.

**Velox / Veloxi** (insectoids; hirable)
- *Disposition:* Arrogant, isolationist since 3330, and worship their Queen. Filfre [S8] and the Genesis guide [S12] say Velox must be addressed obsequiously. The data agrees in spirit: they ask "you are coming to worship of illustrious Queen?"
- *Homeworld:* Votiputox, which is off-limits to aliens. The sacred planet Sphexi is planet 1 at 132,165, where the "Small Egg" sits on "the most magnificent hexagon". Their space is "upspin".
- *Special mechanics* [S1, COMMSPEC]:
  - They demand **tribute** in energy crystals when you trespass.
  - They demand return of the **Focusing Stone**, which the Empire pirate Harrison stole in 3330, if it is aboard.
  - They threaten **war** if you carry their Small Egg (the Crystal Orb).
  - Their home system is "guarded by Veloxi drones… all must answering questions correctly". The hint is "answering Veloxi probe is by 6 multiples the yes answer", and 6 is their sacred number.
- *Plot info:*
  - The Prophecy of the Great Egg: the "Great Egg" at 192,152 (the Crystal Planet) travels core to rim, makes suns flare, and spares the Queen while the Velox hold the Small Egg.
  - An Empire distress call from 175,94.
  - Ruins at 143,115 (28N x 4E, "forbidden") and in the Staff constellation.
  - Lore on the First and Second Waves.
  - The Phlegmak's last base at 35S x 99E, "second planet in Handle of Axe".

**Spemin** (blobs; not hirable)
- *Disposition:* Cowardly braggarts. They bluff with threats, then surrender when pressed: "WE SURRENDER! … WE WILL GIVE YOU A WONDERFUL GIFT".
- *Talking to them:* Elowan advice is that pampering appeases them, but useful information is gained only by force [S1]. The Genesis guide says they are "subjugated through hostile negotiations" [S12].
- *Language:* An untranslated "strange language". A Thrynn-built whining Orb translates it [S1].
- *Homeworld:* 82,148, which a surrendering Spemin gives away and Mechan data confirms. The Genesis guide names it "Spewia" [S12, Genesis source].
- *Plot info:*
  - The Uhlek **mind-ganglion planet is the life-bearing planet at 55,32**, and destroying it cripples the Uhlek.
  - A secret jump route into Uhlek space: 106,139, then 64,186, then 35,186.
  - The Velox Focusing Stone is on planet 1 of 81,98.
  - A great Ancient city lies in a nebula outward of the Spemin homeworld.
  - The Crystal Pearl.
- *Faction:* They sit in water to pass as water-breathers with the Gazurtoid.

**Mechan 9** (Old Empire androids)
- *Disposition:* Programmed guardians of the Noah colony "Heaven".
  - They open with a series of **verification questions**: are you Group 9, do you serve Layton, maintain Code Blue, verify Code Red, are you an enemy of Noah, and so on.
  - If you answer wrongly, they warn and attack.
  - COMMSPEC also checks whether **humans are aboard**: "WE DO NOT DETECT THE PRESENCE OF ANY HUMAN LIFEFORMS … YOU CANNOT BE GROUP 9."
  - An Interstel notice reports androids destroying a ship "20 sectors directly coreward of Arth".
- *Plot info, which is the richest history source:*
  - Endurium was found on Sol-4 in 2100.
  - The Institute was on Akteron 6 (75S x 66E) and had an underground station on Earth at 12N x 104W.
  - The Wave chronology.
  - The Layton faction.
  - Mardan-2 HQ was destroyed by the Uhlek in 3440.
  - Noah 1 failed because of a Ring Device malfunction.
  - "Heaven" is planet 4. Mechan 9 left Earth in 3479, Group 9 never arrived, and they have been attacked 14 times.
  - They hold full race histories of the Spemin, Velox, Thrynn and Elowan.

**Gazurtoid** (aquatic, tentacled zealots; not hirable)
- *Disposition:* Always hostile toward "air breathers". They give no useful information beyond scripture-parody. Missiles are ineffective against them, so use lasers (Thrynn, Spemin and the manual's Noah 2 log all say so).
- *Territory:* They followed the Uhlek outward. Their territory includes the Cross Nexus (98,79) and a "holy land" planet. An intelligence message says they stole a cloaking device and traced it to 68,66, which is where the Shimmering Ball is.

**Uhlek** (hive-mind fleet; not hirable)
- *Disposition:* Never communicate (see originator 9) and carry very powerful weapons.
- *Brain world:* Spemin give 55,32. The Thrynn give Elan (148,63), which is a lie per the Elowan.
- *Territory:* Deep coreward. The Crystal Cone's system 20,198 is "deep in Uhlek space".
- *Interstel warnings:* The first notice says to avoid 135,84, where two ships were lost. The Genesis guide notes an Uhlek flux at 136,84 [S12].
- A walkthrough says the Uhlek won't let you collect the Crystal Cone unless the brain world is destroyed [S13 via search snippet: medium-low confidence].

**Minstrels / Delasa'alia** (space-dwelling beings)
- They communicate in verse telepathically.
- Their songs tell the true story: the "crystal few" came first; to them other life was a spreading "virus"; they fight "by causing suns to ignite"; races flee outward in waves [S1].
- Thrynn tell you to destroy them. Veloxi call them strange but harmless.

**Nomad probes:** Old Empire survey drones that transmit planetary data records [S1].

**Mysterions (name uncertain):** There is a portrait file and a binary-transmission originator, but no SF1 text names them **(see Open Questions)**.

**Ancients:** They do not speak directly in the comm system. Their story arrives through the Minstrels, the Velox prophecy, and McConnell's log on the Crystal Planet.

**Humans:** Exist only on Arth [S6]. They are a crew race, as are Android (DX99, Communication fixed at 0), Velox, Thrynn and Elowan [S2].

---

## 2. Communication system

### 2.1 Manual description [S2]
- **Hail/Respond.** The option reads *Hail* unless the alien hailed first, in which case it reads *Respond*.
- **Postures:** Friendly, Hostile, Obsequious. You pick one when you hail or respond, and you can switch at any time through *Posture*.
- **Statement vs Question.** "Statements are more likely to affect the aliens' attitudes than are questions."
- **Question topics:** Themselves, Other Races, Old Empire, Ancients, General Information. The comm overlay builds menu strings "YOUR RACE", "OTHER RACES", "THE OLD EMPIRE" and "THE ANCIENTS" [S1, COMM-OV words `YOU$`, `OTHER$`, `OEMP`, `ANC$`].
- **Terminate.**
- **Pausing.** The `+` key pauses comms for up to 2 minutes, because the real-time clock otherwise keeps ticking.
- **Posture fit.** Not all races respond best to Friendly, so the manual tells you to experiment.
- **Repeat questions.** Individual members of a race have different knowledge, so ask others the same question.
- **Combat history.** Repeated combat makes friendly relations less likely. If you stop fighting and talk, aliens may surrender and "tell you anything you want to know".
- **Scanning.** Aliens scan you, and raised shields or armed weapons read as hostile gestures.
- **Comm skill.** The translator's quality depends on the Communications Officer's skill: the lower the skill, the more untranslated text you get. A crew member of the alien race adds +25 effective skill, or +50 if that member *is* the Comm Officer.

### 2.2 What the data/code shows [S1]

**Phrase pools.** Each race has about 12 SUBJECT pools. The consistent layout across races, which I inferred from content, is:

| Pool | Content |
|---|---|
| 1 | Farewells |
| 2 | Questions the alien asks you, for yes/no answer |
| 3 | Attitude statements, ranging from friendly through threatening to surrender |
| 4 | Answers about the Ancients |
| 5 | Answers about the Old Empire |
| 6 | General info |
| 7 | Other races |
| 8 | Themselves |
| 9 | "Awaiting your answer" prods |
| 10 | Refusals or "don't know" |
| 11 | Response greetings |
| 12 | Hail greetings, plus a list of 2–4-letter syllables |

The syllable lists (e.g. Elowan "IAE", "LIW"…; Thrynn "SS", "ASA"…) are **(inference)** the material for garbling untranslated text when comm skill is low.

**Disposition variable.** The code tracks a per-race disposition called **EDL**. The Time Extension interview spells it "Emotional Disposition Level" [S9]. The Forth word names in COMM-OV include:
- `NEW-POSTURE>EDL`, `+!EDL`, `BASE-EDL`
- `TIRED-OF-TALKING`, `TIRED-OF-WAITING`
- `?WAS-QUESTIONED`, `?MAKE-STATEMENT`, `?HAS-SURRENDERED`
- `GET-PHRASE-LIMIT`, `#A-PHRASES-LIMIT`, `SPACIAL-CONTEXT` (being in home territory triggers the homeworld warnings)
- `RRND` (random) calls

**How lines are chosen.** Responses are therefore **state-dependent, then randomly picked within a range of the pool.**
- Which pool is used depends on the topic and context: hail versus respond, homeworld or not, surrendered or not.
- Which entries are eligible depends on disposition (EDL), and the phrase-limit words suggest the eligible range grows as relations improve **(inference from names)**.
- The pick within that range is random (`RRND`).

**Special scripted actions.** These live in COMMSPEC [S1]: `SA-ELOWAN`, `SA-THRYNN`, `SA-VELOX`, `SA-MECHAN`, `SA-VPROBE`, plus `CRYSTAL-ORB`, `PLUTONIUM`, `NEED-ENERGY` and `?ARTIFACT`/`-ARTIFACT`. They cover:
- crew-race detection (the Elowan/Thrynn feud and the Mechan human check)
- Velox tribute, Focusing Stone and Small Egg demands
- Thrynn plutonium and artifact purchase, and the "invulnerability device" sale
- Elowan emergency fuel
- the Elan-destroyed reaction
- the Interstel police security-code check

**Player transmissions.** Originator 12 holds the player's own lines. Obsequious lines are grovelling ("we are not fit to grovel in your waste products"), Friendly lines are diplomatic, and Hostile lines are threats. Questions follow a "(CONCERNING / )" template, so the topic is filled in at runtime.

**Design.** Johnson and Kercso designed the comm system as a "context-sensitive grammar compiler" [S9]. Paul Reiche III advised on the conversation-posture mechanic, which descends from *Murder on the Zinderneuf* [S8]. The comm code was rewritten four times [S6].

---

## 3. Story and mystery

### 3.1 Backstory

**From the manual's briefing and timeline [S2]:**
- **2100:** Endurium discovered.
- **2150:** FTL travel begins; the Empire starts.
- **2300:** Humans meet the Velox.
- **2675:** Spemin discovered.
- **2770:** Thrynn and Elowan discovered.
- **3000–3260:** First Wave.
- **3120:** Velox pact.
- **3400:** Second Wave begins.
- **3450:** Project Noah starts.
- **3454:** **Noah 2** expedition launched.
- **3480:** Fall of Earth.
- **3505:** A bomb creates Arth's Southern Hot Zone.
- **3520–4400:** Arth's Dark Years.
- **4594:** Noah 2 colony and Endurium rediscovered. (The briefing narrative says the shaft was found in 4604.)
- **4615:** First Interstel group sent out.
- **4620:** The game's present.

**From the game data [S1]:**
- **First Wave:** Numlox and Phlegmak. The Phlegmak used "black-egg planet bombs".
- **Second Wave:** Uhlek and Gazurtoid, all fleeing coreward→rimward ahead of the flaring stars.
- **The Institute:** On Akteron 6, it studied the Ancients and coreward stellar instability (the "dead zone"), and launched Project Noah as underground colonies.
- **Layton faction:** The Laytonites sabotaged Noah ships.
- **Mechan 9:** Still guards the colony world "Heaven" (planet 4) for Group 9, which never came.
- **Downed colony-ship log** (Oct 31, 3480): it was "on our way to establish the NOAH 9 colony world of 'Heaven' when the ship blew", blaming Laytonite sabotage. Mechans were in "Code Blue"; the crew's arrival would have triggered "Code Red".
- **LASTHOPE beacon:** The distress beacon of the Empire colony ship LASTHOPE (Captain Smelenuf, 3-10-3480) gives landing coordinates 22N x 97W. Thrynn say the derelict is at 175,94. The Veloxi hear its distress call from 175,94, and the first Interstel notice flags "alien activity in the system 175,94".
- **Arth = Earth colony.** Arth's people learn they were an Earth colony (Noah 2) from the briefing [S2]. **Earth** is the Sol system at 215,86 (raw x=1720, y=688), in the Pythagoras constellation [S1]. Its planet 3 carries ruin messages: the final flare log of 2-28-3480 ("surface temp 150°C"), satirical newspapers, and "STARFLIGHT NAVIGATIONAL RESEARCH STATION". Mars hosts the Institute's polar Starflight research station and the Ring Device.

### 3.2 The core mystery
- The **Crystal Planet** at 192,152 (an O-class star in the data) is an Ancient device.
  - The Velox call it the "Great Egg".
  - Interstel loses a ship there in a later notice.
  - The Crystal Planet makes stars with life flare as it moves from core to rim.
- **Arth's sun is destabilising.** Interstel notices report this, a sensor reports each system's stellar condition, and scientists later find the cause "external to the star".
- Every star system in the data carries a `flaredate` field (range −384…792; units not determined). Arth's system (125,100) has flaredate=300 [S1].
- **The revelation** comes from McConnell's log, found on the Crystal Planet [S1, S6]:
  - **The Ancients ARE Endurium.**
  - Their metabolism is so slow that they live in a different time-frame.
  - They view carbon life "something like a virus".
  - The Crystal Planet is "their last defense".
  - The Minstrels' songs foreshadow all of this [S1].

**McConnell sub-plot [S1].**
- *Project Teleport (1-2-3380):* Crystal Base 1 teleports a volunteer with a nuclear charge.
- *Commander McConnell's log (8-8-3382):* entries 1–4 cover arriving, the comms block caused by the planet's EM field, setting charges at the control nexus amid Endurium lumps, and telepathic contact with the Ancients.
- *Crystal Base 1 transcript:* an explosion seen 12h11m after his departure, but the planet survived.
- *Research Council recommendation (5-5-3386):* destruction needs **3 devices**:
  1. the **Crystal Orb**, which nullifies the defences and which the Velox hold as the "Small Egg"
  2. the **Crystal Cone**, which locates the nexus from orbit and is at 29S x 55W on planet 1 of system 20,198, deep in Uhlek space
  3. a bomb "on the order of beva-tons"
- The **Black Egg** is the Phlegmak planet-bomb. A microfilm puts one in Phlegmak ruins at 52N x 16E, planet 3 of 238,189.

### 3.3 Artifacts

| Artifact | Function (as stated in-game unless noted) | Location / how obtained | Source |
|---|---|---|---|
| Crystal Orb (Velox "Small Egg") | Nullifies Crystal Planet defences. "THE CRYSTAL ORB IS GLOWING" on approach. Without it the hull overheats ("**SHIP MELTED**"). Velox threaten war if you carry it. | Sphexi, the Velox sacred planet at 132,165 (planet 1, on the "hexagon"). Walkthrough: 46N x 14E. | S1; S13/S12 |
| Crystal Cone | Identifies the control nexus from orbit | 29S x 55W, planet 1, system 20,198 (Uhlek space) | S1 |
| Black Egg (×3 per guides) | Phlegmak planet-buster. One destroys the Uhlek brain world; one destroys the Crystal Planet. | 238,189 p3 (52N x 16E, per S1). Guides add 143,115 p1 (28N x 4E) and 234,20 p2 (35S x 99E). | S1; S12, S13 |
| Crystal Pearl | Automatic warp-out when the ship is critically damaged ("THE CRYSTAL PEARL IS PULSING!") | "City of the Ancients" in a nebula outward of the Spemin homeworld. Genesis guide: 56,144 p1. | S1; S12 (Genesis) |
| Tesseract ("energy enhancer") | Stolen from the Thrynn by the Uhlek | 15N x 44W, planet 5, 18,50 | S1 |
| Red Cylinder ("Ancient-ruin locater") | Locates ruins | Koann-3, 59N x 64W, planet 3, 112,200 | S1 |
| Dodecahedron | Ancient distress beacon that attracts ships | Genesis guide: 118,146 p4 (16S x 20W, the "colony control" message location) | S1; S12 (Genesis) |
| Shimmering Ball / Orb | Wanted by the Thrynn. **(inference)** May be the stolen cloaking device. | Gazurtoid nebula, via the Cross Nexus 98,79. Guides: 68,66 p1 (12N x 32E). | S1; S12/search |
| Ring Device | Reveals continuum fluxes on screen | Northern Mars, Sol 215,86 p4 | S1; S12 |
| Rod Device | Laser deflection screen | Harrison's; New Scotland / 44S x 137W, planet 1, 81,98 (per the S1 report). Genesis guide: 180,124 p2. | S1; S12 (Genesis): **conflict** |
| Focusing Stone (Velox) | The Velox Queen's hive-mind relay. Return it to the Velox. | Planet 1 of 81,98 (Spemin hint) | S1 |
| Ellipsoid | Asked for by the Velox (Genesis guide). **(inference)** This may be the Focusing Stone under its item name. | 81,98 p1 (Genesis guide) | S12 (Genesis) |
| Whining Orb | Translates the Spemin language | Location not stated | S1 |
| Hypercube | Unknown function | Earth, 215,86 p3 (Genesis guide) | S12 (Genesis) |
| Black Box | Sold by the Thrynn for 30 Endurium as an "invulnerability" device (in-game claim). Also appears on the DOS code wheel. | Thrynn trade | S1 (text), S3 (wheel example "Akteron, Black Box, Uhlek"), S12 |

The extracted text never mentions "bladed toy", "wee green blobbie", "nid berry", "ellipsoid" or "hypercube", and I found no source for them. Artifact *names* appear to be stored in a compressed table that the repo does not expose as plain text. **Treat the minor-artifact names as unverified for DOS.**

### 3.4 Endgame, win and loss

**Win sequence**, assembled from S1, S6, S12 and S13:
1. Obtain the Orb from Sphexi.
2. Destroy the Uhlek brain world (55,32) with a Black Egg, per Spemin info and the walkthrough.
3. Get the Cone at 20,198.
4. Take a second Black Egg to the Crystal Planet at 192,152. The Orb protects the ship there.
5. Land at the nexus, plant the egg, and leave.
6. The planet explodes.

**After the win:** McConnell's log is readable on the Crystal Planet's surface. The final Interstel message awards a **500,000 MU bonus** and ends with "THANK YOU FOR SAVING EVERYTHING THAT WE HOLD DEAR. — LEIGH V. MALONE, CHAIRMAN OF INTERSTEL. (END TRANSMISSION)" [S1]. Play can continue afterwards [S6].

**Game-over states found [S1 / S2]:**
- *Flare:* "STAR IN SYSTEM … FLARED ON STARDATE … THE ISS … AND CREW WERE INCINERATED. GAME OVER". This happens when you are in a system that flares.
- *Crystal Planet without the Orb:* "SHIP MELTED".
- *Interstel police after a wrong security code:* assets seized, then "CREW ARRESTED / GAME OVER".
- *Manual list:* running out of fuel, TV energy or money; combat death; crushing gravity (over 8 g destroys the ship [S6]); storms or lifeforms.

**Exact Arth deadline.** The Elowan say "final week of ten-month" of the current year. The mechanism (per-system `flaredate`) is in the data, but I did not decode it to a calendar date.

---

## 4. Platforms and ports

| Platform | Date | Notes | Sources |
|---|---|---|---|
| IBM PC / Tandy / compatibles (DOS) | 15 Aug 1986 | See the DOS details below this table. | S6, S3, S9 |
| Amiga | 1989 | 512K, Kickstart 1.2/1.3, mouse-driven. No Navigator "Maneuver" (the mouse moves the ship/TV), no Evaluations option, 3 map levels. Has a Sound On toggle covering "music and sound effects". Not copy-protected. Hard-drive install not possible. | S6, S5 |
| Commodore 64 | 1989 | Joystick. No Scan/Look on planets, 2 map magnification levels, colored blocks instead of icons. Kercso ported it and Johnson redid the graphics. | S6, S4, S9 |
| Atari ST, Macintosh | 1990 | Covered by the combined later manual with the Amiga ("Amiga, Atari ST and Mac users have these additional options") | S6, S4 |
| Sega Genesis / Mega Drive | NA 7 Oct 1991, EU 9 Oct 1991 | See the Genesis details below this table. | S6, S10, S11 |

**DOS details** [S6, S3, S9]:
- 2 floppies (A and B). The executable is STARFLT.COM with overlay archives STARA.COM and STARB.COM.
- Original display options: B/W, RGB, Composite, Hercules. EGA is **not** listed on the 1986 refcard. The reverse-engineered build offers a 5th "EGA" option, so it is a later revision. Time Extension says EGA support came later from a fan patch that EA adopted.
- No joystick. Ctrl-S toggles sound. Hard-disk install is supported.
- Code-wheel copy protection: a location, artifact and race combination gives the code.
- Needs 256K per Filfre; Wikipedia says it had to "fit into 128 KB".

**Genesis details** [S6, S10, S11]:
- Developed by **BlueSky Software**, with lead programmer Richard Karpp.
- New graphics, ship changes, and Terrain Vehicle upgrades including amphibious mining.
- Karpp says all content fit because space is procedural, and that he removed no alien text.
- I found **no source** for Binary Systems doing the port. That claim in the brief is unverified, and Wikipedia credits BlueSky.

**Starflight 2: Trade Routes of the Cloud Nebula** (DOS 1989; Amiga/Mac 1991) [S7]. Do not import any of these into SF1:
- The **trade economy**.
- **Shyneum** fuel. Endurium was banned after SF1.
- The **Spemin as main antagonists** with new technology.
- New races: Humna Humna, Dweenle, Tandelou, Umanu, Leghk.
- About 30 species.
- Time travel.
- The Cloud Nebula.

---

## 5. Development history
- **Binary Systems** (originally "Ambient Design(s)"), founded and led by **Rod McConnell**. Note that the in-game martyr is "Commander McConnell". [S8, S9]
- **Team** [S6, S9]:
  - **Greg Johnson:** lead design, story and world
  - **Alec Kercso:** programming, comm system, Starport
  - **Tim C. Lee:** core architecture, graphics, fractals
  - **Bob Gonsalves:** programming of planet exploration and combat per S9; Wikipedia credits him with "sound"
  - **EA producer:** Joe Ybarra
  - **Earlier contributors:** Dave Boulton (fractals; left), Tom Stewart, and Jim Yarbrough, who pitched the concept and withdrew in 1983
  - **Adviser:** Paul Reiche III
- **Manual:** Nicholas Lavroff and Binary Systems, with T.L. Thompson and Caitlin Johnson. **Theme music: Jeff Lubeck** [S2, S4].
- **Forth** with x86 assembly routines [S6, S1]. Decompiled source fragments carry dates like "9-12-85" [S1, MUSIC.c].
- **Development time:** Started late 1982 under the working title "Starquest". Moved from Atari 800 to C64 to IBM PC [S9]. "Fifteen man-years", about three years [S6], about 3.5 years [S9]. The brief's "~5 years" is not supported; the nearest figure is late 1982 to Aug 1986, roughly 3.7 years.
- **Procedural content:** One seed generates the universe, then "massaged" by hand: 270 systems and 811 planets [S9]. I verified this in the data: 270 STARSYSTEM nodes and 811 PLANET nodes [S1]. The "fractal generator" took 6 man-years and raised planet count from 50 to 800; the ecosystem generator took 2 man-years [S6].
- **"Bob Mattes":** **not credited anywhere I checked.** I looked in Wikipedia, Filfre, Time Extension, both manuals' credit blocks, the textfiles review, Karpp's Genesis interview, and a targeted web search. The person is presumably confused with Bob Gonsalves. (MobyGames credits were blocked with a 403, so I could not check them.)

## 6. Audio
- **DOS:**
  - The manual credits **theme music by Jeff Lubeck** [S2].
  - The DOS `MUSIC` overlay (4,960 bytes) contains the words `SONGS`, `SCALE`, `MUSINT`, `INTROS`, `CREDIT.SCREEN`, `TONESTATE` and `BEEPOFF`.
  - Its machine code writes PIT ports 43h/42h and toggles port 61h, which is **PC-speaker tone generation**, interrupt-driven [S1].
  - So the DOS version plays a PC-speaker theme on the intro/credits, plus sound effects that Ctrl-S toggles [S3]. The contemporary review says sound effects are optional [S14].
  - McConnell says music was constrained by disk space [S9].
- **Amiga:** The refcard mentions "music and sound effects" on/off [S5]. No composer is named beyond the shared manual credit.
- **C64:** Kercso created an opening score based on Mussorgsky's *Pictures at an Exhibition* [S9].

---

## 7. Disagreements
1. **Noah 2 vs Noah 9.**
   - The manual says Arth was settled by **Noah 2**, launched 3454 [S2].
   - The game data refers to the **Noah 9** colony world "Heaven" and "Group 9" (Mechan 9) [S1].
   - Wikipedia merges these, saying "Noah 9 left Earth in search of Heaven" [S6].
   - These are different expeditions: Arth = Noah 2, and Heaven = the Noah 9 target.
2. **Number of space-faring races.**
   - Wikipedia says 8 [S6].
   - Other sources say 7 [S8, S12, search snippets].
   - The data shows more speaking entities: Minstrels, Nomads, Mechans, and possibly Mysterions.
3. **Planet count.** Wikipedia says "800" [S6]; Time Extension says 811 [S9]; the data has 811 [S1].
4. **Memory.** Wikipedia says 128 KB [S6]; Filfre says 256K required [S8].
5. **Bob Gonsalves's role.** Wikipedia says "sound" [S6]; Time Extension says exploration and combat programming [S9].
6. **Elan.** The Thrynn call it the Uhlek secret base; the Elowan call it their sacred nursery [S1]. The Spemin put the Uhlek brain at 55,32.
7. **Rod Device location.** The S1 data points to Harrison and New Scotland, with a report placing him at 81,98 p1. The Genesis guide gives 180,124 p2. This may differ by version or be misreported.
8. **PC date.** The Sega-16 review calls the PC original "1988" [S11], which is wrong against S6 and S9.
9. **Genesis developer.** The brief suggests Binary Systems; the sources say BlueSky Software [S6, S10, S11].

## 8. Open questions / gaps
- **Identity of the binary-speaking originator (3) and of "Mysterion."** No SF1 text names them. Need the Genesis manual or a DOS-era FAQ.
- **Artifact name table.** The full DOS list, including minor sellables, is not in plain text in the repo. "Bladed toy", "wee green blobbie" and "nid berry" are unverified.
- **Flaredate units and Arth's exact deadline.** Not decoded.
- **Black Box function.** Is the Thrynn "invulnerability" sale a scam?
- **Exact posture preference per race.** Velox prefer obsequious per S8 and S12. The Thrynn's contempt for grovelling implies they do not, but I did not verify this in code. Spemin need hostile pressure.
- **DOS revision history.** The 1986 build has 4 display modes; a later build has EGA. Version numbers and dates are unknown.
- **The "Interstel" document.** I found only the briefing letter and transcript within the manual; no separate scan was examined.
- **Sources I could not reach:** MobyGames (403), starflight.fandom.com (402), Sega Retro (403), GameBanshee interview (403), Hardcore Gaming 101 (404 at the URL tried). I did not read CGW 1986/87 reviews directly.

---

## Source Ledger (all accessed 2026-10-08)

| ID | Title | Author/Org | URL | Type | Platform | What I inspected | Claims supported | Confidence | License notes |
|---|---|---|---|---|---|---|---|---|---|
| S1 | starflight-reverse | Sebastian Macke (s-macke), GitHub | https://github.com/s-macke/starflight-reverse | Reverse-engineered DOS code and extracted data | DOS (SF1; SF2 also present) | README; `starflt1-out/data/{instance.txt,strings.h,directory.h,dictionary.h}`; overlays COMM-OV, COMMSPEC-OV, HYPERMSG-OV, GAME-OV, MUSIC, ITEMS-OV, IT-OV, ANALYZE-OV; SF2 instance/directory (partial) | Race roster, all dialogue content, comm mechanics, artifacts and plot texts, game-over states, ending text, 270/811 counts, PC-speaker music, EGA option | High for content; medium for my structural inferences | **No license declared** (GitHub API `license: null`). The repo publishes decompiled code and extracted game text from copyrighted EA/Binary Systems software. Users must supply the original game files. Treat its contents as copyrighted material: reference only, quote sparingly. |
| S2 | Starflight manual (briefing letter, transcript, Technical Reference Manual) | Electronic Arts / Binary Systems, ©1986; scan by MOCAGH | https://www.mocagh.org/ea/starflight-manual.pdf | Original manual scan (48 pp) | IBM PC | Full OCR text: races, skills, comm section, combat, appendices D/E, credits | Postures and topics, comm skill bonuses, Elowan/Thrynn rule, timeline, Noah 2, Lubeck theme credit | High | ©1986 EA; scan hosted by a fan archive |
| S3 | Starflight reference card, "IBM and Compatibles" | EA | https://www.mocagh.org/ea/starflight-refcard.pdf | Ref card scan | IBM PC | Setup, display modes, keys, code wheel | 2-disk setup, 4 display modes, Ctrl-S sound, code wheel, HD install | High | ©EA |
| S4 | Starflight manual (later multi-platform edition) | EA / Binary Systems | https://www.mocagh.org/ea/starflight-alt-manual.pdf | Manual scan (27 pp) | Amiga/ST/Mac/C64/IBM | Platform notes, credits | C64 limitations, Amiga/ST/Mac options, Lubeck credit | High | ©EA |
| S5 | Amiga Command Summary Cards (2 scans) | EA | https://www.mocagh.org/ea/starflight-alt-refcard.pdf ; https://www.mocagh.org/ea/advpak-starflight-refcard.pdf | Ref card scans | Amiga | Requirements, UI differences, sound toggle | Amiga differences, music and SFX toggle | High | ©EA |
| S6 | Starflight (Wikipedia, raw wikitext) | Wikipedia contributors | https://en.wikipedia.org/wiki/Starflight | Encyclopedia | All | Infobox, gameplay, story, development, release | Dates, team, Forth, man-years, fractal and ecosystem generator, Genesis changes, plot summary | Medium-high | CC BY-SA 4.0 |
| S7 | Starflight 2: Trade Routes of the Cloud Nebula (Wikipedia) | Wikipedia contributors | https://en.wikipedia.org/wiki/Starflight_2:_Trade_Routes_of_the_Cloud_Nebula | Encyclopedia | DOS/Amiga/Mac | Summary | SF2-only races and mechanics, dates | Medium | CC BY-SA 4.0 |
| S8 | "Starflight" (The Digital Antiquarian) | Jimmy Maher, 28 Oct 2014 | https://www.filfre.net/2014/10/28/ | Historical essay | DOS focus | Via fetch summary | Team, Forth, Reiche and postures, Velox obsequious, 256K, sales | Medium-high | © author |
| S9 | "The Making of Starflight… an oral history" | Time Extension (Stay Forever team), 21 Aug 2026 | https://www.timeextension.com/features/the-making-of-starflight-dont-let-me-die-until-this-game-is-out-an-oral-history-of-the-trailblazing-space-sandbox-sim | Interviews | DOS, C64, Genesis | Via fetch summary | Roles, Starquest, timeline, EDL, 270/811, C64 port and music, EGA patch, music limits | Medium-high | © publisher |
| S10 | Interview: Richard Karpp | Sega-16, Mar 2005 | https://www.sega-16.com/2005/03/interview-richard-karpp/ | Interview | Genesis | Via fetch summary | Genesis lead programmer, content kept | Medium | © publisher |
| S11 | Starflight (Genesis review) | Aaron Savadge, Sega-16, 26 Jan 2006 | https://www.sega-16.com/2006/01/starflight/ | Review | Genesis | Via fetch summary | BlueSky developer, graphics overhaul; misdates PC to 1988 | Medium-low | © publisher |
| S12 | RetroAchievements guide: Starflight | RetroAchievements community | https://github-wiki-see.page/m/RetroAchievements/guides/wiki/Starflight | Fan guide | **Genesis** | Via fetch summary | Artifact coordinates, homeworld names, Black Box, Velox/Spemin tips | Medium; Genesis only | Community wiki; license unclear |
| S13 | Starflight walkthrough | Bob Seljan (rpggamers.com, formerly the-spoiler.com) | https://rpggamers.com/walkthrough/starflight | Walkthrough | Unspecified (likely PC) | Via fetch summary, plus search snippets of the-spoiler pages | Orb, Cone and Black Egg locations; Uhlek brain-world requirement | Medium | © author |
| S14 | STARFLIGHT review (textfiles.com mirror) | Unknown author | https://mirror.cyberbits.eu/textfiles.com/games/REVIEWS/starflt.rev | Contemporary BBS review | IBM PC and Amiga | Via fetch summary | Code wheel behaviour, sound optional, no disk copy protection | Medium | Unknown |
| S15 | Web searches (snippets only): speedrun.com Genesis guide, the-spoiler.com, justgamesretro, Sega Retro | Various | (see query notes in report) | Search snippets | Mixed | Snippets only | Corroborating artifact coordinates; 7 vs 8 races | Low | — |
| S16 | Blocked or unavailable: MobyGames (403), starflight.fandom.com (402), segaretro.org (403), gamebanshee.com interview (403), hardcoregaming101.net/starflight (404) | — | https://www.mobygames.com/game/158/starflight/ etc. | — | — | Not read | None | — | — |
