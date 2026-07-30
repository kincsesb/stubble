# Fields — Fejlesztési Feladatlista

**Projekt:** Fields (working title) — Cozy first-person kaszáló & szénakészítő szimulátor  
**Engine:** Unity URP | **Platform:** Windows (Steam) + Steam Deck | **Co-op:** 2–4 fő (NGO)  
**Spec:** `Assets/MOWING_GAME_SPEC_v1.2.md.pdf`

---

## PHASE 0 — Skeleton / Foundation

### P0-01 | Project Setup — URP, csomagok, mappastruktúra
**Status:** pending

**Cél:** Tiszta Unity URP projekt alapok lerakása, co-op-ready csomagokkal.

**Teendők:**
1. Unity 6 (LTS) projekt ellenőrzés — URP pipeline asset, Quality Settings
2. Package Manager telepítés:
   - Unity Netcode for GameObjects (NGO)
   - Unity Input System (new)
   - Shader Graph
   - Cinemachine
   - TextMeshPro
3. Mappastruktúra:
   ```
   Assets/
     _Game/
       Audio/
       Config/
       Materials/
       Prefabs/
         Tools/ | World/ | UI/ | Network/
       Scenes/
       Scripts/
         Core/ | Grass/ | Tools/ | Hay/ | Economy/ | Network/ | UI/ | Save/
       Shaders/
       VFX/
   ```
4. Layer & Tag setup: Ground, Uncut, Obstacle, Bale, Player, HayPile
5. Physics Matrix beállítás
6. Input Action Asset: Move, Look, UseTool, Interact, Drop, Journal, ToolSelect
7. Bootstrap scene + Game scene

**Elfogadási kritérium:**
- Projekt megnyílik hibátlanul
- NGO NetworkManager jelen van
- Input Action Asset billentyűzeten és gamepaden működik

---

### P0-02 | Core ScriptableObject architektúra
**Status:** pending | **Blokkolt:** P0-01 után

**Cél:** Minden tuneable adat ScriptableObject-ban él — Inspector-ból szerkeszthető, kódmódosítás nélkül.

**Típusok:**
- `ToolData` — toolName, icon, speedLevels[4], enduranceLevels[4], powerLevels[4], upgradeCosts[3], purchaseCost
- `ParcelData` — parcelName, unlockCost, cuttableArea, targetTimeMinutes
- `GameConfig` — hayUnitsPerCollectionCell (60), collectionCellSize (6), gridCellSize (0.4), hayValueMultipliers[]
- `BalerData` — compression speed/carry capacity/density szintek (square + round)

**Teendők:**
1. Fenti osztályok: `Assets/_Game/Scripts/Core/Data/`
2. Default asset példányok a spec 5.3 értékeivel: `Assets/_Game/Config/`
3. Minden szám ScriptableObject-ban — nincs magic number a gameplay kódban

**Elfogadási kritérium:**
- Inspector-ból szerkeszthető minden érték
- ToolData mind az 5 szerszámhoz létezik kitöltve

---

### P0-03 | Grass System — RenderTexture mask + CPU logikai grid
**Status:** pending | **Blokkolt:** P0-01, P0-02 után

**Cél:** Kettős reprezentáció a spec 4. fejezete szerint. Ez a projekt legnagyobb technikai kockázata.

**Architektúra:**
```
GrassField (per parcel):
  GPU: RenderTexture R8, 1024x1024 vagy 2048x2048
       fehér = vágatlan, fekete = vágott
       min-blend write (soha nem lehet "uncut"-ra visszaírni)
       ping-pong buffer / CommandBuffer — NEM self-referencing blit

  CPU: bool[,] ~0.4m per cella — ez az igazságforrás
       completion %, hay yield, save/load, co-op sync mind innen

API:
  CutArea(Vector3 worldPos, float radius)
  CutCapsule(Vector3 from, Vector3 to, float radius)
  float GetCompletionPercent()
  bool[,] GetCutGrid()  // mentéshez
  event OnCellCut       // hay accumulation + co-op sync hallgatja
```

**Teendők:**
1. `GrassField.cs` megírása fenti API-val
2. CutArea / CutCapsule CPU implementáció (world→grid koordinátaváltás)
3. GPU: R8 RenderTexture, CommandBuffer ping-pong setup
4. Hay collection cell grid (6×6m cellák, 60 unit threshold)
5. `GetCompletionPercent()` — CPU gridből számolva
6. Debug Gizmo: CutArea vizualizáció

**Elfogadási kritérium (M1 binding):**
- CutArea után GPU maszkon fekete kör jelenik meg
- Ugyanaz a terület CPU gridben is vágottnak számít
- Completion % helyesen számol
- GPU-ból soha nem olvasunk vissza CPU-ra

---

### P0-04 | Grass Shader + chunked mesh rendering
**Status:** pending | **Blokkolt:** P0-03 után

**Cél:** Vágott fű tarlóvá lapul, nem eltűnik. Ez a játék vizuális szíve.

**Shader Graph (`GrassMask.shadergraph`):**
- `Sample Texture 2D LOD` a **vertex stage**-ben
- Mask 1.0 = teljes magasság, 0.0 = tarlómagasság (~5%)
- Tarló: sárgásabb szín, laposabb normál
- Cartoon stylized, nem PBR

**Chunked mesh:**
- 10×10m chunk-ok, mindegyik egy combined mesh (1 draw call)
- Distance LOD 3 szint: full / 50% / 20% density
- `DrawMeshInstancedIndirect` csak ha profiling bizonyítja (ne indíts onnan)

**Teendők:**
1. Shader Graph létrehozás
2. Chunk mesh generátor script
3. `GrassChunkManager.cs` — chunk instantiálás, shared RenderTexture
4. <150 draw call ellenőrzés (Frame Debugger)

**Elfogadási kritérium:**
- Vágás után fű tarlóvá lapul (nem tűnik el)
- Éles határvonal látszik vágott/vágatlan között
- <150 draw call in-view
- 60 fps integrated GPU-n

---

### P0-05 | Player Controller — FP mozgás, kézrendszer, co-op NetworkPlayer
**Status:** pending | **Blokkolt:** P0-01 után

**Cél:** First-person mozgás, kézvisual, co-op-ready NetworkPlayer — egyetlen prefab mindkét módhoz.

**Kulcspontok:**
- `PlayerController : NetworkBehaviour` — `IsOwner` check mindenhol
- CharacterController alapú mozgás (nem Rigidbody)
- HandsRoot + ToolHolder child transform a kamerán
- Carry penalty: 1 bála -25%, 2 bála -40%, 3 bála -50%
- Más játékos: capsule placeholder (végleges karakter art Stage 2-ben)

**Teendők:**
1. `PlayerController.cs` NetworkBehaviour-ként
2. Input System PlayerInput összekötés
3. `ToolHolder.cs` — equip/unequip, scroll/1-5 input
4. Camera spring/bob placeholder (végleges értékek P3-ban)
5. Gamepad dead-zone és sensitivity
6. Co-op: más játékos névjegy (TMPro szöveg felette)

**Elfogadási kritérium:**
- WASD + egér + gamepad mind működik
- NGO-n át host+client látja egymást
- Stamina carry penalty ScriptableObject-ból jön

---

### P0-06 | Tool System váz — BaseTool, ToolHolder, Hand Sickle alap swing
**Status:** pending | **Blokkolt:** P0-03, P0-05 után

**Cél:** Közös swing ciklus architektúra — különbségek csak adatban és override-okban.

**Swing ciklus (Section 8.1.1):**
- WindUp 25% — kamera ~1° drift ellentétes irányba, vágás NEM aktív
- Sweep 30% — vágás AKTÍV, arc sampling minden frame-en
- Recovery 45% — vágás NEM aktív, következő input 70%-tól queue-lható

**Osztályok:**
```
BaseTool : NetworkBehaviour
  MeleeToolBase : BaseTool   (sarló, kasza) — stamina pool
  PoweredToolBase : BaseTool (trimmer, push mower, ride-on) — fuel pool
  HandSickle : MeleeToolBase
```

**Teendők:**
1. `BaseTool.cs`, `MeleeToolBase.cs`, `PoweredToolBase.cs`
2. Swing state machine (WindUp/Sweep/Recovery arányok)
3. Input queue — soha nem vész el input recovery alatt
4. `HandSickle.cs` — CutCapsule hívás sweep minden frame-én
5. Hit/Whiff placeholder detektálás (audio/kamera P1-ban)

**Elfogadási kritérium:**
- Sarló vágja a fű GPU+CPU maszkot
- Input sosem vész el
- Stamina drain és regen működik
- Ciklus arányok pontosan 25/30/45%

---

### P0-07 | Világ váz — 4 parcel terrain, kerítések, stand + bálázó pozíciók
**Status:** pending | **Blokkolt:** P0-01, P0-02 után

**Cél:** Pálya fizikai váza helyes méretekkel, placeholder art-tal.

**Parcel méretek:**
| Parcel | Relatív méret | Terület | Domborzat |
|--------|--------------|---------|-----------|
| 1 — Home Paddock | 1× | ~1200 m² | Sík |
| 2 — Middle Meadow | 2× | ~2400 m² | Sík, enyhe |
| 3 — Far Meadow | 3.5× | ~4200 m² | Egy bank 8–12° |
| 4 — Top Meadow | 5× | ~6000 m² | Rolling, max 12° |

**Teendők:**
1. Unity Terrain helyes méretekkel
2. Parcel boundary colliderek + trigger
3. Kerítés placeholder mesh-ek
4. `ParcelGate.cs` prefab (Open() animáció stub)
5. Stand + Baler prefab (placeholder cube + InteractTrigger)
6. GrassField komponens mind a 4 parcelre
7. Obstacle placeholder-ek (fák, sziklák, kerítések a budget szerint)

**Elfogadási kritérium:**
- 4 parcel járható
- GrassField minden parcelre inicializálódik
- Parcel 4-en valódi 12°-os lejtő van

---

### P0-08 | Széna akkumuláció + HayPile spawn
**Status:** pending | **Blokkolt:** P0-03 után

**Cél:** Széna fizikai objektum — ott halmozódik ahol a játékos vágott. "The field remembers."

**Logika:**
- 6×6m collection cell grid, float counter cellánként
- Minden CPU cella uncut→cut átmenet: +1 a legközelebbi cellában
- 60 unit → `HayPile` spawn a weighted centroidon, cell reset
- Leftover megmarad, nem vész el
- 3 fázisú loose hay decal (20% / 60% / 90% threshold)

**Teendők:**
1. `HayAccumulationSystem.cs` — collection grid logika
2. `HayPile.cs` NetworkObject — spawn, pickup, carry interface
3. 3 HayPile prefab méret (placeholder mesh)
4. Loose hay decal 3 fázis (placeholder material)
5. GrassField `OnCellCut` event hookup

**Elfogadási kritérium:**
- 60 unit vágás után HayPile megjelenik
- Decal 3 fázisban nő
- Leftover parcel completion után sem vész el

---

### P0-09 | Gazdaság váz — CurrencyManager, Shop stub
**Status:** pending | **Blokkolt:** P0-01, P0-02 után

**Cél:** Valuta és bolt keretrendszer — co-op-ban szerver-autoritatív, shared currency.

**Osztályok:**
- `CurrencyManager : NetworkBehaviour` — `NetworkVariable<int> money`, Earn/Spend ServerRpc
- `ToolUnlockManager : NetworkBehaviour` — `NetworkList<bool> toolsOwned`, `NetworkList<int> toolUpgradeLevels`
- Shop placeholder: OnGUI alapú, 3 tab (csak teszteléshez)

**Teendők:**
1. `CurrencyManager.cs`
2. `ToolUnlockManager.cs` NetworkList-ekkel
3. Shop placeholder OnGUI
4. Stand interact → Shop megnyitás
5. `ParcelManager.cs` — unlocked parcels, gate opening

**Elfogadási kritérium:**
- Co-op-ban pénz és owned tools szinkronizálva mindkét kliensen

---

### P0-10 | Alap HUD + Mentési rendszer váz
**Status:** pending | **Blokkolt:** P0-08, P0-09 után

**Cél:** Minimális HUD + robusztus save/load — fejlesztés közben ne kelljen mindent újrakezdeni.

**HUD elemek:**
- Stamina/Fuel bar (fades out ha teli)
- Animált money counter (~0.4s, nem snap)
- Field completion % (felső sarok)
- Carried bale indicator

**SaveData:**
- money, toolsOwned[5], toolUpgradeLevels[15], parcelsUnlocked[4]
- roundBalerOwned, balerUpgradeLevels[3]
- FieldSaveData: `byte[] cutGridRLE` (Run-Length Encoded)
- BaleSaveData: position, rotation, isRound
- Versioned schema (version: 1)

**Autosave triggerek:** parcel kész / vásárlás / bála eladva / 60 mp

**Elfogadási kritérium:**
- Save/Load teljes állapotot perzisztál
- RLE encoding ≥80%-kal kisebb mint raw bool tömb

---

## PHASE 1 — Core Systems

### P1-01 | Swing Feel System — Hit/Whiff, kamera kick, hang triggerek, partikulák
**Status:** pending | **Blokkolt:** P0-06 után

**Hit/Whiff detektálás (Section 8.1.3):**
| Eredmény | Feltétel | Audio | Kamera | Partikulák |
|----------|----------|-------|--------|------------|
| Full hit | >60% uncut | Crunch, legerősebb | Kick 2.0° | Full burst |
| Partial | 15–60% | Mid layer, -3 dB | Kick 1.2° | Reduced burst |
| Whiff | <15% | Air-swish | Kick 0.6° | Nincs |
| Obstacle | Collider találat | Sharp clang | Kick 3.0° (reversed!) | Sparks + bounce |

**Teendők:**
1. `SwingResultCalculator.cs` — arc cell intersection counting
2. `CameraKickSystem.cs` — spring-damped, első 3 swing 3° boost
3. `IToolAudioSource` interface + `PlaceholderToolAudio.cs`
4. Particle system (grass burst + sparks)
5. Obstacle PhysicsCast sweep phase-ban

---

### P1-02 | Mind az 5 szerszám implementálva
**Status:** pending | **Blokkolt:** P0-06, P1-01 után

**Szerszámok:**
- **Long Scythe** — szélesebb arc (1.4–2.4m), lassabb, mélyebb hang
- **String Trimmer** — folyamatos vágás, bogging (-20% cut rate + RPM drop), 0.3° kamera vibráció
- **Push Mower** — CutCapsule, -20% mozgás uncut fűn, pivot fordulás, deck toggle
- **Ride-On Mower** — kinematic arcade, 0→full in ~1.5s, body roll/pitch, seated FP kamera, deck engage "clunk"

**Elfogadási kritérium:**
- Minden szerszám-váltás azonnal érezhető (Section 8.1.7)
- Ride-on első 30 másodperce wow-feeling

---

### P1-03 | Bálázó gép + négyszög/kerek bála fizika
**Status:** pending | **Blokkolt:** P0-08, P0-09 után

**Bálázó:** Látható kompressziós folyamat → "thunk" hang + camera shake → fizikával ejektált bála.

**Square bale carry (Section 8.5):**
- 1 bála: -25% sebesség | 2 bála: -40%, részben takarja a képet | 3 bála: -50%, navigálni kell
- A takarás szándékos design döntés

**Round bale (Section 8.6) — a szignáló mechanika:**
- Rigidbody, ~80 kg, Drag 1.5, Angular Drag 2.0
- 8°+ lejtőn önállóan gurul — **NEM kell megjavítani**
- Off-center tolás → kanyarodik
- Fal ütközés: hang + camera shake, semmi destrukció
- Soha nem hagyja el a pályát

**Elfogadási kritérium (Section 8.6.5):** Kerek bálát a lejtő tetején elengedve önmagában szórakoztató nézni.

---

### P1-04 | Shop UI, Upgrade rendszer, Parcel unlock flow
**Status:** pending | **Blokkolt:** P0-09, P1-02 után

**Shop — flat panel, 3 tab, NPC nélkül:**
- TOOLS tab — sorban vásárolható 5 szerszám
- UPGRADES tab — 3 stat × 3 szint per owned szerszám
- UNLOCKS tab — round baler, következő parcel

**Upgrade runtime:** Stat értékek ScriptableObject-ból, upgrade után azonnal érvényes.

---

### P1-05 | Teljes eladási loop, parcel completion, végképernyő
**Status:** pending | **Blokkolt:** P1-03, P1-04, P0-10 után

**Selling:** Stand-nál bála lerakás → azonnali kifizetés, animált money counter, multi-bale pitch-chain hang.

**Parcel completion:** 100% cut AND 0 HayPile → completion event → ambient tone.

**End screen:** Mind a 4 parcel kész → stats táblázat (bales made, hay sold, idő, per parcel). Nincs cutscene.

**Journal (Tab):** Per-field completion %, bales made, money earned, tool levels.

---

## PHASE 2 — Co-op

### P2-01 | Co-op Netcode — NGO host/join, player spawn, session flow
**Status:** pending | **Blokkolt:** P1-05 után

**Session flow:**
- Host Game → NGO StartHost → load Game scene
- Join Game → join code → NGO StartClient
- **Egyjátékos = local host** — nincs külön kódútvonal!

**Relay:** Unity Gaming Services Relay, LAN fallback.

**Teendők:**
1. `NetworkSessionManager.cs` — Host/Join/Leave
2. `MainMenuUI.cs` — gombok, join code mező
3. Relay / Unity Transport setup
4. Player spawn + névjegy
5. Disconnect: maradék játékos(ok) folytatják

---

### P2-02 | Fű szinkronizáció co-op-ban — CPU grid delta sync
**Status:** pending | **Blokkolt:** P2-01, P0-03 után

**Megközelítés — delta sync:**
- Kliens vág → `CutAreaServerRpc` → szerver alkalmaz → `CutAreaClientRpc` broadcast
- Late join: szerver küld teljes RLE grid snapshot-ot
- GPU mask minden kliensen lokálisan frissül
- Nincs GPU→network transfer

**Bandwidth:** ~3.4 KB/s 4 játékos × 30 vágás/s — elfogadható.

---

### P2-03 | Bála + HayPile co-op szinkron, gazdaság szinkron
**Status:** pending | **Blokkolt:** P2-01, P1-03 után

**Szinkron szabályok:**
- HayPile: csak szerver spawnolja (NetworkObject)
- Square bale: ownership transfer pickup-kor, foglaltság check
- Round bale: szerver szimulál, NetworkRigidbody interpoláció klienseken
- Baler: compress cycle szerveren, ClientRpc az animációhoz
- Currency: minden purchase ServerRpc → validáció → NetworkList update

---

## PHASE 3 — Polish

### P3-01 | Audio rendszer — AudioMixer, layered ambient, szerszám hangok variációkkal
**Status:** pending | **Blokkolt:** P1-05 után

**AudioMixer csoportok:** Master → Tools / World / Ambience / UI / Music

**Minimális variációk:**
| Hang | Min. variáció | Pitch jitter |
|------|--------------|--------------|
| Sickle full hit | 5 | ±5% |
| Sickle whiff | 3 | ±7% |
| Long scythe full hit | 5 | ±5% |
| Obstacle strike | 3 | ±4% |
| Footstep / surface | 6 | ±8% |
| Bale drop | 4 | ±6% |
| Bale impact | 5 | erő szerint |
| Bale formation complete | 2 | ±3% |
| Sale payout | 3 | pitch-stepped |

**Ambience:** Rétegelt hangágyak (szél, madarak, rovarok, állatok). Nincs komponált zene. Loop pont-ok randomizálva — 60 percen át nem hallható váltás.

**Elfogadási kritérium:** 10 perc folyamatos vágás után a hangok nem idegesítők.

---

### P3-02 | Kamera feel tuning + Haptics + Accessibility
**Status:** pending | **Blokkolt:** P1-05 után

**Kamera paraméterek (Section 8.9):**
| Paraméter | Érték |
|-----------|-------|
| Default FOV | 70° (slider 60–100) |
| Head bob | 0.03m vertical, 0.015m lateral, footstep sync |
| Swing kick | 0.6–3.0° spring-damped, ~0.25s recovery |
| Landing impact | 2° dip, 0.2s recovery |

**Haptics:** Swing full hit (0.08s pulse), Obstacle (double pulse), Tool running (low continuous), Bale impact (strong single).

**Accessibility:** Head bob toggle, Camera shake toggle + intensity slider, Invert Y, FOV slider — mind Settings-ben és PlayerPrefs-ben perzisztálva.

---

### P3-03 | Teljesítmény optimalizálás — 60 fps integrated GPU-n
**Status:** pending | **Blokkolt:** P0-04, P1-05 után

**Hard requirements:**
| Metrika | Target |
|---------|--------|
| Frame rate | 60 fps Intel Iris Xe 1080p Medium |
| Steam Deck | 60 fps 1280×800 |
| Grass draw calls | <150 in-view |
| Cut latency | 1 frame-en belül látható |
| Memory | <2 GB |

**Quality presets:** Low (1024 RT, 50% density) / Medium (2048 RT, 75%) / High (2048 RT, 100%, MSAA 2x)

---

### P3-04 | Game Feel Pass — partikulák, VFX, UI animációk, first-cut moment
**Status:** pending | **Blokkolt:** P3-01, P3-02 után

**First-cut moment:** Első 3 swing = 3° kick (default 2°), legerősebb hit hang, bőséges particles.

**Section 8.12 Acceptance Tests — mind át kell menjen:**
1. **60s teszt** — új játékos instrukció nélkül 60mp után is vág
2. **Silent teszt** — hang nélkül vizuálisan olvasható; kép nélkül hangilag
3. **Gap teszt** — max speed + max mouse = 0 gap a vágásban
4. **Hill teszt** — kerek bála lejtőn önmagában szórakoztató
5. **Repetition teszt** — 10 perc vágás, hangok nem idegesítenek
6. **Transition teszt** — minden új szerszám első 3 másodpercében "jobb" érzés

---

### P3-05 | Steam integráció — Steamworks SDK, 25 achievement, Cloud save, Steam Deck
**Status:** pending | **Blokkolt:** P1-05 után

**25 achievement skeleton** (végleges nevek kliens adja):
ACH_FIRST_BALE, ACH_FIRST_ROUND, ACH_PARCEL_1–4, ACH_ALL_PARCELS, ACH_SPEEDRUN, ACH_MAX_CARRY, ACH_HILL_ROLLER, ACH_TOOL_COLLECTOR, ACH_FULL_UPGRADE, ACH_ALL_UPGRADES, ACH_ROUND_BALER, ACH_HAY_VALUE_MAX, ACH_BALE_100, ACH_BALE_500, ACH_COOP_PLAY, ACH_COOP_4 + 5 kliens által meghatározandó

**Rich Presence:** aktuális parcel neve megjelenik Steam-ben.

---

### P3-06 | Lokalizáció — string externalizáció, 9 nyelv, layout teszt
**Status:** pending | **Blokkolt:** P1-05 után

**Nyelvek:** EN, HU, DE, RU, ZH-Hans, PL, ES, PT-BR, JP

**Unity Localization package** — String Table-ök, Smart Strings, runtime language váltás.

**Max 5 kontextuális hint az egész játékban:**
1. "Hold [LMB] to swing continuously."
2. "Press [E] to interact."
3. "Deposit hay at the baler, then sell at the stand."
4. "The round bale rolls — manage the momentum."
5. "Trimmer and scythes reach corners the mower can't."

**Layout teszt:** DE és RU (~2× hosszabb stringek) nem törhet ki egyetlen UI elemből sem.

---

## Anti-pattern lista — TILOS (Section 8.11)

1. Cut animáció ami blokkol inputot — soha, queue helyette
2. Randomizált swing timing — tönkreteszi a meditatív ritmust
3. Hard stamina lockout — lassít, de nem állít meg
4. Egyetlen vágó hang variáció nélkül
5. Fű eltüntetése tarlóvá laposítás helyett
6. Realisztikus jármű fizika
7. Játékos felborítása bálák által (csak odébb tolja)
8. Lejtőn guruló kerek bála "megjavítása" — az a játék
9. UI számok snapping — mindig animálva
10. Tutorial popup-özón — max 5 egysoros hint összesen

---

## Függőségi gráf (összefoglaló)

```
P0-01 → P0-02 → P0-03 → P0-04
                       → P0-06 → P1-01 → P1-02 → P1-04
                       → P0-08 → P1-03 → P1-05 → P2-01 → P2-02
         P0-01 → P0-05 ↗                        ↘ P2-03
         P0-01 → P0-09 → P1-04                  P3-01
                       → P1-03                  P3-02
                                                P3-03
                P0-08 + P0-09 → P0-10 → P1-05  P3-04 (P3-01+P3-02 után)
                                                P3-05
                                                P3-06
```