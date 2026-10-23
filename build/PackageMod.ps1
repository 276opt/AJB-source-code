param(
    [Parameter(Mandatory = $true)][string]$OutDirectory,
    [Parameter(Mandatory = $true)][string]$DistDirectory,
    [Parameter(Mandatory = $true)][string]$ModName
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$sourceDirectory = (Resolve-Path -LiteralPath $OutDirectory).Path.TrimEnd('\', '/')
$info = Get-Content -LiteralPath (Join-Path $sourceDirectory 'Info.json') -Raw | ConvertFrom-Json
$version = [string]$info.Version
if ($version -notmatch '\A(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\z') {
    throw "Info.json contains a missing or invalid Version: '$version'."
}
if ($ModName -notmatch '\A[0-9A-Za-z][0-9A-Za-z._-]*\z') {
    throw "Invalid mod folder name: '$ModName'."
}
if ($info.Id -ne $ModName -or $info.AssemblyName -ne "$ModName.dll" -or $info.EntryMethod -ne "$ModName.Main.Load") {
    throw 'Info.json must identify the directly loaded UMM mod.'
}
$repositorySuffix = '/releases/latest/download/Repository.json'
if ([string]$info.Repository -notmatch '\Ahttps://github\.com/[^/]+/[^/]+/releases/latest/download/Repository\.json\z') {
    throw 'Info.json Repository must point to the GitHub latest-release Repository.json asset.'
}
$releaseRoot = $info.Repository.Substring(0, $info.Repository.Length - $repositorySuffix.Length)

[System.IO.Directory]::CreateDirectory($DistDirectory) | Out-Null
$zipPath = Join-Path $DistDirectory "$ModName ($version).zip"
$stream = [System.IO.File]::Open($zipPath, [System.IO.FileMode]::Create)
try {
    $archive = [System.IO.Compression.ZipArchive]::new($stream, [System.IO.Compression.ZipArchiveMode]::Create, $true)
    try {
        foreach ($file in Get-ChildItem -LiteralPath $sourceDirectory -File -Recurse -Force | Sort-Object FullName) {
            $relativePath = $file.FullName.Substring($sourceDirectory.Length + 1).Replace('\', '/')
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $archive, $file.FullName, "$ModName/$relativePath",
                [System.IO.Compression.CompressionLevel]::Optimal
            ) | Out-Null
        }
    }
    finally {
        $archive.Dispose()
    }
}
finally {
    $stream.Dispose()
}

Write-Output "Release package created: $zipPath"

# Use the same versioned filename for the release asset, checksum, and UMM URL.
$assetName = "$ModName.$version.zip"
$assetPath = Join-Path $DistDirectory $assetName
Copy-Item -LiteralPath $zipPath -Destination $assetPath -Force
$sha = [Security.Cryptography.SHA256]::Create()
$assetStream = [IO.File]::OpenRead($assetPath)
try { $hash = [BitConverter]::ToString($sha.ComputeHash($assetStream)).Replace('-', '').ToLowerInvariant() }
finally { $assetStream.Dispose(); $sha.Dispose() }
[IO.File]::WriteAllText(($assetPath + '.sha256'), "$hash  $assetName`n", [Text.UTF8Encoding]::new($false))
Write-Output "GitHub release asset: $assetPath"

# Publish this alongside the ZIP. Pin the ZIP to a tag so a newer release cannot
# change the package behind an already downloaded version announcement.
$repository = [ordered]@{ Releases = @([ordered]@{
    Id = [string]$info.Id
    Version = $version
    DownloadUrl = "$releaseRoot/releases/download/v$version/$assetName"
}) }
$repositoryPath = Join-Path $DistDirectory 'Repository.json'
[IO.File]::WriteAllText($repositoryPath, ($repository | ConvertTo-Json -Depth 4), [Text.UTF8Encoding]::new($false))
Write-Output "UMM update manifest: $repositoryPath"
