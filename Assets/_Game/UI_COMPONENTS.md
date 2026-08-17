# Stubble — UI Components & Image Generation Guide

Az összes UI panel és interaktív elem listája, valamint az egységes image-generálási prompt stílus.

---

## Egységes Image Prompt Stílus

Minden generált kép tartsa be ezt az alap stílust:

> **"Flat illustration, warm earthy tones, hand-drawn feel, rustic farm aesthetic. Simple bold shapes, slight grain texture, no photorealism. Color palette: wheat gold (#D4A853), hay green (#7A9A4C), barn red (#8B3A3A), sky blue (#6AABCA), soil brown (#6B4C2A). White or transparent background."**

---

## Képek amelyek szükségesek

### Shop — Tools Tab

| Elem | Leírás | Prompt kiegészítés |
|------|--------|-------------------|
| `tool_handsickle.png` | Hand Sickle ikon | "simple sickle with wooden handle, side view, 128x128" |
| `tool_longscythe.png` | Long Scythe ikon | "long scythe silhouette, blade pointing right, 128x128" |
| `tool_trimmer.png` | String Trimmer ikon | "electric string trimmer / weed eater, top-down view, 128x128" |
| `tool_pushmower.png` | Push Mower ikon | "push lawn mower side view, 128x128" |
| `tool_rideon.png` | Ride-On Mower ikon | "small ride-on lawn tractor, side view, 128x128" |

### Shop — Upgrades Tab

| Elem | Leírás | Prompt kiegészítés |
|------|--------|-------------------|
| `upgrade_baler.png` | Baling machine upgrade | "square hay baler machine, side view, 128x128" |
| `upgrade_blade.png` | Blade sharpness upgrade | "sharpening stone with sparks, 128x128" |
| `upgrade_roundbaler.png` | Round baler upgrade | "round hay bale roller machine, 128x128" |

### Shop — Parcels Tab

| Elem | Leírás | Prompt kiegészítés |
|------|--------|-------------------|
| `parcel_0.png` | Home Field térkép bélyeg | "small green field with farmhouse, top-down map style, 160x160" |
| `parcel_1.png` | East Meadow | "meadow with wildflowers, east facing, 160x160" |
| `parcel_2.png` | Hillside | "sloped hillside field with gradient, 160x160" |
| `parcel_3.png` | South Plateau | "flat plateau field, 160x160" |

### Journal

| Elem | Leírás | Prompt kiegészítés |
|------|--------|-------------------|
| `journal_cover.png` | Journal / napló háttér | "old leather-bound notebook cover, 512x256, worn edges" |
| `journal_tab_parcels.png` | Parcels tab ikon | "map/field icon, 48x48" |
| `journal_tab_stats.png` | Statistics tab ikon | "bar chart / stats icon, 48x48" |
| `journal_tab_records.png` | Records tab ikon | "trophy/medal icon, 48x48" |

### HUD

| Elem | Leírás | Prompt kiegészítés |
|------|--------|-------------------|
| `hud_bale_icon.png` | Bála számláló ikon | "stacked square hay bale, 64x64" |
| `hud_money_icon.png` | Pénz ikon | "coin or dollar bill, 64x64" |
| `hud_stamina_icon.png` | Stamina bar ikon | "lightning bolt / energy icon, 48x48" |

### Main Menu

| Elem | Leírás | Prompt kiegészítés |
|------|--------|-------------------|
| `mainmenu_logo.png` | Játék logó | "STUBBLE wordmark, bold rustic serif font, wheat stalk decoration, 512x128" |
| `mainmenu_bg_overlay.png` | Félátlátszó overlay | "subtle grain/vignette texture, dark edges, 1920x1080, alpha-capable PNG" |

### End Screen

| Elem | Leírás | Prompt kiegészítés |
|------|--------|-------------------|
| `end_nuclear_bg.png` | Nuclear ending háttér | "mushroom cloud silhouette in distance over farm fields, dramatic sunset, 512x256" |
| `end_peaceful_bg.png` | Peaceful ending háttér | "golden hour over cleared hayfield, warm glow, 512x256" |

---

## UI Komponensek listája

### Screens (UIScreen subclasses)

| Script | Canvas neve | Leírás |
|--------|-------------|--------|
| `MainMenuScreen.cs` | MainMenuScreen | Főmenü (Continue / New Game / Settings / Credits / Quit) |
| `PauseScreen.cs` | PauseScreen | Szünet menü |
| `SettingsScreen.cs` | SettingsScreen | Display / Audio / Controls / Accessibility / Language tabok |
| `JournalScreen.cs` | JournalScreen | Parcels / Statistics / Records |
| `ShopUI.cs` | ShopUI | Tools / Upgrades / Parcels shop |
| `CreditsScreen.cs` | CreditsScreen | Görgethető credits |
| `CoopScreen.cs` | CoopScreen | Co-op lobby (host/join) |
| `EndScreen.cs` | EndScreen_Canvas | Befejező statisztikák + graph |
| `ConfirmModal.cs` | ConfirmModal | Megerősítő modál (ok/cancel) |
| `AccessibilitySettings.cs` | — | Accessibility sub-panel |

### HUD Components (nem UIScreen)

| Script / GO neve | Leírás |
|-----------------|--------|
| `HUDController.cs` | Stamina bar, fuel bar, pénz, completion %, bála count |
| `TooltipHUD.cs` | Tab-hold kontrol tooltip panel |
| `AutosaveIndicator.cs` | Mentési folyamatjelző |
| `BaleDebugPanel.cs` | Dev-only debug overlay |

### Interaktív elemek (gombok, sliderek)

Minden `Button` komponens kap automatikusan hover stílust (`UIButtonHoverStyle.cs`):
- **Normal:** `#FFFFFF`
- **Highlighted (hover):** `#D9F2FF` (kék árnyalat)  
- **Pressed:** `#A6BFE6`
- **Disabled:** `#808080` (50% opacity)
- **Fade duration:** 0.1s

---

## Konvenciók

- Minden ikon **PNG, átlátszó háttér**
- Fő méretek: `48×48`, `64×64`, `128×128`, `160×160`, `512×256`
- Shop ikonok: `128×128`, fehér/átlátszó háttér, a stílus köré kerül egy keret a ShopUI-ban
- Map bélyegek: `160×160`, enyhe keret/rounded corners
- Összes kép helye: `Assets/_Game/UI/Sprites/`
