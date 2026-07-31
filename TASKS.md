# Fields — Fejlesztési Feladatlista

**Projekt:** Fields (working title) — Cozy first-person kaszáló & szénakészítő szimulátor  
**Engine:** Unity URP 6 (6000.3.20f1) | **Platform:** Windows (Steam) + Steam Deck  
**Co-op:** 2–4 fő  
**Spec:** `Assets/MOWING_GAME_SPEC_v1.2.md.pdf` | **Dev Estimate:** `Assets/Fields_Unity_Development_Estimate.docx`

---

## ⚠️ Fontos döntések (docx vs spec eltérések)

### Co-op networking stack
A fejlesztői becslés **Mirror Networking + FizzySteamworks/Steamworks** transpoprt-ot javasol,
míg az eredeti spec és az eddigi kódváz **Unity Netcode for GameObjects (NGO)**-t feltételez.

| Szempont | NGO (jelenlegi kód) | Mirror + FizzySteamworks (dev ajánlás) |
|----------|--------------------|-----------------------------------------|
| Steam integráció | Unity Relay / UGS | Natív Steam lobby + overlay |
| CCU cost | UGS pricing | Nincs extra cost (Steam P2P) |
| Host migration | Támogatott | **Nincs** (spec: nincs szükség rá) |
| Kódmennyiség | Kevesebb boilerplate | Több, de Steam-native |
| Dev recommendation | ✗ | ✅ |

**→ Döntés: Mirror + FizzySteamworks (kliens jóváhagyta)**  
NGO eltávolítva a manifest-ből. Mirror manuális telepítés szükséges P2 előtt:
1. Unity Asset Store → "Mirror" → ingyenes import
2. Package Manager → Add from git URL: `https://github.com/Chykary/FizzySteamworks.git`
3. Steamworks.NET: OpenUPM-en keresztül automatikus (`com.rlabrecque.steamworks.net`)

### Asset felelősség (docx)
A fejlesztő azt feltételezi, hogy a kliens biztosítja:
- 3D modellek, animációk, UI art, ikonok
- Audio, ambient sound, zene
- Lokalizációs fájlok (stringek)
- Achievement lista

---

## Státusz jelölések
- ✅ **KÉSZ** — script megírva, commitolva
- 🔧 **RÉSZBEN** — kód kész, Unity Editor wiring hiányzik
- ⏳ **PENDING** — nem kezdődött el
- 🚫 **BLOKKOLT** — függőség hiányzik

---

## PHASE 0 — Skeleton / Foundation
> **Dev estimate: M1 (Vertical Slice/Core Foundation), 6–8 hét, $4,000**

### P0-01 | Project Setup — URP, csomagok, mappastruktúra
**Status:** 🔧 RÉSZBEN

**Kész:**
- Unity 6 URP projekt ✅
- InputSystem 1.19.0 ✅
- Shader Graph 17.3.0 ✅  
- manifest.json-ba felvéve: NGO 2.4.0, Cinemachine 3.1.4 ✅
- Mappa struktúra: `Assets/_Game/Scripts/{Core,Grass,Tools,Hay,Economy,Network,UI,Save}` ✅

**Hiányzik (Unity Editor):**
- [ ] Layers: Ground, Uncut, Obstacle, Bale, Player, HayPile
- [ ] Tags: Player, HayPile, Bale, Stand, ParcelBoundary
- [ ] Physics Matrix beállítás
- [ ] Input Action Asset: `UseTool`, `Interact`, `Drop`, `Journal`, `ToolSelect`, `ScrollTool` akciók hozzáadása
- [ ] Bootstrap scene létrehozása
- [ ] NetworkManager (NGO) elhelyezése scénában

---

### P0-02 | Core ScriptableObject architektúra
**Status:** ✅ KÉSZ

**Fájlok:** `Assets/_Game/Scripts/Core/Data/`
- `ToolData.cs` — toolName, icon, speedLevels[4], enduranceLevels[4], powerLevels[4], upgradeCosts[3], purchaseCost ✅
- `ParcelData.cs` — parcelName, unlockCost, cuttableArea, targetTimeMinutes ✅
- `GameConfig.cs` — gridCellSize(0.4), collectionCellSize(6), hayUnitsPerCollectionCell(60) ✅
- `BalerData.cs` — compressionSpeed, carryCapacity, density szintek, round bale physics ✅

**Hiányzik (Unity Editor):**
- [ ] Default asset példányok létrehozása: `Assets/_Game/Config/` (5 ToolData + 1 GameConfig + 2 BalerData + 4 ParcelData)

---

### P0-03 | Grass System — RenderTexture mask + CPU logikai grid
**Status:** ✅ KÉSZ

**Fájlok:** `Assets/_Game/Scripts/Grass/GrassField.cs`
- CPU bool[,] grid (igazságforrás) ✅
- GPU R8 RenderTexture ping-pong CommandBuffer ✅
- `CutArea(pos, radius)` + `CutCapsule(from, to, radius)` ✅
- `GetCompletionPercent()`, `GetCutGrid()`, `LoadCutGrid()` ✅
- `OnCellCut` event ✅
- Debug Gizmo ✅

**Hiányzik:**
- [ ] GrassField prefab bekötése a 4 parcelre Unity Editorban

---

### P0-04 | Grass Shader + chunked mesh rendering
**Status:** ✅ KÉSZ

**Fájlok:**
- `Assets/_Game/Shaders/GrassMaskWrite.shader` — Hidden CommandBuffer writer, min-blend ✅
- `Assets/_Game/Shaders/GrassBlade.shader` — URP ForwardLit, vertex mask sampling, stubble collapse ✅
- `Assets/_Game/Scripts/Grass/GrassChunkManager.cs` — 10×10m chunks, 3 LOD szint (100%/50%/20%) ✅

**Hiányzik (Unity Editor):**
- [ ] GrassBlade Material létrehozása + GrassMask RT bekötése
- [ ] `<150 draw call` Frame Debugger ellenőrzés

---

### P0-05 | Player Controller — FP mozgás, kézrendszer
**Status:** 🔧 RÉSZBEN

**Fájlok:** `Assets/_Game/Scripts/Core/PlayerController.cs`
- CharacterController mozgás ✅
- Mouse + gamepad look ✅
- Head bob placeholder ✅
- Carry penalty (25/40/50%) GameConfig-ból ✅
- Stamina soft-limit (soha nem hard-lockout) ✅
- IInteractable raycast ✅
- HayPile pickup/drop ✅

**Hiányzik:**
- [ ] PlayerPrefab összerakása: CameraRoot, HandsRoot, ToolHolder child transform
- [ ] PlayerInput component bekötése
- [ ] NGO NetworkBehaviour → P2-ben
- [ ] Gamepad dead-zone és sensitivity beállítás
- [ ] Co-op: más játékos névjegy (TMPro) → P2

---

### P0-06 | Tool System váz — BaseTool, swing FSM, HandSickle
**Status:** ✅ KÉSZ

**Fájlok:** `Assets/_Game/Scripts/Tools/`
- `BaseTool.cs` — equip/unequip, stat helpers ✅
- `MeleeToolBase.cs` — swing FSM WindUp 25% / Sweep 30% / Recovery 45%, input queue ✅
- `PoweredToolBase.cs` — fuel pool, engine on/off ✅
- `HandSickle.cs` — CutCapsule sweep frame, cells-cut tracking ✅
- `ToolHolder.cs` — 1-5 key + scroll selection ✅

**Hiányzik (Unity Editor):**
- [ ] HandSickle prefab + BladeTip Transform beállítás
- [ ] ToolHolder-be ToolData bekötése

---

### P0-07 | Világ váz — 4 parcel terrain, kerítések, stand + bálázó pozíciók
**Status:** 🔧 RÉSZBEN

**Fájlok:** `Assets/_Game/Scripts/Core/`
- `ParcelBoundary.cs` — trigger, completion check (100% cut + 0 HayPile), player enter/exit event ✅
- `WorldBootstrap.cs` — load-on-start, save-on-parcel-complete, end screen stub ✅

**Hiányzik (Unity Editor):**
- [ ] Unity Terrain helyes méretekkel (Parcel 1: ~1200m², 2: ~2400m², 3: ~4200m², 4: ~6000m²)
- [ ] 12°-os lejtő Parcel 4-en
- [ ] Parcel boundary colliderek + trigger komponens
- [ ] Kerítés placeholder mesh-ek
- [ ] `ParcelGate.cs` animáció bekötés
- [ ] Stand + Baler placeholder cube + InteractTrigger
- [ ] GrassField komponens mind a 4 parcelre

---

### P0-08 | Széna akkumuláció + HayPile spawn
**Status:** ✅ KÉSZ

**Fájlok:** `Assets/_Game/Scripts/Hay/`
- `HayAccumulationSystem.cs` — 6×6m collection grid, 60 unit threshold, HayPile spawn, 3-fázisú decal stub ✅
- `HayPile.cs` — IPickupable carry interface ✅

**Hiányzik (Unity Editor):**
- [ ] HayPile prefab (3 méret placeholder mesh)
- [ ] Loose hay decal 3 fázis (placeholder material)

---

### P0-09 | Gazdaság váz — CurrencyManager, Shop stub
**Status:** ✅ KÉSZ

**Fájlok:** `Assets/_Game/Scripts/Economy/`
- `CurrencyManager.cs` — Earn/TrySpend, OnMoneyChanged event ✅
- `ToolUnlockManager.cs` — purchase + 3-level upgrade ✅
- `ParcelManager.cs` — unlock flow, gate opening ✅
- `ShopPlaceholder.cs` — OnGUI, 3 tab (Tools/Upgrades/Unlocks) ✅
- `SaleStand.cs` — IInteractable + sell stub ✅

**Hiányzik:**
- [ ] Stand prefab bekötése SaleStand-dal + ShopPlaceholder-rel

---

### P0-10 | Alap HUD + Mentési rendszer váz
**Status:** ✅ KÉSZ

**Fájlok:**
- `Assets/_Game/Scripts/Save/SaveData.cs` — versioned schema (v1) ✅
- `Assets/_Game/Scripts/Save/RLEEncoder.cs` — RLE encoding ✅
- `Assets/_Game/Scripts/Save/SaveSystem.cs` — save/load, autosave (parcel kész / vásárlás / 60mp) ✅
- `Assets/_Game/Scripts/UI/HUDController.cs` — stamina/fuel bar fade, SmoothDamp money counter (0.4s), completion %, bale count ✅

**Hiányzik (Unity Editor):**
- [ ] HUD Canvas + UI elemek (Image, TextMeshProUGUI) bekötése HUDController-be
- [ ] SaveSystem inspector-ban GrassField[4] + HayAccumulationSystem[4] bekötése

---

## PHASE 1 — Core Systems
> **Dev estimate: M1 vége + M2 + M3, 4–5 hét, $3,000**

### P1-01 | Swing Feel System — Hit/Whiff, kamera kick, partikulák
**Status:** ⏳ PENDING | **Blokkolt:** P0-06 ✅ után

**Tervezett fájlok:**
- `SwingResultCalculator.cs` — arc cell intersection counting (Full >60%, Partial 15-60%, Whiff <15%)
- `CameraKickSystem.cs` — spring-damped, első 3 swing 3° boost
- `IToolAudioSource` interface + `PlaceholderToolAudio.cs`
- Particle system (grass burst + sparks)

---

### P1-02 | Mind az 5 szerszám implementálva
**Status:** ⏳ PENDING | **Blokkolt:** P0-06 ✅, P1-01 után

**Tervezett fájlok:**
- `LongScythe.cs` — 1.4–2.4m arc, lassabb swing
- `StringTrimmer.cs` — folyamatos vágás, bogging (-20%), RPM animáció
- `PushMower.cs` — CutCapsule deck, -20% mozgás uncut fűn, pivot fordulás
- `RideOnMower.cs` — kinematic arcade, 0→full 1.5s, body roll/pitch, FP seated cam

---

### P1-03 | Bálázó gép + négyszög/kerek bála fizika
**Status:** ⏳ PENDING | **Blokkolt:** P0-08 ✅, P0-09 ✅ után

**Tervezett fájlok:**
- `Baler.cs` — kompressziós folyamat, "thunk" + shake, ejectált bála
- `SquareBale.cs` — carry 1/2/3 stack, képtakarás szándékos
- `RoundBale.cs` — Rigidbody ~80kg, Drag 1.5, AngularDrag 2.0, 8°+ lejtőn gurul

---

### P1-04 | Shop UI, Upgrade rendszer, Parcel unlock flow
**Status:** ⏳ PENDING | **Blokkolt:** P0-09 ✅, P1-02 után

---

### P1-05 | Teljes eladási loop, parcel completion, végképernyő
**Status:** ⏳ PENDING | **Blokkolt:** P1-03, P1-04, P0-10 ✅ után

---

## PHASE 2 — Co-op
> **Dev estimate: C1–C5, 11–13 hét, $5,500**
> ⚠️ **Networking döntés szükséges: NGO (jelenlegi) vs Mirror + FizzySteamworks (dev ajánlás)**

### P2-01 | Co-op Foundation — session flow, player spawn
**Status:** ⏳ PENDING | **Blokkolt:** P1-05 után

**Dev note (docx):** Mirror Networking + Steamworks/FizzySteamworks transport ajánlott.
Host-authoritative setup: egy játékos host, a többiek Steam-en cross joinolnak.
Steam lobby + invite flow a Steam overlayön keresztül.
**Nincs dedicated server, nincs host migration.**

---

### P2-02 | Player + Tool Replication
**Status:** ⏳ PENDING | **Blokkolt:** P2-01 után

---

### P2-03 | Fű szinkronizáció — CPU grid delta sync
**Status:** ⏳ PENDING | **Blokkolt:** P2-01, P0-03 ✅ után

**Dev note (docx):** Cut event payload: position, radius/capsule, tool type, parcel ID.
Minden kliens lokálisan festi a maszkot ugyanazokból az eventekből.
Late-join: compressed CPU grid snapshot.

---

### P2-04 | Bálák + Economy + Host Save
**Status:** ⏳ PENDING | **Blokkolt:** P2-01, P1-03 után

---

### P2-05 | Co-op QA + Polish
**Status:** ⏳ PENDING | **Blokkolt:** P2-04 után

---

## PHASE 3 — Polish
> **Dev estimate: M5 (Ship Candidate), 2.5–3 hét, $2,000**

### P3-01 | Audio rendszer
**Status:** ⏳ PENDING | **Blokkolt:** P1-05 után

---

### P3-02 | Kamera feel tuning + Haptics + Accessibility
**Status:** ⏳ PENDING | **Blokkolt:** P1-05 után

---

### P3-03 | Teljesítmény optimalizálás — 60 fps integrated GPU
**Status:** ⏳ PENDING | **Blokkolt:** P0-04 ✅, P1-05 után

---

### P3-04 | Game Feel Pass — partikulák, VFX, UI animációk
**Status:** ⏳ PENDING | **Blokkolt:** P3-01, P3-02 után

---

### P3-05 | Steam integráció — Steamworks SDK, 25 achievement, Cloud save
**Status:** ⏳ PENDING | **Blokkolt:** P1-05 után

**Dev note (docx):** Steam Cloud save, Rich Presence, 25 achievement (lista a klienstől jön).
**Achievement list a klienstől szükséges.**

---

### P3-06 | Lokalizáció — 9 nyelv, string externalizáció
**Status:** ⏳ PENDING | **Blokkolt:** P1-05 után

**Dev note (docx):** Lokalizációs fájlokat a kliens biztosítja.  
Nyelvek: EN, HU, DE, RU, ZH-Hans, PL, ES, PT-BR, JP

---

## Dev Estimate összefoglaló (Fields_Unity_Development_Estimate.docx)

| Milestone | Fókusz | Idő | Fix ár |
|-----------|--------|-----|--------|
| **M1** | Vertical Slice / Core Foundation | 6–8 hét | $4,000 |
| **M2** | Tools Complete | 2.5–3 hét | $1,750 |
| **M3** | Baling Complete | 1.5–2 hét | $1,250 |
| **M4** | Content Complete | 2–2.5 hét | $1,500 |
| **M5** | Ship Candidate | 2.5–3 hét | $2,000 |
| **Stage 1** | Single Player | **15–18.5 hét** | **$10,500** |
| **C1** | Mirror + Steam session | 2–2.5 hét | $1,000 |
| **C2** | Player + Tool Replication | 2–2.5 hét | $1,000 |
| **C3** | Grass + Hay Sync | 2.5–3 hét | $1,300 |
| **C4** | Bálák + Economy + Host Save | 2.5–3 hét | $1,300 |
| **C5** | Co-op QA + Polish | 2 hét | $900 |
| **Stage 2** | Co-op Multiplayer | **11–13 hét** | **$5,500** |
| **Teljes projekt** | | **6.5–8 hónap** | **$16,000** |

**Nem tartalmazza:** 3D art, animáció, audio, lokalizáció tartalom, dedicated server, host migration, anti-cheat.

---

## Anti-pattern lista — TILOS (Spec §8.11)

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

## Függőségi gráf

```
P0-01 → P0-02 → P0-03 → P0-04
                       → P0-06 → P1-01 → P1-02 → P1-04
                       → P0-08 → P1-03 → P1-05 → P2-01 → P2-02
         P0-01 → P0-05 ↗                        ↘ P2-03
         P0-01 → P0-09 → P1-04                  P2-04 → P2-05
                       → P1-03
                P0-08 + P0-09 → P0-10 → P1-05
```
