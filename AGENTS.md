# Development guidelines

## Preserve real judgements

**AJB must not change the actual judgement difficulty required to achieve the same score goal. Players may select an additional failure condition from the explicit whitelist below to end an attempt that can no longer meet that goal. AJB must not fail an attempt that still qualifies, or let display features interfere with gameplay.**

- Never widen or narrow the actual time or angle windows for Perfect, XPerfect, or other judgements. Never fabricate hit grades, scores, misses, or death records. Meter bands and colors are display information only.
- Outside the explicit whitelist, do not change hit acceptance or native miss, damage, and death handling. Additional deaths, synthetic `Hit` calls, and retained protection flags must not make a rejected input count as a hit or change the difficulty of reaching a goal.
- Display exceptions, resource failures, and UI reconstruction must not interrupt hit processing or leave partially applied gameplay state. Handle failures at AJB's own display boundary. Do not swallow native exceptions, fabricate success, or overwrite gameplay state to hide a display failure. Neither the whitelist nor No Fail exempts code from this rule.

### Explicit whitelist

| Feature | Allowed scope |
| --- | --- |
| Perfects Only Patch (`PerfectsOnlyInstantDeath`) | When the player selects native `PerfectsOnly` and enables the patch, an additional death may follow a native judgement that fails that mode's condition. Inputs satisfying the condition must not be killed by AJB. |
| XPerfect Only (`XPerfectOnly`) | When selected and its activation conditions, including competitive mode, are satisfied, the mode may require native XPerfect and add failure/death for a non-XPerfect input. It must not redefine XPerfect, alter its difficulty, or fail an attempt whose inputs all satisfy the condition. |
| Checkpoint restrictions | When the player selects `No Checkpoint` or one of the four existing Only modes (No Miss Only, Perfects Only, PurePerfectOnly, XPerfect Only), AJB may block level checkpoint activation and its save/respawn effects. No Checkpoint uses native `HitMarginLimit.None` and must add no judgement, hit, damage, or death restrictions. Do not clear existing checkpoint progress or change practice/editor starting points. Respect AJB enable/disable and Together compatibility conditions. This permission does not expand any mode's damage/death whitelist. |
| Native No Fail | With native No Fail enabled, preserve the game's immunity, continued play, and `Die` protection under the same native conditions. Whether a protected path calls `Hit` depends on that game version. Preserve real judgements, misses, and death records; do not turn them into a qualifying no-miss/no-death result. |

- **Do not add whitelist entries or expand their scope without the user's explicit consent.** Existing code, similarity to another exception, and describing a change as a fix are not authorization.
- Additional completion conditions apply only while the player selects the relevant mode and its activation conditions hold. Display-only switches must not activate gameplay restrictions. Disabling or switching modes must not leave incompatible hit acceptance or statistics state.
- The whitelist permits selected additional failure conditions, not every implementation side effect. Changes to shared hooks such as `IsCounted` require checks of other modes, maximum possible XAcc, and statistics caches. Keep additional completion rules separate from native judgement grades and scoring.
- Native No Fail permission does not automatically cover cooldown or extra-life side effects caused by an additional AJB `Die` call. Distinguish protection that would already occur from AJB-introduced synthetic hits, life consumption, and re-entry. Calling a native function is not by itself proof of safety.
- `NoMissOnly.cs` is retained existing code, including an additional death path. **No Miss Only death handling is not an authorized whitelist entry.** Its presence and tests do not establish compliance; audit that path against these boundaries before treating it as safe. Its checkpoint restriction is covered only by the checkpoint entry above.

### Severity and verification

Incorrectly killing a qualifying attempt, making its goal easier or harder, fabricating results, and leaving gameplay state after a display failure are major issues.

A missed additional death is generally lower priority if native judgements, hit acceptance, damage, and result records remain intact; record and fix it nonetheless. Immunity meeting native No Fail conditions is allowed and is not a bug solely because the player survives.

For judgement or gameplay hooks, compare against the corresponding game version's source or assemblies. Check qualifying/nonqualifying inputs, No Fail, cooldowns, extra lives, mode changes, and disabling the mod. Passing tests or appearing in the whitelist does not prove the implementation is free of side effects. Identify the affected execution path when a violation is found.

## Working on the archive

- Read relevant source and `git status --short` before changing files. Preserve existing user changes.
- Keep code comments and project documentation in English. Preserve identifiers and commands.
- Limit changes to the requested work. File moves and documentation cleanup must preserve behavior, type names, and setting defaults.
- Report what changed, checks performed, and verification still needed.
- This project is archived. See [README.md](README.md) for the notice and [UPDATING.md](UPDATING.md) for historical packaging and independent fork requirements.

## Structure and compatibility

AJB is an A Dance of Fire and Ice mod using Unity Mod Manager, C#, Unity UI, and Harmony. The main assembly targets `netstandard2.1`; C# test programs target `net10.0` and require the .NET 10 SDK.

[GameCompatibility.cs](src/Core/GameCompatibility.cs) defines the runtime boundary: ADOFAI 3.4.0 predrop or a later version retaining the required Image-based ErrorMeter fields and sprite arrays. Older HOTPATCH builds with the same version number are unsupported. Verify actual game code/assemblies before changing game behavior; a larger version number alone does not prove compatibility.

| Directory | Responsibility |
| --- | --- |
| `src/Core/` | Entry point, lifecycle, compatibility, shared native XPerfect calculations |
| `src/Settings/` | Serialized settings, UMM settings UI, localization |
| `src/Bar/` | Straight/curved meters, band layout, backgrounds, animation, failure display |
| `src/Overload/` | Overload meter |
| `src/Tick/` | Hit tick appearance, sensitivity, lifetime |
| `src/ExtraJudgement/` | Mode state/menu, checkpoint restrictions, mode rules, results UI, editor warning |
| `src/Api/` | Public judgement display API |
| `tests/` | Executable tests with Unity/game substitutes and packaging tests |
| `Languages/`, `Resources/` | Bundled translations and runtime assets |
| `build/` | Local staging and ZIP packaging scripts |

See [src/README.md](src/README.md) for a file guide. Place code in its feature directory; moving files does not require changing the shared `AccurateJudgementBar` namespace.

## Contracts to preserve

- The archived package loads `AccurateJudgementBar.Main.Load(UnityModManager.ModEntry)` from `AccurateJudgementBar.dll` through `Info.json`. An independent fork must coordinate identity changes across code, metadata, packaging, dependencies, and tests; see [UPDATING.md](UPDATING.md).
- Updates are player-initiated through external UMM. Do not download or install updates at game startup. A release feed and its versioned ZIP must refer to the same package.
- `JudgementApi` is display-only. Widths/colors must not change real hits, scoring, health, or death. The extra-condition whitelist does not apply to this API. See [API.md](API.md).
- Public settings fields are serialized into `Settings.xml`. Handle compatibility before renaming, removing, or changing a field's type. Do not serialize transient UI state.
- Use `Localization` for settings text and update matching keys in every bundled language. `Languages/en.json` is also the embedded fallback.
- Harmony patches and Unity UI changes must restore state on disable, retry, and UI reconstruction. Native exceptions must still propagate after AJB display cleanup.
- Some tests directly link production source. Update their `.csproj` paths after moving source and search for stale references.
- `bin/`, `obj/`, `out/`, and `dist/` are generated. Fix sources or build scripts, not generated output. Staging recreates `out/`.
- `MarginLimitState.cs` owns shared mode values, native mappings, and mutual exclusion; `MarginLimitMenu.cs` owns the menu. Keep each mode's gameplay rules separate.
- `NoCheckpoint.cs` intercepts only `ffxCheckpoint.StartEffect(scrPlanet)`. Preserve native PurePerfectOnly checkpoint restrictions after disabling AJB, together with the whitelist boundaries above.
- April Fools behavior changes only the UMM display name and settings title to InaccurateJudgementBar on local April 1. It must not affect judgements, colors, modes, checkpoints, or Together state.
- Custom ErrorMeter sizes must not be written as custom enum values into native `Persistence.hitErrorMeterSize`. Predrop setter overrides must stay within the intended call scope and restore state after nested calls and exceptions.
- Results backgrounds use their own `TextGenerator`; never populate or modify the native text rendering cache. Fixed results colors must not read mutable `worldColourScheme`. Mesh tests use complete current uGUI glyph vertices, without an obsolete sentinel quad.
- The starting-BPM warning must intercept before native end-edit enters `SaveStateScope`. Cancel must preserve BPM, dirty state, and undo history. Restore native UI after replacement or display/measurement failure, and preserve scroll position across delayed layout updates.

## Build and validation

Run from the repository root:

```powershell
dotnet build -c Release -p:DeployToGame=false -p:AutoLaunchGame=false
dotnet run --project tests/ApiTests.csproj
dotnet run --project tests/Toggle/Tests.csproj
dotnet run --project tests/PerfectsOnlyDeath/Tests.csproj
dotnet run --project tests/ResultsGradient/Tests.csproj
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/Packaging/Tests.ps1
```

The build derives the game's Managed directory from `GameExePath`, defaulting to the Windows Steam installation. Pass `-p:GameExePath="C:\path\to\A Dance of Fire and Ice.exe"` for another installation. API tests also accept `GameExePath` or `ManagedAssembliesDir` for their Newtonsoft.Json reference. Game and UMM assemblies must be supplied by the local installation.

Release builds compile the DLL, recreate `out/`, and write ZIPs, a SHA-256 file, and `Repository.json` to `dist/`. These commands do not deploy, launch, or publish. Deploy only when requested, with `-p:DeployToGame=true`; keep `-p:AutoLaunchGame=false` when only deploying.

Run affected tests after source/reference changes. Cross-directory reorganization requires a complete build and all five suites above. Documentation-only edits need content/link checks. Packaging tests require a compiled main DLL. Report missing dependencies and uncompleted checks explicitly.

| Suite | Main regression coverage |
| --- | --- |
| API | Band calculations/layout/animation, native XPerfect, compatibility, localization |
| Toggle | Lifecycle cleanup, failure state, meter size/position, native settings scopes, display fault isolation, Together lifecycle, starting-BPM warning and layout |
| PerfectsOnlyDeath | Death rules, fixed six-mode menu, meter colors, mode changes, checkpoint save/respawn entry points, No Fail, Together compatibility, April Fools isolation |
| ResultsGradient | Glyph UVs, gradients, detailed counts, text/background options, visibility/sibling order, completion messages, cache isolation, retry/UI replacement, fault recovery |
| Packaging | Feed/ZIP consistency, install layout, overwrite migration preserving settings |

Automated tests use substitutes. They do not replace in-game Unity/Harmony verification or a real external UMM update. For relevant changes, verify actual fonts, outlines, masks, layout, scrolling, straight/curved display, multiplayer, No Fail, retry, disable, and UI recreation. Test qualifying/nonqualifying gameplay paths separately from display correctness.

Keep this file limited to reusable rules, paths, and commands. Update it when architecture or verification procedures change; leave task-specific progress in the task report.
