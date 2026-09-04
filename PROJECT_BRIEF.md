# Just Mow It — Project Brief

---

## 1. What Is This Game?

**Just Mow It** is a cozy, low-stress farming sim where you inherit an overgrown field, grab a scythe (or later, a ride-on mower), mow the grass, bale the hay, and sell it. That's the loop.

**Tagline:** *"mow. bale. sell. repeat."*

There are no seasons, no crop rotation, no survival mechanics. Just the meditative satisfaction of turning a messy field into clean rows of bales — and the occasional chaos of doing it with friends.

- **Engine:** Unity 6 URP
- **Platform:** Windows (Steam) + Steam Deck
- **Co-op:** 1–4 players via Steam (Mirror networking)
- **Price:** $6.99
- **Session length:** ~3 hours (one full playthrough)
- **Endings:** 3 possible endings depending on player choices

---

## 2. Core Gameplay Loop

```
Mow grass → Hay accumulates → Stand on it → Hold E to bale
→ Carry square bales to the Sale Stand → Sell → Buy better tools → Repeat
```

### Tools (purchasable in-game)
| Tool | Notes |
|------|-------|
| Hand Sickle | Starter melee tool, basic swing arc |
| Long Scythe | ±22.5° fan sweep, satisfying wide cuts |
| String Trimmer | Precise, motor-powered |
| Push Mower | Ground-level systematic rows |
| Ride-On Mower | Fastest, funniest, most dangerous in co-op |
| Round Baler | Attachable; produces round bales that roll downhill |

All melee tools follow a **WindUp → Sweep → Recovery** swing rhythm. Cutting direction always follows the camera forward (XZ projected), so you mow where you look.

### Baling
- Stand on a mowed area that has accumulated enough hay (threshold: 50 units in radius)
- A HUD progress bar appears — hold or press **E** to bale
- Square bale spawns at your feet
- Carry up to several bales at once; drop them at the Sale Stand to earn money
- Round bales can be released on slopes — they will roll. This is intentional. This is the game.

---

## 3. Characters

Four playable characters, each with a distinct personality and visual look. In first-person play, only the arms and tool are visible. In co-op, other players appear as third-person characters. Character selection happens at the main menu.

*(3D models are pending delivery — placeholder meshes in-engine currently.)*

---

## 4. Cozy Game Feel — Design Principles

Just Mow It is explicitly cozy-first. These rules govern every design decision:

- **No input-blocking animations** — swing queue instead of lockout
- **No hard stamina walls** — stamina slows you, never stops you completely
- **No realistic vehicle physics** — the ride-on mower is fun, not a physics sim
- **No bale-flipping the player** — bales push you aside, they don't launch you
- **Rolling bales on slopes are a feature**, not a bug
- **All UI numbers animate** — no snapping values
- **Max 5 single-line tutorial hints** — no popup storms
- **The grass becomes stubble when cut** — it never just disappears

The world is one open seamless field. No gates, no locked parcels, no loading screens between areas. The player never perceives any internal terrain divisions.

---

## 5. Humor & Viral Mechanics

This section covers the elements specifically designed to generate clips, streams, and memes. These are implemented as optional/emergent moments, never forced.

### 5.1 Co-op Chaos (Phase 7 features)

**Toilet Buff — "Porcelain Throne"**
A toilet near the barn. Sit on it (E) and every sale earns +10% while someone is seated. Only one seat. In co-op: someone has to sacrifice mobility for the team. Toast for the team when occupied: *"Someone is working hard for the team."*
Achievements: Porcelain Throne / Moral Support / Queue Theory

---

**Dog Poop + String Trimmer — "Occupational Hazard"**
5–8 dog poop objects hidden in the grass. The string trimmer (and only the string trimmer, because physics) launches a splatter onto your camera. Brown overlay fades over 15 seconds. Poop can be picked up bare-handed and thrown at other players.
Toast: *"...you should've seen that."*
Achievements: Occupational Hazard / Dedicated Worker / Friendly Splatter

---

**Alcohol — "Liquid Courage"**
A bottle in the barn (not purchasable, one per session). Drink it: 120 seconds of +35% swing speed, camera sway, chromatic aberration, and random hiccup SFX. The ride-on mower refuses to let you on (*"YOU'VE HAD ENOUGH."*). In co-op, you can offer it to other players — they choose whether to accept.
Achievement: The Full Experience — for drinking AND sitting on the toilet.

---

**Rock Projectile — "Rock and Roll"**
Any mowing tool (not string trimmer) has a 1/150 chance per swing of kicking up a hidden rock. The rock flies toward the nearest player. When it hits: slow-motion cinematic camera on the attacker's side, the rock visible in frame, 0.4s slowdown, then snap back to FP. If the target was holding bales: they drop them all.
Achievements: Rock and Roll / Headshot / Duck! / One Stone

---

**Tractor Collision — "Involuntary Flight"**
The ride-on mower at >2 m/s launches any player it hits: forward impulse + upward arc, 0.8s input lock mid-air, puff particle + thud on landing. If the driver was drunk: stronger. Dropped bales scatter and can be picked up by others.
Achievement: Freeloading — for selling bales that someone else dropped.

---

### 5.2 AFK Bird — Cinematic Shoulder Bird

If the player doesn't move for 60 seconds, a bird flies in and lands on their shoulder. Cinematic camera slowly circles around to show their face. Bird SFX loops. If the player moves — the bird flies away.
In co-op: everyone sees the bird on their own character.
Achievement: New Friend — *"A bird landed on you. You are officially a Disney protagonist."*

---

### 5.3 Rolling Bale Cinematic

If a round bale rolls faster than 3 m/s for 2+ continuous seconds: cinematic camera activates, follows the bale from behind, 0.6× slow-motion. Player input is NOT blocked — only the camera switches. When the bale stops or falls off the map: 1.5s hold, then FP camera returns.

---

### 5.4 Cat Economy — "Cat Tax"

There is a cat on the farm. The cat can be pet (hold E near it for 1.2s). Once pet, it follows the player for 5 minutes.

Sale multipliers stack:
- Someone sitting on the toilet: **+10%**
- Cat following a player: **+5%**
- Cat watching while you bale (within 5m, facing you): **+15%**
- Cat grooming itself while facing the seated toilet player: **+20%**

Maximum theoretical stack: **+30%** from toilet + cat combo.
Achievement: Productivity Hack — *"toilet + cat multiplier active at the same time during a sale."*

---

### 5.5 Chicken Army — "They Remember"

The cat chases chickens. If the cat kills a chicken within the first 10 minutes of the session: chickens begin appearing near the silos and barn in waves over the next 8+ minutes, up to 30 total. They do nothing. They just stand there. And watch. If you walk within 5m of a group, they all snap to face you in unison. Silent. Waiting.
Achievement: Walk Away — *"You finished the field with 30 chickens present."*

---

### 5.6 Sale Comments — "Certified Fresh Hay"

Every sale shows an absurd quality comment below the money pop:
- *"The cows voted this Hay of the Year. The vote was not close."*
- *"Gordon Ramsay saw this. He left. He's not coming back."*
- *"Certified fresh. Do not question the certification process."*
- *"The buyer wept. We're not sure why. We didn't ask."*
- *(10 total, no repeat on consecutive sales)*

---

### 5.7 Meme Achievements

| ID | Name | Description |
|----|------|-------------|
| `ACH_TOUCH_GRASS` | Touch Grass | *"You touched grass. Literally."* — triggers on first mow |
| `ACH_FREE_REAL_ESTATE` | It's Free Real Estate | *"You bought land. The grass was already there."* |
| `ACH_NPC_BEHAVIOR` | NPC Behaviour | *"You walked the same path 10 times. You are the NPC."* |
| `ACH_GONE_WITH_WIND` | Gone With The Wind | *"A bale rolled off the map. It's someone else's problem now."* |
| `ACH_THIS_IS_FINE` | This Is Fine | *"You lost 3 bales. Everything is fine."* |
| `ACH_FRIENDLY_FIRE` | Friendly Fire | *"The scythe does not discriminate."* — hit another player in co-op |
| `ACH_TETRIS` | Tetris Farmer | *"Four bales, neatly stacked. Somewhere, a Tetris theme plays."* |
| `ACH_SKILL_ISSUE` | Skill Issue | *"The slope won. The slope always wins."* — dropped a bale on >15° incline |
| `ACH_NICE_69` | Nice. | *"Nice."* — balance hits exactly $69 |
| `ACH_BLAZE_420` | Blaze It, Farmer | *"$420. The hay business is booming."* |
| `ACH_MIDNIGHT` | Midnight Harvest | *"You sold hay at midnight. We don't judge. We just note it."* |
| `ACH_SHEEP` | Did You See That? | A rare sheep runs through the field. Must be watching when it appears. |
| `ACH_42KM` | 42 | *(no description — the community will figure it out)* |

---

### 5.8 In-Game "AI" Terminal — BarnAI

The barn contains a retro terminal. Typing `AI` opens a fake chatbot (BarnAI). It responds to 30+ keywords with absurd farm-flavored answers. It is very confident. It is never correct. It is very entertaining.
Achievement: MEET_AI — *"You consulted the AI. We're sorry."*

---

### 5.9 Cheat Codes (Name Input)

At character naming, entering these names triggers effects:
- `hesoyam` — +$50,000 (GTA San Andreas reference)
- `motherlode` — +$50,000 (The Sims reference)
- `iddqd` — infinite stamina + infinite fuel for 5 minutes (DOOM reference)

### 5.10 Konami Code — Speedrun Timer

`↑↑↓↓←→←→` (WASD/D-pad) at any time during play. Toggles a millisecond-accurate speedrun timer in the top-right corner. No in-game hint. The speedrunning community will find it.

---

## 6. Visual Style

### Overall Aesthetic
Warm, earthy, handmade. Everything looks like it was assembled from materials found in an old farmhouse — cardboard, rope, parchment paper, corkboards, wooden frames. Nothing is perfectly straight or factory-printed.

**This is not:**
- Flat design / Material UI / glassmorphism
- Cartoon with hard outlines
- Fantasy or sci-fi
- Clean and modern

**This is:**
- Textured realism
- Slightly worn and imperfect
- Warm and readable at Steam Deck screen sizes

### The Farmer's Notebook — UI Metaphor
All menus appear as notebook or cardboard panels. Tabs are cardboard strips with paperclips. Buttons are torn paper labels. The world is always visible behind the UI — no full-screen black overlays, no loading transitions.

### Color Palette
| Role | Color |
|------|-------|
| Background | Parchment cream `#F0E2C2` |
| Background dark | Dark parchment `#C7AD84` |
| Text primary | Dark ink brown `#2E1F15` |
| Text secondary | Medium ink brown `#665038` |
| Confirm action | Olive green `#527040` |
| Money | Warm gold `#B88C2E` |
| Danger | Muted barn red `#9E3825` |
| Accent | Rope/jute `#B89451` |

### Typography
| Font | Role |
|------|------|
| **Shantell Sans** | Display — titles, buttons, tabs |
| **Courier Prime** | Body/Data — stats, numbers, HUD |
| **Caveat** | Handwritten — price labels, easter egg text |

All are Google Fonts (open license).

### UI Components
- **Primary button:** Worn cardboard strip, fixed moderate width, scales + brightens on hover
- **Shop buy button:** Price tag shape (rounded rect with string hole), olive when affordable, gray when not
- **Close button:** Small round cork/cardboard with X, feels like a thumbtack
- **Tabs:** Cardboard notebook dividers; active tab has a visible paperclip
- **Progress bar:** Wooden/rope frame, fill color by context (gold=money, olive=progress, red=danger)
- **Scrollbar:** Rope on wooden track, round thumb handle
- **Modal:** Floating note pinned on top of current screen
- **Toast:** Brief horizontal strip (2–3s), fades out, used for sale comments, achievements, autosave

---

## 7. Screens Overview

### Main Menu
Live 3D game world behind a slow cinematic drift. UI floats over the farm. No separate menu scene.
- Logo "JUST MOW IT" — top third
- Tagline *"mow. bale. sell. repeat."* — handwritten style below logo
- Button stack: Continue / New Game / Play with Friends / Settings / Credits / Quit

### Pause
- Single-player: 75% dark overlay, game frozen. Resume / Journal / Settings / Quit to Menu
- Co-op: 55% overlay, game continues. Extra: Invite Friend / Players. Notice: *"The game continues while this menu is open."*

### Shop
Opens when interacting with the Sale Stand in the world. Two tabs: **Tools** | **Upgrades**. Owned items show a text label instead of a buy button. Money displayed top-right in warm gold.

### Journal
A single horizontal notebook page. Two-column grid of stats: area cut over time (line diagram), total area, time, money, bale counts. Reads like a handwritten field report.

### HUD
Minimal. Elements fade out when not relevant.
- Money counter (top-right, gold, animates on earn)
- Completion % (total field, one unified number — not per-parcel)
- Stamina bar (fades when full)
- Fuel bar (only when powered tool is active)
- Baling progress bar (only during baling action)
- Bale carry count (near crosshair)
- Interaction prompt (contextual, fades when nothing nearby)
- Autosave indicator (brief, unobtrusive)

---

## 8. What's Built vs. What's Pending

### Complete ✅
- Full grass mowing system (GPU-instanced, LOD, 180 blades/cell)
- All 5 hand tools + ride-on mower
- Hay accumulation + square baling
- Economy (money, Shop UI, SaleStand auto-sell)
- Save/continue system
- 9-language localization (EN + HU full, 7 language stubs)
- Mirror co-op (2–4 players, Steam lobby)
- 9 SFX clips, audio manager
- 25 Steam achievements (base set)
- 3 endings (Peaceful / Loop / Nuclear)
- BarnAI chatbot terminal
- EndScreen with stat-based commentary
- Bale outline highlight (yellow inverted hull on nearest bale)
- Dirt cells excluded from mowing progress
- UI: drop shadows, hover scale, price tag buy buttons, progress bars

### Pending ⏳
- 3D character models + animations (blocked on asset delivery)
- 3D tool models (blocked on asset delivery)
- Phase 7 co-op chaos mechanics (toilet, poop, alcohol, rocks, tractor launch)
- AFK bird cinematic
- Rolling bale cinematic camera
- Cat economy system
- Chicken army easter egg
- Meme achievement pack (14 new achievements listed above)
- Cheat code system (name input + runtime)
- Konami speedrun timer
- Sale comment toasts
- Currency icon in HUD
- First-launch tutorial hint (max 5 lines)

### Known Bugs (Active)
- Sharpening: should be one-press, not hold; player should be immobile during it
- Baling: should be one-press, not hold (same UX as sharpening)
- Grass mow sound plays even when swinging at already-cut ground
- Bale yellow outline position slightly offset from bale mesh
- Shop still shows $ prices instead of localized currency
- Continue doesn't restore player position (spawns at default instead)
- Journal: remaining area calculation wrong; some stats incorrect
- Bale drop with G key not working
- HUD completion % still divides by parcels instead of total field
- Cat/chicken state not saved on Continue

---

## 9. Marketing Assets Needed

| Asset | Size | Notes |
|-------|------|-------|
| Logo "Just Mow It" | SVG + PNG | Works on light + dark backgrounds |
| Logo + tagline lockup | SVG + PNG | Paired version |
| Steam Capsule (main) | 616×353px | Title + gameplay read + earthy tone |
| Steam Capsule (small/header) | Various | Standard Steam sizes |
| Social Media icon | 1:1 min 500×500px | Circular crop-safe |

Tone for all marketing assets: warm, earthy, slightly absurd. Characters and tools are fair game. The bale, the scythe, and the ride-on mower are the most iconic visual elements.

---

## 10. Things That Must NEVER Change

1. Rolling round bales are a feature — do not "fix" them
2. The field is one seamless open world — no visible parcel boundaries
3. Completion % is a single number for the whole field
4. Stamina slows, never hard-stops
5. All UI numbers animate smoothly — never snap
6. No more than 5 single-line tutorial hints total
7. Mowing leaves stubble — grass is never simply deleted
8. Ride-on mower pushes players, does not flip them
