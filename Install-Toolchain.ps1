param(
    [string]$Root = $PSScriptRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Write-Step([string]$Message) { Write-Host "[INFO] $Message" }
function Write-Ok([string]$Message) { Write-Host "[ OK ] $Message" }
function Fail([string]$Message) { throw "[FAIL] $Message" }

function Invoke-RobustDownload([string]$Uri, [string]$OutFile, [string]$Description) {
    $parent = Split-Path -Parent $OutFile
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
    $partial = "$OutFile.partial"

    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

    for ($attempt = 1; $attempt -le 3; $attempt++) {
        Remove-Item -LiteralPath $partial -Force -ErrorAction SilentlyContinue
        try {
            Write-Step "$Description (Invoke-WebRequest attempt $attempt of 3)."
            Invoke-WebRequest -UseBasicParsing -Uri $Uri -OutFile $partial -TimeoutSec 300
            if ((Test-Path -LiteralPath $partial -PathType Leaf) -and
                (Get-Item -LiteralPath $partial).Length -gt 0) {
                Move-Item -LiteralPath $partial -Destination $OutFile -Force
                return
            }
        }
        catch {
            Write-Step "Download attempt $attempt failed: $($_.Exception.Message)"
        }
        Start-Sleep -Seconds (2 * $attempt)
    }

    $curl = Get-Command curl.exe -ErrorAction SilentlyContinue
    if ($null -ne $curl) {
        Remove-Item -LiteralPath $partial -Force -ErrorAction SilentlyContinue
        Write-Step "$Description (curl.exe fallback)."
        & $curl.Source -L --fail --retry 5 --retry-delay 2 --connect-timeout 30 -o $partial $Uri
        if ($LASTEXITCODE -eq 0 -and
            (Test-Path -LiteralPath $partial -PathType Leaf) -and
            (Get-Item -LiteralPath $partial).Length -gt 0) {
            Move-Item -LiteralPath $partial -Destination $OutFile -Force
            return
        }
    }

    try {
        Import-Module BitsTransfer -ErrorAction Stop
        Remove-Item -LiteralPath $partial -Force -ErrorAction SilentlyContinue
        Write-Step "$Description (BITS fallback)."
        Start-BitsTransfer -Source $Uri -Destination $partial -ErrorAction Stop
        if ((Test-Path -LiteralPath $partial -PathType Leaf) -and
            (Get-Item -LiteralPath $partial).Length -gt 0) {
            Move-Item -LiteralPath $partial -Destination $OutFile -Force
            return
        }
    }
    catch {
        Write-Step "BITS fallback failed: $($_.Exception.Message)"
    }

    Remove-Item -LiteralPath $partial -Force -ErrorAction SilentlyContinue
    Fail "Unable to download $Description from $Uri."
}

function Invoke-Checked([string]$FilePath, [string[]]$Arguments) {
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) { Fail "$FilePath failed with exit code $LASTEXITCODE." }
}

function Get-CommandPath([string]$Name) {
    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if ($null -eq $command) { return $null }
    return $command.Source
}

function Test-VersionOutput([string]$Executable, [string]$ExpectedText) {
    if (-not (Test-Path -LiteralPath $Executable -PathType Leaf)) { return $false }
    try {
        $output = (& $Executable --version 2>&1 | Out-String)
        return $output -match [regex]::Escape($ExpectedText)
    } catch { return $false }
}

function Save-ResolvedToolPath([string]$RepositoryRoot, [string]$Name, [string]$ExecutablePath) {
    $statePath = Join-Path $RepositoryRoot '.toolchain\Inu.ToolPaths.json'
    $state = @{}
    if (Test-Path -LiteralPath $statePath -PathType Leaf) {
        try {
            $existing = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
            foreach ($property in $existing.PSObject.Properties) { $state[$property.Name] = $property.Value }
        } catch { $state = @{} }
    }
    $state[$Name] = $ExecutablePath
    $state | ConvertTo-Json | Set-Content -LiteralPath $statePath -Encoding UTF8
}

function Install-DotNet([string]$RepositoryRoot, [pscustomobject]$Manifest) {
    $installRoot = Join-Path $RepositoryRoot $Manifest.dotNetSdk.installDirectory
    $dotnet = Join-Path $installRoot 'dotnet.exe'
    if (Test-VersionOutput $dotnet $Manifest.dotNetSdk.version) {
        Write-Ok ".NET SDK $($Manifest.dotNetSdk.version) is already valid."
        return
    }

    New-Item -ItemType Directory -Path $installRoot -Force | Out-Null
    $downloads = Join-Path $RepositoryRoot '.toolchain\.downloads'
    New-Item -ItemType Directory -Path $downloads -Force | Out-Null
    $installer = Join-Path $downloads 'dotnet-install.ps1'
    Invoke-RobustDownload 'https://dot.net/v1/dotnet-install.ps1' $installer 'Downloading the official .NET installer'
    Write-Step "Installing .NET SDK $($Manifest.dotNetSdk.version)."
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $installer `
        -Version $Manifest.dotNetSdk.version `
        -InstallDir $installRoot `
        -NoPath 2>&1 | ForEach-Object { Write-Host $_ }

    $installExitCode = $LASTEXITCODE
    if ($installExitCode -ne 0 -or -not (Test-VersionOutput $dotnet $Manifest.dotNetSdk.version)) {
        Fail 'The pinned .NET SDK could not be installed or validated.'
    }
    Write-Ok ".NET SDK $($Manifest.dotNetSdk.version) installed."
}

function Install-NativeAot([string]$RepositoryRoot, [string]$DotNet, [pscustomobject]$Manifest) {
    $project = Join-Path $RepositoryRoot 'toolchains\Inu.NativeAot.Bootstrap.csproj'
    $packages = Join-Path $RepositoryRoot $Manifest.nativeAot.packageDirectory
    $compilerPackage = Join-Path $packages ("microsoft.dotnet.ilcompiler\" + $Manifest.nativeAot.packageVersion)
    $hostPackage = Join-Path $packages ("runtime.win-x64.microsoft.dotnet.ilcompiler\" + $Manifest.nativeAot.packageVersion)
    $ilc = Join-Path $hostPackage 'tools\ilc.exe'

    if ((Test-Path -LiteralPath $compilerPackage -PathType Container) -and
        (Test-Path -LiteralPath $ilc -PathType Leaf)) {
        Save-ResolvedToolPath $RepositoryRoot 'ilc' $ilc
        Write-Ok "NativeAOT ILC compiler $($Manifest.nativeAot.packageVersion) is already valid: $ilc"
        return
    }

    New-Item -ItemType Directory -Path $packages -Force | Out-Null
    Write-Step "Restoring the NativeAOT ILC compiler host $($Manifest.nativeAot.packageVersion)."
    Invoke-Checked $DotNet @(
        'restore',
        $project,
        '--runtime',
        'win-x64',
        '--packages',
        $packages,
        '--nologo',
        "/p:ILCompilerVersion=$($Manifest.nativeAot.packageVersion)"
    )

    if (-not (Test-Path -LiteralPath $compilerPackage -PathType Container) -or
        -not (Test-Path -LiteralPath $ilc -PathType Leaf)) {
        Fail 'The pinned NativeAOT ILC compiler host was not restored to the repository-local package directory.'
    }

    Save-ResolvedToolPath $RepositoryRoot 'ilc' $ilc
    Write-Ok "NativeAOT ILC compiler installed: $ilc"
}

function Install-LlvmTools([string]$RepositoryRoot, [pscustomobject]$Manifest) {
    $installRoot = Join-Path $RepositoryRoot $Manifest.llvm.installDirectory
    $binRoot = Join-Path $installRoot 'bin'
    $allPresent = $true

    foreach ($tool in $Manifest.llvm.requiredTools) {
        if (-not (Test-Path -LiteralPath (Join-Path $binRoot $tool) -PathType Leaf)) {
            $allPresent = $false
            break
        }
    }

    if ($allPresent -and (Test-VersionOutput (Join-Path $binRoot 'ld.lld.exe') $Manifest.llvm.version)) {
        Write-Ok "Clang, LLD and required LLVM utilities $($Manifest.llvm.version) are already valid."
        return
    }

    New-Item -ItemType Directory -Path $installRoot -Force | Out-Null
    $downloads = Join-Path $RepositoryRoot '.toolchain\.downloads'
    New-Item -ItemType Directory -Path $downloads -Force | Out-Null
    $installer = Join-Path $downloads ("LLVM-" + $Manifest.llvm.version + '-win64.exe')
    $url = "https://github.com/llvm/llvm-project/releases/download/llvmorg-$($Manifest.llvm.version)/LLVM-$($Manifest.llvm.version)-win64.exe"

    Invoke-RobustDownload $url $installer "Downloading the official LLVM Windows distribution $($Manifest.llvm.version)"

    Write-Step 'Installing Clang, LLD and LLVM utilities into the repository-local toolchain.'
    $process = Start-Process -FilePath $installer -ArgumentList @('/S', "/D=$installRoot") -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        Fail "LLVM installer failed with exit code $($process.ExitCode)."
    }

    foreach ($tool in $Manifest.llvm.requiredTools) {
        if (-not (Test-Path -LiteralPath (Join-Path $binRoot $tool) -PathType Leaf)) {
            Fail "Required LLVM tool is missing: $tool"
        }
    }

    if ($null -ne $Manifest.llvm.optionalTools) {
        foreach ($tool in $Manifest.llvm.optionalTools) {
            if (Test-Path -LiteralPath (Join-Path $binRoot $tool) -PathType Leaf) {
                Write-Ok "Optional LLVM tool is available: $tool"
            } else {
                Write-Step "Optional LLVM tool is unavailable and is not required: $tool"
            }
        }
    }

    if (-not (Test-VersionOutput (Join-Path $binRoot 'ld.lld.exe') $Manifest.llvm.version)) {
        Fail 'LLD version validation failed.'
    }

    Save-ResolvedToolPath $RepositoryRoot 'clang' (Join-Path $binRoot 'clang.exe')
    Save-ResolvedToolPath $RepositoryRoot 'lldLink' (Join-Path $binRoot 'lld-link.exe')
    Write-Ok 'Clang, LLD and required LLVM utilities installed.'
}

try {
    $Root = [IO.Path]::GetFullPath($Root)
    $manifestPath = Join-Path $Root 'toolchains\Inu.Toolchain.json'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        Fail "Missing toolchain manifest: $manifestPath"
    }

    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    New-Item -ItemType Directory -Path (Join-Path $Root '.toolchain') -Force | Out-Null

    Install-DotNet $Root $manifest

    $dotnet = Join-Path (Join-Path $Root $manifest.dotNetSdk.installDirectory) 'dotnet.exe'
    Install-NativeAot $Root $dotnet $manifest
    Install-LlvmTools $Root $manifest

    $receipt = [ordered]@{
        schemaVersion = 1
        productVersion = [string]$manifest.productVersion
        installedUtc = [DateTimeOffset]::UtcNow.ToString('O')
        installedBy = 'Inu'
        dotNetSdk = [string]$manifest.dotNetSdk.version
        nativeAot = [string]$manifest.nativeAot.packageVersion
        llvm = [string]$manifest.llvm.version
    }
    $receipt | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $Root '.toolchain\.inu-toolchain.json') -Encoding UTF8

    Write-Ok 'Inu toolchain validation completed.'
    exit 0
} catch {
    Write-Host $_.Exception.Message
    exit 1
}
