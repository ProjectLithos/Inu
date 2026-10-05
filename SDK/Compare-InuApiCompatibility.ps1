[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$Baseline,
    [Parameter(Mandatory=$true)][string]$Current,
    [string]$Report
)
$ErrorActionPreference = 'Stop'
function Load-Api([string]$Path) {
    if (!(Test-Path -LiteralPath $Path)) { throw "API inventory not found: $Path" }
    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}
$before = Load-Api $Baseline
$after = Load-Api $Current
$beforeMajor = ([string]$before.apiVersion).Split('.')[0]
$afterMajor = ([string]$after.apiVersion).Split('.')[0]
$old = @{}
$new = @{}
foreach ($assembly in @($before.assemblies)) { foreach ($item in @($assembly.items)) { $old["$($assembly.name)|$($item.QualifiedName)|$($item.Signature)"] = $item } }
foreach ($assembly in @($after.assemblies)) { foreach ($item in @($assembly.items)) { $new["$($assembly.name)|$($item.QualifiedName)|$($item.Signature)"] = $item } }
$removed = @($old.Keys | Where-Object { !$new.ContainsKey($_) } | Sort-Object)
$added = @($new.Keys | Where-Object { !$old.ContainsKey($_) } | Sort-Object)
$compatible = ($removed.Count -eq 0) -or ($beforeMajor -ne $afterMajor)
$result = [ordered]@{
    schemaVersion = 1
    baselineApiVersion = [string]$before.apiVersion
    currentApiVersion = [string]$after.apiVersion
    compatible = $compatible
    breakingChangesRequireNewApiMajor = $true
    removedOrChangedPublicItems = $removed
    addedPublicItems = $added
}
if ($Report) { $result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $Report -Encoding UTF8 }
if (!$compatible) {
    Write-Host "[FAIL] Public API compatibility check found $($removed.Count) removed or changed item(s) without an API major-version change."
    foreach ($item in $removed | Select-Object -First 25) { Write-Host "[FAIL]   $item" }
    exit 1
}
Write-Host "[ OK ] Public API compatibility verified. Added=$($added.Count), removed/changed=$($removed.Count), API $($before.apiVersion) -> $($after.apiVersion)."
exit 0
