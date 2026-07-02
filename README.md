# D2RLootRadar

[![CI]((https://github.com/rbcaputo/D2RLootRadar/actions/workflows/ci.yml/badge.svg)]
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](./LICENSE)

A lightweight Windows overlay tool for **Diablo II: Resurrected** that watches the ground for item base types you care about, and alerts you the moment one drops — no more scanning a pile of white items hoping you didn't miss something underneath.

Tap **ALT** (D2R's native "show item labels" key) and D2RLootRadar reads the labels on screen via OCR, fuzzy-matches them against your personal watch list, and plays a tone plus an on-screen marker if something you're watching for is there.

> **Disclaimer:** D2RLootRadar is an unofficial, fan-made tool and is not affiliated with, endorsed by, or sponsored by Blizzard Entertainment. Diablo® II: Resurrected™ and all related trademarks are property of Blizzard Entertainment, Inc.
>
> The tool is purely passive and external to the game: it only reads pixels already rendered on your screen via OCR, and never reads or writes game memory, injects code, modifies game files, or intercepts network traffic. It has no visibility into and no interaction with anything happening client-side in memory or server-side online — as far as the game is concerned, nothing about how you're playing has changed.
>
> That said, this is a factual description of what the tool does, not a legal assurance — I'd encourage reviewing Blizzard's current EULA/ToS yourself before use, since only Blizzard can determine what is or isn't permitted.

## Why

This started from a familiar frustration: standing over a pile of white items after a group kill or a super chest open, eyes scanning line by line for the one base you actually need for a runeword — easy to miss, especially in a rush or when the labels clump together.

D2RLootRadar doesn't replace that scan, and it isn't perfect — OCR misreads happen, and it won't catch everything. What it does is give you a second pass: a beep and a marker on anything from your watch list, so you don't have to rely on catching it the first time. Some players can eye-scan a burst of drops fast enough that this adds little for them, and that's fine — this is a small quality-of-life tool for the rest of us who'd rather not risk running past a rune base in a stack of junk.

## How it works

```text
ALT pressed
  │
Capture the D2R window (PrintWindow / DWM)
  │
Crop + adaptive threshold + OCR (Tesseract, LSTM)
  │
Fuzzy match detected text against your watch list
  │
  ├─ no match → nothing happens
  │
  └─ match → beep + on-screen marker over the item
```

Everything is triggered by the ALT key press itself — there's no polling loop running in the background, so it costs nothing while you're just playing.

## Features

- **583 item bases** across all D2R item categories — amulets, armor, belts, boots, charms, gems, gloves, helmets, jewels, materials, rings, runes, shields, and weapons — pick exactly which ones you want alerts for
- **OCR-baed detection**, not memory reading or packet inspection — it only ever looks at what's rendered on your screen, the same way you would
- **Fuzzy matching** tolerant of OCR misreads and truncated text (e.g. a partially-obscured "Monarchi" still matches "Monarch")
- **Configurable audio alert** — tone frequency and volume, with a test-sound button
- **On-screen overlay marker** at the detected item's exact screen position, non-interactive and click-through so it never gets in the way of actually playing
- Detects whether D2R is currently running, so the app is safe to leave open all the time

## Requirements

- Windows 10/11
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download) (or the .NET 10 SDK if you're building from source)
- Diablo  II: Resurrected, running in **windowed** or **borderless fullscreen** mode

> **Note on display modes:** true exclusive fullscreen bypasses the Windows compositor (DWM), which both the screen capture and the overlay rely on. If detection or the overlay marker don't seem to work, switch D2R to borderless fullscreen or windowed mode in its display settings.

> **Note on multi-monitor setups:** overlay marker positioning is currently validated for single-monitor use. Mixed-DPI multi-monitor setups may see markers offset by a few pixels — see [Known limitations](#known-limitations).

## Getting started

## Build from source
```bash
git clone https://github.com/rbcaputo/D2RLootRadar.git
cd D2RLootRadar
dotnet build
dotnet run --project D2RLootRadar.Desktop
```

Or open `D2RLootRadar.slnx` in Visual Studio 2022+ and run the `D2RLootRadar.Desktop` project.

### Using the app

1. **Lauch D2RLootRadar** and D2R (in either order — the app polls for the game process every few seconds).
2. In the **main window**, check off the item bases you want to be alerted for. They're grouped by category and searchable by expanding each group.
3. Open **Settings** (gear icon) to configure the alert tone's frequency and volume, preview it with **Test Sound**, and toggle the on-screen overlay marker.
4. Play normally. When loot drops, tap **ALT** the same way you already do to read item labels — if anything on your watch list is on the ground, you'll hear the alert and see a marker over it.

Your selection and settings are saved automatically (debounced ~400ms after your last change) to `settings.json`, next to the executable.

### Advanced configuration

A couple of settings aren't exposed in the UI yet and can be tuned directly in `settings.json`:

|Field|Default|Description|
|-----|-------|-----------|
|`FuzzyMatchThreshold`|`0.80`|Minimum similarity (0.0-1.0) for a detected text token to count as a match. Lower = more forgiving of OCR misreads, but more prone to false positives.|
|`BeepDurationMs`|`200`|Length of the alert tone in milliseconds.|

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

## Contributing

Issues and pull requests are welcome. If you're adding or correcting item base data, `D2RLootRadar.Infrastructure/Data/item-bases.json` is the single source of truth — the same file drives both the watch-list UI and the OCR matching, so no other file needs to change.

## License

MIT — see [LICENSE](./LICENSE).
