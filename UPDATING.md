# Packaging and updates

Accurate Judgement Bar is archived. The original author will not provide further updates, fixes, or releases. This document describes the existing package format and the work an independent fork must complete before publishing its own builds.

The checked-in `Info.json`, assembly identity, and tests preserve the historical AJB configuration. They are not ready-made publication metadata for a renamed fork. A local build does not upload assets, publish releases, or modify the original repository.

## Existing update behavior

AJB uses external Unity Mod Manager (UMM) for player-initiated updates. The mod does not check GitHub, download packages, or update itself when the game starts. UMM may show an available update in its in-game UI; installation is performed through the external manager.

For an existing installation with an available complete package:

1. Close ADOFAI and open `UnityModManager.exe` with ADOFAI selected.
2. Use the **Mods** tab to install the complete ZIP, or choose **Update to <version>** if the configured feed offers it.
3. Start the game after installation. An overwrite installation preserves an existing `Settings.xml` because the package does not ship that file.

Do not uninstall first if you need to preserve settings: UMM's uninstall operation removes the mod folder. Back up any edits to bundled files because files present in the ZIP are overwritten. These instructions describe existing behavior, not a promise of future original-author releases.

## Install layout and migration

```text
AccurateJudgementBar/
  Info.json
  AccurateJudgementBar.dll
  Languages/
  Resources/
  Settings.xml                 (created by UMM; never packaged)
```

UMM calls `AccurateJudgementBar.Main.Load(UnityModManager.ModEntry)` directly from the root DLL. The public `JudgementApi` is provided by that assembly. Dependent mods must declare AJB as a dependency and must not bundle a second copy of its DLL. [GameCompatibility.cs](src/Core/GameCompatibility.cs) checks the game before patches are applied.

Older installations with an empty `Info.json.Repository`, including former bootstrap packages, need a complete-package overwrite before they can discover the configured feed. The former bootstrap updater cannot install the flat package. Preserve `Settings.xml` and avoid having two enabled AJB installations.

After migration, the root `Info.json` selects the root DLL. Leftover `AccurateJudgementBar.Bootstrap.dll` and `Runtime/` files are unused; the current mod does not read `Runtime/state.json` or `Runtime/update.json`. Custom translations from an old `Runtime/versions/<version>/Languages/` directory may be copied into the root `Languages/` directory. These names describe legacy installations, not additional source shipped in this archive.

There is no automatic rollback. To restore an older package, close the game, back up settings, and install that complete package. Restoring a former bootstrap package also restores its former update behavior. Migration to a fork with a new identity is a separate responsibility of that fork; do not assume the historical in-place procedure covers it.

## Build and generated files

The project targets `netstandard2.1`; test executables require the .NET 10 SDK. Supply ADOFAI and UMM assemblies from your local game installation. The default game path is the Windows Steam location; use `-p:GameExePath="C:\path\to\A Dance of Fire and Ice.exe"` for another installation. The API test project accepts the same property, or an explicit `ManagedAssembliesDir`, for Newtonsoft.Json.

Run from the repository root:

```powershell
dotnet build -c Release -p:DeployToGame=false -p:AutoLaunchGame=false
dotnet run --project tests/ApiTests.csproj
dotnet run --project tests/Toggle/Tests.csproj
dotnet run --project tests/PerfectsOnlyDeath/Tests.csproj
dotnet run --project tests/ResultsGradient/Tests.csproj
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/Packaging/Tests.ps1
```

[StageMod.ps1](build/StageMod.ps1) recreates `out/` with the flat installation layout. [PackageMod.ps1](build/PackageMod.ps1) produces these Release outputs using the version in `Info.json`:

- `dist/AccurateJudgementBar (<Version>).zip`: local versioned archive.
- `dist/AccurateJudgementBar.<Version>.zip`: release package.
- `dist/AccurateJudgementBar.<Version>.zip.sha256`: checksum for manual verification.
- `dist/Repository.json`: UMM update feed.

Debug builds stage the layout without packaging. Rebuilding the same version replaces its archives; other versions remain. Deployment and game launch default to disabled. Only use `-p:DeployToGame=true -p:AutoLaunchGame=false` when intentionally deploying without launching.

Packaging tests require the built DLL. They verify the feed, ZIP layout, and simulated overwrite migration while preserving settings. They do not contact GitHub or modify the real game installation. A real external UMM download/install and in-game loading remain separate manual checks.

## Update feed contract

The archive retains this historical `Info.json.Repository` value:

```text
https://github.com/276opt/Accurate-Judgement-Bar/releases/latest/download/Repository.json
```

The build derives its release-download URL from that field. The feed contains a `Releases` array with matching `Id`, `Version`, and `DownloadUrl`. For a configured repository, the URL is:

```text
https://github.com/<owner>/<repository>/releases/download/v<Version>/<ModName>.<Version>.zip
```

The feed follows the latest release, while the package URL pins an exact tag. The version must be a stable numeric `major.minor.patch`. The feed is available only when the configured repository's latest public release includes `Repository.json`. The checksum is for manual verification; UMM does not consume it in this flow.

## Publishing an independent fork

Follow the archival notice in [README.md](README.md): retain required notices, credit opt276, use a distinct name, and identify the fork's independent maintenance.

Before publishing, coordinate the fork's identity and compatibility changes:

1. Point `Info.json.HomePage` and `Info.json.Repository` to your own project and feed. Do not publish fork updates into the original project's repository or advertise its feed as your fork's update source.
2. Review `Id`, `DisplayName`, author attribution, assembly name, entry point, namespace, resource logical names, deployment directory, and dependent-mod compatibility together. The current packager requires `Id == ModName`, `AssemblyName == ModName + ".dll"`, and `EntryMethod == ModName + ".Main.Load"`. A display-name change alone does not update this contract. Choose valid C# names for identifiers, even when your public project name uses a suffix or hyphen.
3. Update `build/StageMod.ps1`, project/target configuration, and tests for the chosen identity. Staging currently names the payload `AccurateJudgementBar.dll`; packaging tests also assert historical names and URLs. Decide how settings/dependencies migrate and prevent two active copies from patching the game.
4. Set the fork's version and run the full build and all five test suites above. Test the fork's own metadata, update URLs, settings migration, and game loading.
5. In your own repository, prepare a stable release tagged `v<Version>`. Upload the generated `<ModName>.<Version>.zip` and `Repository.json` from the same build, retaining the exact ZIP filename. The SHA-256 file may also be included. A source archive is not an install package.
6. If using the existing latest-release feed design, publish it as the repository's latest stable release and include both assets in every later release served by that feed. Verify that the feed retrieves the matching ZIP and test updating an older fork installation through external UMM.

Identity changes can affect third-party mods using the public API. Resolve those compatibility choices deliberately rather than changing only the ZIP name.

## UMM references

- [Info.json fields](https://github.com/newman55/unity-mod-manager/wiki/How-to-create-a-mod-for-unity-game#information-file)
- [Repository format](https://github.com/newman55/unity-mod-manager/blob/master/UnityModManager/Repository.cs)
- [External manager download and install flow](https://github.com/newman55/unity-mod-manager/blob/master/UnityModManagerApp/Mods.cs)
