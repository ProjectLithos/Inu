# Run with Windows PowerShell 5.1 or newer; no compiler/QEMU required.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'StageCache.ps1')
$Configuration = 'Release'
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('InuCacheTest-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
$source = Join-Path $testRoot 'Source.cs'
$output = Join-Path $testRoot 'Output.obj'
$script:executions = 0
function Check([bool]$condition, [string]$message) { if (-not $condition) { throw $message } }
function Compile([switch]$Force, [switch]$Dry) {
    Invoke-InuCachedAction -Stage 'Test compile' -CacheDirectory (Join-Path $testRoot 'Cache') -Inputs @($source) -Outputs @($output) -Force:$Force -Dry:$Dry -Action {
        $script:executions++
        [IO.File]::WriteAllText($output, [IO.File]::ReadAllText($source))
    }
}
try {
    [IO.File]::WriteAllText($source, 'one'); Compile; Compile
    Check ($script:executions -eq 1) 'Unchanged stage did not hit.'
    $stamp = [IO.File]::GetLastWriteTimeUtc($source)
    [IO.File]::WriteAllText($source, 'two'); [IO.File]::SetLastWriteTimeUtc($source, $stamp); Compile
    Check ($script:executions -eq 2) 'Content change with preserved timestamp did not invalidate.'
    [IO.File]::WriteAllText($output, 'damaged'); Compile
    Check ($script:executions -eq 3) 'Damaged output was reused.'
    Remove-Item -LiteralPath $output; Compile
    Check ($script:executions -eq 4) 'Deleted output was reused.'
    Compile -Force; Check ($script:executions -eq 5) 'Force did not execute.'
    Compile -Dry; Compile
    Check ($script:executions -eq 7) 'Dry run retained/published a cache.'
    $Configuration = 'Debug'; Compile; Compile
    Check ($script:executions -eq 8) 'Configuration separation failed.'
    $Configuration = 'Release'
    try {
        Invoke-InuCachedAction -Stage 'Test compile' -CacheDirectory (Join-Path $testRoot 'Cache') -Inputs @($source) -Outputs @($output) -Force -Action { throw 'Expected stage failure' }
        throw 'Failure was not propagated'
    } catch { Check ($_.Exception.Message -eq 'Expected stage failure') 'Wrong failure result.' }
    Compile; Check ($script:executions -eq 9) 'Failed stage retained a hit.'
    foreach ($record in Get-ChildItem -LiteralPath (Join-Path $testRoot 'Cache') -Filter '*.json') { Set-Content -LiteralPath $record.FullName -Value '{}' }
    Compile; Check ($script:executions -eq 10) 'Corrupt cache did not miss.'
    $image = Join-Path $testRoot 'Writable.img'; $script:images = 0
    foreach ($pass in 1..2) {
        Invoke-InuCachedAction -Stage 'Test image' -CacheDirectory (Join-Path $testRoot 'Cache') -Inputs @($output) -Outputs @($image) -MutableOutputs -Action {
            $script:images++; [IO.File]::WriteAllText($image, 'image')
        }
        [IO.File]::WriteAllText($image, 'users')
    }
    Check ($script:images -eq 1) 'Writable image mutation invalidated the image cache.'
    Check ((Get-InuStageHash -Paths @($source)) -ne (Get-InuStageHash -Paths @($source,(Join-Path $testRoot 'missing.cs')))) 'Missing path markers were omitted.'
    Write-Host '[ OK ] Stage cache regressions passed.'
} finally { Remove-Item -LiteralPath $testRoot -Recurse -Force }
