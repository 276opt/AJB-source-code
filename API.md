# Judgement display API (v1)

`AccurateJudgementBar.JudgementApi` supplies symmetric bands for the straight and curved meters. **It is a display API:** it does not change native HitMargin values, hit acceptance, scoring, health, or death. A custom judgement mod should supply its own display window formula without using this API to replace native hit processing.

Reference AJB's DLL and the game's Unity DLLs, declare AJB as a load dependency, and do not bundle another copy of `AccurateJudgementBar.dll`. All API operations and callbacks must run on Unity's main thread. Register when your mod is enabled and remove or restore your changes when it is disabled.

## Register and restore definitions

```csharp
using AccurateJudgementBar;
using UnityEngine;

// Return the one-sided window from the center, in milliseconds.
JudgementApi.Register(new JudgementDefinition(
    "MyMod.UltraPerfect",
    c => c.CalculateWindow(10f, 8f, 8f),
    c => new Color(1f, 1f, 1f),
    enabled: c => true));

// When disabling your mod:
JudgementApi.Remove("MyMod.UltraPerfect");
```

IDs are case-sensitive; use a mod-qualified prefix. Built-in IDs are `red`, `orange`, `yellow`, `green`, and `blue`, also exposed as constants on `JudgementApi`. The `blue` ID is retained for compatibility even though its rendered band is white. Do not register `AccurateJudgementBar.failure`; the display layer reserves it for the failure border.

Definitions are immutable. `Register` throws for duplicate IDs; `Replace` requires an existing ID. `Remove` returns whether an entry was removed. `GetDefinitions()` returns a read-only snapshot; use `TryGet` to retrieve a definition.

When overriding an existing definition, keep the original and restore it only if your replacement is still installed:

```csharp
JudgementApi.TryGet(JudgementApi.Yellow, out var original);
var replacement = new JudgementDefinition(
    original.Id, c => c.CalculateWindow(40f, 28f), original.Color,
    original.PixelPadding, original.Enabled);
JudgementApi.Replace(replacement);

// When disabling your mod:
if (JudgementApi.TryGet(original.Id, out var current) &&
    ReferenceEquals(current, replacement))
    JudgementApi.Replace(original);
```

The example assumes the built-in definition is still present. External mods are responsible for coordinating replacement/removal conflicts and restoring their own changes.

## Context and units

`JudgementContext.Bpm` is the already-normalized judgement BPM:

```text
Bpm = conductor.bpm * planetarySystem.speed * Pitch / MarginScale / SpeedTrial
```

The meter does not clamp, round, or animate this BPM. If constructing a context yourself, pass the normalized value: the constructor stores it and does not apply `Pitch`, `MarginScale`, or `SpeedTrial` again.

The three-argument constructor `(bpm, difficulty, speedTrial = 1)` defaults Pitch and MarginScale to 1. The five-argument constructor also accepts pitch and marginScale. Both capture `AsyncInputManager.isActive` in the read-only `UseTimeBasedJudgement` property. The six-argument constructor lets you specify that boolean explicitly. BPM, speed trial, pitch, and margin scale must all be positive and finite.

`CalculateWindow(angle, baseWindowMs, minimumMs = 25)` returns the actual one-sided window in milliseconds:

```text
max(max(60000 / Bpm * angle / 180, baseWindowMs) / SpeedTrial, minimumMs)
```

Do not divide its result by MarginScale again. Built-in red/orange use 60 degrees and base windows of 91/65/40 ms for Lenient/Normal/Strict. Yellow uses 45 degrees and 30 ms; green uses 30 degrees and 20 ms. These definitions use the default 25 ms minimum.

Native XPerfect uses a separate formula for both input paths:

```text
WindowMs = max(60000 / Bpm * 12.5 / 180 / SpeedTrial, 1000 / 60)
```

Its 16.67 ms floor is an actual-time floor; it does not shrink with speed and has no 27.5 ms upper cap. Do not multiply by MarginScale or divide by Pitch again. `UseTimeBasedJudgement` remains available to callers, but does not select a different built-in XPerfect formula.

For example, raw BPM 21.25, MarginScale 0.25, Pitch 1, SpeedTrial 1, and planet speed 1 give green 117.65 ms and XPerfect 49.02 ms.

## Evaluation, widths, and errors

`Evaluate(context)` returns a read-only list of `JudgementBand` values with `Id`, `WindowMs`, `Width`, and `Color`. The native orange window is always the 300 px reference, even if a mod replaces the orange definition:

```text
Width = Clamp(Round(300 * WindowMs / nativeOrangeMs) + PixelPadding, 0, 400)
```

Red uses the same window as orange with 16 px padding, producing 316 px. Yellow has no extra padding. The curved meter uses Width / 5 as its half-angle in degrees. Pixels here are original UI design coordinates and scale with the RectTransform.

Bands are sorted widest first. Equal widths use this order: red, orange, yellow, green, blue, then custom IDs in ordinal order. Later bands cover earlier bands. Straight and curved renderers share this order.

Public `Evaluate` always returns accurate target widths, regardless of `EnableAccurateJudgementBar`. It contains neither animation nor the purple failure border. With accurate widths disabled, only the meter's displayed built-in widths become red=316, orange=300, yellow=225, green=150, blue=62.5 px, including replaced built-in IDs. Their callbacks, Enabled flags, and WindowMs still evaluate normally; custom IDs retain calculated widths.

Callbacks are evaluated every frame. Keep them lightweight and free of gameplay side effects. Definition changes made during evaluation apply on the next evaluation because the definition collection is snapshotted. Dynamic colors, windows, and Enabled flags need no separate refresh notification.

An exception, a nonpositive/nonfinite window, or a nonfinite color component skips that definition and logs an error once per ID. Replacing or removing the ID clears its error record. Empty IDs, null callbacks, and nonfinite padding are rejected during definition construction; null definitions/context and invalid registration operations throw to the caller.

## Meter presentation

Widths animate by ID using `AnimationDuration` and Ease Out Cubic. A changed target resumes from the currently displayed width; an unchanged target does not restart animation. Initial display and UI reconstruction use the current target immediately. Zero duration completes immediately. Colors, Enabled flags, additions, and removals apply immediately. Sorting uses the current animated widths and the tie order above.

All bands remain centered and symmetric. Curved slices use the same overlap rules; outlines surround only visible white sections, joining adjacent sections without bridging intervening colors.

The built-in blue definition is enabled only when both `Persistence.hitMarginPerfectTick.IsShowXPerfect()` and AJB's `ShowXPerfectJudgement` allow it. This respects the effective native competitive-mode setting; it does not read the judgement-text option.

Blue's API Color comes from the native XPerfect UI color, but the renderer uses a white band with a pastel outline and retains only that color's alpha. Replacing the blue definition also retains the selected visual style:

- **AJB style** is the default. It uses a rounded outline with Off/Thin/Normal/Thick choices through `XPerfectOutlineWidth` (-1/0/1/2; default 1).
- **Vanilla style** (`VanillaXPerfectStyle = true`) uses a white band, central white line, and fixed 2 UI px outward pastel outline with square corners. `VanillaXPerfectOutline` controls that outline.
- Switching styles preserves each style's settings. Neither style changes WindowMs, API Width, actual hits, competitive mode, or native tick/text defaults.

The purple failure border is a separate display layer, not a scoring judgement. `ShowFailureJudgement` enables an 8 px extension on each side of the widest visible band. `FailureBandDisplayMode` selects: 0 after failure (default), 1 while No Fail or eligible safe-tile protection is active, 2 either failure or that protection, 3 both, or 4 always. Safe-tile protection is read from player one's current tile only when the native margin limit is None; cooldowns and extra lives are not included. Every mode still respects the display switch and mode restrictions below. Failure state tracks the attempt's actual death or FailMiss/FailOverload display records, including native No Fail failures; it does not simply track `alive`.

The border follows its own expansion animation. Tracker reset/checkpoint reversion clears the relevant player's failure record; replacing the controller/trackers clears records as well. The border is hidden in NoMissOnly, PerfectsOnly, PurePerfectOnly, and XPerfectOnly regardless of the limit-color switch. Display ticks use native failure records; ordinary TooEarly/TooLate keep their native red. These display paths must not change hit or death processing.

Layer1 uses `MeterBackgroundGraphic` geometry rather than an image: a centered 330 x 66 rectangle in a 400 x 200 straight-meter canvas, or a curved sector with radii 117/183 and half-angle 66 degrees. `BackgroundOpacity` defaults to 0.6. At full failure expansion, the straight width is 346 and curved half-angle 69.2 degrees. Layer3 retains its image. Custom API bands can extend beyond these fixed base dimensions: the background adds at most 16 px and does not automatically fit an arbitrarily wide custom band.

## Relevant defaults and verification

Saved values in `Settings.xml` take precedence over field defaults in [Settings.cs](src/Settings/Settings.cs).

| Field | Default |
| --- | --- |
| `EnableAccurateJudgementBar` | `true` |
| `AnimationDuration` | `0.4` seconds |
| `BackgroundOpacity` | `0.6` |
| `ShowHitMarginLimit` | `true` |
| `ShowXPerfectJudgement` | `true` |
| `VanillaXPerfectStyle` | `false` |
| `VanillaXPerfectOutline` | `true` |
| `XPerfectOutlineWidth` | `1` |
| `ShowFailureJudgement` | `true` |
| `FailureBandDisplayMode` | `0` |

The runtime compatibility check is defined by [GameCompatibility.cs](src/Core/GameCompatibility.cs). ADOFAI 3.4.0 predrop is the minimum supported build; older builds with the same 3.4.0/r150 version label are rejected using display/timing API checks before patches, settings, or objects are created. Later versions must retain the required Image ErrorMeter fields and Sprite arrays. Passing that check does not verify future formula or Unity behavior.

Run `dotnet run --project tests/ApiTests.csproj -- --judgement` for judgement/animation checks, or omit the arguments to include localization and the other API regressions. Tests cover calculations, normalization, registration/replacement/removal, error isolation, ordering, animation, geometry, and UI reconstruction using game/Unity substitutes. Actual rendering, masks, tick placement, multiplayer, No Fail, retry, and Harmony execution still require in-game verification.
