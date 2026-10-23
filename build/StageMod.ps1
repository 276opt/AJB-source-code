param(
    [Parameter(Mandatory = $true)][string]$ProjectDirectory,
    [Parameter(Mandatory = $true)][string]$PayloadPath
)
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path -LiteralPath $ProjectDirectory).Path.TrimEnd('\', '/')
$info = Get-Content -LiteralPath (Join-Path $projectRoot 'Info.json') -Raw | ConvertFrom-Json
$version = [string]$info.Version
if ($version -notmatch '\A(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\z') {
    throw 'UMM updates require a stable major.minor.patch version in Info.json.'
}
if (!(Test-Path -LiteralPath $PayloadPath -PathType Leaf)) {
    throw 'Build the mod DLL before staging.'
}
# Only this project's generated out folder is cleared, never a caller-selected directory.
$outPath = [IO.Path]::GetFullPath((Join-Path $projectRoot 'out'))
if ([IO.Path]::GetDirectoryName($outPath) -ne $projectRoot -or [IO.Path]::GetFileName($outPath) -ne 'out') {
    throw 'Output directory escaped the project.'
}
if (Test-Path -LiteralPath $outPath) {
    $existing = Get-Item -LiteralPath $outPath -Force
    if ($existing.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Output directory must not be a link.' }
    $links = Get-ChildItem -LiteralPath $outPath -Recurse -Force | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }
    if ($links) { throw 'Output directory contains links; refusing recursive cleanup.' }
    Remove-Item -LiteralPath $outPath -Recurse -Force
}
New-Item -ItemType Directory -Path $outPath -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'Info.json') -Destination $outPath
Copy-Item -LiteralPath $PayloadPath -Destination (Join-Path $outPath 'AccurateJudgementBar.dll')
Copy-Item -LiteralPath (Join-Path $projectRoot 'Languages') -Destination $outPath -Recurse
Copy-Item -LiteralPath (Join-Path $projectRoot 'Resources') -Destination $outPath -Recurse
foreach ($obsolete in @('curved_layer1.png', 'straight_layer1.png', 'test.txt', 'README.txt')) {
    $obsoletePath = Join-Path $outPath "Resources\$obsolete"
    if (Test-Path -LiteralPath $obsoletePath -PathType Leaf) { Remove-Item -LiteralPath $obsoletePath }
}
Write-Output "Staged AJB $version for direct UMM loading (no player settings)."
