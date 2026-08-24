# Stubble — UI Components

Az összes létező UI screen, HUD elem és control típus listája — kizárólag a forráskódban ténylegesen szereplő elemek alapján.

---

## Screens (UIScreen subclassok)

### MainMenuScreen.cs
| Mező | Típus | Funkció |
|------|-------|---------|
| `continueButton` | Button | Mentett játék folytatása |
| `newGameButton` | Button | Új játék (ConfirmModal-lal ha van mentés) |
| `playWithFriendsButton` | Button | CoopScreen megnyitása |
| `settingsButton` | Button | SettingsScreen megnyitása |
| `creditsButton` | Button | CreditsScreen megnyitása |
| `quitButton` | Button | Kilépés (ConfirmModal-lal) |

---

### PauseScreen.cs
| Mező | Típus | Funkció |
|------|-------|---------|
| `resumeButton` | Button | Játék folytatása |
| `journalButton` | Button | Journal megnyitása |
| `settingsButton` | Button | Settings megnyitása |
| `quitButton` | Button | Kilépés főmenübe |
| `inviteFriendButton` | Button | Steam meghívó (co-op, csak aktív lobby esetén) |
| `playersButton` | Button | Játékosok listája (co-op) |
| `overlay` | Image | Félátlátszó háttér dimmer |

---

### SettingsScreen.cs — 5 tab

**Tab navigation**
| Mező | Típus |
|------|-------|
| `tabButtons[5]` | Button[] — [0]=Display [1]=Audio [2]=Controls [3]=Accessibility [4]=Language |
| `tabPanels[5]` | GameObject[] — párhuzamos a tabButtons-szal |
| `closeButton` | Button |

**Display tab**
| Mező | Típus |
|------|-------|
| `fovSlider` | Slider |
| `fovValueLabel` | TextMeshProUGUI |
| `windowModeButtons[3]` | Button[] — Fullscreen / Borderless / Windowed |
| `qualityButtons[3]` | Button[] — Low / Medium / High |
| `vsyncToggle` | Toggle |
| `frameCapButtons[5]` | Button[] — 30 / 60 / 120 / 144 / Unlimited |

**Audio tab**
| Mező | Típus |
|------|-------|
| `masterSlider` | Slider |
| `toolsSlider` | Slider |
| `worldSlider` | Slider |
| `ambienceSlider` | Slider |
| `uiSlider` | Slider |
| `masterLabel` … `uiLabel` | TextMeshProUGUI (5 db %-os label) |

**Controls tab**
| Mező | Típus |
|------|-------|
| `mouseSensXSlider` | Slider |
| `mouseSensYSlider` | Slider |
| `invertYToggle` | Toggle |
| `gamepadSensSlider` | Slider |
| `gamepadDeadzoneSlider` | Slider |
| `toolHoldButton` / `toolToggleButton` | Button (kizáró pár) |
| `sprintHoldButton` / `sprintToggleButton` | Button (kizáró pár) |
| `resetRebindsButton` | Button |

**Accessibility tab**
| Mező | Típus |
|------|-------|
| `headBobToggle` | Toggle |
| `cameraShakeToggle` | Toggle |
| `cameraShakeSlider` | Slider |
| `swingKickSlider` | Slider |
| `rumbleToggle` | Toggle |
| `rumbleSlider` | Slider |
| `uiScaleButtons[4]` | Button[] — 80% / 100% / 120% / 150% |
| `showStaminaToggle` | Toggle |
| `showCarryToggle` | Toggle |
| `showMoneyToggle` | Toggle |
| `showCompletionToggle` | Toggle |
| `contextualHintsToggle` | Toggle |
| `highContrastToggle` | Toggle |

**Language tab**
| Mező | Típus |
|------|-------|
| `languageButtons[]` | Button[] — egy gomb nyelvenként |

**Resolution revert dialog**
| Mező | Típus |
|------|-------|
| `revertDialog` | GameObject (panel) |
| `revertCountdownText` | TextMeshProUGUI — "Reverting in Xs…" |

---

### JournalScreen.cs — 3 tab
| Mező | Típus | Funkció |
|------|-------|---------|
| `tabButtons[]` | Button[] — Parcels / Statistics / Records | Tab váltás |
| `tabPanels[]` | GameObject[] | Párhuzamos a tabButtons-szal |
| `parcelCards[4]` | ParcelCard[] | Parcella kártyák (0–3) |
| `statisticsContent` | Transform | Statistics ScrollView tartalom |
| `recordsContent` | Transform | Records ScrollView tartalom |
| Stat TextMeshProUGUI mezők | TextMeshProUGUI | Area, Hay, Bales, Money, Distance, Playtime stb. |
| Close gomb | Button | Journal bezárása |

---

### ShopUI.cs — 3 tab (Unlocks rejtett)
| Mező | Típus | Funkció |
|------|-------|---------|
| `tabTools` | Button | Tools tab |
| `tabUpgrades` | Button | Upgrades tab |
| `tabUnlocks` | Button | Unlocks tab (SetActive(false) — jelenleg rejtett) |
| `closeButton` | Button | Shop bezárása |
| `contentParent` | Transform | ScrollView tartalma — sorok procedurálisan épülnek fel |
| `dimmerOverlay` | GameObject | Háttér dimmer |

---

### CreditsScreen.cs
| Mező | Típus | Funkció |
|------|-------|---------|
| `scrollRect` | ScrollRect | Auto-scroll alulról felfelé |
| `backButton` | Button | Vissza / bezárás |

---

### CoopScreen.cs — 2 tab
| Mező | Típus | Funkció |
|------|-------|---------|
| `hostTabButton` / `joinTabButton` | Button | Tab váltás |
| `hostPanel` / `joinPanel` | GameObject | Tab panelek |
| `startButton` | Button | Lobby indítása |
| `inviteButton` | Button | Steam meghívó (csak aktív lobby esetén aktív) |
| `visibilityButtons[2]` | Button[] — Friends Only / Invite Only | Lobby láthatóság |
| `noticeText` | TextMeshProUGUI | Tájékoztató szöveg |
| `emptyStateText` | TextMeshProUGUI | "Nincs található lobby" szöveg (Join tab) |
| `statusText` | TextMeshProUGUI | Státusz szöveg (pl. "Creating lobby…") |
| `backButton` | Button | Vissza |

---

### EndScreen.cs
| Mező | Típus | Funkció |
|------|-------|---------|
| Cím label | TextMeshProUGUI | "All Fields Cleared!" / "☢ Nuclear Ending" |
| Stat sorok | TextMeshProUGUI | Idő, pénz, terület |
| Graph panel | `StatsRenderer` | CUT AREA OVER TIME görbe |
| Achievement lista | ScrollView | Feloldott achievementek |
| `mainMenuButton` | Button | Főmenübe visszatérés |
| `playAgainButton` | Button | Újrajátszás |
| `quitButton` | Button | Kilépés |

---

### ConfirmModal.cs
| Mező | Típus | Funkció |
|------|-------|---------|
| `headerText` | TextMeshProUGUI | Fejléc (pl. "Quit") |
| `bodyText` | TextMeshProUGUI | Leírás szöveg |
| `confirmButton` | Button | Megerősítés |
| `cancelButton` | Button | Visszavonás (alapértelmezett fókusz) |

---

## HUD Komponensek (nem UIScreen)

### HUDController.cs
| Mező | Típus | Leírás |
|------|-------|--------|
| `staminaBar` | MMProgressBar | Kitartás sáv |
| `fuelBar` | MMProgressBar | Üzemanyag sáv (ride-on mower) |
| `grassCutBar` | MMProgressBar | Parcella haladás sáv |
| `balingBar` | MMProgressBar | Bálázás progress sáv |
| `wearBar` | MMProgressBar | Eszköz kopás sáv |
| `moneyText` | TextMeshProUGUI | Pénz kijelző |
| `completionText` | TextMeshProUGUI | Befejezettség % |
| `baleCountText` | TextMeshProUGUI | Hordott bálák száma |
| `balingBarLabel` | TextMeshProUGUI | "Hold [E] to bale" / "Baling… X%" |
| `promptText` | TextMeshProUGUI | Interakciós prompt (pl. "[E] Pick up bale") |
| `crosshair` | Image | Célkereszt ikon |

### TooltipHUD.cs
| Mező | Típus | Leírás |
|------|-------|--------|
| `panel` | GameObject | Teljes tooltip panel (Tab/View hold → látható) |
| `tooltipText` | TextMeshProUGUI | Kontroll lista + tippek |

### AutosaveIndicator.cs
| Mező | Típus | Leírás |
|------|-------|--------|
| `group` | CanvasGroup | Fade-in (0.25s) → Hold (1.5s) → Fade-out (0.8s) animáció |
| `label` | TextMeshProUGUI | Mentés szöveg |

---

## UI Control Típusok

### Progress Bar (MMProgressBar)
5 darab: stamina, fuel, grassCut, baling, wear — a HUDController-ben. MoreMountains Feel könyvtár.

### ScrollView (ScrollRect)
| Hol | Tartalom |
|-----|---------|
| ShopUI `contentParent` | Procedurálisan épített eszköz/upgrade sorok |
| JournalScreen `statisticsContent` | Statisztika sorok |
| JournalScreen `recordsContent` | Rekord sorok |
| CreditsScreen `scrollRect` | Auto-scroll szöveg |
| EndScreen achievements | Achievement lista |

### Tab Button (kizáró gombcsoport)
| Screen | Tabok |
|--------|-------|
| SettingsScreen | Display / Audio / Controls / Accessibility / Language |
| ShopUI | Tools / Upgrades / (Unlocks — rejtett) |
| JournalScreen | Parcels / Statistics / Records |
| CoopScreen | Host / Join |

### Close / Back gomb
| Script | Mező |
|--------|------|
| SettingsScreen | `closeButton` |
| ShopUI | `closeButton` |
| CreditsScreen | `backButton` |
| CoopScreen | `backButton` |

### Toggle
Megtalálható a SettingsScreen Accessibility és Controls tabján: `vsyncToggle`, `invertYToggle`, `headBobToggle`, `cameraShakeToggle`, `rumbleToggle`, és 6 HUD láthatóság toggle.

### Slider
Megtalálható a SettingsScreen Display, Audio és Controls tabján: FOV, 5 hangerő, 4 kontroll érzékenység, camera shake, swing kick, rumble intenzitás.

### Kizáró Button Csoport (index-alapú)
Zöld/szürke tintával jelzi az aktív elemet (`SetActiveButton` helper). Használja: `windowModeButtons`, `qualityButtons`, `frameCapButtons`, `uiScaleButtons`, `languageButtons`, `visibilityButtons` (CoopScreen).

### Dimmer Overlay
| Script | Mező |
|--------|------|
| PauseScreen | `overlay` (Image) |
| ShopUI | `dimmerOverlay` (GameObject) |

### CanvasGroup Fade
`AutosaveIndicator` — fade-in / hold / fade-out animáció CanvasGroup alpha-val.

### Hover ColorBlock
Minden Button kap `UIButtonHoverStyle` által beállított `ColorBlock`-ot:
- Normal: `#FFFFFF`
- Highlighted: `(0.85, 0.95, 1.00)` — kék árnyalat
- Pressed: `(0.65, 0.75, 0.90)`
- Disabled: 50% szürke
- Fade: 0.1s
