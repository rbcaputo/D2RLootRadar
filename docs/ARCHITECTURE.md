# Technical Architecture & Design Log (v2.2.2)

This document exists for the same reason a codebase's in-line comments aren't enough on their own: comments explain what a piece of code does and, at best, why ir does it *that way* — they don't have room to record what was tried before, why an earlier approach fell short, or the reasoning trail that led from one design to the next. That trail is exactly what gets lost first when nobody wrote it down, and it's the thing a future contributor (including a future version of the person reading this) most needs when touching a piece of logic that already looks "finished".

**Relationship to other docs:** [`README.md`](../README.md) is user-facing — what the app does, how to run it, what its current limitations are. This document is contributor-facing — how it's built, and why it's built that way. If you're deciding whether to change how something works, this is the file to read first and the file to update afterward.

---

## Architecture at a glance

The solution is a strict four-layer dependency chain, inside-out:

```text
D2RLootRadar.Domain → Plain records/enums, zero dependencies
       ↑
D2RLootRadar.Application → Orchestration, contracts (interfaces), no concrete implementations
       ↑
D2RLootRadar.Infrastructure → Concrete implementations: OCR, screen capture, fuzzy matching, JSON I/O, Win32
       ↑
D2RLootRadar.Desktop → WPF UI (MVVM via CommunityToolkit.Mvvm), composition root (DI via Microsoft.Extensions.Hosting)
```

Dependencies only ever point inward. `Domain` and `Application` don't reference `System.Drawing`, `System.Windows`, or anything Windows-specific at all — every platform-specific concern (GDI+ capture, the low-level keyboard hook, WPF windows) lives in `Infrastructure` or `Desktop`, behind an interface declared in `Application`. This is why `D2RLootRdadar.Tests` can exercise fuzzy matching, rarity scoring, and settings clamping on Linux CI runners with no Windows Desktop workload installed at all — none of that logic touches a Windows API.

The practical test for "does this belong in `Application` or `Infrastructure`": would this logic still make sense if the OCR engine, the capture method, or the UI framework were swapped out entirely? If yes, `Application`. If the answer depends on *how* screen pixels get onto the CPU or *how* a window handle gets found, `Infrastructure`.

---

## The detection pipeline, in depth

```text
ALT pressed
  │
GameCaptureService: PrintWindow / DWM thumbnail → raw BGRA frame of the D2R window
  │
OcrService.RunOcr:
  │   1. Crop the bottom HUD strip (BottomCropFraction) - never contains item labels, wastes OCR time
  │   2. Upscale (Tesseract's LSTM model is tuned for a certain nominal text size)
  │   3. AdaptiveTextMask: local-adaptive luminance threshold → binary black/white mask
  │   4. Tesseract reads the mask, not the color frame - flat binary input is far more reliable
  │      for LSTM text recognition than antialiased, variably-lit color text
  │   5. For each detected word/line: SampleRarity reads the *original color frame*, restricted to
  │      only the pixels the mask flagged as text → (LabelRarity, confidence) pair
  │
LootMonitoringService.RunDetectionLoopAsync:
  │   For each detection x each watch-list item:
  │     combinedScore = TextWeight × fuzzy(detection.text, item.name) + ColorWeight x RarityScore(detection, item.SelectedRarities)
  │   Keep the highest-scoring item that clears FuzzyMatchThreshold
  │
  └─ match found → beep + on-screen marker over the item; no match → nothing happens
```

Two things worth calling out because they're easy to miss just from reading the code:

- **The mask and the color sample come from the same frame, at the same moment**, just read two different ways — one binarized for Tesseract, one left in color and filtered by that same binarization. There's no risk of the two disagreeing about *which pixels* are text, because they're not independently detected; the color sample is defined in terms of the mask.
- **Rarity classification never sees the whole label box** — only the pixels the mask cells foreground. Sampling the whole box would pull in the translucent background panel, which is not obviously separable from certain text colors by color alone.

---

## Design decisions

Entries are in roughly chronological order. Each one records the "problem", not just the *change* — the goal is that a future entry can be added the same way, and a reader can trace the reasoning without having to reconstruct it from a diff.

### DD-1 — Layered architecture with `Application`-owned contracts (project inception)

**Context.** A WPF desktop app that does OCR, screen capture, and Win32 interop is easy to write as one big project where the ViewModel directly instantiates `TesseractEngine` and `Bitmap`. That's fast to write once and expensive to test or extend afterward — anything touching OCR or capture becomes untestable without a real D2R window on screen.

**Decision.** `Domain` and `Application` define *what* the app does (contracts, plain data) with no knowledge of *how* — `IOcrService`, `IGameCaptureService`, `ISettingsStore`, etc. are interfaces owned by `Applications`; their real implementations live in `Infrastructure` and are wired up via DI (`Microsoft.Extensions.Hosting`) at the `Desktop` composition root.

**Consequence.** `D2RLootRadar.Tests` runs on any platform, no D2R instance or Windows box required, and covers exactly the parts of the pipeline where correctness actually matters most to get right by hand (fuzzy matching, rarity scoring, settings clamping) — see [Running tests](../README.md#running-tests) in the README.

### DD-2 — Settings persistence: load-fresh-then-save, not a cached in-memory snapshot (2026-07-03, `a3fd66c`)

**Context.** `MainViewModel` held a `UserSettings _settings` field, loaded once and mutated/saved from that same field on every change. In practice, saves from the main window could silently discard changes made elsewhere (e.g. the Settings window, saved through a separate path) — each side's cached snapshot only knew about its own edits, and whichever side saved last won, overwriting the other's changes on disk even though neither side did anything wrong in isolation.

**Decision.** Removed the cached `_settings` field entirely. Every save now does `_settingsStore.Load()` immediately before merging in that view's own change and writing back — so a save can never clobber a change made through a different path, since it's always built on top of the current on-disk state, not a stale in-memory copy of it.

**Related, same commit — the "zombie process" fix.** WPF's default shutdown-mode auto-detection (`ShutdownMode.OnMainWindowClose`) infers "the main window" from whichever window's native handle was created first — which could end up being `OverlayWindow`, not `MainWindow`, since the overlay's handle can be created earlier in startup. That left the process running invisibly after the user closed the main window. Fixed by making shutdown explicit: `MainWindow.OnClosed` calls `Application.Current.Shutdown()` directly rather than relying on that auto-detection at all.

**Lesson generalized.** Any piece of mutable state that can be read-modified-written from more than one place should either be owned by exactly one place, or always be re-read immediately before every write. A cached snapshot is a promised that nothing else changed the underlying state in the meantime — true right up until it isn't.

### DD-3 — Rarity classified by sampled label color, not a separate detection mode (2026-07-14, `0014fad`)

**Context.** Item quality (Normal, Magic, Rare, Set, Unique, ...) needed to factor into matching, since a user watching "Ring" for Uniques only shouldn't get alerted for eevry magic Magic ring on the ground.

**Decision.** Rather than a separate user-facing "detection mode" setting layered on top of matching (`DetectionMode.cs` was removed in this commit), rarity became an intrinsic property of each detection — sampled directly from the label's rendered color at OCR time, alongside the text. This meant rarity selection could become *per-item, per-rarity* (see `RarityFlags`, a `[Flags]` enum) instead of one global mode — "watch this Ring for rare and Unique both" is a natural selection now, not a UI/data awkwardness.

**Consequence.** This tied item matching to color sampling accuracy from day one — which made the sampling-quality work later in this log (DD-5 onward) a correctness issue for the *matching logic*, not just a cosmetic one for a rarity-dot UI indicator.

### DD-4 — Rarity color sampling: per-pixel vote instead of a single RGB average

**Context.** Field testing surfaced misclassifications in both directions: white read as gray and vice versa, gray read as blue and vice versa. The original `SampleRarity` averaged raw RGB across every foreground (mask-flagged) pixel in a label's box, then classified that one averaged color.

**Root cause.** Anti-aliased edge pixels blend the glyph color toward the panel background. Averaged into one RGB value alongside the many unambiguous "core" glyph pixels, a *minority* of those blended pixels can still drag the *average* hue/saturation/value across a classification boundary — explaining both reported symptoms as the same failure mode tripping two different thresholds (white/gray → Value boundary, gray/blue → Saturation boundary).

**Decision.** Classify every foreground pixel individually with the same `LabelRarityClassifier`, then take a majority vote, instead of averaging color first and classifying once. A minority of contaminated edge pixels gets outvoted rather than skewing the single sample the whole label depended on.

**What it does and doesn't fix.** It fixes contamination from a *minority* of pixels — the anti-aliasing case. It does **not** fix a *systemic* shift affecting the whole label at once (e.g. the user's in-game gamma-brightness slider, monitor calibration, ENB/ReShade color grading — see the [Known limitations](../README.md#known-limitations) in the README) — voting can't recover a case where the majority of pixels are shifted together, not just a minority of outliers. That's a real remaining gap, not something later work in this log has closed — see [Open questions](#open-questions--deliberately-not-done-yet).

### DD-5 — Fuzzy matching: best match among candidates, not first match above threshold

**Context.** El Rune and Eld Rune were consfused with each other when both were on the same watch list.

**Root cause.** This was never an OCR or color problem — it was the matching loop. It iterated `watchList.Items` and took the *first* item whose name cleared `FuzzyMatchThreshold`, then stopped. But `"el rune"` scores 1.0 against `"El Rune"` *and* ~0.875 against `"Eld Rune"` (edit distance 1 over 8 characters) — both comfortably clear the default 0.80 threshold. Whichever rune happened to be listes first in the watch list always won the match, regardless of which one the OCR text actually said.

**Decision.** Score every eligible watch-list item and keep the highest-scoring one that clears the threshold, instead of stopping at the first. `"el rune"` now correctly resolves to `El Rune` (1.0 beats 0.875) rather than whichever was listed first.

**Generalized risk.** Any two catalog entries within a short edit distance of each other are a latent instance of this same bug if they're ever watched simultaneously — it's not unique to El/Eld. Worth keeping in mind when adding new item-base entries with very similar names.

### DD-6 — Matching: blended text + color score, not two independent hard gates

**Context.** Rarity was a hard boolean gate (`IsRarityMatch`) ANDed with the text-fuzzy-match gate — both had to independently pass. This meant a merely-ambiguous color read (a near-50/50 pixel vote, see DD-4) could silently drop an otherwise strong, correct text match — converting a genuine, correctly-identified item into a missed alert, since rarity selection is a real filter (e.g. "Ring", "Unique only") and a dropped detection produces no marker and no alert at all.

**Decision.** `IsRarityMatch` became `RarityScore` (0.0-1.0, see its doc comment for the exact rules) and combines with the text-fuzzy score as a single weighted value:

```text
combinedScore = 0.7 x textSimilarity + 0.3 x RarityScore(detection, item.SelectedRarities)
```

checked against the same `FuzzyMatchThreshold` the text-only check used before. Two invariants from the old boolean gate were deliberately preserved rather than loosened:

- **`LabelRarity.Unknown` (no usable color sample at all) still hard-fails** (`RarityScore` returns 0) — no evidence either way must never be treated as *positive* evidence, no matter how good the text match is.
- **A confidently-wrong color still effectively blocks the match** — even a perfect text score can't clear the default 0.80 combined threshold against a near-1.0-confidence vote for the wrong tier (0.7 x 1.0 + 0.3 x ~0.0 = 0.7 < 0.80).

What actually changed is the *middle-ground*: a knife's-edge vote (confidence near 0.5) now scores near 0.5 either way, rather than collapsing to the same flat pass/fail a landslide vote would — so it can be outweighed by a strong text match instead of vetoing it outright.

**Why 0.7/0.3 and not some other split.** Reasoned from first principles, not measured against real capture data: text is the *only* signal that identifies which item a label is at all; color only narrows down which variant of an already-identified item it is. The split was chosen so that a perfect color read can't rescue a text score much below ~0.71 similarity at the default threshold, keeping text dominant. This weighting has **not** been validated against real detection logs — see [Open questions](#open-questions--deliberately-not-done-yet).

### DD-7 — Catalog search: explicit `IsVisible`/`ApplySearch`, not `CollectionView` filtering

**Context.** The main window's ~600-item catalog needed a search box to filter down to matching items.

**Decision.** Rather than introducing `CollectionViewSource`/`ICollectionView` filtering (the "default" WPF answer), search state is an explicit `IsVisible` flag per `ItemBaseViewModel` and per `CategoryViewModel`, updated directly by `ApplySearch()` calls fanned out from `MainViewModel.OnSearchTextChanged`. Matching is plain case-insensitive substring containment against the item's name *and* its Set/Unique variant names (so seraching "Halequin Crest" finds the `Shako` it drops on, not just the literal base names) — deliberately not the app's `IFuzzyMatcher`, which is tuned for scoring complete, noisy OCR tokens, not for narrowing results on every keystroke of a deliberately partial query.

**Why not `ICollectionView`.** Two considerations: it doesn't compose cleanly across the two-level Category → Items hierarchy (a category needs to hide itself when *all* its items are filtered out, which means the category-level view and the item-level view have to stay in sync some other way regardless); and every other piece of view-state in this codebase (`IsExpanded`, selection counts, the summary panel) is already a hand-updated observable property, not a `CollectionView`. Matching that existing pattern keeps onde state-management idiom in the ViewModel layer instead of two.

**A detail worth remembering if this breaks:** a category remembers its `IsExpanded` value from just before a search starts (`_expandedBeforeSearch`) and restores it when the search is cleared, so the user's own manual expand/collapse choices survive a search round-trip instead of every category coming back auto-expanded (or auto-collapsed) once filtering stops.

**Superseded/extended by DD-8.** `ApplySearch` was renamed `ApplyFilters` and now takes a `CatalogFilter` covering Tier, Category, and Set/Unique variant constraints alongside search text — the `IsVisible`/explicit-flag mechanism and the `_expandedBeforeSearch` round-trip described above carried forward unchanged; only the number of constraints being checked grew.

### DD-8 — Filtering generalized via a single `CatalogFilter`, not one method per dimension (2026-07-22)

**Context.** Search text alone (DD-7) wasn't enough to narrow ~600 items in practice — user's wanted to filter by item Tier (Normal/Exeptional/Elite), by Category, and to isolate bases with a Set ot Unique variant at all, on top of the existing search.

**Decision.** Rather than adding a parallel method per new filter dimension (`ApplySearch` `ApplyTierFilter`, `ApplyCategoryFilter`, ...) all four constraints — search text, Tiers, Categories, and the two variant toggles — collapse into one `CatalogFilter` record, and `CategoryViewModel.ApplySearch` became `ApplyFilters(CatalogFilter filter)`, evaluating all of them per item in a single pass. `MainViewModel.OnFiltersChanged` is the one funnel every filter control (search box, Tier checkboxes, Category checkboxes, either variant toggle) routes through before fanning `ApplyFilters` out to every category.

**Three smaller decisions worth recording alongside the main one:**
- **Tier and Category share one `FilterOptionViewModel` shape** (a name plus `IsSelected`) instead of Tier getting hand-written named bool properties (`IsNormalTierSelected`, etc.) — see its own remarks. The tradeoff is a small amount of indirection (`MainViewModel` has to listen for `PropertyChanged` on each option and keep `_selectedTiers`/`_selectedProperties` in sync itself, rather than binding straight to dedicated properties) for one shared `ItemsControl` binding pattern in the popup instead of two.
- **Category is checked once per category, not once per item**, in `ApplyFilters` — every item in a `CategoryViewModel` already shares the same `Name` by construction (`MainViewModel.Load`'s grouping), so re-testing it per item would be ~40x redundant work for no behavioral difference.
- **`ActiveFilters` (the removable-tag row) is rebuilt from scratch on every filter change** rather than incrementally diffed against its previous contents — see `ActiveFilterTag`'s remarks. At most a few dozen short-lived records, so the simpler rebuild-outright approach was judged cheap enough to not be worth the bookkeeping a diff would add.

**Consequence.** Adding a fifth filter dimension in the future means adding one field to `CatalogFilter`, one clause to the `matches` expression in `ApplyFilters`, and one contribution each to `HasActiveFilters`/`ActiveFilterCount`/`RefreshActiveFilters` — not a new parallel method or a new per-category pass over `Items`.

### DD-9 — Warm up virtualized item containers at startup, behind a loading veil (2026-07-23)

**Context.** The first search, filter-popup checkbox, or manual category expand of a session caused a noticeable stutter that eevry later one didn't.

**Root cause.** Each category's row `ItemsControl` (the DataTemplate for `CategoryViewModel` in MainWindow.xaml) uses `VirtualizingPanel.IsVirtualizing="True"` gated by `Visibility="{Binding IsExapanded}"`, and categories start collapses — so their `VirtualizingStackPanel` had never been measured, and none of its row containers (each carrying a Popup-based rarity picker and info tooltip) existed yet. `CategoryViewModel.ApplyFilters` (DD-7/DD-8) auto-expands every matching category at once as soon as any filter is active, so whichever filter interaction happened first was also the first thing to force *every* matching category's containers to realize simultaneously, in one synchronous layout pass on the UI thread.

**Decision.** `MainWindow` runs a `WarmUpItemContainerAsync` pass from its `Loaded` event, once, before the user can interact with anything: for each category, expand it, force a real layout pass with `UpdateLayout()` (which is what actually realizes the containers), collapse it back, the `await Dispatcher.Yield(DispatcherPriority.Background)` before the next one — so the pass pays the same total cost up front instead of on whichever interaction happens to touch each category first, and doesn't freeze the window for one long stretch doing it. `MainViewModel.IsWarmingUp` (true until `CompleteWarmup()` is called at the end of the pass) drives a "Loading watch list..." veil over the category list in the meantime, so the cost is visible and explained rather than silent.

**Why a warm-up pass instead of changing the virtualization or auto-expand behavior itself.** Both of those exist for good reason already documented elsewhere — virtualization avoids building all ~600 row's visual trees (Popup, rarity checkboxes, tooltip) up front regardless of whether the user ever expands a given category, and auto-expand-on-filter is the whole point of filtering (a match the user can't see because its category satyed collapsed isn't useful). Neither behavior was the actual problem; *when* the one-time realization cost got paid was. A warm-up pass changes that timing without touching either mechanism.

**Consequence.** The window takes slightly longer to become interactive at lauch (well under a second for ~600 items across the current category count) in exchange for every filter/expand interaction afterward — including the very first one — being uniformly fast. If the catalog grows enough that the warm-up pass itself becomes noticeable, the per-category `Dispatcher.Yield` already spreads it across frames rather than one block, so the next lever would be shortening that further (e.g. yielding mid-category on very large ones) rather than restructuring the approach.

### DD-10 — Filter matching extracted to `Application/Catalog`, so it's unit-testable without a Desktop reference (2026-07-23)

**Context.** DD-8's filtering logic (`MatchesSearch`/`MatchesTier`/`MatchesVariants`) lived on `ItemBaseViewModel`, and `CatalogFilter` lived alongside it in `Desktop/ViewModels` — both reasonable at the time, but it meant the one place with real, easy-to-get-subtly-wrong boolean composition (empty-selection-means-everything, OR-within-a-group for Tier vs. the same OR-within-a-group for variants having a genuinely different practical effect, search checking three fields not one) had no test coverage, and couldn't get any without `D2RLootRadar.Tests` taking reference to `D2RLootRadar.Desktop` — a `net10.0-windows`/`UseWPF` project. `Tests` deliberately has never referenced `Desktop`, for the same reason DD-1 keeps `Domain`/`Application` free of Windows-specific dependencies in the first place.

**Decision.** `CatalogFilter` and the three matching methods moved to a new `D2RLootRadar.Application/Catalog/` folder — `CatalogFilter` unchanged, and the matching methods became a static `CatalogFilterMatcher` operating directly on `Domain`'s `ItemBase` record (the same data the catalog is already built from) instead of on the view model wrapper. `ItemBaseViewModel.MatchesSearch/MatchesTier/MatchesVariants` are now one-line callers into `CatalogFilterMatcher`, supplying the item's own fields — `CategoryViewModel.ApplyFilters` didn't need to change at all, since it was already calling those three methods by name rather than reimplementing the logic itself.

**Why `Application` and not `Domain`.** `CatalogFilterMatcher` is pure and stateless, so either would work mechanically, but it's UI-driven policy (what the *main window's* search box and filter popup mean), not a fact about the game data itself the way `ItemBase` or `RarityFlags` are — the same distinction the already keeps `LootMonitoringService` in `Application` rather than `Domain`.

**Consequence.** `D2RLootRadar.Tests/Catalog/CatalogFilterMatcherTests.cs` now exercises every rule referenced above directly, the same way `RarityScoreTests`/`UserSettingsTests` already cover their own layer's pure logic — and any future filter dimension gets the same test coverage for free by construction, since there's no view-model-only code path left for filtering logic to hide in.

### DD-11 — Fixed a confidence-dillution bug in `SampleRarity`, and extracted the vote math to `Application/Ocr` (2026-07-24)

**Context.** Reported symptom: label color seemed to have no effect on matching at all — a white-labeled item would still trigger a match when only Unique/Set was selected for that base, and a Superior-prefixed detection behaved the same way regardless of what was actually selected.

**Root cause.** `OcrService.SampleRarity` tallies a vote per foreground pixel (`LabelRarityClassifier.Classify`) and computed `confidence = bestVotes / count`, where `count` was *every* sampled foregrounf pixel — including ones that classified as `LabelRarity.Unknown` (anti-aliased edhe pixels, upscaling artifacts; this method's own remarks already described that noise as routine, just hadn't accounted for what it does to the denominator). An `Unknown` pixel didn't vote against the winning tier, it didn't vote at all, but dividing by every samples pixel treated it as if it had — chronically deflating `RarityConfidence` on real captures. Fed into DD-6's blended score (`TextWeight = 0.7`/`ColorWeight = 0.3`, default `FuzzyMatchThreshold = 0.80`), a deflated confidence meant a wrong color needed roughly ≥ 67% *confident-wrong* votes before it could drag a strong text match below threshold — a bar real noisy capture rarely cleared, so in practice color barely gated anything.

**Decision.** Confidence is now `bestVotes / (count - unknownVotes)` — the winning tier's share of pixels that voted for *some* color, excluding `Unknown` from the denominator entirely. The vote-tallying and confidence math were also pulled out of `SampleRarity` into a new static `D2RLootRadar.Application/Ocr/RarityVoteTally.Resolve(ReadOnlySpan<int> votes)`, with `RarityVoteTally.TierCount` as the single source of truth for the vote array's size (removing a constant that used to be duplicated as `OcrService.RarityTierCount`).

**Why extracted, given `D2RLootRadar.Tests` already references `Infrastructure` (unlike the Desktop boundary DD-10 exists for).** `Infrastructure` targets plain `net10.0`, not `net10.0-windows`, so there's no hard TFM/workload wall here the way there is for `Desktop`. The real obstacle was narrower: `SampleRarity` is `private` and its vote math is entangled with iterating a real `System.Drawing.Bitmap`'s pixel data — exercising the exact scenario that caused this bug (a majority color vote diluted by a chunk of `Unknown` votes) would otherwise mean constructing real bitmaps and masks just to test a formula over an `int` array. Pulling the math out into something that takes a plain vote tally sidesteps that entirely.

**Consequence.** `D2RLootRadar.Tests/Ocr/RarityVoteTallyTests.cs` covers the exact regression (a landslide real-color vote diluted by a large minority of `Unknown` votes must still score full confidence), plus the surrounding edge cases (all-zero, all-`Unknown`, minority-wrong-tier-plues-`Unknown`, a genuine close split between two real tiers, and the deterministic tie-break). The 0.7/0.3 weighting itself (DD-6) wasn't touched — the working theory is that correcting the dilution bug alone restores enough real-world confidence separation for DD-6's existing math to gate correctly, but that's a claim only real gameplay testing can confirm; see the open question on that weighting below if it turns out no to be enough on its own.

### DD-12 — Catalog-wide Unique/Set bulk-select is a separate popup, and touches one bit at a time (2026-07-24)

**Context.** Selecting Unique (or Set) for every eligible base one row at a time was the only way to build a "watch everything Unique" list, across ~150+ Unique capable bases spread over a dozen-plus categories.

**Decision.** `MainViewModel.AllUniqueSelected`/`AllSetSelected` are tri-state properties (`false`/`true`/`null`, same three-value shape as `CategoryViewModel.AllSelected`) scoped to the whole catalog rather than one category, and to a single rarity bit rather than every applicable one. Setting either one calls a new `CategoryViewModel.SetAllUnique`/`SetAllSet` on every category, which only touches items where `ItemBaseViewModel.ShowUnique`/`ShowSet` is true, and only flips that one bit (`IsUniqueSelected`/`IsSetSelected`) — an item that already has, say, Magic selected keeps it; bulk-select adds to a selection, it doesn't replace one. That's the same stacking rule every other selection control in the app already follows (DD before this one didn't need to state it explicitly because nothing else touched more than one item's full selection at once until now).

**Why a separate "Select" popup instead of folding this into the existing Filters popup.** The filters popup already has "Has Unique"/"Has Set" checkboxes sitting right there — reusing that same popup for "select every Unique" would put a control that changes the watch list directly next to two that only narrow what's currently *shown*, with near-identical labels. That's a much easier mix-up than the cost of one more `ToggleButton`+`Popup` pair (same pattern as Filters, just scoped to selection instead of visibility).

**Why this ignores the current search/filter state.** Like `CategoryViewModel.AllSelected` before it, `AllUniqueSelected`/`AllSetSelected` reach every item in the catalog, not just what's currently visible under an active search or filter. The alternative — scoping the bulk toggle to only the currently-filtered-subset — would make the tri-state's own meaning depend on transient UI state that has nothing to do with the watch list itself; clearing a search could then make an already-"true" tri-state suddenly read as "mixed" purely because more items became visible, with no change to any actual selection.

**Notification plumbing, without adding a new signal.** `CategoryViewModel.SelectedCount`'s `PropertyChanged` notification already fires on every single rarity toggle in that category (see DD-8's remarks — it's not value-diffed, so a toggle that doesn't change the numeric count still raises it), and `MainViewModel.OnCategoryPropertyChanged` already listens for exactly that. Re-raising `AllUniqueSelected`/`AllSetSelected` from inside that same handler means the two new tri-states stay correct after *any* rarity change anywhere — a single item's popup toggle, a category's own "select all", or the new catalog-wide toggles — without a second notification path to keep in sync with the first.

**Batching.** `CategoryViewModel.SetAllUnique`/`SetAllSet` share a private `SetEligibleItems` helper that unsubscribes/reapplies/notifies-once per category, the same shape `AllSelected`'s setter already used — so toggling "all Unique" across ~150 item fires one `PropertyChanged` per category touched (a dozen-plus), not one per item.

**Consequence.** No new test coverage was added for this one — `ComputeBulkTriState`'s logic (count eligible, count selected, three-way compara) is a smaller, three-way version of the same shape `CategoryViewModel.AllSelected`'s getter already has un-tested, and both remain untestable in isolation for the same structural reason DD-10 existed to fix (they live on Desktop-layer view models, which `D2RLootRadar.Tests` deliberately doesn't reference). Unlike DD-8/DD-10's filter-matching logic, there's no genuinely tricky boolean composition here to guard against regressing — it's a threshold count, not multiple independently-toggleable constraints being AND'd/OR'd together — so it wasn't judged worth its own `Application`-layer extraction the way `CatalogFilterMatcher`/`RarityVoteTally` were. If a fourth or fifth "select all X" toggle gets added later and this three-way tri-state math ends up duplicated a third or fourth time, that's the point worth revisiting it.

---

## Open questions / deliberately not done yet

Recorded here rather than as a TODO comment, since these are judgement calls about *whether* to do something, not just *how* — the "why not yet" is the part worth keeping.

- **The 0.7/0.3 text/color weighting (DD-6) is a first-principles guess, not a calibrated value.** `OcrService.SampleRarity` already logs `[RarityDebug] ... votes=[...] -> tier (confidence=X.XX)` in DEBUG builds — the intended next step, if misclassifications are still frequent enough to matter after DD-4 and DD-6, is to gather that data from real sessions and revisit the weighting (or the underlying color thresholds) against real vote-margin distribution instead of more guessing.
- **No per-user color calibration.** DD-4 explicitly does not correct for a systemic shift across an entire label (gamma/brightness slider, monitor calibration, ENB/ReShade). A possible future direction: sample the translucent label panel's own background color as a per-label reference point (it's the same overlay everywhere in an unmodified client), rather than only ever reading it as an adaptive threshold input the way `AdaptiveMask` currently does. Not implemented — flagged as the next real lever if the debug vote data shows this is actually the dominant remaining error source, rather than built speculatively ahead of that evidence.
- **Mixed-DPI multi-monitor overlay placement.** DPI scale is captured once, from whichever monitor initializes it first — not per monitor. Documented as a known limitation in the README rather than fixed, since it needs real multi-monitor testing to get right rather than a guess.
