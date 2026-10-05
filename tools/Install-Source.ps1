[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$SourceRoot,
    [Parameter(Mandatory=$true)][string]$DestinationRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Info([string]$m) { Write-Host "[INFO] $m" }
function Ok([string]$m) { Write-Host "[ OK ] $m" }
function Fail([string]$m) { throw "[FAIL] $m" }

$SourceRoot = [IO.Path]::GetFullPath($SourceRoot).TrimEnd('\')
$DestinationRoot = [IO.Path]::GetFullPath($DestinationRoot).TrimEnd('\')
$sourceInu = Join-Path $SourceRoot 'Inu'
$sourceKath = Join-Path $SourceRoot 'Kath'
$destInu = Join-Path $DestinationRoot 'Inu'
$destKath = Join-Path $DestinationRoot 'Kath'

function Read-Version([string]$p, [string]$name) {
    if (-not (Test-Path -LiteralPath $p -PathType Leaf)) { Fail "$name VERSION is missing: $p" }
    $v = (Get-Content -LiteralPath $p -TotalCount 1).Trim()
    if ($v -notmatch '^\d+\.\d+\.\d+$') { Fail "$name VERSION is invalid: '$v'" }
    return $v
}

function Invoke-Mirror([string]$from, [string]$to, [string[]]$excludeDirectories) {
    if (-not (Test-Path -LiteralPath $to)) { New-Item -ItemType Directory -Path $to -Force | Out-Null }
    $args = @($from,$to,'/MIR','/R:1','/W:1','/NFL','/NDL','/NJH','/NJS','/NP')
    if ($excludeDirectories.Count -gt 0) { $args += '/XD'; $args += $excludeDirectories }
    & robocopy.exe @args | Out-Null
    if ($LASTEXITCODE -ge 8) { Fail "robocopy failed while mirroring $from to $to (exit $LASTEXITCODE)." }
}

function Remove-FilesOutsideSdkSourceManifest([string]$sourceSdkRoot, [string]$destinationSdkRoot) {
    $manifestPath = Join-Path $sourceSdkRoot 'Inu-SourceManifest.json'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        Fail "SDK source manifest is missing: $manifestPath"
    }
    if (-not (Test-Path -LiteralPath $destinationSdkRoot -PathType Container)) {
        Fail "Destination SDK root is missing: $destinationSdkRoot"
    }

    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ($null -eq $manifest.files) { Fail "SDK source manifest has no files array: $manifestPath" }

    $allowed = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in @($manifest.files)) {
        $relative = [string]$entry.path
        if ([string]::IsNullOrWhiteSpace($relative)) { Fail 'SDK source manifest contains an empty path.' }
        $relative = $relative.Replace('/','\').TrimStart('\')
        if ([IO.Path]::IsPathRooted($relative) -or (($relative -split '\\') -contains '..')) {
            Fail "SDK source manifest contains an unsafe path: $relative"
        }
        [void]$allowed.Add($relative)
    }
    # The source manifest intentionally does not hash itself.
    [void]$allowed.Add('Inu-SourceManifest.json')

    # Preserve private/runtime state that is intentionally excluded from the clean-source manifest.
    $preserveTop = @('.toolchain','Artifacts','Bin','.git')
    $destinationSdkRoot = [IO.Path]::GetFullPath($destinationSdkRoot).TrimEnd('\')
    foreach ($file in @(Get-ChildItem -LiteralPath $destinationSdkRoot -Recurse -File -Force -ErrorAction SilentlyContinue)) {
        $relative = $file.FullName.Substring($destinationSdkRoot.Length).TrimStart('\')
        $first = ($relative -split '\\',2)[0]
        if ($first -in $preserveTop -or $relative -match '(?i)(^|\\)(bin|obj)(\\|$)') { continue }
        if (-not $allowed.Contains($relative)) {
            Remove-Item -LiteralPath $file.FullName -Force
        }
    }

    # Remove empty stale directories after their obsolete files are gone.  Keep the
    # preserved private/runtime roots even when currently empty.
    $directories = @(Get-ChildItem -LiteralPath $destinationSdkRoot -Recurse -Directory -Force -ErrorAction SilentlyContinue | Sort-Object { $_.FullName.Length } -Descending)
    foreach ($directory in $directories) {
        $relative = $directory.FullName.Substring($destinationSdkRoot.Length).TrimStart('\')
        $first = ($relative -split '\\',2)[0]
        if ($first -in $preserveTop -or $relative -match '(?i)(^|\\)(bin|obj)(\\|$)') { continue }
        if (-not @(Get-ChildItem -LiteralPath $directory.FullName -Force -ErrorAction SilentlyContinue).Count) {
            Remove-Item -LiteralPath $directory.FullName -Force
        }
    }
}

function Remove-KnownObsolete([string]$root) {
    Remove-Item -LiteralPath (Join-Path $DestinationRoot 'Inu\Bin\Kath.exe') -Force -ErrorAction SilentlyContinue

    Info 'Cleaning the Kath and Inu root and removing obsolete layout files.'

    # This is the dedicated Kath&Inu product root. Keep only the supported
    # root contract. Hidden Git metadata is preserved when present.
    $keepRoot = @('Build.bat','Build.ps1','Run-Kath.bat','Version.bat','Version.ps1','VERSION','Inu','Kath','.git')
    Get-ChildItem -LiteralPath $root -Force -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -notin $keepRoot } |
        ForEach-Object {
            Remove-Item -LiteralPath $_.FullName -Recurse -Force
        }

    if (Test-Path -LiteralPath $destInu) {
        foreach ($name in @('Repos','toolchain','Inu.Common','Inu.Language.Input','Inu.Language.Output','Inu.Target','Inu.Build')) {
            $p = Join-Path $destInu $name
            if (Test-Path -LiteralPath $p) { Remove-Item -LiteralPath $p -Recurse -Force }
        }
        foreach ($name in @('Commit-Repos.bat','Run-Kath.bat','Prepare-Sources.ps1','README.md','SDKPolicy.md')) {
            $p = Join-Path $destInu $name
            if (Test-Path -LiteralPath $p -PathType Leaf) { Remove-Item -LiteralPath $p -Force }
        }
        Get-ChildItem -LiteralPath $destInu -File -Filter '*.zip' -ErrorAction SilentlyContinue | Remove-Item -Force
    }

    if (Test-Path -LiteralPath $destKath) {
        $obsoleteEmbeddedSdk = Join-Path $destKath 'SDK'
        if (Test-Path -LiteralPath $obsoleteEmbeddedSdk) { Remove-Item -LiteralPath $obsoleteEmbeddedSdk -Recurse -Force }
        foreach ($name in @('Ancillary','Assets')) {
            $p = Join-Path $destKath $name
            if (Test-Path -LiteralPath $p) { Remove-Item -LiteralPath $p -Recurse -Force }
        }

        # 0.2.0: Kath never owns or embeds the Inu SDK.
        # A ChangedFiles overlay cannot remove paths by itself, so delete every
        # pre-0.0.136 maintained implementation mirror before verification/build.
        $sdkRoot = Join-Path $destInu 'SDK'
        $canonicalCoreLib = [IO.Path]::GetFullPath((Join-Path $sdkRoot 'src\Inu.Freestanding.CoreLib\CoreLib.cs'))
        if (Test-Path -LiteralPath $sdkRoot) {
            Get-ChildItem -LiteralPath $sdkRoot -Recurse -File -Filter 'CoreLib.cs' -ErrorAction SilentlyContinue |
                Where-Object { [IO.Path]::GetFullPath($_.FullName) -ine $canonicalCoreLib } |
                ForEach-Object {
                    Remove-Item -LiteralPath $_.FullName -Force
                }
        }
        foreach ($p in @(
            (Join-Path $destKath 'packages\kath\scripts')
        )) { if (Test-Path -LiteralPath $p) { Remove-Item -LiteralPath $p -Recurse -Force } }
        $oldIcon = Join-Path $destKath 'Kath.ico'
        if (Test-Path -LiteralPath $oldIcon -PathType Leaf) { Remove-Item -LiteralPath $oldIcon -Force }

        $cjs = Join-Path $destKath 'CJS'
        if (Test-Path -LiteralPath $cjs) {
            Get-ChildItem -LiteralPath $cjs -File -ErrorAction SilentlyContinue |
                Where-Object { $_.Name -notin @('extension-files.cjs','stage-cache.cjs','test-stage-cache.cjs') } |
                Remove-Item -Force
        }
        $json = Join-Path $destKath 'JSON'
        if (Test-Path -LiteralPath $json) {
            Get-ChildItem -LiteralPath $json -File -Filter 'Kath-*-ReleaseValidation.json' -ErrorAction SilentlyContinue | Remove-Item -Force
        }
        $docs = Join-Path $destKath 'docs'
        if (Test-Path -LiteralPath $docs) {
            Get-ChildItem -LiteralPath $docs -File -ErrorAction SilentlyContinue |
                Where-Object { $_.Name -notin @('README.md','Release-0.0.200.md','Release-0.1.0.md') } |
                Remove-Item -Force
        }
        $obsoleteScripts = @(
            'Apply-Kath-0.44.19.ps1','Apply-KathDeletedFiles.ps1','Clean-InuOfflineDocumentation.ps1',
            'Create-Inu-FullSource.bat','Publish-KathSource.ps1','Refresh-InuSdkDocumentation.bat',
            'Refresh-InuSdkDocumentation.ps1'
        )
        foreach ($name in $obsoleteScripts) {
            $p=Join-Path (Join-Path $destKath 'Scripts') $name
            if (Test-Path -LiteralPath $p -PathType Leaf) { Remove-Item -LiteralPath $p -Force }
        }
        Get-ChildItem -LiteralPath $destKath -File -Filter '*.zip' -ErrorAction SilentlyContinue | Remove-Item -Force
    }
}

$release = Read-Version (Join-Path $SourceRoot 'VERSION') 'FullSource'
$inuVersion = Read-Version (Join-Path $sourceInu 'VERSION') 'Inu'
$kathVersion = Read-Version (Join-Path $sourceKath 'VERSION') 'Kath'
if ($inuVersion -ne $release -or $kathVersion -ne $release) {
    Fail "Version mismatch: FullSource=$release Inu=$inuVersion Kath=$kathVersion"
}
foreach ($p in @(
    (Join-Path $sourceInu 'src\Common\Inu.Cli\Inu.Cli.csproj'),
    (Join-Path $sourceInu 'targets\X64\PC\UEFI\Target.json'),
    (Join-Path $sourceInu 'toolchains\Inu.Toolchain.json'),
    (Join-Path $sourceKath 'Build-Kath.bat'),
    (Join-Path $sourceKath 'Build-Kath.ps1'),
    (Join-Path $sourceKath 'CJS\stage-cache.cjs'),
    (Join-Path $sourceKath 'applications\electron\package.json'),
    (Join-Path $sourceKath 'packages\kath\package.json')
)) { if (-not (Test-Path -LiteralPath $p -PathType Leaf)) { Fail "Required clean source file is missing: $p" } }
if (Test-Path -LiteralPath (Join-Path $sourceInu 'Repos')) { Fail 'The clean Inu source must not contain Repos.' }

if ($SourceRoot -ine $DestinationRoot) {
    Info "Mirroring Inu $release into $destInu"
    Invoke-Mirror $sourceInu $destInu @('.git','.toolchain','Bin','obj','Artifacts')
    Info "Mirroring Kath $release into $destKath"
    Invoke-Mirror $sourceKath $destKath @('.git','.toolchain','node_modules','Bin','Artifacts','.browser_modules',(Join-Path $destKath 'packages\kath\lib'),(Join-Path $destKath 'applications\electron\lib'),(Join-Path $destKath 'applications\electron\src-gen'),(Join-Path $sourceKath 'packages\kath\lib'),(Join-Path $sourceKath 'applications\electron\lib'),(Join-Path $sourceKath 'applications\electron\src-gen'))
    Copy-Item -LiteralPath (Join-Path $SourceRoot 'VERSION') -Destination (Join-Path $DestinationRoot 'VERSION') -Force
    foreach ($rootFile in @('Build.bat','Build.ps1','Run-Kath.bat','Version.bat','Version.ps1')) {
        $sourceRootFile = Join-Path $SourceRoot $rootFile
        if (Test-Path -LiteralPath $sourceRootFile -PathType Leaf) {
            Copy-Item -LiteralPath $sourceRootFile -Destination (Join-Path $DestinationRoot $rootFile) -Force
        }
    }
}

Remove-KnownObsolete $DestinationRoot
Remove-FilesOutsideSdkSourceManifest (Join-Path $sourceInu 'SDK') (Join-Path $destInu 'SDK')
Ok "Clean Kath and Inu source layout prepared for $release."
