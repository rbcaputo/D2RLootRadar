# D2RLootRadar (v2.1.0)

[![CI](https://github.com/rbcaputo/D2RLootRadar/actions/workflows/ci.yml/badge.svg)](https://github.com/rbcaputo/D2RLootRadar/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](./LICENSE)

A lightweight Windows overlay tool for **Diablo II: Resurrected** that watches the ground for item base types you care about, and alerts you the moment one drops — no more scanning a pile of white items hoping you didn't miss something underneath.

Tap **ALT** (D2R's native "show item labels" key) and D2RLootRadar reads the labels on screen via OCR, fuzzy-matches them against your personal watch list, and plays a tone plus an on-screen marker if something you're watching for is there.

> **Disclaimer:** D2RLootRadar is an unofficial, fan-made tool and is not affiliated with, endorsed by, or sponsored by Blizzard Entertainment. Diablo® II: Resurrected™ and all related trademarks are property of Blizzard Entertainment, Inc.
>
> The tool is purely passive and external to the game: it only reads pixels already rendered on your screen via OCR, and never reads or writes game memory, injects code, modifies game files, or intercepts network traffic. It has no visibility into and no interaction with anything happening client-side in memory or server-side online — as far as the game is concerned, nothing about how you're playing has changed.
>
> That said, this is a factual description of what the tool does, not a legal assurance — I'd encourage reviewing Blizzard's current EULA/ToS yourself before use, since only Blizzard can determine what is or isn't permitted.

## Why

This started from a familiar frustration: standing over a pile of white items after a group kill or a super chest open, eyes scanning line by line for the one base you actually need for a runeword — easy to miss, especially in a rush or when the labels clump together. It gets worse the longer a farming session runs: eye strain and plain mental fatigue creep in over hours of grinding, and a label you'd catch instantly on your first run can slip right past on your fiftieth.

D2RLootRadar doesn't replace that scan, and it isn't perfect — OCR misreads happen, and it won't catch everything. What it does is give you a second pass: a beep and a marker on anything from your watch list, so you don't have to rely on catching it the first time. Some players can eye-scan a burst of drops fast enough that this adds little for them, and that's fine — this is a small quality-of-life tool for the rest of us who'd rather not risk running past a rune base in a stack of junk.

It's best thought of as commplementary to D2R's own native loot filter, not a replacement for it — the in-game filter already cuts down the clutter you have to scan in the first place; D2RLootRadar is the safety net that catches what's left, even in the labels that do stay visible.

## How it works

```text
ALT pressed
  │
Capture the D2R window (PrintWindow / DWM)
  │
Crop + adaptive threshold + OCR (Tesseract, LSTM)
  │
For each watch-list item: blend fuzzy text similarity with the label's rarity color confidence
  │
  ├─ combined score below Match Sensitivity → nothing happens
  │
  └─ combined score at or above Match Sensitivity → beep + on-screen marker over the item
```

Text carries most of the wight in that blend — it's the only signal that identifies *which* item a label is; color only narrows down *which variant*. A strong text match can survive an ambiguous color read, but not a confidently wrong one.

Everything is triggered by the ALT key press itself — there's no polling loop running in the background, so it costs nothing while you're just playing.

## Features

- **600+ item bases** (as of this writing) across all D2R item categories — amulets, armor, belts, boots, charms, gems, gloves, helmets, jewels, materials, rings, runes, shields, and weapons — pick exactly which ones you want alerts for. The catalog may lag behind the newest patches; see [Known limitations](#known-limitations).
- **Catalog search box** in the main window — type any part of a base's name, ot the name of a Set/Unique item that drops on it (e.g. "Harlequin Crest" for the Shako it drops on), to instantly narrow the list down to matches.
- **Per-item, per-rarity watch selection.** Instead of a single watched/unwatched toggle, each base gets its own rarity picker listing only the qualities it can actually appear as, and selections stack — watch a base for Set *and* Unique, or Ethereal/Socketed *and* Superior, in any combination. Bases with only one possible quality (Runes, Gems, Materials) skip the picker and get a plain checkbox instead, since there's nothing to choose between.
- **Info icon (ⓘ)** next to a base's name, shown only when there's something to say: its maximum socket count, and/or the names of any Set and Unique items that share that base.
- **Accurate detection across all important item type, not just equipment.** Runes and Materials render in a distinct orange label, and Worldstone Shards in red — both colors are calibrated the same way as the six equipment quality tiers, so matching respects rarity uniformly across the whole catalog rather than treating non-equipment as a special case.
- **"Currently watching" summary panel**, listing every selected item grouped by category, each with dots showing exactly which rarities are selected for it — a running readout of your whole watch list without having to re-expand every category to check. The panel's width is adjustable via a draggable divider.
- **OCR-based detection**, not memory reading or packet inspection — it only ever looks at what's rendered on your screen, the same way you would.
- **Fuzzy matching** tolerant of OCR misreads and truncated text (e.g. a partially-obscured "Monarchii" still matches "Monarch").
- **Configurable audio alert** — tune tone frequency and volume, with a test-sound button.
- **Configurable match sensitivity** — tune how forgiving OCR matching is.
- **Configurable marker display time** — tune how long the on-screen marker stays visible.
- **On-screen overlay marker** at the detected item's exact screen position, non-interactive and click-through so it never gets in the way of actually playing.
- Detects whether D2R is currently running, so the app is safe to leave open all the time.

## Requirements

- Windows 10/11
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download) (or the .NET 10 SDK if you're building from source).
- Diablo  II: Resurrected, running in **windowed** or **borderless fullscreen** mode.

> **Note on display modes:** true exclusive fullscreen bypasses the Windows compositor (DWM), which both the screen capture and the overlay rely on. If detection or the overlay marker don't seem to work, switch D2R to borderless fullscreen or windowed mode in its display settings.

> **Note on multi-monitor setups:** overlay marker positioning is currently validated for single-monitor use. Mixed-DPI multi-monitor setups may see markers offset by a few or more pixels — see [Known limitations](#known-limitations).

## Getting started

### Download

Grab the latest build from the [Releases page](https://github.com/rbcaputo/D2RLootRadar/releases/latest) — download `D2RLootRadar-v2.0.0-win-x64.zip` (not the "Source code" zip/tar.gz — those are just the raw source, not a runnable build), extract it, and run `D2RLootRadar.Desktop.exe`.

Building from source is only necessary if you want to modify the code yourself.

### Build from source

```bash
git clone https://github.com/rbcaputo/D2RLootRadar.git
cd D2RLootRadar
dotnet build
dotnet run --project D2RLootRadar.Desktop
```

Or open `D2RLootRadar.slnx` in Visual Studio 2022+ and run the `D2RLootRadar.Desktop` project.

### Using the app

1. **Launch D2RLootRadar** and D2R (in either order — the app polls for the game process every few seconds).
2. In the **main window**, expand a category and pick your rarities for each base you care about. Most bases open a small picker listing only the qualities they can actually appear as — selections stack, so you can watch a base for Set *and* Unique at once. Bases with only one possible quality (Runes, Gems, Materials) get a plain checkbox instead. An ⓘ icon next to a name means there's more to see — hover it for max sockets and/or the Set/Unique item names for that base.
3. Check the **summary panel** on the right any time for a running list of everything you're currently watching, grouped by category with a dot per selected rarity. Drag the divider between the two panels to resize either one.
4. Open **Settings** to configure the alert tone's frequency and volume, preview it with **Test Sound**, and toggle the on-screen overlay marker.
5. Play normally. When loot drops, tap **ALT** the same way you already do to read item labels — if anything on your watch list is on the ground, in a rarity you selected for it, you'll hear the alert and see a marker over it.

Your selection and settings are saved automatically (debounced ~400ms after your last change) to `settings.json`, next to the executable.

### Alert & overlay tuning

All of the alert and overlay behavior is configurable from the Settings window:

|Setting|Default|Description|
|-------|-------|-----------|
|Beep Volume|10%|0 mutes the alert entirely|
|Beep Frequency|800 Hz| Pitch of the alert tone|
|Beep Duration|200ms|Length of the alert tone|
|Match Sensitivity|80%|Minimum similarity for OCR text to count as a match. Lower is more forgiving of misreads but more prone to false positives|
|Marker Display Time|2s| How long the on-screen marker stays visible. Kept short by default — see [Known limitations](#known-limitations) regarding marker drift if you move after a detection|
|Show overlay on detection|On|Toggles the on-screen marker entirely|

Each settings has an ⓘ icon next to it with a tooltip with the same explanation.

## Project structure

The solution follows a layered architecture, from the inside out:

```text
D2RLootRadar.Domain         → Plain records/enums with no dependencies (ItemBase, WatchList, PixelRect, ...)
D2RLootRadar.Application    → Orchestration and contracts (LootMonitoringService, IOcrService, UserSettings, ...)
D2RLootRadar.Infrastructure → Concrete implementations (OCR, screen capture, fuzzy matching, JSON persistence, Win32 interop)
D2RLootRadar.Desktop        → WPF UI (MVVM, CommunityToolkit.Mvvm, Microsoft.Extensions.Hosting for DI)
D2RLootRadar.Tests          → xUnit tests for the pure-logic pieces (fuzzy matching, settings clamping, watch-list deduplication, ...)
```

`Domain` and `Application` have no Windows-specific dependencies at all; everything platform-specific (GDI+ capture, Win32 keyboard hook, WPF) is isolated in `Infrastructure` and `Desktop`.

## Running tests

```bash
dotnet test
```

Covers the parts of the pipeline that are pure logic and don't require a running instance of D2R: fuzzy string matching, OCR-text quality-prefix stripping, settings value clamping, and watch-list deduplication behavior.

## Known limitations

- **True exclusive fullscreen isn't supported.** Both capture and overlay rely on DWM composition. Use windowed or borderless fullscreen instead.
- **Mixed-DPI multi-monitor setups aren't validated yet.** The overlay currently captures DPI scale once, from whichever monitor initializes it — on a setup with different scaling per monitor, marker placement could be off by a few pixels on secondary displays. Single-monitor setups are unnafected.
- **OCR accuracy depends on your in-game text/UI scale.** If you're seeing missed detections, try adjusting D2R's UI scale settings, or lower `FuzzyMatcherThreshold` slightly.
- **Marker offset while a side panel (inventory or character screen) is open.** D2R shifts its rendered game viewport left/right to make room for the panel, but the captured window's screen rectangle stays the same — so a detection made while a panel is open can place the overlay marker off from the item's actual position by roughly the panel's width. **Workaround:** prefer scanning for items with those panels closed, or re-tap ALT after closing the panel to get a fresh, correctly-aligned reading.
- **Rarity detection is color-calibrated against standard D2R visuals.** Each quality tier (including the two non-equipment colors, Rune/Material orange and Worldstone Shard red) is classified by sampling the label's actual rendered color and matching its hue/saturation. If you run ENB, ReShade, or another visual mod that shifts color grading, classification accuracy may degrade for whichever rarities you've selected — the color bands were tuned against unmodified D2R.
- **Selecting Superior for a base depends on OCR reading the "Superior" word itself**, since Superior shares its white/gray label color with Cracked/Damaged/Low Quality and can't be distinguished by color the way rarities can. If a label is partially obscured such that OCR misses or mangles the word "Superior", that detection is filtered out even though the item is genuinely Superior.

## Contributing

Issues and pull requests are welcome. If you're adding or correcting item base data, `D2RLootRadar.Infrastructure/Data/item-bases.json` is the single source of truth — the same file drives both the watch-list UI and the OCR matching, so no other file needs to change.

## License

MIT — see [LICENSE](./LICENSE).
