# Accurate Judgement Bar — Archival Notice

In light of [7th Beat Games’ IP Usage Policy](https://7thbeat.notion.site/ip-policy), particularly item 6 under “Terms,” and other considerations, I have decided to stop maintaining Accurate Judgement Bar and archive this repository.

The repository and source code will remain publicly available under the project’s existing license. I will no longer provide updates, bug fixes, or new releases.

## Forks and continued development

You are welcome to fork, modify, and continue developing the project under its license. No additional permission from me is required.

Please credit opt276 as the original author in your README and retain all required copyright and license notices.

If you publish a modified version, please use a distinct name, such as AccurateJudgementBar-yourname, and clearly identify it as an independently maintained fork. Please do not present your fork as being maintained or endorsed by me.

## About the project

Accurate Judgement Bar (AJB) is a Unity Mod Manager mod for **A Dance of Fire and Ice**. It provides configurable straight and curved judgement bars, hit tick styling, an Overload bar, additional judgement options, and result display improvements.

This archive contains the main **2.5.0** source snapshot, resources, translations, build scripts, and regression tests. Historical compatibility branches, development drafts, generated builds, and game assemblies are omitted. The existing [MIT license](LICENSE) is retained.

## Finding your way around the source

The code lives in `src/` and shares the `AccurateJudgementBar` namespace. Start with these files:

| Area | Main files | Purpose |
| --- | --- | --- |
| Startup | [Main.cs](src/Core/Main.cs), [GameCompatibility.cs](src/Core/GameCompatibility.cs) | Load the mod, manage enable/disable, and check the game build. |
| Settings | [Settings.cs](src/Settings/Settings.cs), [Localization.cs](src/Settings/Localization.cs) | Save preferences, draw the settings UI, and load translations. |
| Judgement bars | [StraightMeter.cs](src/Bar/StraightMeter.cs), [CurveMeter.cs](src/Bar/CurveMeter.cs), [MeterWatcher.cs](src/Bar/MeterWatcher.cs) | Draw the bars and update their displayed judgement bands. |
| Hit ticks and Overload | [BetterHit.cs](src/Tick/BetterHit.cs), [LegacyHitVisual.cs](src/Tick/LegacyHitVisual.cs), [OverloadHP.cs](src/Overload/OverloadHP.cs) | Customize tick appearance and display the Overload meter. |
| Extra options | [MarginLimitState.cs](src/ExtraJudgement/MarginLimitState.cs), [MarginLimitMenu.cs](src/ExtraJudgement/MarginLimitMenu.cs), [StartingBpmWarning.cs](src/ExtraJudgement/StartingBpmWarning.cs) | Manage judgement modes and the editor's initial BPM warning. |
| Results | [EnchantedDetailedResults.cs](src/ExtraJudgement/EnchantedDetailedResults.cs), [DetailedResultsView.cs](src/ExtraJudgement/DetailedResultsView.cs) | Format result counts and their backgrounds, colors, and layout. |
| Display API | [JudgementApi.cs](src/Api/JudgementApi.cs) | Let other mods register judgement bands for display. |

See the [complete source guide](src/README.md) for all 40 C# files. `Languages/` contains the English and Simplified Chinese translations; `Resources/` contains runtime artwork. `tests/` contains console test projects and packaging checks, and `build/` contains staging and packaging scripts.

Before changing gameplay hooks, read [AGENTS.md](AGENTS.md), including its judgement protection rules and the unresolved **No Miss Only** allowlist caveat. This archival cleanup is not a gameplay audit or a claim that every existing patch is safe.

## Building locally

You need:

- Windows with PowerShell and the **.NET 10 SDK**. The mod targets `netstandard2.1`; the test programs target `net10.0`.
- **ADOFAI 3.4.0 predrop**, or a newer build that retains the required Image-based ErrorMeter and sprite arrays. Older 3.4.0 HOTPATCH builds are not supported; the version number alone is insufficient.
- **Unity Mod Manager 0.27.0 or later**, installed for that game. Game, Unity, and Harmony assemblies are read from your local installation and are not included here.

Run this from the repository root, using your game executable's path:

```powershell
dotnet build -c Release -p:DeployToGame=false "-p:GameExePath=C:\Games\ADOFAI\A Dance of Fire and Ice.exe"
```

Omit `GameExePath` if the game uses the default Steam location in `AccurateJudgementBar.csproj`. The build produces `out/` and release packages in `dist/`; `DeployToGame=false` keeps it local. See [UPDATING.md](UPDATING.md) for package layout and manual installation/update details.

## Running the checks

Build the mod first, then run these five checks from the repository root:

```powershell
dotnet run --project tests/ApiTests.csproj
dotnet run --project tests/Toggle/Tests.csproj
dotnet run --project tests/PerfectsOnlyDeath/Tests.csproj
dotnet run --project tests/ResultsGradient/Tests.csproj
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/Packaging/Tests.ps1
```

For a custom game location, append the same `"-p:GameExePath=..."` argument to the API test command. These tests cover display calculations, settings and lifecycle behavior, mode rules, results styling, and package structure. They use game/Unity substitutes and do **not** prove real Unity rendering or Harmony behavior; those still require in-game verification.

## Further documentation

- [Source guide](src/README.md): a short description of each C# file.
- [API.md](API.md): the public display API and its limits.
- [UPDATING.md](UPDATING.md): packaging, installation migration, and the existing UMM update mechanism.
- [AGENTS.md](AGENTS.md): development rules, gameplay boundaries, and validation guidance.
- [LICENSE](LICENSE): the project's license and copyright notice.
