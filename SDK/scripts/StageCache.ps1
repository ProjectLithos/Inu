# Content-addressed stage cache. Compatible with Windows PowerShell 5.1.
function Get-InuStageFiles {
    param([string[]]$Paths, [switch]$Outputs)
    foreach ($path in @($Paths | Sort-Object -Unique)) {
        if ([string]::IsNullOrWhiteSpace($path)) { continue }
        if (Test-Path -LiteralPath $path -PathType Leaf) { Get-Item -LiteralPath $path; continue }
        if (Test-Path -LiteralPath $path -PathType Container) {
            Get-ChildItem -LiteralPath $path -Recurse -File | Where-Object {
                $Outputs -or $_.FullName -notmatch '[\\/](bin|obj|Artifacts|\.toolchain|node_modules|\.git|\.vs|Runs|BuildLogs)[\\/]'
            }
        }
    }
}

function Get-InuStageHash {
    param([string[]]$Paths, [string[]]$Values = @(), [switch]$Outputs)
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        # Encode boundaries and missing paths, not just file bytes. Directory listings
        # detect additions/removals, including removed commands and deleted outputs.
        foreach ($value in @('InuStageCache:2') + @($Values) + @($Paths | Sort-Object -Unique)) {
            $bytes = [Text.Encoding]::UTF8.GetBytes(([string]$value + "`0"))
            [void]$sha.TransformBlock($bytes, 0, $bytes.Length, $bytes, 0)
        }
        foreach ($file in @(Get-InuStageFiles -Paths $Paths -Outputs:$Outputs | Sort-Object FullName -Unique)) {
            $bytes = [Text.Encoding]::UTF8.GetBytes(($file.FullName + "`0" + $file.Length + "`0"))
            [void]$sha.TransformBlock($bytes, 0, $bytes.Length, $bytes, 0)
            $stream = [IO.File]::OpenRead($file.FullName)
            try {
                $buffer = New-Object byte[] 65536
                while (($count = $stream.Read($buffer, 0, $buffer.Length)) -gt 0) {
                    [void]$sha.TransformBlock($buffer, 0, $count, $buffer, 0)
                }
            } finally { $stream.Dispose() }
        }
        $empty = New-Object byte[] 0
        [void]$sha.TransformFinalBlock($empty, 0, 0)
        return ([BitConverter]::ToString($sha.Hash)).Replace('-', '').ToLowerInvariant()
    } finally { $sha.Dispose() }
}

function Get-InuStageOutputHash {
    param([string[]]$Paths, [switch]$MutableOutputs)
    if (-not $MutableOutputs) { return Get-InuStageHash -Paths $Paths -Outputs }
    $immutable = @(); $sizes = @()
    foreach ($path in $Paths) {
        if ([IO.Path]::GetExtension($path) -eq '.img') {
            # FAT32 images have a fixed allocated length. User data may change their
            # bytes, while deletion/truncation must still invalidate image reuse.
            $sizes += ($path + ':' + (Get-Item -LiteralPath $path).Length)
        } else { $immutable += $path }
    }
    return Get-InuStageHash -Paths $immutable -Values $sizes -Outputs
}

function Invoke-InuCachedAction {
    param(
        [string]$Stage, [string]$CacheDirectory, [string[]]$Inputs,
        [string[]]$Outputs, [string[]]$KeyArguments = @(), [scriptblock]$Action,
        [switch]$Force, [switch]$Dry, [switch]$MutableOutputs
    )
    $key = Get-InuStageHash -Paths @() -Values (@($Stage, $Configuration) + $Outputs)
    $cacheFile = Join-Path $CacheDirectory ($key + '.json')
    $inputHash = Get-InuStageHash -Paths $Inputs -Values (@($Stage, $Configuration) + $KeyArguments)
    $ready = $Outputs.Count -gt 0
    foreach ($output in $Outputs) {
        if (-not (Test-Path -LiteralPath $output)) { $ready = $false; break }
        if (@(Get-InuStageFiles -Paths @($output) -Outputs).Count -eq 0) { $ready = $false; break }
    }
    $cache = $null
    if (-not $Force -and -not $Dry -and $ready -and (Test-Path -LiteralPath $cacheFile -PathType Leaf)) {
        try {
            $cache = Get-Content -LiteralPath $cacheFile -Raw | ConvertFrom-Json
            foreach ($property in @('schema','inputs','outputs','mutable')) {
                if ($null -eq $cache.PSObject.Properties[$property]) { $cache = $null; break }
            }
        } catch { $cache = $null }
    }
    if ($null -ne $cache -and $cache.schema -eq 2 -and $cache.inputs -eq $inputHash -and
        $cache.mutable -eq [bool]$MutableOutputs -and
        ($cache.outputs -eq (Get-InuStageOutputHash -Paths $Outputs -MutableOutputs:$MutableOutputs))) {
        Write-Host "[ OK ] ${Stage}: cached; inputs and outputs are current."
        return
    }
    # Invalidate before running: a failed/interrupted stage can never retain a hit.
    if (Test-Path -LiteralPath $cacheFile -PathType Leaf) { Remove-Item -LiteralPath $cacheFile -Force }
    & $Action
    if ($Dry) { return }
    foreach ($output in $Outputs) {
        if (-not (Test-Path -LiteralPath $output) -or @(Get-InuStageFiles -Paths @($output) -Outputs).Count -eq 0) {
            throw "$Stage did not produce its required cache output: $output"
        }
    }
    if (-not $Outputs.Count) { return }
    # Refuse to publish a cache if an editor changed inputs while the stage ran.
    if ((Get-InuStageHash -Paths $Inputs -Values (@($Stage, $Configuration) + $KeyArguments)) -ne $inputHash) {
        Write-Host "[INFO] ${Stage}: inputs changed during execution; cache not saved."
        return
    }
    New-Item -ItemType Directory -Path $CacheDirectory -Force | Out-Null
    $temporary = $cacheFile + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
    try {
        [pscustomobject]@{ schema=2; inputs=$inputHash; mutable=[bool]$MutableOutputs;
            outputs=Get-InuStageOutputHash -Paths $Outputs -MutableOutputs:$MutableOutputs
        } | ConvertTo-Json | Set-Content -LiteralPath $temporary -Encoding UTF8
        Move-Item -LiteralPath $temporary -Destination $cacheFile -Force
    } finally { if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force } }
}

function Get-InuProjectStageInputs {
    param([string]$ProjectFile, [hashtable]$Seen = @{})
    $ProjectFile = [IO.Path]::GetFullPath($ProjectFile)
    if ($Seen.ContainsKey($ProjectFile)) { return }
    $Seen[$ProjectFile] = $true
    $ProjectFile
    $directory = Split-Path -Parent $ProjectFile
    $directory
    $parent = $directory
    while ($parent) {
        foreach ($name in @('Directory.Build.props','Directory.Build.targets','Directory.Packages.props','global.json','NuGet.Config')) {
            Join-Path $parent $name
        }
        $next = Split-Path -Parent $parent
        if ($next -eq $parent) { break }; $parent = $next
    }
    if (-not (Test-Path -LiteralPath $ProjectFile -PathType Leaf)) { return }
    [xml]$xml = Get-Content -LiteralPath $ProjectFile -Raw
    foreach ($reference in $xml.SelectNodes("//*[local-name()='ProjectReference']")) {
        $include = $reference.GetAttribute('Include')
        if ($include -and $include -notmatch '\$\(') {
            Get-InuProjectStageInputs -ProjectFile (Join-Path $directory $include) -Seen $Seen
        }
    }
    # Literal linked sources/imports outside the project directory also count.
    foreach ($item in $xml.SelectNodes("//*[local-name()='Compile' or local-name()='Import']")) {
        $include = if ($item.LocalName -eq 'Import') { $item.GetAttribute('Project') } else { $item.GetAttribute('Include') }
        if ($include -and $include -notmatch '[*?]|\$\(') { Join-Path $directory $include }
    }
}
