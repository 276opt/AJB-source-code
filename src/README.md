# Source guide

AJB's 40 C# files are grouped by responsibility. They share the `AccurateJudgementBar` namespace; the folder structure does not change type names or public APIs.

## Core: startup and shared services

| File | Responsibility |
| --- | --- |
| [Main.cs](Core/Main.cs) | Unity Mod Manager entry point, Harmony setup, settings refresh, and enable/disable cleanup. |
| [GameCompatibility.cs](Core/GameCompatibility.cs) | Check the minimum game version, predrop Image/sprite-array fields, and the 3.4.0 timing-formula fingerprint. |
| [DisplayErrors.cs](Core/DisplayErrors.cs) | Deduplicate errors at AJB's display boundaries. It must not suppress exceptions from the game's hit, damage, or death logic. |
| [TogetherCompatibility.cs](Core/TogetherCompatibility.cs) | Detect active Together sessions without a direct DLL dependency and clear incompatible extra mode selections. This does not establish full multiplayer compatibility. |
| [NativeXPerfect.cs](Core/NativeXPerfect.cs) | Share access to native XPerfect settings, colors, and formulas across the bar, ticks, and display API. |

## Settings: preferences and translation

| File | Responsibility |
| --- | --- |
| [Settings.cs](Settings/Settings.cs) | Persist `Settings.xml` fields and draw the mod's settings pages. Preserve saved-field compatibility when editing. |
| [Localization.cs](Settings/Localization.cs) | Load JSON translations from `Languages/`, with embedded English as a fallback. |

## Bar: judgement meter rendering

| File | Responsibility |
| --- | --- |
| [StraightMeter.cs](Bar/StraightMeter.cs) | Bind to the native straight meter and render AJB's judgement bands. |
| [CurveMeter.cs](Bar/CurveMeter.cs) | Bind to the curved meter and generate its curved band geometry. |
| [MeterWatcher.cs](Bar/MeterWatcher.cs) | Build display contexts, evaluate animated bands, and choose band colors. |
| [MeterPosition.cs](Bar/MeterPosition.cs) | Apply configured meter position offsets after layout. |
| [ErrorMeterSizeSettings.cs](Bar/ErrorMeterSizeSettings.cs) | Manage the native size choices and AJB's custom scale, including menu preview and restoration. |
| [NativeMeterSettings.cs](Bar/NativeMeterSettings.cs) | Preserve saved meter size/shape during the predrop tick-setting refresh and update the display afterward. |
| [MeterBandLayout.cs](Bar/MeterBandLayout.cs) | Convert evaluated bands into slices used by the curved renderer. |
| [MeterBackgroundGraphic.cs](Bar/MeterBackgroundGraphic.cs) | Draw the straight or curved meter background. |
| [JudgementBandAnimation.cs](Bar/JudgementBandAnimation.cs) | Interpolate judgement-band widths when the displayed boundaries change. |
| [FailureBandAnimation.cs](Bar/FailureBandAnimation.cs) | Animate the outer failure-band boundary. |
| [FailureJudgement.cs](Bar/FailureJudgement.cs) | Display failure bands and associated ticks, including a tick for relevant death events. |
| [XPerfectGradient.cs](Bar/XPerfectGradient.cs) | Supply XPerfect gradient colors for meter rendering. |
| [XPerfectOutlineMesh.cs](Bar/XPerfectOutlineMesh.cs) | Build XPerfect outlines for the AJB and vanilla-style appearances. |

## Tick and Overload

| File | Responsibility |
| --- | --- |
| [BetterHit.cs](Tick/BetterHit.cs) | Apply tick sensitivity and lifetime settings, then restore native values when disabled. |
| [LegacyHitVisual.cs](Tick/LegacyHitVisual.cs) | Apply the legacy tick appearance and restore captured visuals when disabled or when display setup fails. |
| [OverloadHP.cs](Overload/OverloadHP.cs) | Create and update a configurable bar from the game's Overload counter. |

## ExtraJudgement: modes, editor options, and results

| File | Responsibility |
| --- | --- |
| [MarginLimitState.cs](ExtraJudgement/MarginLimitState.cs) | Centralize mode selection, native enum mapping, and mutually exclusive saved preferences. |
| [MarginLimitMenu.cs](ExtraJudgement/MarginLimitMenu.cs) | Extend the native mode menu with six choices and handle confirmation, cancellation, and restart counts. |
| [PerfectsOnlyDeath.cs](ExtraJudgement/PerfectsOnlyDeath.cs) | Implement the optional Perfects Only Patch for the selected native `PerfectsOnly` mode. |
| [NoMissOnly.cs](ExtraJudgement/NoMissOnly.cs) | Request death after relevant native `TooEarly`/`TooLate` damage, subject to its guards. See the unresolved allowlist caveat below. |
| [NoCheckpoint.cs](ExtraJudgement/NoCheckpoint.cs) | Block new checkpoint activation in No Checkpoint and the four Only modes; retain existing checkpoint progress and editor/practice starts. |
| [XPerfectOnly.cs](ExtraJudgement/XPerfectOnly.cs) | Apply the selected XPerfect-only acceptance/death rules when their activation conditions, including competitive mode, hold. |
| [StrictOnly.cs](ExtraJudgement/StrictOnly.cs) | Control whether all difficulty choices appear, with an option to preserve the native official-level menu. |
| [TickMarginScaleFix.cs](ExtraJudgement/TickMarginScaleFix.cs) | Use `hitFloor.nextfloor.marginScale` for tick positioning during the native meter-update call, while retaining the departure tile's speed. |
| [StartingBpmWarning.cs](ExtraJudgement/StartingBpmWarning.cs) | Track loaded levels and request confirmation before editing their initial BPM; cancellation leaves the edit unapplied. |
| [StartingBpmWarningLayout.cs](ExtraJudgement/StartingBpmWarningLayout.cs) | Measure and lay out that warning, provide scrolling for long text, and restore the native notification UI. |
| [AprilFools.cs](ExtraJudgement/AprilFools.cs) | Change only the mod's display name and settings title on April 1, then restore them. |
| [EnchantedDetailedResults.cs](ExtraJudgement/EnchantedDetailedResults.cs) | Read native per-player counts and format separate PerfectMinus, XPerfect, and PerfectPlus result text. |
| [DetailedResultsView.cs](ExtraJudgement/DetailedResultsView.cs) | Manage result-text backgrounds, colors, shadows, layout measurement, and restoration. |
| [CompletionResultsText.cs](ExtraJudgement/CompletionResultsText.cs) | Apply the shared result styling to native completion messages while retaining their text and visibility. |
| [XPerfectResultsColor.cs](ExtraJudgement/XPerfectResultsColor.cs) | Identify and style XPerfect result counts, coordinate refreshes, and restore native results. |
| [XPerfectResultsGradient.cs](ExtraJudgement/XPerfectResultsGradient.cs) | Draw the XPerfect result gradient/outline using the native text glyph mesh. |

## Api: integration with other mods

| File | Responsibility |
| --- | --- |
| [JudgementApi.cs](Api/JudgementApi.cs) | Define display contexts and bands; register, replace, remove, and evaluate custom display definitions. See [API.md](../API.md). |

## Boundaries and maintenance

The bar and its API describe judgement windows for display. They must not change actual timing windows, judgement records, scoring, or hit acceptance. Extra mode hooks have separate, narrowly defined permissions in [AGENTS.md](../AGENTS.md); their presence in this snapshot is not proof that they comply with every boundary.

In particular, **No Miss Only's additional death behavior is not included in the current gameplay allowlist**. The checkpoint restriction does cover that mode, but does not authorize its damage/death behavior. Do not treat this existing implementation as permission to expand the allowlist. Follow the audit guidance in [AGENTS.md](../AGENTS.md) before maintaining gameplay hooks, including native No Fail, cooldown, extra-life, mode-switch, and disable paths.

Display failures should be handled at AJB's own boundary, with captured visuals restored where possible. Do not catch native gameplay exceptions or manufacture successful hits to hide a display failure. Settings changes, retries, UI reconstruction, and disabling the mod all need cleanup checks.

The regression tests link selected source files against game/Unity substitutes. They are useful for logic and lifecycle checks, but do not replace in-game verification of Harmony hooks, fonts, masks, or Unity layout. Build/test commands are in the [root README](../README.md); packaging details are in [UPDATING.md](../UPDATING.md).
