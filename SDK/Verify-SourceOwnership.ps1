[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$SdkRoot = [IO.Path]::GetFullPath((Split-Path -Parent $MyInvocation.MyCommand.Path))
$InuRoot = Split-Path -Parent $SdkRoot
$ProductRoot = Split-Path -Parent $InuRoot

function Fail([string]$Message) { throw "[FAIL] Source ownership: $Message" }

$canonicalShell = Join-Path $SdkRoot 'src\Userland\Shell\InuShell.cs'
$shells = @(Get-ChildItem -LiteralPath $SdkRoot -Recurse -File -Filter 'InuShell.cs' -ErrorAction Stop)
if ($shells.Count -ne 1 -or -not [string]::Equals([IO.Path]::GetFullPath($shells[0].FullName), [IO.Path]::GetFullPath($canonicalShell), [StringComparison]::OrdinalIgnoreCase)) {
    Fail ('InuShell.cs must exist exactly once at src\Userland\Shell\InuShell.cs. Found: ' + (($shells | ForEach-Object FullName) -join ', '))
}

foreach ($legacy in @(
    (Join-Path $InuRoot 'toolchains\Inu.NativeAot.Bootstrap.cs'),
    (Join-Path $InuRoot 'toolchains\Inu.NativeAot.Bootstrap.csproj'),
    (Join-Path $SdkRoot 'src\Inu.Kernel.Bootstrap\Kernel.cs'),
    (Join-Path $SdkRoot 'src\Inu.Kernel.Bootstrap\KernelPanicTransport.cs'),
    (Join-Path $SdkRoot 'src\Inu.Kernel.Bootstrap\KernelStructuredLogging.cs')
)) {
    if (Test-Path -LiteralPath $legacy) { Fail "legacy duplicate source must not exist: $legacy" }
}

foreach ($templateImplementation in @('Boot','HAL','Startup','Kernel')) {
    $directory = Join-Path $SdkRoot ("templates\InuKernel\" + $templateImplementation)
    if (-not (Test-Path -LiteralPath $directory -PathType Container)) { continue }
    $source = @(Get-ChildItem -LiteralPath $directory -Recurse -File -Filter '*.cs' -ErrorAction Stop)
    if ($source.Count -ne 0) { Fail "templates may contain scaffolding/configuration but not C# implementations: $($source[0].FullName)" }
}

$hashOwners = @{}
foreach ($file in @(Get-ChildItem -LiteralPath $SdkRoot -Recurse -File -Filter '*.cs' -ErrorAction Stop | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' })) {
    $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    if ($hashOwners.ContainsKey($hash)) { Fail "byte-identical C# implementations found: $($hashOwners[$hash]) and $($file.FullName)" }
    $hashOwners[$hash] = $file.FullName
}

# Storage format/provisioning Byte* zero-fill regressions must be explicit Byte writes.
foreach ($relative in @('src\Inu.Filesystem.FatFs\FatFs.Format.cs','src\Inu.Kernel.Storage\KernelDiskProvisioning.cs')) {
    $path = Join-Path $SdkRoot $relative
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { Fail "storage validation source is missing: $relative" }
    $text = [IO.File]::ReadAllText($path)
    if ($text -match '\[[^\]]+\]\s*=\s*0U\s*;') { Fail "UInt32 zero assigned directly through an indexed Byte target in $relative; use an explicit (Byte) cast" }
}


# Kath/Inu has one host executable contract.  SDK host implementations remain modular
# source projects but must not emit their own Windows apphost executables.
foreach ($module in @('Inu.ManagedCompiler','Inu.Linker','Inu.ImageBuilder','Inu.QemuLauncher','Inu.ProjectCreator','Inu.UserlandCompiler')) {
    $project = Join-Path $SdkRoot ("src\$module\$module.csproj")
    if (-not (Test-Path -LiteralPath $project -PathType Leaf)) { Fail "host module project is missing: $project" }
    $projectText = [IO.File]::ReadAllText($project)
    if ($projectText -notmatch '<UseAppHost>\s*false\s*</UseAppHost>') { Fail "$module must be hosted through Inu\Bin\inu.exe and must not emit a separate apphost executable" }
}
$buildScript = [IO.File]::ReadAllText((Join-Path $SdkRoot 'Build-Inu.ps1'))
if ($buildScript -notmatch 'Consolidated Inu host') { Fail 'Build-Inu.ps1 must route SDK host operations through the consolidated Inu host.' }

$rootVersion = Join-Path $ProductRoot 'VERSION'
if (-not (Test-Path -LiteralPath $rootVersion -PathType Leaf)) { Fail "authoritative root VERSION is missing: $rootVersion" }
foreach ($mirror in @((Join-Path $InuRoot 'VERSION'), (Join-Path (Join-Path $ProductRoot 'Kath') 'VERSION'))) {
    if (Test-Path -LiteralPath $mirror) { Fail "per-product VERSION mirror must not exist: $mirror" }
}

Write-Host '[ OK ] Source ownership verified: one implementation authority per audited component.'
