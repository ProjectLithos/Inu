$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Write-Step([string]$Message) { Write-Host "[INFO] $Message" }
function Write-Ok([string]$Message) { Write-Host "[ OK ] $Message" }
function Fail([string]$Message) { throw "[FAIL] $Message" }

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

function Install-DotNet([string]$RepositoryRoot, [pscustomobject]$Manifest) {
    $installRoot = Join-Path $RepositoryRoot $Manifest.dotNetSdk.installDirectory
    $dotnet = Join-Path $installRoot 'dotnet.exe'
    if (Test-VersionOutput $dotnet $Manifest.dotNetSdk.version) {
        Write-Ok ".NET SDK $($Manifest.dotNetSdk.version) is already valid."
        return
    }

    New-Item -ItemType Directory -Path $installRoot -Force | Out-Null
    $installer = Join-Path $env:TEMP 'Inu-dotnet-install.ps1'
    Write-Step "Downloading the official .NET installer."
    Invoke-WebRequest -UseBasicParsing -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installer
    Write-Step "Installing .NET SDK $($Manifest.dotNetSdk.version)."
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $installer -Version $Manifest.dotNetSdk.version -InstallDir $installRoot -NoPath 2>&1 | ForEach-Object { Write-Host $_ }
    $installExitCode = $LASTEXITCODE
    if ($installExitCode -ne 0 -or -not (Test-VersionOutput $dotnet $Manifest.dotNetSdk.version)) {
        Fail 'The pinned .NET SDK could not be installed or validated.'
    }
    Write-Ok ".NET SDK $($Manifest.dotNetSdk.version) installed."
}


function Install-NativeAot([string]$RepositoryRoot, [string]$DotNet, [pscustomobject]$Manifest) {
    $project = Join-Path $RepositoryRoot 'toolchain\Inu.NativeAot.Bootstrap.csproj'
    $packages = Join-Path $RepositoryRoot $Manifest.nativeAot.packageDirectory
    $compilerPackage = Join-Path $packages ("microsoft.dotnet.ilcompiler\" + $Manifest.nativeAot.packageVersion)
    $hostPackage = Join-Path $packages ("runtime.win-x64.microsoft.dotnet.ilcompiler\" + $Manifest.nativeAot.packageVersion)
    $ilc = Join-Path $hostPackage 'tools\ilc.exe'
    if ((Test-Path -LiteralPath $compilerPackage -PathType Container) -and (Test-Path -LiteralPath $ilc -PathType Leaf)) {
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
    if (-not (Test-Path -LiteralPath $compilerPackage -PathType Container) -or -not (Test-Path -LiteralPath $ilc -PathType Leaf)) {
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
        if (-not (Test-Path -LiteralPath (Join-Path $binRoot $tool) -PathType Leaf)) { $allPresent = $false; break }
    }
    if ($allPresent -and (Test-VersionOutput (Join-Path $binRoot 'ld.lld.exe') $Manifest.llvm.version)) {
        Write-Ok "LLD and required LLVM utilities $($Manifest.llvm.version) are already valid."
        return
    }

    New-Item -ItemType Directory -Path $installRoot -Force | Out-Null
    $installer = Join-Path $env:TEMP ("LLVM-" + $Manifest.llvm.version + '-win64.exe')
    $url = "https://github.com/llvm/llvm-project/releases/download/llvmorg-$($Manifest.llvm.version)/LLVM-$($Manifest.llvm.version)-win64.exe"
    Write-Step "Downloading the official LLVM Windows distribution $($Manifest.llvm.version)."
    Invoke-WebRequest -UseBasicParsing -Uri $url -OutFile $installer
    Write-Step 'Installing LLD and LLVM utilities into the repository-local toolchain.'
    $process = Start-Process -FilePath $installer -ArgumentList @('/S', "/D=$installRoot") -Wait -PassThru
    if ($process.ExitCode -ne 0) { Fail "LLVM installer failed with exit code $($process.ExitCode)." }
    foreach ($tool in $Manifest.llvm.requiredTools) {
        if (-not (Test-Path -LiteralPath (Join-Path $binRoot $tool) -PathType Leaf)) { Fail "Required LLVM tool is missing: $tool" }
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
    if (-not (Test-VersionOutput (Join-Path $binRoot 'ld.lld.exe') $Manifest.llvm.version)) { Fail 'LLD version validation failed.' }
    Write-Ok 'LLD and required LLVM utilities installed.'
}

function Find-InstalledExecutable([string]$CommandName, [string[]]$CandidatePaths) {
    $commandPath = Get-CommandPath $CommandName
    if ($null -ne $commandPath) { return $commandPath }

    foreach ($candidate in $CandidatePaths) {
        if ([string]::IsNullOrWhiteSpace($candidate)) { continue }
        $expanded = [Environment]::ExpandEnvironmentVariables($candidate)
        if (Test-Path -LiteralPath $expanded -PathType Leaf) { return $expanded }
    }

    return $null
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

function Ensure-Qemu([string]$RepositoryRoot, [pscustomobject]$Manifest) {
    $candidates = @(
        '%ProgramFiles%\qemu\qemu-system-x86_64.exe',
        '%ProgramFiles(x86)%\qemu\qemu-system-x86_64.exe',
        '%LOCALAPPDATA%\Programs\qemu\qemu-system-x86_64.exe',
        '%LOCALAPPDATA%\Microsoft\WinGet\Links\qemu-system-x86_64.exe'
    )
    $qemu = Find-InstalledExecutable 'qemu-system-x86_64.exe' $candidates
    if ($null -eq $qemu) {
        $winget = Get-CommandPath 'winget.exe'
        if ($null -eq $winget) { Fail 'QEMU is missing and winget.exe is unavailable.' }
        Write-Step 'Installing QEMU with winget.'
        Invoke-Checked $winget @('install', '--id', $Manifest.qemu.wingetId, '--exact', '--accept-package-agreements', '--accept-source-agreements', '--silent')
        $machinePath = [Environment]::GetEnvironmentVariable('Path', 'Machine')
        $userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
        $env:Path = "$machinePath;$userPath"
        $qemu = Find-InstalledExecutable 'qemu-system-x86_64.exe' $candidates
    }
    if ($null -eq $qemu) { Fail 'QEMU was installed but qemu-system-x86_64.exe could not be found in PATH or standard installation locations.' }
    Save-ResolvedToolPath $RepositoryRoot 'qemuSystemX64' $qemu
    Write-Ok "QEMU is available: $qemu"
    return $qemu
}

function Find-OvmfFirmware([string]$QemuPath, [string[]]$FileNames) {
    $qemuDirectory = Split-Path -Parent $QemuPath
    $roots = @(
        $qemuDirectory,
        (Join-Path $qemuDirectory 'share'),
        (Join-Path $qemuDirectory 'share\qemu'),
        ([IO.Path]::GetFullPath((Join-Path $qemuDirectory '..\share'))),
        ([IO.Path]::GetFullPath((Join-Path $qemuDirectory '..\share\qemu'))),
        ([Environment]::ExpandEnvironmentVariables('%ProgramFiles%\qemu')),
        ([Environment]::ExpandEnvironmentVariables('%ProgramFiles(x86)%\qemu')),
        ([Environment]::ExpandEnvironmentVariables('%LOCALAPPDATA%\Programs\qemu'))
    ) | Select-Object -Unique

    foreach ($root in $roots) {
        if (-not (Test-Path -LiteralPath $root -PathType Container)) { continue }
        foreach ($fileName in $FileNames) {
            $direct = Join-Path $root $fileName
            if (Test-Path -LiteralPath $direct -PathType Leaf) { return (Resolve-Path -LiteralPath $direct).Path }
            $recursive = Get-ChildItem -LiteralPath $root -Filter $fileName -File -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($null -ne $recursive) { return $recursive.FullName }
        }
    }
    return $null
}

function Ensure-Ovmf([string]$RepositoryRoot, [pscustomobject]$Manifest, [string]$QemuPath) {
    $codeNames = @($Manifest.ovmf.codeFileNames)
    $varsNames = @($Manifest.ovmf.variableStoreFileNames)
    $code = Find-OvmfFirmware $QemuPath $codeNames
    $vars = Find-OvmfFirmware $QemuPath $varsNames

    if ($null -eq $code -or $null -eq $vars) {
        $winget = Get-CommandPath 'winget.exe'
        if ($null -eq $winget) { Fail 'x64 OVMF firmware is missing and winget.exe is unavailable to repair the QEMU installation.' }
        Write-Step 'The QEMU installation does not contain the required x64 OVMF files. Repairing QEMU with winget.'
        Invoke-Checked $winget @('install', '--id', $Manifest.qemu.wingetId, '--exact', '--accept-package-agreements', '--accept-source-agreements', '--silent', '--force')
        $code = Find-OvmfFirmware $QemuPath $codeNames
        $vars = Find-OvmfFirmware $QemuPath $varsNames
    }

    if ($null -eq $code) { Fail 'x64 OVMF code firmware was not found after QEMU installation: edk2-x86_64-code.fd or OVMF_CODE.fd.' }
    if ($null -eq $vars) { Fail 'x64 OVMF variable-store template was not found after QEMU installation: edk2-i386-vars.fd, edk2-x86_64-vars.fd or OVMF_VARS.fd.' }
    Save-ResolvedToolPath $RepositoryRoot 'ovmfCodeX64' $code
    Save-ResolvedToolPath $RepositoryRoot 'ovmfVarsX64' $vars
    Write-Ok "x64 OVMF code firmware is available: $code"
    Write-Ok "x64 OVMF variable-store template is available: $vars"
    return $true
}

function Ensure-WingetTool([string]$RepositoryRoot, [string]$DisplayName, [string]$CommandName, [string]$WingetId, [string[]]$CandidatePaths) {
    $existing = Find-InstalledExecutable $CommandName $CandidatePaths
    if ($null -ne $existing) {
        Save-ResolvedToolPath $RepositoryRoot $CommandName $existing
        Write-Ok "$DisplayName already exists: $existing"
        return
    }
    $winget = Get-CommandPath 'winget.exe'
    if ($null -eq $winget) { Fail "$DisplayName is missing and winget.exe is unavailable." }
    Write-Step "Installing $DisplayName with winget."
    Invoke-Checked $winget @('install', '--id', $WingetId, '--exact', '--accept-package-agreements', '--accept-source-agreements', '--silent')
    $machinePath = [Environment]::GetEnvironmentVariable('Path', 'Machine')
    $userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
    $env:Path = "$machinePath;$userPath"
    $existing = Find-InstalledExecutable $CommandName $CandidatePaths
    if ($null -eq $existing) { Fail "$DisplayName was installed but $CommandName could not be found." }
    Save-ResolvedToolPath $RepositoryRoot $CommandName $existing
    Write-Ok "$DisplayName installed: $existing"
}

try {
    $repositoryRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
    $manifestPath = Join-Path $repositoryRoot 'toolchain\Inu.Toolchain.json'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { Fail "Missing toolchain manifest: $manifestPath" }
    $embeddedSdk = $env:INU_EMBEDDED_SDK -eq '1'
    if ($embeddedSdk) {
        Write-Step 'Embedded SDK mode: skipping standalone Git repository/clean-tree gate.'
    } else {
        if (-not (Test-Path -LiteralPath (Join-Path $repositoryRoot '.git') -PathType Container)) { Fail 'The source must be committed in a Git repository before installing the toolchain.' }
        & git.exe -C $repositoryRoot diff --quiet
        if ($LASTEXITCODE -ne 0) { Fail 'The repository has uncommitted source changes.' }
        & git.exe -C $repositoryRoot diff --cached --quiet
        if ($LASTEXITCODE -ne 0) { Fail 'The repository has staged but uncommitted source changes.' }
    }

    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    New-Item -ItemType Directory -Path (Join-Path $repositoryRoot '.toolchain') -Force | Out-Null
    Install-DotNet $repositoryRoot $manifest
    $dotnet = Join-Path (Join-Path $repositoryRoot $manifest.dotNetSdk.installDirectory) 'dotnet.exe'
    Install-NativeAot $repositoryRoot $dotnet $manifest
    Install-LlvmTools $repositoryRoot $manifest
    $qemu = Ensure-Qemu $repositoryRoot $manifest
    if (-not (Ensure-Ovmf $repositoryRoot $manifest $qemu)) { Fail 'x64 OVMF validation failed.' }
    Ensure-WingetTool $repositoryRoot 'NASM' 'nasm.exe' $manifest.nasm.wingetId @(
        '%LOCALAPPDATA%\bin\NASM\nasm.exe',
        '%ProgramFiles%\NASM\nasm.exe',
        '%ProgramFiles(x86)%\NASM\nasm.exe',
        '%LOCALAPPDATA%\Microsoft\WinGet\Links\nasm.exe'
    )
    $fontInstaller = Join-Path $repositoryRoot 'Install-InuFonts.ps1'
    if (Test-Path -LiteralPath $fontInstaller -PathType Leaf) {
        Write-Step 'Installing optional Linux-kernel console font pack.'
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $fontInstaller
        if ($LASTEXITCODE -ne 0) { Write-Host "[WARN] Optional Linux-kernel font installation returned exit code $LASTEXITCODE; continuing because fonts are not required for the SDK toolchain." }
    }
    Write-Ok 'Inu toolchain validation completed.'
    exit 0
} catch {
    Write-Host $_.Exception.Message
    exit 1
}
