# FIELDS — Implementation Task: Menu System, Journal & Save/Load Rework

**Target:** Unity 6 (URP), C#, new Input System
**Scope:** Main menu, settings, pause, journal, statistics tracking, save/load schema v2
**Co-op:** Stage 2 (2–4 players, host-authoritative) is confirmed and WILL be built. Everything below must be written co-op-aware even though this task ships single-player only.

---

## 0. ROLE AND GROUND RULES

You are implementing the complete front-end and persistence layer for a first-person cozy mowing/haymaking game. The gameplay systems (grass cutting, hay piles, baling, transport, shop, economy) already exist or are being built separately. Your job is everything around them: menus, settings, pause, the journal, the statistics that feed the journal, and the save system that persists all of it.

**Binding rules:**

1. **No hardcoded UI strings.** Every user-visible string goes through a localization key. Ship languages: EN, HU, DE, RU, ZH-Hans, PL, ES, PT-BR, JA. UI layout must survive 2× string length expansion without clipping or overlapping.
2. **All mutable game state routes through a single authority object** (`SessionState`). Never let UI read gameplay MonoBehaviours directly, and never let UI write gameplay state directly. Single-player is a session with one participant. This is what makes co-op possible later without a rewrite.
3. **Settings are NOT part of the save game.** They live in a separate file and survive deleting the save.
4. **Never snap numeric values in the UI.** Money, percentages, and counters animate to their new value over ~0.4 s.
5. **Full gamepad support is mandatory** (Steam Deck is a target platform). Every screen must be fully navigable with a controller, with a sensible default focus on open and no dead ends.
6. Target: 60 fps at 1080p on Intel Iris Xe class integrated GPUs and 60 fps at 1280×800 on Steam Deck. UI must not allocate per-frame; no `GetComponent` or LINQ in `Update`.

---

## 1. INPUT BINDING CHANGE — DO THIS FIRST

`Tab` is currently bound to the contextual help tooltip. **It stays there.**

Move the journal to a new binding:

| Action | Keyboard | Gamepad |
|---|---|---|
| Contextual help tooltip | `Tab` (hold) | `View`/`Back` (hold) |
| **Journal** | **`J`** (toggle) | **`View`/`Back` (tap)** |
| Pause | `Esc` | `Menu`/`Start` |

Hold-vs-tap on the gamepad `View` button: tap under 0.4 s opens the journal, hold over 0.4 s shows the help tooltip. Implement with an Input System `Hold` interaction on the help action and a `Tap` interaction on the journal action, on the same physical binding.

All three actions must appear in the rebinding UI. `Esc` may be rebound but must always have a working fallback to close the topmost open screen.

---

## 2. SCREEN ARCHITECTURE

Implement a **screen stack** (`UIManager`), not a set of independent canvases toggling each other.

```
UIManager
  Push(screen)   → new screen on top, previous stays loaded but non-interactive
  Pop()          → close topmost, restore focus to the one below
  PopAll()       → return to gameplay
```

Requirements:

- Only the topmost screen receives input.
- `Esc` / `B` / `Circle` always pops one level. Never traps the player.
- Gamepad focus is saved per screen and restored on pop.
- One `EventSystem`, one `Canvas` per screen, screens are prefabs instantiated once and pooled — do not instantiate/destroy per open.
- Opening any screen from gameplay releases and unlocks the cursor; popping the last one re-locks it.

---

## 3. MAIN MENU

Six entries, in this order. `Continue` is the default focused item when a save exists; otherwise `New Game`.

| Entry | Behaviour |
|---|---|
| **Continue** | Hidden entirely (not greyed) if no save exists. Loads the single save slot. |
| **New Game** | If a save exists, show a confirmation modal warning it will be overwritten. Requires explicit confirm; default focus on Cancel. |
| **Play with Friends** | Opens the co-op submenu. **In this task, implement the screen and navigation; the networking layer is stubbed.** The button is visible and functional, and shows a "Coming soon" state until Stage 2 lands. Build the UI now so Stage 2 is UI-complete on arrival. |
| **Settings** | Opens the settings screen (Section 5). Same screen instance is reused from the pause menu. |
| **Credits** | Scrollable, skippable with any input. |
| **Quit** | Confirmation modal. |

**Co-op submenu** (UI now, netcode later):

- **Host Lobby** — 2–4 player slots showing avatar, name, ready state. Steam invite button (opens the Steam overlay invite dialog). Lobby visibility toggle: Friends Only / Invite Only. Start button enabled when host is ready.
- **Join** — list of Steam friends currently in a joinable lobby. Empty state message when none. **Do not build a public lobby browser.**
- Both screens must display two persistent notices, styled as informational not as warnings:
  - *"Guests start with base tools and their progress is not saved."*
  - *"The host owns the save file."*

Background: render the game's meadow scene behind the menu with a slow idle camera drift, not a static image. This is nearly free — the scene already exists — and it sells the game's tone on the first screen the player sees.

---

## 4. PAUSE MENU

**Single-player:** true pause. `Time.timeScale = 0`, full-screen panel, audio ducked to ~20% on the World and Tools mixer groups (Ambience stays at ~60% — silence is jarring in this genre).

**Co-op:** the world does not stop. `Time.timeScale` is never touched. The pause menu becomes a translucent overlay with the game visible and running behind it, plus a persistent line at the top: *"The game continues while this menu is open."* Audio is not ducked.

Write this as a single component that reads `SessionState.IsMultiplayer` — do not fork into two prefabs.

Entries:

| Entry | Single-player | Co-op |
|---|---|---|
| Resume | ✓ | ✓ |
| Journal | ✓ | ✓ |
| Settings | ✓ | ✓ |
| Invite Friend | — | ✓ (Steam overlay) |
| Players | — | ✓ (list, host may kick) |
| Quit to Main Menu | ✓ (autosaves first) | ✓ (host: ends session with confirm; guest: leaves) |

Host quitting in co-op requires a confirmation modal that explicitly states all guests will be disconnected.

---

## 5. SETTINGS

Five tabs. Accessibility is its own tab — do not bury these options inside Display. This audience searches for them.

Every setting applies live on change (no Apply button), except resolution and window mode, which use a 15-second revert-unless-confirmed dialog.

### 5.1 Display
- Resolution (dropdown, populated from `Screen.resolutions`)
- Window mode: Fullscreen / Borderless / Windowed
- Quality preset: **Low / Medium / High** — drives grass density, geometry distance band, shadow quality, and post-processing. Low targets Steam Deck and integrated GPUs; High targets dedicated GPUs.
- FOV slider, 60–100, default 70
- VSync toggle
- Frame rate cap: 30 / 60 / 120 / 144 / Unlimited
- Brightness / gamma

### 5.2 Audio
Five sliders mapping exactly to the `AudioMixer` groups. **There is no Music slider — the game ships with ambient soundscapes only and has no composed score.**
- Master
- Tools
- World
- Ambience
- UI

Each slider plays a short representative sample on release so the player hears what they are adjusting. Store as linear 0–1 in the settings file, convert to dB with `Mathf.Log10(v) * 20` when applying, clamping v to a floor of 0.0001 to avoid `-Infinity` at zero.

### 5.3 Controls
- Mouse sensitivity (separate X and Y)
- Invert Y
- Gamepad look sensitivity, separate from mouse
- Gamepad stick deadzone
- Tool use: Hold / Toggle
- Sprint: Hold / Toggle
- Full rebinding for every action, keyboard and gamepad, using `InputActionRebindingExtensions.PerformInteractiveRebinding`
- Conflict detection: warn on duplicate binding, offer to swap or cancel
- "Reset to defaults" per section

Serialize rebinds with `InputActionAsset.SaveBindingOverridesAsJson()` into the settings file. Do not hand-roll this.

### 5.4 Accessibility
- Head bob: on/off (spec 8.9 requires this toggle)
- Camera shake: on/off **plus a 0–100% intensity slider** — the slider is required, not just the toggle
- Swing camera kick intensity: 0–100%
- Controller rumble: on/off plus intensity slider
- UI scale: 80% / 100% / 120% / 150%
- HUD element toggles: stamina/fuel bar, carry indicator, money counter, completion %, teammate nameplates — each independently hideable
- Contextual hints: on/off
- High-contrast HUD mode

Camera shake and head bob at 0% must result in mathematically zero camera movement, not reduced movement. Test this — a "makes me nauseous" review is disproportionately damaging to a cozy game.

### 5.5 Language
Nine languages, applied immediately without restart. Changing language must rebuild all open UI text, including screens further down the stack.

---

## 6. JOURNAL

Opened with `J` (keyboard) or a tap of `View` (gamepad). Non-blocking in co-op, same rule as the pause menu.

Three tabs.

### 6.1 Tab: Parcels

One card per parcel (Home Paddock, Middle Meadow, Far Meadow, Top Meadow). Locked parcels show a locked card with the unlock price, not an empty slot.

Per parcel:

| Field | Notes |
|---|---|
| Completion % | Grass cut, from the CPU logical grid — **not** derived from the GPU mask |
| Area cut | m², absolute |
| Area remaining | m², absolute |
| Hay piles spawned | Lifetime total |
| Hay piles collected | Lifetime total |
| **Hay piles still in the field** | Highlighted — this is the parcel's only unfinished business and blocks completion |
| Square bales made | |
| Round bales made | |
| Money earned here | Attributed at the moment of sale to the parcel the hay was cut in |
| Time spent | Seconds of gameplay with the player inside the parcel bounds |
| Status | In Progress / Complete |

A parcel reads Complete only when grass is 100% cut **and** zero hay piles remain in it. Show both conditions as separate ticks so the player can see which one is outstanding.

### 6.2 Tab: Statistics

Global, and in co-op split into a column per player.

**Per player:** area cut (m²), hay piles collected, square bales, round bales, money earned, money spent, tools owned with upgrade levels, distance travelled, total swings, playtime.

**Session-wide:** total playtime, shared wallet balance, total earned, total spent, parcels completed, overall completion %.

In single-player render exactly one column and no player header — do not show a degenerate co-op layout.

Money earned per player is attribution, not a separate wallet. The wallet is shared. Make this visually unambiguous: label the shared balance clearly at the top and mark per-player figures as contributions.

### 6.3 Tab: Records

Local leaderboard and personal bests. Cheap to build, meaningfully improves retention in this genre.

- Fastest parcel completion, per parcel
- Largest area cut in a single session
- Most bales in one delivery run
- Longest continuous cutting streak without stopping
- Full-game completion time

Wire these to Steam Leaderboards where a global board makes sense (full-game completion time, fastest parcel). Local-only is acceptable for the rest.

---

## 7. STATISTICS TRACKING

The journal is only as good as the data feeding it, and most of these counters **do not exist yet**. Implement a `StatisticsTracker` that subscribes to gameplay events. It must not poll.

Events to subscribe to:

```
OnGridCellCut(parcelId, playerId, cellArea)
OnHayPileSpawned(parcelId, worldPos)
OnHayPileCollected(parcelId, playerId)
OnBaleCreated(parcelId, playerId, BaleType)
OnBaleSold(parcelId, playerId, value)
OnToolPurchased(playerId, ToolId)
OnToolUpgraded(playerId, ToolId, StatAxis, level)
OnParcelUnlocked(parcelId, cost)
OnSwingPerformed(playerId, SwingResult)
OnParcelEntered(parcelId, playerId) / OnParcelExited(...)
```

Rules:

- `parcelId` on a sale is the parcel the **hay was cut in**, not where the stand is. All bales are sold at one farmstead, so attributing by stand location would make every parcel except Parcel 1 show zero income. Tag hay piles with their origin parcel at spawn and carry that tag through baling to sale.
- `playerId` is always present. In single-player it is always `0`. Do not add it later.
- Time-in-parcel accumulates only while the game is unpaused and the player is inside the parcel collider.
- All counters are `long` or `double`. A player can cut millions of cells.
- The tracker is owned by `SessionState`, not by a scene object, and survives scene reloads.

---

## 8. SAVE / LOAD REWORK

The existing save format does not contain statistics, per-player data, or settings separation. Migrate it, do not replace it destructively.

### 8.1 File layout

Three separate files in `Application.persistentDataPath`:

| File | Contents | Steam Cloud |
|---|---|---|
| `save_slot0.dat` | Game state (Section 8.3) | Yes |
| `settings.json` | All settings, including input rebinds | Yes |
| `records.json` | Local leaderboard and personal bests | Yes |

Settings must survive deleting the save. Records must survive starting a new game.

### 8.2 Versioned schema with migration

```csharp
[Serializable]
public class SaveData {
    public int schemaVersion;   // current: 2
    // ...
}
```

Implement a migration chain: `v1 → v2`, extensible to `v2 → v3`. On load:

1. Read `schemaVersion`.
2. If lower than current, run each migration step in sequence.
3. If higher than current, refuse to load and show a clear message (the player downgraded the game).
4. Write back at the current version on the next save.

**v1 → v2 migration must not lose data and must not fail.** Missing statistics fields are backfilled: derive area cut from the existing cut grid, set unknown counters to zero, attribute all existing money to `playerId 0`. A v1 save must load and produce a journal that is plausible rather than empty.

Write a unit test that loads a synthetic v1 save and asserts a valid v2 result.

### 8.3 What must persist

**Session:** schema version, save timestamp, total playtime, shared wallet, unlocked parcels, `isMultiplayerSave` flag.

**Per parcel:** cut grid, hay piles spawned/collected, bales by type, money earned, time spent, completion flag.

**Per player:** playerId, owned tools, per-tool per-axis upgrade levels, baler upgrade levels, hay value multiplier level, and the full statistics block.

**World objects:** every bale currently in the world — position, rotation, type, density multiplier at creation; every hay pile in the world — position, size variant, origin parcel; partial collection-cell hay units below the spawn threshold.

The last item matters: spec 6.1.1 states leftover units carry over and nothing is ever lost. If partial cell values are not saved, hay silently vanishes across a save/load and the player will notice.

### 8.4 Cut grid encoding

The cut grid is the largest item in the save. Persist the **CPU logical grid**, never the GPU mask, and never as an image.

- Bit-pack one bit per cell where the tool requires a single pass, or run-length encode where multi-pass sharpness state must be preserved.
- Parcel 4 is ~6,000 m² at 0.4 m cells ≈ 37,500 cells ≈ 4.7 KB bit-packed. All four parcels stay comfortably under 20 KB. This is small — prioritize correctness over compression.
- On load, the GPU mask is regenerated from the CPU grid. It is never itself saved.
- Round-trip test: save, load, and assert the restored completion percentage matches the pre-save value within 0.1%.

### 8.5 Autosave

Triggers: parcel completion, any purchase, any bale sold, and every 60 seconds.

- Write to a temp file, then atomically move over the real file. A crash mid-write must never corrupt an existing save.
- Keep one rolling backup (`save_slot0.bak`). If the primary fails validation on load, offer to restore the backup.
- Autosave must not cause a frame hitch. Serialize on a background thread; only the final file write touches the main thread. Show a small, non-intrusive autosave indicator.
- In co-op, only the host saves. Guests never write save data.

---

## 9. ACCEPTANCE CRITERIA

All must pass:

1. Every screen is fully navigable with a gamepad only, with no dead ends and sensible default focus.
2. `Esc` / `B` always closes exactly one level of the screen stack.
3. `J` opens the journal; `Tab` still shows the help tooltip; gamepad tap vs hold on `View` reliably distinguishes the two.
4. Journal parcel figures match actual world state within 0.1% after a save/load cycle.
5. Money earned attributes to the parcel the hay was cut in, verified by cutting in Parcel 2 and selling at the Parcel 1 farmstead.
6. Camera shake and head bob at 0% produce mathematically zero camera movement.
7. A synthetic v1 save loads, migrates, and produces a populated journal.
8. Killing the process mid-autosave never produces an unloadable save.
9. Hay piles, bales, and partial collection-cell values all survive save/load with positions intact.
10. Every visible string comes from the localization system; switching to German or Russian breaks no layout.
11. Settings survive deleting the save file.
12. No per-frame allocation in any open UI screen (verified in the Profiler).
13. Menus hold 60 fps on Steam Deck at 1280×800.

---

## 10. DELIVERABLES

- All source in the existing project, following its established conventions.
- `SessionState` as the single authority object, with a clean interface the future networking layer can wrap.
- Localization key file covering every new string, English filled in, other languages as keys awaiting translation.
- Unit tests for save migration and cut grid round-trip.
- A short README documenting the save schema, the migration chain, and how to add a v3.

---

## 11. EXPLICITLY OUT OF SCOPE

Multiple save slots. Cloud save conflict resolution UI beyond Steam's default. Public lobby browser. In-game chat. Emote wheel. Photo mode. Any actual netcode — the co-op UI is built and stubbed, not wired.
