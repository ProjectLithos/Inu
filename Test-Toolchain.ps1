param(
    [string]$Root = $PSScriptRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

try {
    $Root = [IO.Path]::GetFullPath($Root)
    $manifestPath = Join-Path $Root 'toolchains\Inu.Toolchain.json'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { exit 1 }

    $m = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json

$receiptPath = Join-Path $Root '.toolchain\.inu-toolchain.json'
    if (-not (Test-Path -LiteralPath $receiptPath -PathType Leaf)) { exit 1 }
    $receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
    if ([string]$receipt.dotNetSdk -ne [string]$m.dotNetSdk.version) { exit 1 }
    if ([string]$receipt.nativeAot -ne [string]$m.nativeAot.packageVersion) { exit 1 }
    if ([string]$receipt.llvm -ne [string]$m.llvm.version) { exit 1 }

    $dotnet = Join-Path (Join-Path $Root $m.dotNetSdk.installDirectory) 'dotnet.exe'
    $llvmBin = Join-Path (Join-Path $Root $m.llvm.installDirectory) 'bin'
    $lld = Join-Path $llvmBin 'ld.lld.exe'

    if (-not (Test-Path -LiteralPath $dotnet -PathType Leaf)) { exit 1 }
    if ((& $dotnet --version).Trim() -ne [string]$m.dotNetSdk.version) { exit 1 }

    foreach ($tool in $m.llvm.requiredTools) {
        if (-not (Test-Path -LiteralPath (Join-Path $llvmBin $tool) -PathType Leaf)) { exit 1 }
    }

    $lldVersion = (& $lld --version 2>&1 | Out-String)
    if ($lldVersion -notmatch [regex]::Escape([string]$m.llvm.version)) { exit 1 }

    $packages = Join-Path $Root $m.nativeAot.packageDirectory
    $ilc = Join-Path $packages ("runtime.win-x64.microsoft.dotnet.ilcompiler\" + $m.nativeAot.packageVersion + '\tools\ilc.exe')
    if (-not (Test-Path -LiteralPath $ilc -PathType Leaf)) { exit 1 }

    exit 0
}
catch {
    exit 1
}
