param(
    [Parameter(Position = 0)]
    [string]$ArchiveFolder
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Fail([string]$Message) { throw "[FAIL] $Message" }

function Get-VersionFromName([string]$Name) {
    $match = [regex]::Match($Name, '^Kath&Inu-(?:ChangedFiles|FullSource)-(?<version>\d+\.\d+\.\d+)(?:\(\d+\))?\.zip$', [Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if (-not $match.Success) { return $null }
    return [version]$match.Groups['version'].Value
}

$repositoryRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$folders = [Collections.Generic.List[string]]::new()
if (-not [string]::IsNullOrWhiteSpace($ArchiveFolder)) { $folders.Add((Resolve-Path -LiteralPath $ArchiveFolder).Path) }
$folders.Add($repositoryRoot)
$folders.Add((Get-Location).Path)
$downloads = Join-Path ([Environment]::GetFolderPath('UserProfile')) 'Downloads'
if (Test-Path -LiteralPath $downloads -PathType Container) { $folders.Add($downloads) }

$candidates = foreach ($folder in ($folders | Select-Object -Unique)) {
    if (-not (Test-Path -LiteralPath $folder -PathType Container)) { continue }
    Get-ChildItem -LiteralPath $folder -File -Filter 'Kath&Inu-*.zip' | ForEach-Object {
        $version = Get-VersionFromName $_.Name
        if ($null -ne $version) {
            $kindRank = if ($_.Name -like 'Kath&Inu-ChangedFiles-*') { 0 } else { 1 }
            [pscustomobject]@{ File = $_; Version = $version; KindRank = $kindRank }
        }
    }
}

$selected = $candidates | Sort-Object @{ Expression = 'Version'; Descending = $true }, @{ Expression = 'KindRank'; Descending = $false } | Select-Object -First 1
if ($null -eq $selected) { Fail 'No Kath&Inu ChangedFiles or FullSource archive was found.' }
Write-Host "[ OK ] Bootstrap selected $($selected.File.Name)."

Add-Type -AssemblyName System.IO.Compression.FileSystem
$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) ('KathInuUpdaterBootstrap-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temporaryRoot | Out-Null
try {
    $archive = [IO.Compression.ZipFile]::OpenRead($selected.File.FullName)
    try {
        $entry = $archive.GetEntry('Update-Inu.ps1')
        if ($null -eq $entry) { Fail 'Selected archive does not contain Update-Inu.ps1.' }
        $scriptPath = Join-Path $temporaryRoot 'Update-Inu.ps1'
        [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $scriptPath, $true)
    } finally {
        $archive.Dispose()
    }

    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $scriptPath $ArchiveFolder
    exit $LASTEXITCODE
} finally {
    Remove-Item -LiteralPath $temporaryRoot -Recurse -Force -ErrorAction SilentlyContinue
}
