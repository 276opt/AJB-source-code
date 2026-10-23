param([string]$PayloadPath = 'bin/Release/netstandard2.1/AccurateJudgementBar.dll')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.IO.Compression.FileSystem

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$payload = (Resolve-Path -LiteralPath $PayloadPath).Path
$tempParent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/')
$fixtureRoot = Join-Path $tempParent ('AJB-Packaging-' + [Guid]::NewGuid().ToString('N'))
$checks = 0
function Check([bool]$condition, [string]$message) {
    if (!$condition) { throw $message }
    $script:checks++
}

try {
    New-Item -ItemType Directory -Path $fixtureRoot | Out-Null
    foreach ($name in @('Info.json', 'Languages', 'Resources')) {
        Copy-Item -LiteralPath (Join-Path $projectRoot $name) -Destination $fixtureRoot -Recurse
    }
    # A previous build and a local settings file must not leak into new packages.
    $oldOutput = Join-Path $fixtureRoot 'out/Runtime/versions/1.0.0'
    New-Item -ItemType Directory -Path $oldOutput -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $oldOutput 'AccurateJudgementBar.dll') -Value 'old runtime'
    Set-Content -LiteralPath (Join-Path $fixtureRoot 'out/AccurateJudgementBar.Bootstrap.dll') -Value 'old loader'
    Set-Content -LiteralPath (Join-Path $fixtureRoot 'Settings.xml') -Value '<Settings>private</Settings>'

    & (Join-Path $projectRoot 'build/StageMod.ps1') -ProjectDirectory $fixtureRoot -PayloadPath $payload
    $staging = Join-Path $fixtureRoot 'out'
    $dist = Join-Path $fixtureRoot 'dist'
    & (Join-Path $projectRoot 'build/PackageMod.ps1') -OutDirectory $staging -DistDirectory $dist -ModName 'AccurateJudgementBar'

    $info = Get-Content -LiteralPath (Join-Path $staging 'Info.json') -Raw | ConvertFrom-Json
    $feed = Get-Content -LiteralPath (Join-Path $dist 'Repository.json') -Raw | ConvertFrom-Json
    Check ($feed.Releases -is [array] -and $feed.Releases.Count -eq 1) 'UMM feed must contain a Releases array with one AJB entry.'
    $release = $feed.Releases[0]
    Check ($release.Id -eq $info.Id -and $release.Version -eq $info.Version) 'Feed must match the installed mod ID and version.'
    Check ($info.Repository -eq 'https://github.com/276opt/Accurate-Judgement-Bar/releases/latest/download/Repository.json') 'Installed Info.json must discover the latest release feed.'
    Check ($release.DownloadUrl -eq "https://github.com/276opt/Accurate-Judgement-Bar/releases/download/v$($info.Version)/AccurateJudgementBar.$($info.Version).zip") 'Download URL must pin the advertised version and versioned asset name.'

    $assetName = ([Uri]$release.DownloadUrl).Segments[-1]
    $zipPath = Join-Path $dist $assetName
    Check (Test-Path -LiteralPath $zipPath -PathType Leaf) 'Feed must reference a generated release ZIP.'
    $hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    Check ((Get-Content -LiteralPath ($zipPath + '.sha256') -Raw).Trim() -eq "$hash  $assetName") 'Published ZIP checksum and filename must match.'
    Check ((Get-FileHash -LiteralPath (Join-Path $dist "AccurateJudgementBar ($($info.Version)).zip")).Hash.ToLowerInvariant() -eq $hash) 'Local history and release ZIPs must be identical.'

    $archive = [IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        $names = @($archive.Entries | ForEach-Object { $_.FullName })
        Check (@($names | Where-Object { !$_.StartsWith('AccurateJudgementBar/') }).Count -eq 0) 'Every ZIP entry must be in the AJB folder.'
        foreach ($required in @('Info.json', 'AccurateJudgementBar.dll', 'Languages/en.json', 'Languages/zh-CN.json', 'Resources/legacy_tick.png')) {
            Check ($names -contains "AccurateJudgementBar/$required") "Missing install file: $required"
        }
        Check (@($names | Where-Object { $_ -match '(?i)(/Runtime/|Bootstrap|Settings\.xml|Repository\.json|runtime\.json)' }).Count -eq 0) 'ZIP must exclude obsolete updater files, settings, and the separate feed.'

        # Model UMM's overwrite extraction over the former bootstrap layout.
        $mods = Join-Path $fixtureRoot 'installed/Mods'
        $installed = Join-Path $mods 'AccurateJudgementBar'
        New-Item -ItemType Directory -Path (Join-Path $installed 'Runtime/versions/1.0.0') -Force | Out-Null
        $settingsPath = Join-Path $installed 'Settings.xml'
        [IO.File]::WriteAllBytes($settingsPath, [Text.Encoding]::UTF8.GetBytes('<Settings><Language>zh-CN</Language></Settings>'))
        $settingsHash = (Get-FileHash -LiteralPath $settingsPath).Hash
        Set-Content -LiteralPath (Join-Path $installed 'Info.json') -Value '{"AssemblyName":"AccurateJudgementBar.Bootstrap.dll","Repository":""}'
        Set-Content -LiteralPath (Join-Path $installed 'AccurateJudgementBar.Bootstrap.dll') -Value 'old loader'
        Set-Content -LiteralPath (Join-Path $installed 'Runtime/state.json') -Value '{"Current":"1.0.0"}'
        Set-Content -LiteralPath (Join-Path $installed 'Runtime/update.json') -Value '{"Enabled":true}'
        foreach ($entry in $archive.Entries) {
            $destination = [IO.Path]::GetFullPath((Join-Path $mods $entry.FullName))
            if (!$destination.StartsWith($installed + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'ZIP entry escaped the mod directory.' }
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $destination, $true)
        }
        $installedInfo = Get-Content -LiteralPath (Join-Path $installed 'Info.json') -Raw | ConvertFrom-Json
        Check ($installedInfo.AssemblyName -eq 'AccurateJudgementBar.dll' -and $installedInfo.EntryMethod -eq 'AccurateJudgementBar.Main.Load') 'Migrated install must select the root DLL and direct entry point.'
        Check ($installedInfo.Repository -eq $info.Repository -and $installedInfo.Version -eq $release.Version) 'Migrated install must advertise its actual installed version and update feed.'
        Check ((Get-FileHash -LiteralPath $settingsPath).Hash -eq $settingsHash) 'Migration must preserve settings byte-for-byte.'
        Check ((Get-FileHash -LiteralPath (Join-Path $installed $installedInfo.AssemblyName)).Hash -eq (Get-FileHash -LiteralPath $payload).Hash) 'UMM-selected DLL must be the newly built mod.'
        Check (Test-Path -LiteralPath (Join-Path $installed 'Runtime/state.json')) 'Migration must not require deleting old runtime state.'
    }
    finally { $archive.Dispose() }
    Write-Output "PASS: $checks packaging and migration checks."
}
finally {
    # Only delete our generated fixture after checking its resolved location and links.
    if (Test-Path -LiteralPath $fixtureRoot) {
        $resolved = (Resolve-Path -LiteralPath $fixtureRoot).Path
        if ([IO.Path]::GetDirectoryName($resolved) -ne $tempParent -or [IO.Path]::GetFileName($resolved) -notmatch '\AAJB-Packaging-[0-9a-f]{32}\z') { throw 'Unsafe fixture cleanup path.' }
        $items = @((Get-Item -LiteralPath $resolved -Force)) + @(Get-ChildItem -LiteralPath $resolved -Recurse -Force)
        if ($items | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }) { throw 'Refusing fixture cleanup through links.' }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
