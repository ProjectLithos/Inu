[CmdletBinding()]
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$BuildArguments
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$buildScript = Join-Path $root 'Build-Inu.ps1'
if (-not (Test-Path -LiteralPath $buildScript -PathType Leaf)) {
    Write-Host "[FAIL] Inu build script was not found: $buildScript"
    exit 1
}

function ConvertTo-InuNativeArgument {
    param([AllowNull()][string]$Value)

    if ($null -eq $Value -or $Value.Length -eq 0) { return '""' }
    if ($Value -notmatch '[\s"]') { return $Value }

    $builder = New-Object System.Text.StringBuilder
    [void]$builder.Append('"')
    $slashes = 0
    foreach ($ch in $Value.ToCharArray()) {
        if ($ch -eq '\') {
            $slashes++
            continue
        }
        if ($ch -eq '"') {
            [void]$builder.Append(('\' * ($slashes * 2 + 1)))
            [void]$builder.Append('"')
            $slashes = 0
            continue
        }
        if ($slashes -gt 0) {
            [void]$builder.Append(('\' * $slashes))
            $slashes = 0
        }
        [void]$builder.Append($ch)
    }
    if ($slashes -gt 0) { [void]$builder.Append(('\' * ($slashes * 2))) }
    [void]$builder.Append('"')
    return $builder.ToString()
}

function Get-NewLines {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][ref]$ReadCount
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return @() }
    $all = @(Get-Content -LiteralPath $Path -ErrorAction SilentlyContinue)
    if ($all.Count -le $ReadCount.Value) { return @() }

    $result = @()
    for ($index = $ReadCount.Value; $index -lt $all.Count; $index++) {
        $result += [string]$all[$index]
    }
    $ReadCount.Value = $all.Count
    return $result
}

$powershellHost = Join-Path $PSHOME 'powershell.exe'
if (-not (Test-Path -LiteralPath $powershellHost -PathType Leaf)) {
    $powershellHost = 'powershell.exe'
}

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$relayRoot = Join-Path $root 'Artifacts\BuildLogs'
if (-not (Test-Path -LiteralPath $relayRoot -PathType Container)) {
    New-Item -ItemType Directory -Path $relayRoot -Force | Out-Null
}
$stdoutPath = Join-Path $relayRoot "$stamp-IDE-build-relay.stdout"
$stderrPath = Join-Path $relayRoot "$stamp-IDE-build-relay.stderr"
Set-Content -LiteralPath $stdoutPath -Value @() -Encoding UTF8
Set-Content -LiteralPath $stderrPath -Value @() -Encoding UTF8

$arguments = @(
    '-NoLogo',
    '-NoProfile',
    '-ExecutionPolicy',
    'Bypass',
    '-File',
    $buildScript
)
if ($null -ne $BuildArguments -and $BuildArguments.Count -gt 0) {
    $arguments += $BuildArguments
}
$argumentLine = (@($arguments | ForEach-Object {
    ConvertTo-InuNativeArgument ([string]$_)
}) -join ' ')

$stdoutRead = 0
$stderrRead = 0
$lastActualOutputUtc = [DateTime]::UtcNow
$stageStartUtc = $lastActualOutputUtc
$stageName = 'Inu build'
# This is a fallback only. Build-Inu.ps1 emits its own 20-second stage heartbeat.
# Keep the relay interval longer so it does not duplicate those messages.
$heartbeatIntervalSeconds = 30
$heartbeatDueUtc = $lastActualOutputUtc.AddSeconds($heartbeatIntervalSeconds)

try {
    $process = Start-Process -FilePath $powershellHost -ArgumentList $argumentLine `
        -PassThru -NoNewWindow `
        -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath

    while (-not $process.WaitForExit(250)) {
        $hadActualOutput = $false

        foreach ($line in @(Get-NewLines -Path $stdoutPath -ReadCount ([ref]$stdoutRead))) {
            Write-Host $line
            $hadActualOutput = $true
            $lastActualOutputUtc = [DateTime]::UtcNow
            if ($line -match '^(?<stage>.+?)\.\.\.\s*$') {
                $stageName = $Matches['stage'].Trim()
                $stageStartUtc = $lastActualOutputUtc
            }
        }

        foreach ($line in @(Get-NewLines -Path $stderrPath -ReadCount ([ref]$stderrRead))) {
            Write-Host $line
            $hadActualOutput = $true
            $lastActualOutputUtc = [DateTime]::UtcNow
        }

        if ($hadActualOutput) {
            $heartbeatDueUtc = [DateTime]::UtcNow.AddSeconds($heartbeatIntervalSeconds)
        }
        elseif ([DateTime]::UtcNow -ge $heartbeatDueUtc) {
            $elapsedSeconds = [int][Math]::Floor(([DateTime]::UtcNow - $stageStartUtc).TotalSeconds)
            if ($elapsedSeconds -lt $heartbeatIntervalSeconds) { $elapsedSeconds = $heartbeatIntervalSeconds }
            Write-Host ("[INFO] {0} still running ({1}s elapsed)." -f $stageName, $elapsedSeconds)
            $heartbeatDueUtc = [DateTime]::UtcNow.AddSeconds($heartbeatIntervalSeconds)
        }
    }

    $process.WaitForExit()

    foreach ($line in @(Get-NewLines -Path $stdoutPath -ReadCount ([ref]$stdoutRead))) {
        Write-Host $line
    }
    foreach ($line in @(Get-NewLines -Path $stderrPath -ReadCount ([ref]$stderrRead))) {
        Write-Host $line
    }

    exit $process.ExitCode
}
catch {
    Write-Host "[FAIL] Inu build progress relay failed: $($_.Exception.Message)"
    exit 1
}
finally {
    Remove-Item -LiteralPath $stdoutPath,$stderrPath -Force -ErrorAction SilentlyContinue
}
