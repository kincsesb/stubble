# Stubble — Endgame System Implementation Plan

**Dátum:** 2026-08-12  
**Branch:** main  
**Engine:** Unity 6 URP + Mirror 96.6.4 + Feel (MoreMountains)

---

## Áttekintés

Három lehetséges befejezés, egyetlen settings toggle alapján:

| Setting | Idő | Ending |
|---|---|---|
| `theatricalEnding = true` | < 3 óra (< 10 800 s) | **Speed Run Loop** — a fű visszanő, előlről |
| `theatricalEnding = true` | ≥ 3 óra (≥ 10 800 s) | **Nuclear** — repülő + atombomba + nuke VFX |
| `theatricalEnding = false` | — | **Peaceful** — azonnal EndScreen, semmi extra |

A toggle `settings.json`-ba ment (PlayerPrefs-független), default `true`.

---

## Jelenlegi state összefoglaló (mit nem kell írni)

- `GameEvents.OnFullGameCompleted(float totalPlaytimeSeconds)` — már létezik és tüzel ✅
- `WorldBootstrap.OnParcelCompleted` — már kezeli a kompletálást ✅
- `GrassField.ResetGrass()` — publikus, elérhető ✅
- `SessionState.TotalPlaytime` — real-time követett gameplay másodpercek ✅
- `GameFeelController` — shake, flash, bloom, MMF_Player dispatcher ✅
- `EndScreen.cs` — stats megjelenítés ✅
- `SettingsManager` + `SettingsData` — JSON alapú settings ✅
- SFX: `Assets/SFX/airplane.mp3` + `Assets/SFX/nuclear-bomb.mp3` ✅
- Achievement: `THE_HARD_WAY` — az adatbázisban van, nuclear endinghez ✅

---

## Step 1 — SettingsData: `theatricalEnding` toggle hozzáadása

**Fájl:** `Assets/_Game/Scripts/Settings/SettingsData.cs`

A `// ── Language` szekció elé, külön `// ── Gameplay` szekcióba:

```csharp
// ── Gameplay ──────────────────────────────────────────────────────── //
public bool theatricalEnding = true;   // false = peaceful, no cutscene
```

---

## Step 2 — SettingsManager: setter hozzáadása

**Fájl:** `Assets/_Game/Scripts/Settings/SettingsManager.cs`

`ApplyAccessibility()` után új metódus:

```csharp
public void SetTheatricalEnding(bool v)
{
    Data.theatricalEnding = v;
    MarkDirty();
    NotifyChanged();
}
```

---

## Step 3 — WorldBootstrap módosítás

**Fájl:** `Assets/_Game/Scripts/Core/WorldBootstrap.cs`

**Probléma:** jelenleg `OnParcelCompleted` maga aktiválja az `endScreenRoot`-ot.  
Az `EndingOrchestrator` fogja ezt kezelni — a WorldBootstrap csak az eventet tüzelje, ne nyissa az end screent.

`OnParcelCompleted`-ben ez a két sor cserélendő:

```csharp
// ELŐTTE:
if (endScreenRoot != null) endScreenRoot.SetActive(true);

// UTÁNA — törlés, EndingOrchestrator veszi át
// (Az endScreenRoot-ot az EndingOrchestrator kapja meg ref-ként)
```

A `GameEvents.FireFullGameCompleted(totalTime)` és `SteamManager.OnAllFieldsComplete()` marad.

---

## Step 4 — `EndingOrchestrator.cs` — új fájl

**Fájl:** `Assets/_Game/Scripts/Core/EndingOrchestrator.cs`  
**Namespace:** `Fields.Core`

Ez a központi orchestrátor. Feliratkozik az `OnFullGameCompleted` eventre,
kiválasztja az endingot és futtatja a coroutine-t.

### Inspector referenciák

```
[Header("Scene refs")]
GrassField[]    grassFields           // mind a 4 GrassField Inspector-ben bekötve
Transform       airplaneTransform     // airplane_3d_model Transform
Transform       bombTarget            // "Bomb" empty GO Transform
GameObject      nuclearBombPrefab     // ledobandó bomba prefab
ParticleSystem  vfxNuke               // vfx_StylizedExplosion_Nuke_L
Light           nukeLight             // Point Light a robbanásnál (runtime beállítva)
Volume          nuclearVolume         // külön URP Volume, weight=0 alap, nuke-nál animálva
GameObject      endScreenRoot         // a meglévő endScreenRoot

[Header("Audio")]
AudioSource     airplaneAudioSource
AudioSource     explosionAudioSource
AudioClip       airplaneClip          // Assets/SFX/airplane.mp3
AudioClip       nuclearBombClip       // Assets/SFX/nuclear-bomb.mp3

[Header("Feel — Loop Ending")]
MMF_Player      feedbackCinematicIn   // letterbox slide in (időlassítás + bar anim)
MMF_Player      feedbackGrassReturn   // a bump: Bloom + Flash + Shake + ChromAb + FreezeFrame

[Header("Feel — Nuclear Ending")]
MMF_Player      feedbackNuclearBuild  // épülő shake + alacsony rumble ahogy közeledik a gép
MMF_Player      feedbackNuclearBlast  // robbanás pillanata: Flash + mega Shake + Bloom + FreezeFrame
MMF_Player      feedbackNuclearSettle // lecsengés: vignette eltűnik, FOV visszaáll

[Header("Timing")]
float           playerTurnDuration  = 2.0f   // játékos elfordulás másodpercek
float           airplaneTravelTime  = 9.0f   // repülő megy a Bomb-ig
float           bombDropDuration    = 1.8f   // bomba esés idő
float           postBlastHold       = 4.0f   // robbanás utáni várakozás
float           fadeOutDuration     = 1.5f   // fade to black
float           loopRestartDelay    = 3.5f   // loop ending utáni scene reload delay

[Header("Nuclear Post-Process targets")]
float           nukeBloomPeak       = 12f
float           nukeChromAbPeak     = 1f
float           nukeVignettePeak    = 0.75f
float           nukeLightIntensity  = 200f
float           nukePostDecayTime   = 3.0f
```

### Teljes coroutine-flow vázlat

#### 4a. Peaceful Ending (theatrical = false)
```
endScreenRoot.SetActive(true)    // azonnal
```

#### 4b. Speed Run Loop Sequence (theatrical = true, playtime < 10800s)
```
t=0.0s  DisableInput()
t=0.0s  feedbackCinematicIn.PlayFeedbacks()      // fekete letterbox csúszik be
t=0.0s  → RotatePlayersToward(fieldCenter, 2s)   // coroutine: Lerp rotation
t=2.0s  0.5s csend (feszültség)
t=2.5s  feedbackGrassReturn.PlayFeedbacks()       // BUMP
         → foreach grassField: grassField.ResetGrass()
         → SessionState.Instance megmarad (TotalPlaytime folytatódik a reset-elt WorldBootstrap-en)
t=4.0s  ShowLoopTitleCard("Fields Refreshed!")    // rövid overlay UI szöveg
t=6.5s  FadeToBlack(1.5s)
t=8.0s  WorldBootstrap.PendingFreshStart = true
         SceneManager.LoadScene(activeScene.buildIndex)
```

**Megjegyzés:** A loop ending nem mutat EndScreen-t. A PendingFreshStart = true a WorldBootstrap-ban újrakezdi a játékot és a SessionState újrainicializálódik.

#### 4c. Nuclear Sequence (theatrical = true, playtime ≥ 10800s)
```
t=0.0s   DisableInput()
t=0.0s   feedbackCinematicIn.PlayFeedbacks()       // letterbox csúszik be
t=0.0s   → RotatePlayersToward(airplaneTransform.position, 2s)
t=0.5s   airplaneAudioSource.Play(airplaneClip)    // repülő zaj, volume 0→1 over 3s
t=2.0s   → MoveAirplane(bombTarget.position, airplaneTravelTime)  // 9s alatt repül
         + feedbackNuclearBuild.PlayFeedbacks()     // épülő shake: amplitude nő az idővel
t=8.0s   (repülő 80%-nál) → DropBomb()            // nuclearBombPrefab Instantiate, gravity esés
t=9.5s   (bomba közel a talajhoz)
         airplaneAudioSource.Stop()
         explosionAudioSource.Play(nuclearBombClip)  // robbanás hang (LOUD)
         vfxNuke.Play()                              // vfx_StylizedExplosion_Nuke_L
         feedbackNuclearBlast.PlayFeedbacks()        // mega Blast feel
         → AnimateNuclearVolume(peak, decayTime)     // post-process: Bloom/ChromAb/Vignette
         → AnimateNukeLight(nukeLightIntensity)      // Point Light pulse
t=13.5s  feedbackNuclearSettle.PlayFeedbacks()       // lecsengés
t=13.5s  SteamManager.UnlockAchievement(THE_HARD_WAY)
t=14.0s  FadeToBlack(1.5f)
t=15.5s  endScreenRoot.SetActive(true)               // EndScreen megjelenik
```

---

## Step 5 — Feel konfigurációk (Inspector-ban)

### `feedbackCinematicIn` (MMF_Player)
Mindkét theatrical endingben használva. Hatás: mozivászon érzés.

| Feedback típus | Konfig |
|---|---|
| `MMF_ImageAlpha` (letterbox top) | 0→1, 0.4s ease-in |
| `MMF_ImageAlpha` (letterbox bottom) | 0→1, 0.4s ease-in |
| `MMF_TimeScale` | 1.0→0.85, duration 0.3s, majd visszaáll 1.0-ra 0.6s múlva |
| `MMF_Vignette` | 0→0.3, duration 0.5s |

> **Megjegyzés:** A két fekete letterbox bar egy Canvas-on él, `EndingOrchestrator` kapja a referenciát. Alternativa: `MMF_CanvasGroupAlpha` ha van ilyen a Feel-ben.

---

### `feedbackGrassReturn` (MMF_Player) — Loop Ending
A fő "bump" pillanat amikor visszanő a fű.

| Feedback típus | Konfig |
|---|---|
| `MMF_PositionShake` | Amplitude 0.25, Frequency 40, Duration 0.5s |
| `MMF_Bloom` | Peak intensity +4.5, Decay 1.2s |
| `MMF_ChromaticAberration` | Peak 0.9, Decay 0.8s |
| `MMF_CameraFOV` | FOV -8 punch, Recovery 0.6s |
| `MMF_Flash` (white) | Alpha 0.85, Duration 0.25s |
| `MMF_FreezeFrame` | 0.07s |
| `MMF_Sound` | whoosh / blade swing (meglévő SFX variáció) |

---

### `feedbackNuclearBuild` (MMF_Player) — Nuclear Ending
Épülő feszültség a repülő közeledésével. Loopolt vagy több részletben hívott.

| Feedback típus | Konfig |
|---|---|
| `MMF_PositionShake` | Amplitude 0.04→0.12 (kódból skálázva), Frequency 18, Duration 8s |
| `MMF_ChromaticAberration` | 0→0.25, Duration 8s |
| `MMF_Vignette` | 0.3→0.55, Duration 8s |

> Az amplitúdót az `EndingOrchestrator` manuálisan skálázza a repülő közelségével arányosan, nem csak egyszeri PlayFeedbacks.

---

### `feedbackNuclearBlast` (MMF_Player) — Nuclear Ending
A robbanás pillanata — a játék legerősebb Feel pillanata.

| Feedback típus | Konfig |
|---|---|
| `MMF_Flash` (white) | Alpha 1.0 (teljes white-out), Duration 0.15s, majd decay 0.8s |
| `MMF_PositionShake` | Amplitude 0.6, Frequency 25, Duration 3.0s |
| `MMF_RotationShake` | Amplitude 8°, Frequency 20, Duration 2.0s |
| `MMF_Bloom` | Peak +10, Decay 2.5s |
| `MMF_ChromaticAberration` | 1.0 (max), Decay 2.0s |
| `MMF_CameraFOV` | +15 punch (wide shock), Recovery 1.2s |
| `MMF_FreezeFrame` | 0.12s |
| `MMF_Rumble` | Heavy, Duration 2.0s |

---

### `feedbackNuclearSettle` (MMF_Player) — Nuclear Ending
Lecsengés, a por ülepszik.

| Feedback típus | Konfig |
|---|---|
| `MMF_PositionShake` | Amplitude 0.05, Frequency 8, Duration 1.5s |
| `MMF_Bloom` | visszaáll base-re, Duration 1.5s |
| `MMF_Vignette` | 0.75→0.3, Duration 2.0s |

---

## Step 6 — Nuclear Post-Process Volume

**Mit csináljunk:** Hozzáadunk egy dedikált `Volume` komponenst a scene-ben (pl. `NuclearPostProcessVolume` GameObject-en), amelynek:
- `Priority` = 20 (magasabb mint a base volume)
- `Weight` = 0 alap állapotban
- `Profile` tartalmaz: `Bloom`, `ChromaticAberration`, `Vignette`, `ColorAdjustments`, `LensDistortion`

Az `EndingOrchestrator` a robbanás pillanatában animálja a `Weight`-et 0→1-re (0.05s), majd 1→0-ra a `nukePostDecayTime` alatt.

A `ColorAdjustments` settingek:
- Saturation: +60 (oversaturated yellow-orange)
- Hue Shift: -15° (sárga felé)

A `LensDistortion`:
- Intensity: -0.3 (enyhe fish-eye a blast-nál), majd visszaáll

**A nukleáris Point Light:**  
Az `EndingOrchestrator` Instantiate egy Point Light-ot a `bombTarget.position`-ban a robbanáskor:
- Intensity: 200 → 0 decay 2s alatt (coroutine)
- Color: #FF9900 (narancssárga)
- Range: 150m

---

## Step 7 — Airplane mozgás

Az `airplane_3d_model` a scene-ben statikusan van elhelyezve (off-screen start pozíción).  
Az `EndingOrchestrator` a `MoveAirplane` coroutine-ban `Lerp`-pel mozgatja a `bombTarget.position` felé:

```csharp
IEnumerator MoveAirplane(Vector3 target, float duration)
{
    Vector3 start = airplaneTransform.position;
    // Airplane height: stay at Y, only move XZ toward target
    Vector3 targetFlat = new Vector3(target.x, start.y, target.z);
    float elapsed = 0f;
    while (elapsed < duration)
    {
        elapsed += Time.deltaTime;
        airplaneTransform.position = Vector3.Lerp(start, targetFlat, elapsed / duration);
        // Optional: rotate airplane to face movement direction
        yield return null;
    }
}
```

A repülő `80%`-os útján (elapsed > duration * 0.8f) az `EndingOrchestrator` Instantiate-eli a `nuclearBombPrefab`-ot a repülő pozíciójában, majd a bomba `bombTarget.position` felé esik.

---

## Step 8 — Player elfordulás (co-op aware)

```csharp
IEnumerator RotatePlayersToward(Vector3 worldTarget, float duration)
{
    var pc = PlayerController.Instance;
    if (pc == null) yield break;

    Vector3 dir = (worldTarget - pc.transform.position);
    dir.y = 0f;
    Quaternion targetRot = dir != Vector3.zero
        ? Quaternion.LookRotation(dir.normalized)
        : pc.transform.rotation;

    Quaternion startRot = pc.transform.rotation;
    float elapsed = 0f;
    while (elapsed < duration)
    {
        elapsed += Time.unscaledDeltaTime;
        pc.transform.rotation = Quaternion.Slerp(startRot, targetRot, elapsed / duration);
        yield return null;
    }
}
```

**Co-op megjegyzés:** Mirror ClientRpc-vel kell minden kliensnek elküldeni a `rotateToward` parancsot. Az `EndingOrchestrator`-nak lesz egy `[ClientRpc]` metódusa: `RpcTriggerEnding(float totalPlaytime)`. Ez a terv scope-ján kívül van (Mirror hook), de a szerkezet fel van készítve rá.

---

## Step 9 — Loop Ending: Field Center kiszámítása

```csharp
Vector3 GetFieldCenter()
{
    if (grassFields == null || grassFields.Length == 0) return Vector3.zero;
    Vector3 sum = Vector3.zero;
    int count = 0;
    foreach (var gf in grassFields)
    {
        if (gf == null) continue;
        sum += gf.transform.position;
        count++;
    }
    return count > 0 ? sum / count : Vector3.zero;
}
```

---

## Step 10 — Loop Ending: Title Card UI

A loop ending nem mutat EndScreen-t, csak egy rövid "Fields Refreshed!" overlay-t.  
Ez egy egyszerű `CanvasGroup` a scene-ben (pl. `LoopTitleCard` GameObject):
- TMP szöveg: lokalizált kulcs `end.loop.title`
- Alpha: 0 → 1 (0.4s) → 1 tartás 1.5s → 0 (0.6s) → scene reload

Nincs Play Again / Quit gomb — automatikusan indul újra.

---

## Step 11 — EndScreen módosítások

**Fájl:** `Assets/_Game/Scripts/UI/EndScreen.cs`

Az `EndingOrchestrator` aktiválja az `endScreenRoot`-ot. Az `EndScreen.OnEnable()` lefut, és a `BuildComment()` logikában új ágak kellenek:

**Jelenlegi `BuildComment` küszöbök:**
- speedrun: `elapsed < 20 * 60`
- veteran: `elapsed > 65 * 60`

**Új ágak a nuclear endinghez** (theater mode, ≥ 3 óra):
- `elapsed >= 10800f` → `loc.Get("end.comment.nuclear", hours)` (priority legfelül)

A peaceful endinghez nincs változás — az aktuális logika fut le.

**EndScreen nem tudja melyik ending volt** — az `EndingOrchestrator` set egy statikus/singleton flaget a megjelenítés előtt:
```csharp
// EndingOrchestrator:
EndScreen.PendingEndingType = EndingType.Nuclear;
endScreenRoot.SetActive(true);
```
```csharp
// EndScreen:
public static EndingType PendingEndingType = EndingType.Peaceful;

public enum EndingType { Peaceful, Loop, Nuclear }
```

---

## Step 12 — Lokalizáció (en.json + hu.json)

### Új kulcsok:

```json
"end.comment.nuclear": "The fields had {0} hours. They chose extinction.",
"end.loop.title": "Fields Refreshed!",
"end.loop.comment": "Under 3 hours. The grass bows to you. Go again.",
"settings.theatrical": "Theatrical Endings",
"settings.theatrical.desc": "Speed-run loop or nuclear finale. Off for a quiet finish."
```

```json
// hu.json
"end.comment.nuclear": "A mezők {0} óráig vártak. A végső ítélet megjött.",
"end.loop.title": "Mezők megfrissültek!",
"end.loop.comment": "3 óra alatt. A fű meghajol előtted. Csináld újra.",
"settings.theatrical": "Theatrális Befejezések",
"settings.theatrical.desc": "Speed-run loop vagy nukleáris finálé. Kikapcsolva csendes befejezés."
```

---

## Step 13 — Inspector Assignments (scene-ben bekötni)

`EndingOrchestrator` component a `WorldBootstrap` GameObject-re kerül (vagy saját GO-ra):

| Field | Érték |
|---|---|
| `grassFields[0..3]` | Scene-ben lévő 4 GrassField component |
| `airplaneTransform` | `airplane_3d_model` GameObject Transform |
| `bombTarget` | `Bomb` empty GameObject Transform |
| `nuclearBombPrefab` | Nuclear bomb prefab (Assets/_Game/Prefabs/-ban) |
| `vfxNuke` | `vfx_StylizedExplosion_Nuke_L` ParticleSystem |
| `nuclearVolume` | `NuclearPostProcessVolume` GameObject Volume |
| `endScreenRoot` | Meglévő endScreenRoot ref (WorldBootstrap-ból kivesszük, EndingOrchestrator kapja) |
| `airplaneClip` | `Assets/SFX/airplane.mp3` |
| `nuclearBombClip` | `Assets/SFX/nuclear-bomb.mp3` |
| `feedbackCinematicIn` | MMF_Player component (Inspector konfigurálva) |
| `feedbackGrassReturn` | MMF_Player component |
| `feedbackNuclearBuild` | MMF_Player component |
| `feedbackNuclearBlast` | MMF_Player component |
| `feedbackNuclearSettle` | MMF_Player component |

---

## Megvalósítás sorrendje

1. `SettingsData.cs` — `theatricalEnding` field hozzáadás (5 perc)
2. `SettingsManager.cs` — `SetTheatricalEnding()` metódus (5 perc)
3. `WorldBootstrap.cs` — `endScreenRoot.SetActive(true)` eltávolítása (2 perc)
4. `EndScreen.cs` — `PendingEndingType` enum + nuclear comment ág (15 perc)
5. `EndingOrchestrator.cs` — teljes implementáció (60–90 perc)
6. `en.json` + `hu.json` — új lokalizáció kulcsok (10 perc)
7. **Unity Editor:** 
   - `NuclearPostProcessVolume` GameObject létrehozása (Volume + Profile)
   - `LoopTitleCard` Canvas UI létrehozása
   - `EndingOrchestrator` Inspector bekötése
   - 5 MMF_Player feedback konfigurálása
   - Letterbox bar Image-ek létrehozása

---

## Kockázatok / megjegyzések

- **Repülő modell mozgása:** Az `airplane_3d_model`-nek el kell kezdenie mozogni — ha animált van rajta (Animator), a repülés animációt be kell kapcsolni. Ha nincs, csak Transform Lerp megy.
- **vfx_StylizedExplosion_Nuke_L:** Ha `stopAction = Disable`, a VFX végén a GO kikapcsol — ez OK. Ha `Loop = true`, manuálisan kell Stop()-olni.
- **Bomb prefab:** Ha nincs kész bomb prefab, egy egyszerű sphere collider + rigidbody (gravity=true) is megteszi kezdetnek; a VFX takarja.
- **Co-op:** A player elfordulás jelenleg csak a local player-en működik. Mirror ClientRpc teljes co-op supporthoz szükséges — ez a következő fázis.
- **Time.timeScale:** A theatrical ending alatt `Time.timeScale = 1f` marad (nem a szokásos EndScreen pause). Az EndScreen aktiválásakor `Time.timeScale = 0f`-re áll az `OnEnable`-ben — ez változatlan marad.
- **Input letiltás:** `PlayerController.Instance.enabled = false` vagy egy dedikált `IsInputBlocked` flag.