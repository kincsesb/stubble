# Stubble — Fejlesztési Feladatlista

**Engine:** Unity 6 URP (6000.3.20f1) | **Platform:** Windows (Steam) + Steam Deck  
**Co-op:** 2–4 fő (Mirror 96.6.4 + FizzySteamworks) | **Ár:** $6.99 | **Célidő:** ~3 óra

---

## Státusz jelölések

- ✅ **KÉSZ** — implementálva és commitolva
- 🔧 **RÉSZBEN** — kód kész, wiring vagy finomítás hiányzik
- ⏳ **PENDING** — nem kezdődött el
- 🚫 **BLOKKOLT** — külső függőség hiányzik

---

## PHASE 0–3 — Foundation, Core, Co-op, Polish

> Minden P0–P3 task lezárva. Részletes előzmény: git history.

| Task | Státusz |
|------|---------|
| P0 — Project setup, grass system, player, tools, világ, gazdaság, save | ✅ KÉSZ |
| P1 — Mind az 5 tool, bálázó gép, Shop UI, parcel completion, EndScreen | ✅ KÉSZ |
| P2 — Mirror co-op, grass sync, bála/economy replication, Steam lobby | ✅ KÉSZ |
| P3 — Audio (9 SFX), Feel lib, LOD optimalizálás, Steam (25 ach.), 9 nyelv | ✅ KÉSZ |
| P2-05 — Co-op QA gameplay tesztelés 2+ játékossal | ⏳ PENDING |

---

## PHASE 4 — Karakterek & Animációk

> Blokkoló: 3D asset delivery (4 karakter modell, 4 tool modell + rig + animációk).

### V4-01 | 3D Karakterek integrálása
**Status:** 🚫 BLOKKOLT — modellek hiányoznak

- [ ] 4 egyedi karakter prefab létrehozása (1P nézet: csak karok + tool látszik)
- [ ] Karakter kiválasztó képernyő a főmenübe
- [ ] Co-op: más játékosok 3rd-person karakterként jelennek meg
- [ ] NetworkedPlayer: karakter index SyncVar-ként szinkronizálva

---

### V4-02 | 3D Tool Modellek integrálása
**Status:** 🚫 BLOKKOLT — tool modellek hiányoznak

- [ ] HandSickle, LongScythe, StringTrimmer, PushMower — 3D model csere placeholder helyett
- [ ] Tool animátor controller per-tool (Idle, WindUp, Sweep, Recovery állapotok)
- [ ] MeleeToolBase.cs: meglévő FSM animátor triggerekre kötve

---

### V4-03 | Interakciós Animációk
**Status:** 🚫 BLOKKOLT — animációk hiányoznak

- [ ] Kaszálás: WindUp → Sweep → Recovery (per karakter, per tool)
- [ ] Bálázás: E-tartás animáció (lehajol, présel)
- [ ] HayPile felvétel / lerakás
- [ ] Ride-on mower: seated idle + kormány animáció
- [ ] Idle animáció (légzés, várakozás)

---

## PHASE 5 — Viral & Cinematic Features

> Ezek a leginkább sharable / memelhető pillanatok. Marketing szempontból kritikus fázis.

---

### V5-01 | Kerek Bála Gurulás — Fix + Cinematic Kamera
**Status:** 🔧 RÉSZBEN — gurulás van, terrain-követés és pivot hibás

**Problémák (javítandó):**
- [ ] **Terrain-követés:** a bála jelenleg lebeg / átsüpped a terepen — Rigidbody + MeshCollider vagy SphereCast alapú lejtőkövetés szükséges
- [ ] **Pivot pont:** a gördülési tengely nem a bála közepén van — prefab center of mass fix
- [ ] **Forgás-irány:** a bála csak egy tengelyen forog, lejtőn célzatosan kell pörögnie

**Cinematic kamera (hozzáadandó):**
- [ ] Ha a kerek bála 2+ másodpercig folyamatosan gurul és >3m/s sebességet ér el → cinematic kamera aktivál
- [ ] Kamera a bála mögé/mellé csúszik (Cinemachine Virtual Camera), követi a bálát
- [ ] Slow-motion faktor: `Time.timeScale = 0.6` a cinematic alatt
- [ ] Játékos input nem blokkolódik (csak a kameranézet vált)
- [ ] Ha a bála megáll vagy leesik → 1.5s után visszavált FP kamerára
- [ ] `RoundBale.cs`-be: sebesség threshold + `CinematicCameraController` hívás

---

### V5-02 | AFK Madár — Cinematic Shoulder Bird
**Status:** ⏳ PENDING

**Logika:**
- [ ] `AFKDetector.cs` — ha a játékos **60 másodpercig** nem mozdul (pozíció + rotáció threshold), aktivál
- [ ] Madár prefab spawn: a játékos válla közelében repül be, leül (animált)
- [ ] Cinematic kamera: Cinemachine virtual cam lassan köré kerül elölről, a játékos arcát/vállát mutatja
- [ ] Hangeffekt: madárcsiripelés loop amíg ül
- [ ] Ha a játékos mozdul → madár elrepül (animáció), FP kamera visszavált
- [ ] Co-op: mindenki látja a madarat a saját karakterén (NetworkBehaviour spawn)
- [ ] `PlayerController.cs`-be: mozgás/input event → `AFKDetector.ResetTimer()`
- Találni kell egy jó assetset!
---

## PHASE 6 — Vicces Achievementek & Easter Eggek

> Alacsony implementációs cost, magas viral potenciál.

### V6-01 | Achievement csomag — Mém achievementek
**Status:** ⏳ PENDING

Az összes achievement a meglévő `SteamManager.UnlockAchievement(string id)` API-n keresztül oldódik fel. Az ellenőrzési pontok alább per-achievement részletezve. A Steam achievement ID-k egyeznek az alábbi `ACH_*` konstansokkal.

**Implementáció általánosan:**
- [ ] `SteamManager.Achievements` statikus osztályba felvenni az új konstansokat
- [ ] `SteamManager.Thresholds`-ba kerülnek a numerikus küszöbök
- [ ] Az ellenőrző hívások a legközelebb lévő logikai pontba kerülnek (nem külön Tracker osztály)

---

**`ACH_TOUCH_GRASS` — "Touch Grass"**
*"You touched grass. Literally."*
- Trigger: `GameEvents.OnGridCellCut` első hívása (bármely tool, bármely parcel)
- Hívás helye: `StatisticsTracker.OnGridCellCut()` — első cut után `SteamManager.Instance?.UnlockAchievement(...)`
- Elvárás: megjelenik az első kaszáláskor, nem késleltethető

---

**`ACH_FREE_REAL_ESTATE` — "It's Free Real Estate"**
*"You bought land. The grass was already there."*
- Trigger: `GameEvents.OnParcelUnlocked` első hívása
- Hívás helye: `StatisticsTracker.OnParcelUnlocked()` — parcelCount == 1 esetén
- Elvárás: csak az első vásárláskor tüzel, második parcelre nem

---

**`ACH_NPC_BEHAVIOR` — "NPC Behaviour"**
*"You walked the same path 10 times. You are the NPC."*
- Trigger: a játékos 10-szer kasszál ugyanazon a ~3m sugarú körzetben (nem egymás után, összesen a session során)
- Detektálás: `NpcBehaviourTracker.cs` (új, lightweight) — `OnGridCellCut` eventre figyel, az utolsó 50 vágási pozíciót tárolja egy `Queue<Vector3>`-ban; ha egy 3m sugarú cellán belül >10 találat akkumulálódik → unlock
- Elvárás: NEM kell egymás után 10-szer, csak ugyanazon a területen összességében; az optimális kaszáló-útvonal NEM triggeri (mert különböző cellákat vág)

---

**`ACH_GONE_WITH_WIND` — "Gone With The Wind"**
*"A bale rolled off the map. It's someone else's problem now."*
- Trigger: `RoundBale` pozíciója eléri a terrain boundary szélét (map edge collider vagy `transform.position.x/z > mapBounds` check)
- Hívás helye: `RoundBale.Update()` — boundary check, egyszer tüzel per bála, nem repeated
- Elvárás: a bála fizikailag guruljon le (ne teleport), a boundary ~5m-rel a látható terepen kívül legyen

---

**`ACH_THIS_IS_FINE` — "This Is Fine"**
*"You lost 3 bales. The field is fine. Everything is fine."*
- Trigger: session során 3 bála elveszik anélkül hogy eladták volna (dropped + 60s timeout után despawn, VAGY beleesik a kútba, VAGY traktor löki ki a kezedből)
- Detektálás: `SessionState.LocalPlayer.BalesLost` counter, `++` minden `SquareBale.OnDespawnWithoutSale()` és `OnKnockFromPlayer()` hívásakor
- Hívás helye: `StatisticsTracker` — `BalesLost >= 3` esetén unlock
- Elvárás: az eladott bálák NEM számítanak elveszettnek; a counter session-szinten akkumulál

---

**`ACH_FRIENDLY_FIRE` — "Friendly Fire"**
*"The scythe does not discriminate."*
- Trigger: co-op-ban `MeleeToolBase.OnSweepHit()` egy másik `PlayerController`-t talál el (nem önmagát)
- Hívás helye: `MeleeToolBase.cs` — `hit.collider.TryGetComponent<PlayerController>(out var other) && other != localPlayer`
- Elvárás: csak co-op-ban (2+ játékos aktív); single-playerben nem érhető el; véletlenszerű és szándékos egyaránt számít

---

**`ACH_TETRIS` — "Tetris Farmer"**
*"Four bales, neatly stacked. Somewhere, a Tetris theme plays."*
- Trigger: 4 `SquareBale` objektum egyszerre 1.5m-en belül van egymástól (2×2 elrendezés vagy torony)
- Detektálás: `BaleStackDetector.cs` (új) — minden bála spawn/drop esetén `Physics.OverlapSphere(pos, 1.5f, baleLayer)` → ha ≥4 találat → unlock
- Elvárás: báláknak a földön kell lenniük (nem kézben tartva); a detektálás a drop pillanatában fut le

---

**`ACH_SKILL_ISSUE` — "Skill Issue"**
*"The slope won. The slope always wins."*
- Trigger: játékos bálát ejt (drop vagy knockback) olyan ponton ahol a terrain slope >15 fok
- Detektálás: `PlayerController.DropSquareBales()` híváskor → `Physics.Raycast` lefelé → `TerrainSampler.GetSlopeAngle(pos)` → ha >15f → unlock
- Elvárás: NEM kell a bálának legurulnia (a lejtőn való ejtés elég); a traktor knockback is számít ha a talaj lejtős

---

**`ACH_SPEED_COMPLETE` — "Certified Fresh Hay"**
*"Done in record time. The cows are impressed."*
- Trigger: `EndScreen.OnEnable()` — `elapsed < SteamManager.Thresholds.SPEEDRUN_SECONDS` (már implementálva `EndScreen.cs`-ben)
- Elvárás: már kész, csak a Steam ID-t kell felvenni a `SteamManager.Achievements`-be ha még nincs

---

**`ACH_TOO_LONG` — "Farmolás: Végleges megoldás"**
*"You were warned."*
- Trigger: Nuclear ending lezárultakor (`EndingOrchestrator` nuclear path vége)
- Hívás helye: `EndingOrchestrator` — a nuclear sequence befejezésekor, mielőtt az EndScreen megnyílik
- Elvárás: csak a Nuclear ending triggereli, a Peaceful/Loop nem

---

**`ACH_AFK_BIRD` — "New Friend"**
*"A bird landed on you. You are officially a Disney protagonist."*
- Trigger: V5-02 `AFKDetector` bird spawn esemény — madár leszáll a játékos vállára
- Hívás helye: `AFKDetector.OnBirdLanded()` callback
- Elvárás: V5-02 implementációjának függvénye; a madárnak fizikailag le kell ülnie

---

**`ACH_MIDNIGHT` — "Midnight Harvest"**
*"You sold hay at midnight. We don't judge. We just note it."*
- Trigger: `SaleStand.Interact()` meghívásakor `System.DateTime.Now.Hour == 0` (helyi idő szerint)
- Hívás helye: `SaleStand.Interact()` — az earn hívás után, külön check
- Elvárás: valódi rendszerórán alapul, nem in-game időn; 00:00–00:59 között bármikor számít

---

**`ACH_NICE_69` — "Nice."**
*"Nice."*
- Trigger: `CurrencyManager._money == 69` bármely tranzakció után
- Hívás helye: `CurrencyManager.CheckSpecialAmounts()` — `Earn()` és `TrySpend()` után egyaránt hívva (V6-02-ben részletezve)
- Elvárás: toast is jelenik meg (*"Nice."*) az achievement mellett; nem kell pontosan eladásból jönnie

---

**`ACH_BLAZE_420` — "Blaze It, Farmer"**
*"$420. The hay business is booming."*
- Trigger: `CurrencyManager._money == 420`
- Hívás és elvárás: azonos az `ACH_NICE_69`-cel

---

**`ACH_HESOYAM` / `ACH_MOTHERLODE` / `ACH_DOOM` — Cheat kód achievementek**
- Trigger: `CheatCodeActivator.Activate(string code)` — V6-02 implementációjának része
- Elvárás: az activator hívja az unlock-ot, nem a terminál közvetlenül

---

**`ACH_SHEEP` — "Did You See That?"**
*"A sheep ran through your field. This is not in the manual."*
- Trigger: ritka birka NPC megjelenik a mezőn, és a játékos **látja** (kamerájának forward vektora a birka irányába mutat, ≤30m, 3 másodpercig)
- Spawn esély: `1/500` per mowing tick (kb. ~3-5 percenként 1 esély ha folyamatosan kaszálsz)
- A birka megjelenik a mező szélén, átfut a másik oldalra (~8 másodperc), eltűnik — semmi más
- Hívás helye: `SheepSpawner.cs` (új, egyszerű) — spawn + timer + player facing check
- Elvárás: ha a játékos nem néz oda, NEM kapja meg az achievementet; ez szándékos

---

**`ACH_FOUR_LEAF` — "Four Leaf Clover"**
*"All four of you, together. For one brief moment, united by hay."*
- Trigger: mind a 4 aktív co-op játékos 2m-en belül van egymástól, legalább 3 másodpercig
- Detektálás: `CoopProximityDetector.cs` (új) — minden frame-ben, csak ha `NetworkedPlayer.activeCount == 4`; 3s folyamatos proximity → unlock
- Elvárás: csak 4 játékos esetén érhető el; 2-3 fős co-op-ban nem triggerelhető

---

**`ACH_42KM` — (csak szám: "42")**
*[Steam leírás szándékosan üres — a játék nem magyaráz]*
- Trigger: `SessionState.LocalPlayer.DistanceTravelledM >= 42000`
- Hívás helye: `StatisticsTracker.Update()` — egyszer ellenőrzi és unlock-ol
- Elvárás: a Steam oldalon nincsen leírás, csak a szám "42" szerepel achievement névként; a közösség kitalálja a referenciát

---

### V6-02 | Cheat Kód Rendszer — Név-alapú és Runtime kódok
**Status:** ⏳ PENDING

**Implementáció:** `NameCodeHandler.cs` (névbevitelnél figyel) + `CheatCodeActivator.cs` (runtime, bárhonnan hívható)

#### Név-alapú kódok (karakter névválasztásnál aktivál)

| Név | Hatás | Referencia |
|-----|-------|-----------|
| `hesoyam` | +50.000$ azonnal + `ACH_HESOYAM` achievement | GTA San Andreas |
| `motherlode` | +50.000$ + "Sims Energy" achievement toast | The Sims |
| `iddqd` | Végtelen stamina **és** végtelen üzemanyag 5 percig + `ACH_DOOM` achievement | DOOM |

**Technikai részletek:**
- [ ] `NameCodeHandler.cs` — névbevitel `OnEndEdit` eseményre figyel, case-insensitive összehasonlítás
- [ ] `CheatCodeActivator.cs` — singleton, `Activate(string code)` publikus API, `CurrencyManager` / `PlayerController` / `PoweredToolBase` módosítja
- [ ] `iddqd` esetén: `PlayerController._stamina = float.MaxValue` guard + `PoweredToolBase._fuel = float.MaxValue` guard, 300s timer után visszaáll
- [ ] Minden kód aktiválásakor: rövid "cheat aktivált" toast UI + achievement unlock
- [ ] Kódok nem stackelhetők (ugyanaz a kód másodszor nem vált ki hatást)

#### Pénz-összeg alapú achievementek (CurrencyManager.Earn() után ellenőriz)
- [ ] `ACH_NICE_69` — ha a játékos egyenlege pontosan $69 → *"Nice."* toast + achievement
- [ ] `ACH_BLAZE_420` — ha a játékos egyenlege pontosan $420 → *"Blaze It, Farmer"* toast + achievement
- [ ] `CurrencyManager.cs`-be: `CheckSpecialAmounts()` hívás minden `Earn()` / `TrySpend()` után

---

### V6-04 | Easter Egg — "Certified Fresh Hay" eladási kommentek
**Status:** ⏳ PENDING

**Mit csinál:** Minden eladáskor (bármekkora összegnél) a Sale Stand egy szubjektív, abszurd "minőségértékelést" mutat a pénz-animáció alatt, 2.5 másodpercig látható toast formájában.

**Megjelenítés:**
- A meglévő `HUDController` toast rendszerben jelenik meg, a pénzösszeg felirat alatt
- Halvány szín (pl. `#AAAAAA`), kisebb betűméret mint a pénz
- 2.5s után kifakul (alpha tween), nem kell kattintani

**Kommentek pool (en.json `sale.comment.*` kulcsok, 10 db):**
- `sale.comment.0`: *"The cows voted this Hay of the Year. The vote was not close."*
- `sale.comment.1`: *"Slightly dusty. We'll take it."*
- `sale.comment.2`: *"Suspicious origin. Accepted anyway."*
- `sale.comment.3`: *"Premium quality. According to you."*
- `sale.comment.4`: *"Gordon Ramsay saw this. He left. He's not coming back."*
- `sale.comment.5`: *"The hay has character. Too much character, arguably."*
- `sale.comment.6`: *"We've seen worse. Once."*
- `sale.comment.7`: *"A+ packaging. C- content. We'll average it out."*
- `sale.comment.8`: *"The buyer wept. We're not sure why. We didn't ask."*
- `sale.comment.9`: *"Certified fresh. Do not question the certification process."*

**Kiválasztás logikája:** nem teljesen random — az előző eladáshoz képest más komment jön (lastIndex tracking), így nem ismétlődhet kétszer egymás után.

**Implementáció:**
- [ ] `SaleStand.cs`: `ShowSaleComment(int earned)` metódus, `_lastCommentIndex` int field
- [ ] `HUDController.ShowSaleComment(string text)` — új metódus, meglévő toast rendszer alapján
- [ ] `en.json` + `hu.json`: 10-10 komment hozzáadva a `sale.comment.*` kulcsokhoz
- [ ] Ha `earned == 0` (üres eladás): ne jelenjen meg komment

---

### V6-05 | Easter Egg — Konami Kód Speedrun Timer
**Status:** ⏳ PENDING

**Mit csinál:** A Konami-kód (↑↑↓↓←→←→) begépelése után a HUD-on megjelenik egy ms-pontos speedrun timer, amely a session elejétől mér. A speedrun közösség meg fogja találni, és ingyen marketingeli a játékot.

**Input szekvencia:**
- `Up Up Down Down Left Right Left Right` — 8 input, bármikor begépelhető játék közben
- Keyboard: `W W S S A D A D` (WASD layout alapján) — NINCS B A a végén, mert az Interact és Jump
- Gamepad: D-pad `Up Up Down Down Left Right Left Right`
- Az `InputSequenceDetector` az InputSystem `InputAction` Performed callbackjeit figyeli

**Timer megjelenítése:**
- Pozíció: jobb felső sarok, a completion % HUD elem fölé igazítva
- Formátum: `HH:MM:SS.mmm` — pl. `00:47:23.441`
- Font: monospace (a meglévő terminal fonthoz hasonló stílus)
- Szín: halvány fehér (`#CCCCCC`), kis betűméret — nem tolakodik
- Toggle: első kód → megjelenik; második begépelésre → eltűnik
- Csak a jelenlegi session elejétől mér (`Time.realtimeSinceStartup`); nem mentett

**Implementáció:**
- [ ] `InputSequenceDetector.cs` — általános szekvencia detektor: `Vector2[]` elvárt irányok, `OnSequenceCompleted` Unity event
- [ ] `SpeedrunTimerHUD.cs` — `Update()` frissíti a szöveget, `Toggle()` be/ki kapcsol
- [ ] `InputSequenceDetector` és `SpeedrunTimerHUD` egyazon GameObject-en a scene-ben (nem PlayerController, scene-szintű)
- [ ] Achievement: `ACH_KONAMI_CODE` — a meglévő Konami achievement (SteamManager-ben már van) az `InputSequenceDetector.OnSequenceCompleted` hívásból unlock-ol

---

## Anti-pattern lista — TILOS (Spec §8.11)

1. Cut animáció blokkol inputot — queue helyette
2. Randomizált swing timing — tönkreteszi a ritmust
3. Hard stamina lockout — lassít de nem állít meg
4. Egyetlen vágó hang variáció nélkül
5. Fű eltüntetése — tarlóvá laposítás kell
6. Realisztikus jármű fizika
7. Játékos felborítása bálák által — csak odébb tolja
8. Lejtőn guruló kerek bála "megjavítása" — **ez a játék**
9. UI számok snapping — mindig animálva
10. Tutorial popup-özón — max 5 egysoros hint
11. Unicode szimbólumok (★☆✓) ShopUI-ban — LiberationSans SDF nem támogatja

---

## Függőségek és kockázatok

| Kockázat | Súlyosság | Mitigation |
|----------|-----------|------------|
| 3D asset delivery késik (V4 block) | 🔴 Magas | Placeholder mesh-ekkel fejlesztés, swap in later |
| Bála terrain-követés fizika bugos | 🟡 Közepes | SphereCast alapú megoldás stabilabb mint Rigidbody MeshCollider |
| Cinematic kamera + co-op szinkron | 🟡 Közepes | Cinematic csak lokálisan fut, nem kell hálózaton szinkronizálni |
| >3h ending spoiler kerülendő | 🟢 Alacsony | Ne kerüljön trailerbe, csak a játékon belül fedezhető fel |

---

## PHASE 7 — Co-op Chaos & Viral Mechanics

> Ezek a mechanikák co-op stream-content generátorok. Minden egyes mechanika egy potenciális clip. Prioritás: alacsony kód-cost, magas vicc-sűrűség.

### V7-01 | WC Buff — "Porcelain Throne"
**Status:** ⏳ PENDING

**Logika:**
- WC objektum a pajta/barn közelében, `IInteractable` interface
- Leülés: `PlayerController.IsSitting = true` → mozgás blokkolva, animáció (sit pose)
- `SaleStand.SellBales()`: minden eladáskor check → ha `AnyPlayerSitting()` → értékmultiplier **+10%**
- Felállás: újabb `E` gomb

**Implementáció:**
- [ ] `ToiletInteractable.cs` — `IInteractable`, `IsSitting` toggle, `NetworkedPlayer` SyncVar szinkron (co-op)
- [ ] `SaleStand.GetParcelMultiplier()`: `+ (ToiletInteractable.AnyoneSeated ? 0.10f : 0f)` szorzó
- [ ] HUD tooltip: *"Someone is working hard for the team."*
- [ ] Ha egyszerre 2 játékos próbál ülni (csak 1 WC): toast *"Occupied."*

**Achievement-ek:**
- `ACH_PORCELAIN_THRONE` — **"Porcelain Throne"** — leültél a WC-re
- `ACH_MORAL_SUPPORT` — **"Moral Support"** — te ültél miközben a másik eladott
- `ACH_QUEUE_THEORY` — **"Queue Theory"** — co-op-ban mindenki egyszerre próbált ülni

---

### V7-02 | Kutya Szar + Damilos Fűnyíró — "Occupational Hazard"
**Status:** ⏳ PENDING

**Logika:**
- 5–8 db `DogPoop` objektum szétszórva a mezőn (alacsony, fűben rejtett, kis barna mesh)
- `StringTrimmer.OnSweep()`: ellenőriz `DogPoop`-ot a sweep cone-ban → ha talál → `CameraFXController.PlayPoopSplatter()`
- Egyéb eszközök (kasza, push mower, ride-on): NEM triggereli — a damilos specifikus (fizikailag indokolt, oldalra csap)
- Bare Hand + E a poopra: felvehető, dobható más játékos irányába (`ThrowObject()`)

**Vizuális effekt:**
- [ ] Barna splash overlay (`ScreenFX` UI Image, screen-space), alpha fade 15 másodperc alatt
- [ ] SFX: undorító fröccs hang (rövid, egyedi clip)
- [ ] HUD toast: *"...you should've seen that."*
- [ ] Ha más játékos kamerájára fröccsen (dob): ugyanaz az overlay + *"Really?"* toast

**Implementáció:**
- [ ] `DogPoopObject.cs` — trigger zone, `IsActive` flag (ha felvette valaki → deaktivál)
- [ ] `CameraFXController.cs` — `PlayPoopSplatter(float duration)` metódus, `Image` alpha coroutine
- [ ] `StringTrimmer.cs` → `OnSweepHit()`: `Physics.OverlapSphere` → `DogPoopObject` check

**Achievement-ek:**
- `ACH_OCCUPATIONAL_HAZARD` — **"Occupational Hazard"** — elcsaptad a szart
- `ACH_DEDICATED_WORKER` — **"Dedicated Worker"** — kézzel vetted fel
- `ACH_FRIENDLY_SPLATTER` — **"Friendly Splatter"** — más játékos kamerájára dobted

---

### V7-03 | Alkohol — "Liquid Courage"
**Status:** ⏳ PENDING

**Logika:**
- Interaktálható palack (barn-ban elhelyezve, fix pozíció — NEM vásárolható, nem respawn)
- `PlayerController.isDrunk = true`, **120 másodperc** időtartam
- Co-op: másik játékosnak "odakínálható" (E a játékos közelében aki tartja) — ők döntik el elfogadják-e

**Hatások amíg részeg:**
- Swing speed multiplier: **×1.35** (gyorsabb kaszálás)
- Camera sway: sin hullám yaw-ra `±3°, 0.8Hz`
- Chromatic aberration enyhe boost (URP Volume)
- Alkalmi hiccup SFX (véletlenszerűen 8–20 másodpercenként)
- HUD: `LIQUID COURAGE: 1:47` visszaszámláló

**Traktorra vonatkozó szabályok:**
- `RideOnMower.TryMount()`: ha `isDrunk` → MEGTAGAD + toast: *"YOU'VE HAD ENOUGH."*
- Ha mégis megpróbál felszállni 3-szor egymás után: felszáll, azonnal leesik, 5 másodpercig blokkolja a traktort
- Push Mower részegen: engedélyezett, de a sway miatt nehézkes célozni

**Implementáció:**
- [ ] `DrinkableBottle.cs` — `IInteractable`, `PlayerController.SetDrunk(120f)` hívás
- [ ] `PlayerController`: `isDrunk` bool, `_drunkTimer`, `ApplyDrunkSway()` az `UpdateCamera()`-ban
- [ ] `RideOnMower.TryMount()`: `if (player.isDrunk) return false;` guard
- [ ] `ToolAudioManager`: `PlayHiccup()` metódus (új AudioSource slot, ha kell)

**Achievement-ek:**
- `ACH_LIQUID_COURAGE` — **"Liquid Courage"** — első ivás
- `ACH_DESIGNATED_DRIVER` — **"Designated Driver"** — te sober voltál amíg a többiek ittak
- `ACH_INTERVENTION` — **"Intervention"** — 3× próbáltál részegen traktorra szállni
- `ACH_FULL_EXPERIENCE` — **"The Full Experience"** — részegen ültél a WC-re *(Alkohol + WC kombó)*

---

### V7-04 | Kavics Projektil — "Rock and Roll"
**Status:** ⏳ PENDING

**Logika:**
- Bármely **melee / motor eszközzel** (Hand Sickle, Long Scythe, Push Mower, Ride-On Mower) véletlenszerűen elcsapható egy rejtett kavics
- **String Trimmerrel NEM** — fizikailag indokolt (a damilos nem dobja ki a törmeléket)
- Valószínűség: **1/150 swing** melee eszközöknél, **1/80 mowing tick** powered eszközöknél
- Kavics véletlenszerű irányban indul, de előnyben részesíti a legközelebbi másik játékost (`Physics.Raycast` → closest `PlayerController`)
- Becsapódás: a célzott játékosnál kis screen shake + `_externalVelocity` lökés + flinch SFX

**Cinematic kamera:**
- Az OKOZÓ játékosnál aktivál (aki a kavicsot elcsapta), nem a célpontnál
- Cinemachine Virtual Cam: kicsit oldalra csúszik, **0.4s slow-motion** (`Time.timeScale = 0.4f`), a kavics röppályáját mutatja
- A kavics a kamerában látható kis mesh (`RockProjectile` prefab, fast Rigidbody, 40–60 m/s)
- Becsapódás pillanatában visszavált FP kamerára, `Time.timeScale = 1f`
- Ha nem talált el senkit (kavics kiment a mezőn): nincs slow-mo, csak SFX

**Bála veszteség:**
- Ha a célzott játékos épp bálát cipel: `DropSquareBales()` kényszerítve — bálák szétszóródnak
- Ha bálázás közben kapja (E tartva): bálázás megszakítva, timer reset

**Implementáció:**
- [ ] `RockProjectile.cs` — `Rigidbody`, `OnCollisionEnter` → `PlayerController.TakeRockHit()`, destroy after 5s
- [ ] `MeleeToolBase.OnSweepHit()` + `PoweredToolBase.OnMowTick()`: 1/N eséllyel `TrySpawnRock()`
- [ ] `TrySpawnRock()`: raycast legközelebbi playert, spawn `RockProjectile`, `AddForce` irányba
- [ ] `CinematicRockCamera.cs`: coroutine, Cinemachine blending, `Time.timeScale` kezelés
- [ ] String Trimmer kivétel: `StringTrimmer` nem hívja `TrySpawnRock()`-ot

**Achievement-ek:**
- `ACH_ROCK_AND_ROLL` — **"Rock and Roll"** — elcsaptál egy kavicsot
- `ACH_HEADSHOT` — **"Headshot"** — kavics eltalált egy másik játékost
- `ACH_DUCK` — **"Duck!"** — te voltál a célpont és elvesztetted a bálád
- `ACH_THREE_BIRDS` — **"One Stone"** — egy kavics 2 játékost ért el (bounce / átsüvít)

---

### V7-05 | Traktoros Ütközés — "Involuntary Flight"
**Status:** ⏳ PENDING

**Logika:**
- `RideOnMower` sebességalapú ütközés detektálás (`OnControllerColliderHit` vagy trigger)
- Csak **>2 m/s** traktor sebességnél aktivál (lassú mowing nem dob el senkit)
- Az elütött játékos `_externalVelocity` kap: `forward * speed * 2.5f + Vector3.up * 4f`
- Magatehetetlenül repül: input lock **0.8 másodpercig** repülés közben (nem tud korrigálni)
- Landoláskor: puff particle + dull thud SFX

**Bála veszteség:**
- `DropSquareBales()` kényszerítve az ütközéskor
- A kiesett bálák random `Physics.AddForce` iránnyal szóródnak szét (3-5 irány)
- Más játékos felveheti a szétszóródott bálát és eladhatja → achievement

**Részeg sofőr:**
- Ha az ütő játékos részeg (`isDrunk`): `speed * 3.5f + Vector3.up * 6f` — erősebb lökés
- Toast az ütöttnél: *"IMPAIRED DRIVER"*

**Implementáció:**
- [ ] `RideOnMower.cs`: `OnControllerColliderHit(ControllerColliderHit hit)` → `TryHitPlayer(hit)`
- [ ] `TryHitPlayer()`: speed check → `player.AddExternalVelocity(impulse)` + `player.DropSquareBales()` + `player.LockInputFor(0.8f)`
- [ ] `PlayerController.LockInputFor(float seconds)` — új metódus, `_inputLockTimer` float

**Achievement-ek:**
- `ACH_INVOLUNTARY_FLIGHT` — **"Involuntary Flight"** — traktoros elrepített
- `ACH_CHAUFFEUR` — **"Chauffeur"** — te repítettél el valakit
- `ACH_ECONOMY_CLASS` — **"Economy Class"** — 10+ métert repültél egy ütéssel
- `ACH_FREELOADING` — **"Freeloading"** — felvettél és eladtál egy másik játékos által elejtett bálát

---

### V8-07 | Cat Economy — "Cat Tax"
**Status:** ⏳ PENDING

**Logika — szorzók stackelnek, egyszerre több feltétel is aktív lehet:**

- Bárki ül a WC-n (V7-01): **+10%**
- Macska veled szemben ül és nyalogatja magát, miközben te a WC-n ülsz: **+20%**

A WC-n ülés bónusza (+10%) és a macska WC-s bónusza (+20%) egymásra rakódik — tehát a WC + macska kombó önmagában **+30%** a csapatnak.

**Co-op példa:** A játékos ül a WC-n, a macska szemben nyalogatja magát → B játékos eladja a bálát → B játékos **+30%**-ot kap az eladáson.

**Macska követése (simogatás mechanika):**
- `E` tartva a macska közelében (1.5m, 1.2s) → macska követ 5 percig
- Miközben követ: `CatFollowing = true` → `SaleStand` +5% bónusz
- Ha a macska bálázáskor 5m-en belül van és a játékos felé néz: +15% (felülírja a +5%-ot)

**Macska WC-detektálás:**
- `ToiletInteractable.SeatedPlayer` referencia → `CatEconomy.CheckCatToiletBonus()`
- Macska `IsSittingIdle` (animátor state: `Sit_loop_1` vagy `Sit_loop_2`) ÉS `Vector3.Dot(cat.forward, toPlayer) > 0.7f` (szemben néz) ÉS `distance < 2m` → aktív
- Nincs időkorlát — amíg a feltételek fennállnak, a bónusz él

**Implementáció:**
- [ ] `CatEconomyManager.cs` — singleton, `GetCurrentSaleMultiplier()` visszaad `float` bónusz értéket; `SaleStand` ezt hívja
- [ ] `FarmAnimalChaseSystem.cs`: `IsCatFollowing` property, `IsCatWatchingBaling(Vector3 balePos)` metódus
- [ ] `ToiletInteractable.cs` (V7-01): `SeatedPlayer` property expose-olása `CatEconomyManager`-nek
- [ ] `CatEconomy`: `IsGrooming()` = macska `Sit_loop` animban van ÉS `_phase >= 1` (idle fázisban) ÉS szembe néz az ülő játékossal

**Achievement-ek:**
- `ACH_CAT_TAX` — **"Cat Tax"** — macska bónusszal adtál el bálát
- `ACH_PRODUCTIVITY_HACK` — **"Productivity Hack"** — WC + macska kombó egyszerre aktív volt eladáskor
- `ACH_PET_THE_CAT` — **"Pet the Cat"** *(már létezik a SteamManager-ben)* — simogatás triggerel

---

### V8-08 | Csirke Hadsereg — "They Remember"
**Status:** ⏳ PENDING

**Trigger:** A macska megöli a csirkét ÉS az eltelt játékidő **< 10 perc** (`_elapsed < 600f` a `FarmAnimalChaseSystem`-ben).

**Logika:**
- `ChickenDeath()` coroutine-ban: ha trigger feltétel teljesül → `StartCoroutine(SpawnChickenArmy())`
- Csirkék fokozatosan jelennek meg a **silók és a red barn** közelében, hullámokban:
  - **0. hullám** (azonnal): 3 csirke, 30m-en belül a silóktól, idle animáció
  - **2. perc:** +4 csirke, barn északi oldala
  - **4. perc:** +5 csirke, silók másik oldala, körbe állnak
  - **6. perc:** +6 csirke, minden csoport közelebb tolódik 5 méterrel
  - **8. perc+:** minden további 2 percben +3 csirke, max **30 csirke** összes
- Csirkék **semmit nem csinálnak** — csak állnak és néznek
- Ha a játékos **5m-en belül** megközelít bármelyik csoportot: az egész csoport szinkronban a játékos felé fordul (animáció nélkül, snap rotation), majd visszafordulnak ha elmegy
- Hangeffekt: nagyon halk, távoli "clucking" loop (3D spatial, csak ha közel vagy)

**Vizuális:**
- Ugyanaz a `Chicken` prefab mint az eredeti csirke, csak `FarmAnimalChaseSystem` AI nélkül
- Enyhe lélegzés/idle bob animáció (`CHK_IDLE` state)
- Éjszaka (ha van nap-éjjel ciklus): sötétben csak a szemeik csillognak

**Implementáció:**
- [ ] `ChickenArmyController.cs` — singleton, `Activate()` hívja `FarmAnimalChaseSystem.ChickenDeath()`, `IEnumerator SpawnWaves()`
- [ ] `SpawnWaves()`: `WaitForSeconds(120f)` ciklusban, random pozíció a silo/barn körüli `float radius` sugarú gyűrűn belül
- [ ] `FarmAnimalChaseSystem.ChickenDeath()`: `if (_elapsed < 600f) ChickenArmyController.Instance?.Activate();`
- [ ] Csirkék: `NavMeshAgent` nélkül, `Animator` only, `LookAt` playerhez ha közel

**Achievement-ek:**
- `ACH_THEY_REMEMBER` — **"They Remember"** — a csirke hadsereg megjelent
- `ACH_SURROUNDED` — **"Surrounded"** — 20+ csirke van egyszerre a mezőn
- `ACH_WALK_AWAY` — **"Just Walk Away"** — 30 csirke jelenlétében fejezted be a mezőt

---

## Apró TODO-k & Polish

| Feladat | Státusz |
|---------|---------|
| Currency Image — pénznem ikon a HUD-ba | ⏳ PENDING |
| Tutorial tipp az első indításkor (game loop magyarázat) | ⏳ PENDING |
| Jobb betűtípus kiválasztása (fő UI font) | ⏳ PENDING |
| BUG: Bála spawn SFX hiányzik — `CompleteBaling()` után nincs hang amikor a bála előbukkan | ✅ KÉSZ |
| BUG: Élezésnél dupla `%%` ikon jelenik meg a UI-ban | ✅ KÉSZ |
| BUG: Macska state mentve, csirke nélkül is aktív és körbe fut — csak akkor fusson ha van élő csirke (`FarmAnimalChaseSystem`) | ✅ KÉSZ |
---

## CR:
- Currency Image nincs benne a UI briefben ezt pótolni kell!
- Traktoros vezetésnél legyen valami