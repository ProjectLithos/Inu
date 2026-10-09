[CmdletBinding()]
param(
    [string]$Project = "",
    [ValidateSet("Debug", "Release")][string]$Configuration = "Release",
    [ValidateRange(5, 300)][int]$BootTimeoutSeconds = 20,
    [switch]$Run,
    [switch]$NoRun,
    [switch]$ForceRebuild,
    [switch]$RuntimeConformance,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
# Normal Inu build/run progress is visible in the IDE output pane. Detailed stage output is
# also retained under Artifacts\BuildLogs so failures remain fully diagnosable.
$VerbosePreference = "Continue"
if (Test-Path variable:PSCSharpommandUseErrorActionPreference) { $PSCSharpommandUseErrorActionPreference = $false }
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$releaseVersion = ([IO.File]::ReadAllText((Join-Path (Split-Path -Parent $root) 'VERSION'))).Trim()

function Get-InuSdkCodeFingerprint {
    param([Parameter(Mandatory = $true)][string]$SdkRoot)
    $extensions = @('.cs','.csproj','.props','.targets','.asm','.c','.h','.json')
    $files = @()
    foreach ($relativeRoot in @('src','native','templates')) {
        $candidate = Join-Path $SdkRoot $relativeRoot
        if (Test-Path -LiteralPath $candidate -PathType Container) {
            $files += @(Get-ChildItem -LiteralPath $candidate -Recurse -File -ErrorAction SilentlyContinue | Where-Object { $extensions -contains $_.Extension.ToLowerInvariant() })
        }
    }
    # Release identity changes on every package and does not change runtime behaviour.
    $releaseIdentity = [IO.Path]::GetFullPath((Join-Path $SdkRoot 'src\Inu.Core\InuSdkContract.cs'))
    $records = [System.Collections.Generic.List[string]]::new()
    foreach ($file in @($files | Sort-Object FullName)) {
        if ([IO.Path]::GetFullPath($file.FullName) -ieq $releaseIdentity) { continue }
        $relative = $file.FullName.Substring(([IO.Path]::GetFullPath($SdkRoot).TrimEnd('\').Length + 1)).Replace('\','/')
        $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        [void]$records.Add(($relative + '|' + $hash))
    }
    $payload = [Text.Encoding]::UTF8.GetBytes(($records -join "`n"))
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash($payload))).Replace('-','').ToLowerInvariant() } finally { $sha.Dispose() }
}

$runtimeValidationDirectory = Join-Path $root 'Artifacts\Validation'
$runtimeValidationStamp = Join-Path $runtimeValidationDirectory 'runtime-conformance-source.sha256'
$currentSdkCodeFingerprint = Get-InuSdkCodeFingerprint -SdkRoot $root
$lastValidatedSdkCodeFingerprint = if (Test-Path -LiteralPath $runtimeValidationStamp -PathType Leaf) { (Get-Content -LiteralPath $runtimeValidationStamp -TotalCount 1).Trim().ToLowerInvariant() } else { '' }
$runtimeConformanceRequired = $currentSdkCodeFingerprint -ne $lastValidatedSdkCodeFingerprint
$runtimeConformanceEnabled = $RuntimeConformance -and $Run -and -not $NoRun
if ($runtimeConformanceRequired) {
    Write-Host '[INFO] SDK implementation code changed since the last successful conformance validation.'
    Write-Host '[INFO] The host-side Inu .NET conformance suite will run once before this build continues.'
    if ($runtimeConformanceEnabled) { Write-Host '[INFO] Explicit -RuntimeConformance also enables the in-kernel managed/BCL/GC validation gate.' }
} elseif ($runtimeConformanceEnabled) {
    Write-Host '[INFO] Explicit -RuntimeConformance requested; the in-kernel managed/BCL/GC validation gate will run.'
} else {
    Write-Host '[ OK ] SDK code fingerprint already passed conformance; validation is skipped.'
}

$inuBuildLogRoot = Join-Path $root "Artifacts\BuildLogs"
if (-not (Test-Path -LiteralPath $inuBuildLogRoot -PathType Container)) {
    New-Item -ItemType Directory -Path $inuBuildLogRoot -Force | Out-Null
}

function Write-InuFailureLogPointer {
    param([Parameter(Mandatory = $true)][string]$logPath)
    Write-Host "[INFO] Full failing stage log: $logPath"
}

function ConvertTo-InuNativeArgument {
    param([AllowNull()][string]$Value)

    if ($null -eq $Value -or $Value.Length -eq 0) { return '""' }
    if ($Value -notmatch '[\s"]') { return $Value }

    # Windows CommandLineToArgvW/CRT-compatible quoting. Backslashes are doubled
    # only when they precede a quote or the closing quote.
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

function Invoke-InuCapturedStage {
    param(
        [Parameter(Mandatory = $true)][string]$Stage,
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $false)][string[]]$Arguments = @(),
        [Parameter(Mandatory = $false)][int]$TimeoutSeconds = 0
    )

    $safeStage = [Regex]::Replace($Stage, '[^A-Za-z0-9._-]+', '-')
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
    $logPath = Join-Path $inuBuildLogRoot "$stamp-$safeStage.log"
    Write-Host "$Stage..."

    $argumentLine = (@($Arguments | ForEach-Object { ConvertTo-InuNativeArgument ([string]$_) }) -join ' ')
    Write-Host ("[INFO] Command: {0} {1}" -f $FilePath, $argumentLine)

    $process = $null
    try {
        $startInfo = New-Object System.Diagnostics.ProcessStartInfo
        $startInfo.FileName = $FilePath
        $startInfo.WorkingDirectory = (Get-Location).ProviderPath
        $startInfo.Arguments = $argumentLine
        $startInfo.UseShellExecute = $false
        $startInfo.CreateNoWindow = $true
        # Capture child output ourselves instead of relying on inherited console handles.
        # The IDE launches this script through an output bridge where inherited native
        # stdout/stderr can disappear. Read both streams asynchronously so Roslyn/ILC
        # diagnostics are always retained without risking a full-pipe deadlock.
        $startInfo.RedirectStandardOutput = $true
        $startInfo.RedirectStandardError = $true

        $process = New-Object System.Diagnostics.Process
        $process.StartInfo = $startInfo
        if (-not $process.Start()) { throw "$Stage could not start child process." }
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()

        $clock = [Diagnostics.Stopwatch]::StartNew()
        # Keep long-running stages visibly alive without flooding Kath's Output pane.
        $heartbeatIntervalSeconds = 20
        $nextHeartbeatSeconds = $heartbeatIntervalSeconds
        while (-not $process.HasExited) {
            Start-Sleep -Milliseconds 250
            $process.Refresh()
            $elapsedSeconds = [int][Math]::Floor($clock.Elapsed.TotalSeconds)
            if ($TimeoutSeconds -gt 0 -and $clock.Elapsed.TotalSeconds -ge $TimeoutSeconds) {
                try { $process.Kill($true) } catch { try { $process.Kill() } catch {} }
                throw ("{0} exceeded its hard stage timeout of {1} seconds." -f $Stage, $TimeoutSeconds)
            }
            if ($elapsedSeconds -ge $nextHeartbeatSeconds) {
                Write-Host ("[INFO] {0} still running ({1}s elapsed)." -f $Stage, $elapsedSeconds)
                $nextHeartbeatSeconds += $heartbeatIntervalSeconds
            }
        }

        $process.WaitForExit()
        $stdout = $stdoutTask.GetAwaiter().GetResult()
        $stderr = $stderrTask.GetAwaiter().GetResult()
        $clock.Stop()
        $stageExitCode = [int]$process.ExitCode

        $logLines = @(
            "[INFO] Stage: $Stage",
            "[INFO] Command: $FilePath $argumentLine",
            "[INFO] Elapsed seconds: $([Math]::Round($clock.Elapsed.TotalSeconds, 1))",
            "[INFO] Exit code: $stageExitCode",
            "[INFO] Captured stdout/stderr follows."
        )
        if (-not [string]::IsNullOrWhiteSpace($stdout)) {
            $logLines += "[STDOUT]"
            $logLines += $stdout.TrimEnd()
        }
        if (-not [string]::IsNullOrWhiteSpace($stderr)) {
            $logLines += "[STDERR]"
            $logLines += $stderr.TrimEnd()
        }
        Set-Content -LiteralPath $logPath -Encoding UTF8 -Value $logLines
    }
    catch {
        Set-Content -LiteralPath $logPath -Value ([string]$_) -Encoding UTF8
        Write-Host "[FAIL] $Stage could not be started."
        Write-InuFailureLogPointer -logPath $logPath
        Get-Content -LiteralPath $logPath -ErrorAction SilentlyContinue | ForEach-Object { Write-Host $_ }
        exit 1
    }

    if ($null -ne $process) { $process.Dispose() }

    if ($stageExitCode -ne 0) {
        Write-Host "[FAIL] $Stage failed with exit code $stageExitCode."
        Write-Host "[INFO] Captured compiler/tool diagnostics:"
        if (-not [string]::IsNullOrWhiteSpace($stdout)) {
            $stdout.TrimEnd().Split([Environment]::NewLine) | ForEach-Object { Write-Host $_ }
        }
        if (-not [string]::IsNullOrWhiteSpace($stderr)) {
            $stderr.TrimEnd().Split([Environment]::NewLine) | ForEach-Object { Write-Host $_ }
        }
        Write-InuFailureLogPointer -logPath $logPath
        exit $stageExitCode
    }

    Write-Host ("[ OK ] {0} completed in {1:N1}s." -f $Stage, $clock.Elapsed.TotalSeconds)
    return $logPath
}


function ConvertTo-InuLowerHex {
    param([Parameter(Mandatory = $true)][byte[]]$Bytes)
    # Windows PowerShell 5.1 runs on .NET Framework, which does not provide
    # Convert.ToHexString(). BitConverter is available on every supported host.
    return ([BitConverter]::ToString($Bytes)).Replace('-', '').ToLowerInvariant()
}

function Get-InuRelativePath {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$FullPath
    )

    # Path.GetRelativePath() is a .NET Core API and is unavailable when the SDK
    # is launched from Windows PowerShell 5.1. All fingerprinted files are known
    # descendants of Root, so a canonical prefix subtraction is sufficient and
    # keeps the cache key independent of the absolute DCLG installation path.
    $rootFull = [IO.Path]::GetFullPath($Root).TrimEnd([char[]]@([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar))
    $fileFull = [IO.Path]::GetFullPath($FullPath)
    $prefix = $rootFull + [IO.Path]::DirectorySeparatorChar
    if (-not $fileFull.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Cannot make fingerprint path relative: '$fileFull' is not below '$rootFull'."
    }
    return $fileFull.Substring($prefix.Length).Replace([IO.Path]::DirectorySeparatorChar, [char]'/')
}

# Inu 0.35.22 migration guard.
# Filesystem implementations are end-user selectable projects. An incremental
# overlay from 0.35.20 can leave KernelFat32.cs on disk even though the 0.35.21
# contracts have already removed its FAT-specific types. Remove those stale files
# before any project is compiled.
$obsoleteBuiltInFileSystemFiles = @(
    (Join-Path $root "src\Inu.Kernel.Storage\KernelFat32.cs")
)
foreach ($obsoleteFileSystemFile in $obsoleteBuiltInFileSystemFiles) {
    if (-not (Test-Path -LiteralPath $obsoleteFileSystemFile -PathType Leaf)) { continue }
    Write-Host "Removing obsolete built-in filesystem source: $obsoleteFileSystemFile"
    Remove-Item -LiteralPath $obsoleteFileSystemFile -Force
    if (Test-Path -LiteralPath $obsoleteFileSystemFile -PathType Leaf) {
        throw "Could not remove obsolete built-in filesystem source: $obsoleteFileSystemFile"
    }
    Write-Host "[ OK ] Removed obsolete built-in filesystem source."
}


function Find-Executable {
    param(
        [Parameter(Mandatory = $true)][string]$DisplayName,
        [AllowNull()][AllowEmptyCollection()][string[]]$Candidates
    )

    $usableCandidates = @($Candidates | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) })
    foreach ($candidate in $usableCandidates) {
        $expanded = [Environment]::ExpandEnvironmentVariables($candidate)
        if (Test-Path -LiteralPath $expanded -PathType Leaf) {
            return (Resolve-Path -LiteralPath $expanded).Path
        }
    }

    if ($usableCandidates.Count -eq 0) {
        throw "Required build tool is unavailable: $DisplayName. No usable candidate paths were supplied."
    }
    throw "Required build tool is unavailable: $DisplayName. Checked: $($usableCandidates -join ', ')"
}

function Find-Firmware {
    param(
        [Parameter(Mandatory = $true)][string]$DisplayName,
        [AllowNull()][string]$RecordedPath,
        [Parameter(Mandatory = $true)][string]$QemuPath,
        [Parameter(Mandatory = $true)][string[]]$FileNames
    )

    if (-not [string]::IsNullOrWhiteSpace($RecordedPath)) {
        $expanded = [Environment]::ExpandEnvironmentVariables($RecordedPath)
        if (Test-Path -LiteralPath $expanded -PathType Leaf) {
            return (Resolve-Path -LiteralPath $expanded).Path
        }
    }

    $qemuDirectory = Split-Path -Parent $QemuPath
    $roots = @(
        $qemuDirectory,
        (Join-Path $qemuDirectory "share"),
        (Join-Path $qemuDirectory "share\qemu"),
        ([IO.Path]::GetFullPath((Join-Path $qemuDirectory "..\share"))),
        ([IO.Path]::GetFullPath((Join-Path $qemuDirectory "..\share\qemu"))),
        ([Environment]::ExpandEnvironmentVariables("%ProgramFiles%\qemu")),
        ([Environment]::ExpandEnvironmentVariables("%ProgramFiles(x86)%\qemu")),
        ([Environment]::ExpandEnvironmentVariables("%LOCALAPPDATA%\Programs\qemu"))
    ) | Select-Object -Unique

    foreach ($searchRoot in $roots) {
        if (-not (Test-Path -LiteralPath $searchRoot -PathType Container)) { continue }
        foreach ($fileName in $FileNames) {
            $direct = Join-Path $searchRoot $fileName
            if (Test-Path -LiteralPath $direct -PathType Leaf) {
                return (Resolve-Path -LiteralPath $direct).Path
            }
            $recursive = Get-ChildItem -LiteralPath $searchRoot -Filter $fileName -File -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($null -ne $recursive) {
                return $recursive.FullName
            }
        }
    }

    throw "$DisplayName was not found beside the QEMU installation. Rebuild Kath from the Kath&Inu root so the pinned Inu toolchain is prepared before launching an OS."
}

$ideRoot = Split-Path -Parent $root
$dclgRoot = Split-Path -Parent $ideRoot
$localToolchainRoot = Join-Path $root '.toolchain'
$sharedToolchainRoot = Join-Path $dclgRoot 'Inu\.toolchain'

$dotnet = Find-Executable -DisplayName ".NET SDK dotnet.exe" -Candidates @(
    (Join-Path $localToolchainRoot "DotNet\dotnet.exe"),
    (Join-Path $sharedToolchainRoot "DotNet\dotnet.exe")
)

$toolPathsFile = @(
    (Join-Path $localToolchainRoot 'Inu.ToolPaths.json'),
    (Join-Path $sharedToolchainRoot 'Inu.ToolPaths.json')
) | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
$paths = $null
if ($toolPathsFile) {
    try {
        $paths = Get-Content -LiteralPath $toolPathsFile -Raw | ConvertFrom-Json
    } catch {
        throw "Tool-path manifest is invalid: $toolPathsFile. $($_.Exception.Message)"
    }
}

function Get-RecordedPath {
    param([string[]]$Names)
    if ($null -eq $paths) { return $null }
    foreach ($name in $Names) {
        $property = $paths.PSObject.Properties[$name]
        if ($null -ne $property -and -not [string]::IsNullOrWhiteSpace([string]$property.Value)) {
            return [string]$property.Value
        }
    }
    return $null
}

$localLlvmRoot = Join-Path $localToolchainRoot "LLVM\bin"
$sharedLlvmRoot = Join-Path $sharedToolchainRoot "LLVM\bin"
$lldLink = Find-Executable -DisplayName "LLD linker (lld-link.exe)" -Candidates @(
    (Join-Path $localLlvmRoot "lld-link.exe"),
    (Join-Path $sharedLlvmRoot "lld-link.exe"),
    (Get-RecordedPath @("lld-link.exe", "lldLink"))
)
$llvmNm = Find-Executable -DisplayName "LLVM symbol tool (llvm-nm.exe)" -Candidates @(
    (Join-Path $localLlvmRoot "llvm-nm.exe"),
    (Join-Path $sharedLlvmRoot "llvm-nm.exe"),
    (Get-RecordedPath @("llvm-nm.exe", "llvmNm"))
)
$nasm = Find-Executable -DisplayName "NASM assembler (nasm.exe)" -Candidates @(
    (Get-RecordedPath @("nasm.exe", "nasm", "nasmPath")),
    "%LOCALAPPDATA%\bin\NASM\nasm.exe",
    "%ProgramFiles%\NASM\nasm.exe",
    "%ProgramFiles(x86)%\NASM\nasm.exe",
    "%LOCALAPPDATA%\Microsoft\WinGet\Links\nasm.exe",
    ((Get-Command nasm.exe -ErrorAction SilentlyContinue).Source)
)
$toolchainManifestPath = Join-Path $root "toolchain\Inu.Toolchain.json"
$toolchainManifest = Get-Content -LiteralPath $toolchainManifestPath -Raw | ConvertFrom-Json
$ilcVersion = [string]$toolchainManifest.nativeAot.packageVersion
$ilc = Find-Executable -DisplayName "NativeAOT compiler (ilc.exe)" -Candidates @(
    # Prefer the toolchain beneath the current Kath&Inu root. A migrated ToolPaths.json
    # may still contain an absolute C:\DCLG path and must never override C:\KandI.
    (Join-Path $localToolchainRoot "NuGetPackages\runtime.win-x64.microsoft.dotnet.ilcompiler\$ilcVersion\tools\ilc.exe"),
    (Join-Path $sharedToolchainRoot "NuGetPackages\runtime.win-x64.microsoft.dotnet.ilcompiler\$ilcVersion\tools\ilc.exe"),
    (Get-RecordedPath @("ilc", "ilc.exe", "ilcPath"))
)

Write-Host "[ OK ] dotnet : $dotnet"
Write-Host "[ OK ] lld-link: $lldLink"
Write-Host "[ OK ] llvm-nm: $llvmNm"
Write-Host "[ OK ] nasm    : $nasm"
Write-Host "[ OK ] ilc     : $ilc"

$nativeOutput = Join-Path $root "Artifacts\Native\x64"
New-Item -ItemType Directory -Path $nativeOutput -Force | Out-Null
$clang = Join-Path (Split-Path -Parent $lldLink) 'clang.exe'
if (-not (Test-Path -LiteralPath $clang -PathType Leaf)) { throw "The LLVM clang compiler required for the UEFI loader was not found next to lld-link: $clang" }
Write-Host "[ OK ] clang   : $clang"

if ($runtimeConformanceRequired -and -not $DryRun) {
    $referenceConformanceProject = Join-Path $root 'tests\Inu.DotNetConformance.Tests\Inu.DotNetConformance.Tests.csproj'
    $null = Invoke-InuCapturedStage -Stage 'SDK .NET reference conformance' -FilePath $dotnet -Arguments @('run','--project',$referenceConformanceProject,'-c','Release','--no-launch-profile') -TimeoutSeconds 180
    New-Item -ItemType Directory -Path $runtimeValidationDirectory -Force | Out-Null
    Set-Content -LiteralPath $runtimeValidationStamp -Encoding ASCII -Value $currentSdkCodeFingerprint
    $lastValidatedSdkCodeFingerprint = $currentSdkCodeFingerprint
    $runtimeConformanceRequired = $false
    Write-Host '[ OK ] Current SDK code fingerprint passed host-side .NET conformance.'
}

$projectFontRoot = $null
if (-not [string]::IsNullOrWhiteSpace($Project)) {
    $fontProjectManifest = if ([IO.Path]::IsPathRooted($Project)) { [IO.Path]::GetFullPath($Project) } else { [IO.Path]::GetFullPath((Join-Path $root $Project)) }
    if (Test-Path -LiteralPath $fontProjectManifest -PathType Leaf) {
        $projectFontRoot = Split-Path -Parent $fontProjectManifest
    }
}
$projectConsoleTtf = if ($projectFontRoot) { Join-Path $projectFontRoot 'Kernel\Provided\Assets\Fonts\TrueType\Console.ttf' } else { $null }
$consoleFontCandidates = @()
if ($projectConsoleTtf) { $consoleFontCandidates += $projectConsoleTtf }
$consoleFontCandidates += @(
    (Join-Path $ideRoot 'Assets\Fonts\TrueType\DejaVuSansMono.ttf'),
    (Join-Path $ideRoot 'Assets\Fonts\TrueType\Console.ttf')
)
if ($env:LOCALAPPDATA) {
    $consoleFontCandidates += (Join-Path $env:LOCALAPPDATA 'Microsoft\Windows\Fonts\CascadiaMono.ttf')
    $consoleFontCandidates += (Join-Path $env:LOCALAPPDATA 'Microsoft\Windows\Fonts\CascadiaCode.ttf')
}
if ($env:WINDIR) {
    $consoleFontCandidates += (Join-Path $env:WINDIR 'Fonts\consola.ttf')
    $consoleFontCandidates += (Join-Path $env:WINDIR 'Fonts\lucon.ttf')
    $consoleFontCandidates += (Join-Path $env:WINDIR 'Fonts\cour.ttf')
}
$consoleTtf = $consoleFontCandidates | Where-Object { $_ -and (Test-Path -LiteralPath $_ -PathType Leaf) } | Select-Object -First 1
if (-not $consoleTtf) { throw 'A TrueType console font is required, but no usable local font was found.' }

# Existing OS projects created before project-local font staging are migrated here.
# Provided assets are SDK-owned source/assets, so this never modifies coder-owned files.
if ($projectConsoleTtf -and -not (Test-Path -LiteralPath $projectConsoleTtf -PathType Leaf)) {
    $fontDir = Split-Path -Parent $projectConsoleTtf
    New-Item -ItemType Directory -Path $fontDir -Force | Out-Null
    Copy-Item -LiteralPath $consoleTtf -Destination $projectConsoleTtf -Force
    $consoleTtf = $projectConsoleTtf
}
if ($projectConsoleTtf -and (Test-Path -LiteralPath $projectConsoleTtf -PathType Leaf)) {
    $consoleTtf = $projectConsoleTtf
    Write-Host '[ OK ] Project TrueType console font is ready.'
} else {
    Write-Host '[ OK ] TrueType console font is ready.'
}
$trueTypeRendererProject = Join-Path $root 'src\Inu.Kernel.TrueType\Inu.Kernel.TrueType.csproj'
if (-not (Test-Path -LiteralPath $trueTypeRendererProject -PathType Leaf)) { throw "TrueType renderer source is missing: $trueTypeRendererProject" }
Write-Host '[ OK ] TrueType console renderer is available.'
$consoleFontAsm = Join-Path $nativeOutput 'ConsoleFont.generated.asm'
$consoleFontObj = Join-Path $nativeOutput 'ConsoleFont.obj'
@"
bits 64
section .text align=16
global InuX64GetConsoleTrueTypeFontAddress
global InuX64GetConsoleTrueTypeFontLength
InuX64GetConsoleTrueTypeFontAddress:
    xor rax, rax
    ret
InuX64GetConsoleTrueTypeFontLength:
    xor rax, rax
    ret
section .bss align=16
InuConsoleTrueTypeCoverage: resb 65536
InuConsoleTrueTypeWorkspace: resb 262144
InuConsoleTrueTypeCache: resb 2101248
section .text align=16
global InuX64GetConsoleTrueTypeCoverageAddress
global InuX64GetConsoleTrueTypeCoverageLength
global InuX64GetConsoleTrueTypeWorkspaceAddress
global InuX64GetConsoleTrueTypeWorkspaceLength
global InuX64GetConsoleTrueTypeCacheAddress
global InuX64GetConsoleTrueTypeCacheLength
InuX64GetConsoleTrueTypeCoverageAddress: lea rax, [rel InuConsoleTrueTypeCoverage]
    ret
InuX64GetConsoleTrueTypeCoverageLength: mov rax, 65536
    ret
InuX64GetConsoleTrueTypeWorkspaceAddress: lea rax, [rel InuConsoleTrueTypeWorkspace]
    ret
InuX64GetConsoleTrueTypeWorkspaceLength: mov rax, 262144
    ret
InuX64GetConsoleTrueTypeCacheAddress: lea rax, [rel InuConsoleTrueTypeCache]
    ret
InuX64GetConsoleTrueTypeCacheLength: mov rax, 2101248
    ret
"@ | Set-Content -LiteralPath $consoleFontAsm -Encoding Ascii
if (-not (Test-Path -LiteralPath $consoleTtf -PathType Leaf)) {
    throw 'The project TrueType console font disappeared before native build.'
}
Write-Host '[ OK ] Graphics console font payload is ready for image staging.'


# Each stage owns a separate cache; QEMU acceptance always executes.
. (Join-Path $root 'scripts\StageCache.ps1')
$stageCacheDirectory = Join-Path $root 'Artifacts\StageCache'
$commonStageInputs = @((Join-Path $root 'Build-Inu.ps1'), (Join-Path $root 'scripts\StageCache.ps1'), (Join-Path $root 'toolchain\Inu.Toolchain.json'))
$dotnetStageInputs = @($dotnet) + @((Get-InuStageFiles -Paths @((Join-Path (Split-Path -Parent $dotnet) 'sdk'),(Join-Path (Split-Path -Parent $dotnet) 'host'),(Join-Path (Split-Path -Parent $dotnet) 'shared')) -Outputs).FullName)
$ilcStageInputs = @((Get-InuStageFiles -Paths @((Split-Path -Parent $ilc)) -Outputs).FullName)
$dotnetIdentity = Get-InuStageHash -Paths $dotnetStageInputs
$ilcIdentity = Get-InuStageHash -Paths $ilcStageInputs
$dotnetStageInputs = @($dotnet)
$ilcStageInputs = @($ilc)
function Invoke-InuBuildStage {
    param([string]$Stage, [string]$FilePath, [string[]]$Arguments,
        [string[]]$Inputs, [string[]]$Outputs, [string[]]$CleanDirectories = @(), [switch]$MutableOutputs)
    $identity = @()
    if ($FilePath -eq $dotnet) { $identity += $dotnetIdentity }
    if ($Inputs -contains $ilc) { $identity += $ilcIdentity }
    Invoke-InuCachedAction -Stage $Stage -CacheDirectory $stageCacheDirectory `
        -Inputs ($commonStageInputs + @($FilePath) + $Inputs) -Outputs $Outputs `
        -KeyArguments (@($FilePath) + $Arguments + $identity) -Force:$ForceRebuild -Dry:$DryRun -MutableOutputs:$MutableOutputs `
        -Action {
            if (-not $DryRun) { foreach ($clean in $CleanDirectories) { if (Test-Path -LiteralPath $clean) { Remove-Item -LiteralPath $clean -Recurse -Force } } }
            $null = Invoke-InuCapturedStage -Stage $Stage -FilePath $FilePath -Arguments $Arguments
        }
}
if ($ForceRebuild) { Write-Host '[INFO] Force Rebuild: every stage cache is bypassed.' }
Write-Host "Building x64 native kernel entry objects and the minimal UEFI FAT32 loader."
$entryNasmArguments = @("-f", "win64")
if ($Configuration -eq "Debug") {
    $entryNasmArguments += "-dINU_DEBUG=1"
    Write-Host "Debug kernel image rendezvous enabled for source-breakpoint relocation."
}
$entryNasmArguments += (Join-Path $root "native\x64\Entry.asm")
$entryNasmArguments += "-o"
$entryNasmArguments += (Join-Path $nativeOutput "Entry.obj")
Invoke-InuBuildStage -Stage 'Native Entry' -FilePath $nasm -Arguments $entryNasmArguments -Inputs @((Join-Path $root 'native\x64\Entry.asm')) -Outputs @((Join-Path $nativeOutput 'Entry.obj'))
$bootLoaderSource = Join-Path $root 'native\x64\BootLoader.c'
$bootLoaderObject = Join-Path $nativeOutput 'BootLoader.obj'
Invoke-InuBuildStage -Stage 'Native UEFI loader' -FilePath $clang -Arguments @('-target','x86_64-pc-windows-msvc','-ffreestanding','-fno-stack-protector','-fno-builtin','-fno-exceptions','-fno-unwind-tables','-fno-asynchronous-unwind-tables','-c',$bootLoaderSource,'-o',$bootLoaderObject) -Inputs @($bootLoaderSource) -Outputs @($bootLoaderObject)
foreach ($nativeName in @('Cpu','Runtime','ExceptionHandling','Descriptors','Interrupts','InterruptControllers','Paging','Syscalls','UserMode')) {
    $nativeSource = Join-Path $root "native\x64\$nativeName.asm"
    $nativeObject = Join-Path $nativeOutput "$nativeName.obj"
    $nativeArguments = @('-f','win64')
    if ($nativeName -eq 'Syscalls' -and $Configuration -eq 'Debug') { $nativeArguments += '-dINU_DEBUG=1' }
    $nativeArguments += @($nativeSource,'-o',$nativeObject)
    Invoke-InuBuildStage -Stage "Native $nativeName" -FilePath $nasm -Arguments $nativeArguments -Inputs @($nativeSource) -Outputs @($nativeObject)
}
Invoke-InuBuildStage -Stage 'Native console font' -FilePath $nasm -Arguments @('-f','win64',$consoleFontAsm,'-o',$consoleFontObj) -Inputs @($consoleFontAsm,$consoleTtf) -Outputs @($consoleFontObj)

Write-Host "Kernel build mode: selected project + required build tools only."
$requiredToolProjects = @(
    "src\Inu.ManagedCompiler\Inu.ManagedCompiler.csproj",
    "src\Inu.Linker\Inu.Linker.csproj",
    "src\Inu.ImageBuilder\Inu.ImageBuilder.csproj",
    "src\Inu.QemuLauncher\Inu.QemuLauncher.csproj",
    "src\Inu.ProjectCreator\Inu.ProjectCreator.csproj",
    "src\Inu.UserlandCompiler\Inu.UserlandCompiler.csproj"
)
foreach ($requiredToolProjectRelative in $requiredToolProjects) {
    $requiredToolProject = Join-Path $root $requiredToolProjectRelative
    Invoke-InuBuildStage -Stage "Tool $requiredToolProjectRelative" -FilePath $dotnet -Arguments @(
        "build", $requiredToolProject, "--configuration", $Configuration, "--property:Platform=x64", "--nologo"
    ) -Inputs ($dotnetStageInputs + @(Get-InuProjectStageInputs -ProjectFile $requiredToolProject)) -Outputs @((Join-Path (Split-Path -Parent $requiredToolProject) "bin\x64\$Configuration\net10.0"))
}

# Required tools are explicitly built with Platform="x64" above. The SDK
# must execute those exact outputs rather than a stale bin\$Configuration copy
# left by an older build that did not use the platform-specific output folder.
$requiredToolOutput = Join-Path "bin\x64" "$Configuration\net10.0"
$compiler = Join-Path $root "src\Inu.ManagedCompiler\$requiredToolOutput\Inu.ManagedCompiler.dll"
$linker = Join-Path $root "src\Inu.Linker\$requiredToolOutput\Inu.Linker.dll"
$imageBuilder = Join-Path $root "src\Inu.ImageBuilder\$requiredToolOutput\Inu.ImageBuilder.dll"
$qemuLauncher = Join-Path $root "src\Inu.QemuLauncher\$requiredToolOutput\Inu.QemuLauncher.dll"
$userlandCompiler = Join-Path $root "src\Inu.UserlandCompiler\$requiredToolOutput\Inu.UserlandCompiler.dll"
foreach ($tool in @(
    @{Name='Inu.ManagedCompiler';Path=$compiler},
    @{Name='Inu.Linker';Path=$linker},
    @{Name='Inu.ImageBuilder';Path=$imageBuilder},
    @{Name='Inu.QemuLauncher';Path=$qemuLauncher},
    @{Name='Inu.UserlandCompiler';Path=$userlandCompiler}
)) {
    if (-not (Test-Path -LiteralPath $tool.Path -PathType Leaf)) {
        throw "$($tool.Name) was not produced: $($tool.Path)"
    }
}

$projectCreator = Join-Path $root "src\Inu.ProjectCreator\$requiredToolOutput\Inu.ProjectCreator.dll"
if (-not (Test-Path -LiteralPath $projectCreator -PathType Leaf)) {
    throw "Inu.ProjectCreator was not produced: $projectCreator"
}
Write-Host "[ OK ] ManagedCompiler runtime: $compiler"
Write-Host "[ OK ] Linker runtime         : $linker"
Write-Host "[ OK ] ImageBuilder runtime   : $imageBuilder"
Write-Host "[ OK ] QemuLauncher runtime   : $qemuLauncher"
Write-Host "[ OK ] UserlandCompiler runtime: $userlandCompiler"
Write-Host "[ OK ] ProjectCreator runtime : $projectCreator"

$externalKernelDirectory = Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::UserProfile)) "Source\Repos\InuKernel"
if ((Test-Path -LiteralPath $externalKernelDirectory -PathType Container)) {
    Invoke-InuBuildStage -Stage "Refresh external Inu kernel project" -FilePath $dotnet -Arguments @(
        $projectCreator, "create", "--output", $externalKernelDirectory, "--sdk-root", $root
    ) -Inputs (@($externalKernelDirectory,(Join-Path $root 'templates'),(Join-Path $root 'src\Userland')) + @((Get-InuStageFiles -Paths @((Split-Path -Parent $projectCreator)) -Outputs).FullName)) -Outputs (@((Get-InuStageFiles -Paths @($externalKernelDirectory)).FullName) + @((Join-Path $externalKernelDirectory 'InuProject.json')))

    $legacyRootKernel = Join-Path $externalKernelDirectory "Kernel.cs"
    if (Test-Path -LiteralPath $legacyRootKernel -PathType Leaf) {
        throw "External kernel migration left an obsolete root Kernel.cs in place: $legacyRootKernel"
    }

    $externalUserKernel = Join-Path $externalKernelDirectory "Kernel\Kernel.cs"
    $externalKernelSource = Ensure-HighLevelUserKernelSource `
        -KernelPath $externalUserKernel `
        -SdkRoot $root `
        -DisplayName "external user kernel"
    foreach ($forbiddenKernelToken in @("DllImport", "internal static class Native", "WritePort8", "RuntimeExport", "FramebufferConsole", "0x3F8")) {
        if ($externalKernelSource.IndexOf($forbiddenKernelToken, [StringComparison]::Ordinal) -ge 0) {
            throw "External user kernel still exposes low-level token '$forbiddenKernelToken': $externalUserKernel"
        }
    }

    Write-Host "[ OK ] External kernel project refreshed: $externalKernelDirectory"
    Write-Host "[ OK ] External user kernel is high-level only: $externalUserKernel"
}

function Ensure-HighLevelUserKernelSource {
    param(
        [Parameter(Mandatory = $true)][string]$KernelPath,
        [Parameter(Mandatory = $true)][string]$SdkRoot,
        [Parameter(Mandatory = $true)][string]$DisplayName
    )

    $canonicalKernelPath = Join-Path $SdkRoot "templates\\InuKernel\\Kernel\\Kernel.cs"
    if (-not (Test-Path -LiteralPath $canonicalKernelPath -PathType Leaf)) {
        throw "Canonical Inu high-level user kernel template was not found: $canonicalKernelPath"
    }

    $canonicalSource = [IO.File]::ReadAllText($canonicalKernelPath)
    if ([string]::IsNullOrWhiteSpace($canonicalSource)) {
        throw "Canonical Inu high-level user kernel template is empty: $canonicalKernelPath"
    }

    $needsRepair = -not (Test-Path -LiteralPath $KernelPath -PathType Leaf)
    if (-not $needsRepair) {
        $currentSource = [IO.File]::ReadAllText($KernelPath)
        $needsRepair = [string]::IsNullOrWhiteSpace($currentSource)
    }

    if ($needsRepair) {
        $kernelDirectory = Split-Path -Parent $KernelPath
        if (-not (Test-Path -LiteralPath $kernelDirectory -PathType Container)) {
            New-Item -ItemType Directory -Path $kernelDirectory -Force | Out-Null
        }
        [IO.File]::WriteAllText($KernelPath, $canonicalSource)
        Write-Host "[ OK ] Repaired empty $DisplayName from canonical SDK template: $KernelPath"
    }

    $verifiedSource = [IO.File]::ReadAllText($KernelPath)
    if ([string]::IsNullOrWhiteSpace($verifiedSource)) {
        throw "$DisplayName is empty immediately before compilation: $KernelPath"
    }

    $verifiedLength = (Get-Item -LiteralPath $KernelPath).Length
    Write-Host "[ OK ] $DisplayName source verified: $verifiedLength bytes"
    return $verifiedSource
}

$defaultKernelDirectory = Join-Path $root "src\Inu.Kernel.Bootstrap"
$defaultProjectManifest = Join-Path $defaultKernelDirectory "InuProject.json"
$projectManifest = if ([string]::IsNullOrWhiteSpace($Project)) {
    $defaultProjectManifest
} elseif ([IO.Path]::IsPathRooted($Project)) {
    [IO.Path]::GetFullPath($Project)
} else {
    [IO.Path]::GetFullPath((Join-Path $root $Project))
}

if (-not (Test-Path -LiteralPath $projectManifest -PathType Leaf)) {
    throw "Inu project manifest was not found: $projectManifest"
}

if (-not [string]::Equals([IO.Path]::GetFullPath($projectManifest), [IO.Path]::GetFullPath($defaultProjectManifest), [StringComparison]::OrdinalIgnoreCase)) {
    $selectedProjectDirectory = Split-Path -Parent $projectManifest
    Invoke-InuBuildStage -Stage "Refresh selected Inu project" -FilePath $dotnet -Arguments @(
        $projectCreator, "create", "--output", $selectedProjectDirectory, "--sdk-root", $root
    ) -Inputs (@($selectedProjectDirectory,(Join-Path $root 'templates'),(Join-Path $root 'src\Userland')) + @((Get-InuStageFiles -Paths @((Split-Path -Parent $projectCreator)) -Outputs).FullName)) -Outputs (@((Get-InuStageFiles -Paths @($selectedProjectDirectory)).FullName) + @((Join-Path $selectedProjectDirectory 'InuProject.json')))
    $projectManifest = Join-Path $selectedProjectDirectory "InuProject.json"

    $selectedOsName = $null
    $ideConfigPath = Join-Path $selectedProjectDirectory "Inu.json"
    if (Test-Path -LiteralPath $ideConfigPath -PathType Leaf) {
        $ideConfig = Get-Content -LiteralPath $ideConfigPath -Raw | ConvertFrom-Json
        $selectedOsName = [string]$ideConfig.name
    }
    if ([string]::IsNullOrWhiteSpace($selectedOsName)) {
        $manifestForName = Get-Content -LiteralPath (Join-Path $selectedProjectDirectory "InuProject.json") -Raw | ConvertFrom-Json
        $selectedOsName = [string]$manifestForName.Name
    }
    if ([string]::IsNullOrWhiteSpace($selectedOsName)) {
        throw "Could not determine the OS name needed to locate Kernel\<OSName>\Kernel.cs."
    }
    $selectedOsSegment = ($selectedOsName -replace '[^A-Za-z0-9_.-]', '_')
    $selectedUserKernel = Join-Path $selectedProjectDirectory ("Kernel\" + $selectedOsSegment + "\Kernel.cs")
    if (-not (Test-Path -LiteralPath $selectedUserKernel -PathType Leaf)) {
        throw "Coder-owned kernel source is missing: $selectedUserKernel. Kath must create Kernel\<OSName>\Kernel.cs; Inu will not recreate the obsolete Kernel\Kernel.cs."
    }
    $selectedKernelSource = [IO.File]::ReadAllText($selectedUserKernel)
    if ([string]::IsNullOrWhiteSpace($selectedKernelSource)) {
        throw "Coder-owned kernel source is empty: $selectedUserKernel"
    }
    foreach ($forbiddenKernelToken in @("DllImport", "class Native", "WritePort8", "RuntimeExport", "NativeEntry", "FramebufferConsole", "0x3F8", "InitializeSerial")) {
        if ($selectedKernelSource.IndexOf($forbiddenKernelToken, [StringComparison]::Ordinal) -ge 0) {
            throw "Coder-owned kernel source still exposes low-level token '$forbiddenKernelToken': $selectedUserKernel"
        }
    }
    Write-Host "[ OK ] Coder-owned kernel source: $selectedUserKernel"
}

Write-Host "[ OK ] C# kernel project manifest: $projectManifest"

Write-Host "Inu configured target architecture will be validated before managed compilation."
try {
    $configuredProject = Get-Content -LiteralPath $projectManifest -Raw | ConvertFrom-Json
    $configuredArchitecture = [string]$configuredProject.TargetArchitecture
    $configuredKernelModel = [string]$configuredProject.KernelModel
    if ([string]::IsNullOrWhiteSpace($configuredArchitecture)) { $configuredArchitecture = "x64" }
    if ([string]::IsNullOrWhiteSpace($configuredKernelModel)) { $configuredKernelModel = "Monolithic" }
    Write-Host "[ OK ] Target architecture: $configuredArchitecture"
    Write-Host "[ OK ] Kernel model       : $configuredKernelModel"
    if ($configuredArchitecture -notin @("x64", "X64")) {
        throw "The project is configured for '$configuredArchitecture', but this Inu installation currently contains only the x64 architecture pack. Install/implement the matching architecture pack or reopen Inu: Configure Project and select x64. Inu will not silently build an x64 kernel for a different configured target."
    }
} catch {
    if ($_.Exception.Message -like "The project is configured for*") { throw }
    throw "Could not validate Inu project configuration: $($_.Exception.Message)"
}

$projectData = Get-Content -LiteralPath $projectManifest -Raw | ConvertFrom-Json
$projectDirectory = Split-Path -Parent $projectManifest
$outputDirectory = if ([IO.Path]::IsPathRooted([string]$projectData.OutputDirectory)) {
    [IO.Path]::GetFullPath([string]$projectData.OutputDirectory)
} else {
    [IO.Path]::GetFullPath((Join-Path $projectDirectory ([string]$projectData.OutputDirectory)))
}
$imagePath = Join-Path $outputDirectory (([string]$projectData.Name) + ".img")

$dry = @()
if ($DryRun) { $dry = @("--dry-run") }

$kernelProjectFile = if ([IO.Path]::IsPathRooted([string]$projectData.ProjectFile)) { [IO.Path]::GetFullPath([string]$projectData.ProjectFile) } else { [IO.Path]::GetFullPath((Join-Path $projectDirectory ([string]$projectData.ProjectFile))) }
$projectMetadata = @($projectManifest) + @(Get-ChildItem -LiteralPath $projectDirectory -File | Where-Object { $_.Extension -in @('.props','.targets','.csproj') -or $_.Name -eq 'Inu.Configuration.json' } | ForEach-Object { $_.FullName })
$kernelInputs = if (Test-Path -LiteralPath (Join-Path $projectDirectory 'Kernel') -PathType Container) {
    # The copied SDK includes userland libraries that are not kernel compile inputs.
    @((Get-InuStageFiles -Paths @((Join-Path $projectDirectory 'Boot'),(Join-Path $projectDirectory 'Kernel'))).FullName) + @(Get-InuProjectStageInputs -ProjectFile (Join-Path $root 'src\Inu.Freestanding.CoreLib\Inu.Freestanding.CoreLib.csproj'))
} else { @(Get-InuProjectStageInputs -ProjectFile $kernelProjectFile) }
$kernelInputs += $projectMetadata
$nativeAotObject = Join-Path $outputDirectory ('NativeAot\' + [string]$projectData.Name + '.obj')
$compileManifestPath = Join-Path $outputDirectory 'Inu.Compile.json'
$kernelBin = Join-Path $outputDirectory (([string]$projectData.Name) + '.bin')
$loaderBin = Join-Path $outputDirectory (([string]$projectData.Name) + '.bootx64.efi')
$compileOutputs = @($nativeAotObject,$compileManifestPath,(Join-Path $outputDirectory 'ManagedIL'),(Join-Path $outputDirectory 'NativeAot'))
$linkOutputs = @($kernelBin,$loaderBin,(Join-Path $outputDirectory (([string]$projectData.Name) + '.map')))
if ($Configuration -eq 'Debug') { $linkOutputs += @((Join-Path $outputDirectory 'Inu.DebugSymbols.json'), (Join-Path $outputDirectory (([string]$projectData.Name) + '.pdb'))) }
    $compileArgs = @($compiler, "compile", $projectManifest, "--dotnet", $dotnet, "--ilc", $ilc, "--configuration", $Configuration, "--sdk-root", $root) + $dry
    if ($runtimeConformanceEnabled) { $compileArgs += '--runtime-conformance' }
    Invoke-InuBuildStage -Stage "Managed IL + NativeAOT ILC" -FilePath $dotnet -Arguments $compileArgs -Inputs ($kernelInputs + $dotnetStageInputs + $ilcStageInputs + @((Get-InuStageFiles -Paths @((Split-Path -Parent $compiler)) -Outputs).FullName)) -Outputs $compileOutputs -CleanDirectories @((Join-Path $outputDirectory 'ManagedIL'),(Join-Path $outputDirectory 'NativeAot'))

    $linkArgs = @($linker, "link", $projectManifest, "--lld-link", $lldLink, "--llvm-nm", $llvmNm, "--nasm", $nasm, "--native-root", $nativeOutput) + $dry
    Invoke-InuBuildStage -Stage "Native link" -FilePath $dotnet -Arguments $linkArgs -Inputs (@($projectManifest,$compileManifestPath,$nativeAotObject,$lldLink,$llvmNm,$nasm) + @((Get-InuStageFiles -Paths @((Split-Path -Parent $linker)) -Outputs).FullName) + @((Get-InuStageFiles -Paths @((Join-Path $outputDirectory 'NativeAot'),(Join-Path $outputDirectory 'ManagedIL')) -Outputs).FullName) + @(Get-ChildItem -LiteralPath $nativeOutput -Filter "*.obj" | Where-Object { $_.Name -notin @("UserEntry.obj","UserExceptionHandling.obj") } | ForEach-Object { $_.FullName })) -Outputs $linkOutputs

    $userlandArgs = @(
        $userlandCompiler, "compile", $projectManifest,
        "--dotnet", $dotnet,
        "--ilc", $ilc,
        "--lld-link", $lldLink,
        "--nasm", $nasm,
        "--native-root", $nativeOutput,
        "--sdk-root", $root,
        "--configuration", $Configuration
    )
    $userlandInputs = $projectMetadata + $dotnetStageInputs + $ilcStageInputs + @($lldLink,$nasm,(Join-Path $nativeOutput 'Runtime.obj'),(Join-Path $root 'native\x64\UserEntry.asm'),(Join-Path $root 'native\x64\ExceptionHandling.asm')) + @((Get-InuStageFiles -Paths @((Split-Path -Parent $userlandCompiler)) -Outputs).FullName)
    $userlandInputs += @(Get-ChildItem -LiteralPath (Join-Path $projectDirectory 'Userland') -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -ne 'Provided' } | ForEach-Object { $_.FullName }) + @((Join-Path $projectDirectory 'Userland\Provided\Shell'),(Join-Path $root 'src\Userland\Shell'))
    foreach ($library in @('Inu.Freestanding.CoreLib','Inu.Userland.RuntimeSupport','Inu.Runtime.UserlandNativeAot','Inu.Userland.Runtime')) {
        $userlandInputs += @(Get-InuProjectStageInputs -ProjectFile (Join-Path $root "src\$library\$library.csproj"))
    }
    $userlandOutputs = @()
    $coderUserlandDirectories = @(Get-ChildItem -LiteralPath (Join-Path $projectDirectory 'Userland') -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -ne 'Provided' })
    foreach ($coderDirectory in $coderUserlandDirectories) {
        if (Test-Path -LiteralPath (Join-Path $coderDirectory.FullName 'Shell.cs')) { $userlandOutputs += (Join-Path $outputDirectory 'UserlandApps\Shell') }
        if (@(Get-ChildItem -LiteralPath (Join-Path $coderDirectory.FullName 'Commands') -Filter '*.cs' -File -ErrorAction SilentlyContinue).Count) { $userlandOutputs += (Join-Path $outputDirectory 'UserlandApps\Commands') }
    }
    if ($ForceRebuild) { $userlandArgs += '--force-rebuild' }
    Invoke-InuBuildStage -Stage "Ring-3 userland executables" -FilePath $dotnet -Arguments $userlandArgs -Inputs $userlandInputs -Outputs $userlandOutputs

    if (-not $DryRun) {
        $userlandApps = Join-Path $outputDirectory "UserlandApps"
        New-Item -ItemType Directory -Path $userlandApps -Force | Out-Null
        $desktopDir = Join-Path $projectDirectory "Userland\Provided\Desktop"
        $loginDir = Join-Path $projectDirectory "Userland\Provided\Login"
        $desktopAsm = Join-Path $desktopDir "Desktop.asm"
        $loginAsm = Join-Path $loginDir "Login.asm"
        $desktopObj = Join-Path $userlandApps "INU-DESKTOP.obj"
        $loginObj = Join-Path $userlandApps "INU-LOGIN.obj"
        $desktopExe = Join-Path $userlandApps "INU-DESKTOP.EXE"
        $loginExe = Join-Path $userlandApps "INU-LOGIN.EXE"
        foreach ($required in @($desktopAsm,$loginAsm,(Join-Path $desktopDir "INU-HEX.BGRA"),(Join-Path $loginDir "LOGIN.BGRA"))) {
            if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { throw "Graphical userland source/asset is missing: $required" }
        }
        foreach ($gui in @(
            @{Name='Desktop';Directory=$desktopDir;Source=$desktopAsm;Object=$desktopObj;Exe=$desktopExe;Entry='InuDesktopEntry'},
            @{Name='Login';Directory=$loginDir;Source=$loginAsm;Object=$loginObj;Exe=$loginExe;Entry='InuLoginEntry'}
        )) {
            Push-Location $gui.Directory
            try {
                Invoke-InuBuildStage -Stage ("GUI assemble " + $gui.Name) -FilePath $nasm -Arguments @('-f','win64',(Split-Path -Leaf $gui.Source),'-o',$gui.Object) -Inputs @($gui.Directory) -Outputs @($gui.Object)
            } finally { Pop-Location }
            Invoke-InuBuildStage -Stage ("GUI link " + $gui.Name) -FilePath $lldLink -Arguments @('/nologo','/subsystem:native','/machine:x64','/nodefaultlib',('/entry:' + $gui.Entry),'/base:0x0000400000100000','/fixed',('/out:' + $gui.Exe),$gui.Object) -Inputs @($gui.Object) -Outputs @($gui.Exe)
        }
        Write-Host "[ OK ] Ring-3 GUI applications: $desktopExe, $loginExe"
    }

    $imageArgs = @($imageBuilder, "create", $projectManifest, "--output", $imagePath, "--sdk-root", $root, "--ide-root", $ideRoot) + $dry
    $imageInputs = $projectMetadata + $linkOutputs + @($consoleTtf, (Join-Path $root 'Assets\System')) + @((Get-InuStageFiles -Paths @((Split-Path -Parent $imageBuilder)) -Outputs).FullName) + @(Get-ChildItem -LiteralPath (Join-Path $outputDirectory 'UserlandApps') -Recurse -File -Filter '*.EXE' | ForEach-Object { $_.FullName })
    Invoke-InuBuildStage -Stage "FAT32 system image" -FilePath $dotnet -Arguments $imageArgs -Inputs $imageInputs -Outputs @($imagePath,(Join-Path $outputDirectory 'Inu.Image.json')) -MutableOutputs


if ($NoRun -or -not $Run) {
    Write-Host "[ OK ] Inu x64 NativeAOT build and FAT32 image creation completed."
    if ($NoRun) {
        Write-Host "QEMU launch was skipped because -NoRun was supplied."
    } else {
        Write-Host "QEMU launch is disabled for a normal build. Supply -Run or use the Visual Studio Run command to launch it."
    }
    exit 0
}

$qemu = Find-Executable -DisplayName "QEMU x64 system emulator" -Candidates @(
    (Get-RecordedPath @("qemuSystemX64", "qemu-system-x86_64.exe")),
    "%ProgramFiles%\qemu\qemu-system-x86_64.exe",
    "%ProgramFiles(x86)%\qemu\qemu-system-x86_64.exe",
    "%LOCALAPPDATA%\Programs\qemu\qemu-system-x86_64.exe",
    "%LOCALAPPDATA%\Microsoft\WinGet\Links\qemu-system-x86_64.exe",
    ((Get-Command qemu-system-x86_64.exe -ErrorAction SilentlyContinue).Source)
)
$ovmfCode = Find-Firmware -DisplayName "x64 OVMF code firmware" -RecordedPath (Get-RecordedPath @("ovmfCodeX64", "ovmfCode")) -QemuPath $qemu -FileNames @("edk2-x86_64-code.fd", "OVMF_CODE.fd")
$ovmfVars = Find-Firmware -DisplayName "x64 OVMF variable-store template" -RecordedPath (Get-RecordedPath @("ovmfVarsX64", "ovmfVars")) -QemuPath $qemu -FileNames @("edk2-i386-vars.fd", "edk2-x86_64-vars.fd", "OVMF_VARS.fd")
Write-Host "[ OK ] qemu    : $qemu"
Write-Host "[ OK ] OVMF code: $ovmfCode"
Write-Host "[ OK ] OVMF vars: $ovmfVars"

$effectiveBootTimeoutSeconds = if ($runtimeConformanceEnabled) { [Math]::Max($BootTimeoutSeconds, 90) } else { $BootTimeoutSeconds }
if ($runtimeConformanceEnabled -and $effectiveBootTimeoutSeconds -ne $BootTimeoutSeconds) {
    Write-Host ("[INFO] Explicit in-kernel runtime conformance uses a {0}s bounded boot window; normal OS runs keep the {1}s window." -f $effectiveBootTimeoutSeconds,$BootTimeoutSeconds)
}
$qemuArgs = @($qemuLauncher, "run", $projectManifest, "--qemu", $qemu, "--image", $imagePath, "--ovmf-code", $ovmfCode, "--ovmf-vars", $ovmfVars, "--timeout-seconds", [string]$effectiveBootTimeoutSeconds) + $dry
$qemuStageHardTimeout = ($effectiveBootTimeoutSeconds * 2) + 65
$null = Invoke-InuCapturedStage -Stage "QEMU runtime acceptance" -FilePath $dotnet -Arguments $qemuArgs -TimeoutSeconds $qemuStageHardTimeout

# Surface the exact accepted run identity to Kath. Successful captured stages normally keep
# child stdout in the per-stage log, so without these session-local lines the IDE would have
# to rediscover the run through the shared Inu.Run.json pointer and could attach to stale data.
$runManifestPath = Join-Path $outputDirectory "Inu.Run.json"
if (-not (Test-Path -LiteralPath $runManifestPath -PathType Leaf)) {
    Write-Host "[FAIL] QEMU acceptance completed without publishing Inu.Run.json: $runManifestPath"
    exit 1
}
try {
    $acceptedRun = Get-Content -LiteralPath $runManifestPath -Raw | ConvertFrom-Json
} catch {
    Write-Host "[FAIL] QEMU acceptance published an unreadable Inu.Run.json: $runManifestPath"
    exit 1
}
if ([int]$acceptedRun.qemuProcessId -le 0 -or [string]::IsNullOrWhiteSpace([string]$acceptedRun.serialLog)) {
    Write-Host "[FAIL] QEMU acceptance published an incomplete Inu.Run.json: $runManifestPath"
    exit 1
}
Write-Host ("[INFO] Accepted QEMU PID: {0}" -f [int]$acceptedRun.qemuProcessId)
Write-Host ("[INFO] Accepted QEMU serial log: {0}" -f [string]$acceptedRun.serialLog)
Write-Host ("[INFO] Accepted QEMU stop report: {0}" -f [string]$acceptedRun.diagnosticReport)
Write-Host ("[INFO] Accepted QEMU run directory: {0}" -f [string]$acceptedRun.runDirectory)
Write-Host ("[INFO] Accepted QEMU UTC: {0}" -f [string]$acceptedRun.acceptedUtc)

if ($runtimeConformanceEnabled) {
    New-Item -ItemType Directory -Path $runtimeValidationDirectory -Force | Out-Null
    Set-Content -LiteralPath $runtimeValidationStamp -Encoding ASCII -Value $currentSdkCodeFingerprint
    Write-Host '[ OK ] Explicit in-kernel runtime conformance passed for the current SDK fingerprint.'
}

Write-Host "[ OK ] Inu x64 NativeAOT boot-and-run acceptance completed."
