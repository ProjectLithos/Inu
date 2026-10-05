param([string]$Root = $PSScriptRoot, [switch]$ForceRebuild)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$Root = [IO.Path]::GetFullPath($Root)
$VersionPath = Join-Path $Root 'VERSION'
if(-not(Test-Path -LiteralPath $VersionPath -PathType Leaf)){throw "[FAIL] Inu VERSION is missing: $VersionPath"}
$Version = (Get-Content -LiteralPath $VersionPath -Raw).Trim()
if($Version -notmatch '^\d+\.\d+\.\d+$'){throw "[FAIL] Inu VERSION is invalid: '$Version'"}
$Bin = Join-Path $Root 'Bin'
$DotNet = Join-Path $Root '.toolchain\DotNet\dotnet.exe'
$Clang = Join-Path $Root '.toolchain\LLVM\bin\clang.exe'
$LldLink = Join-Path $Root '.toolchain\LLVM\bin\lld-link.exe'
function Ok([string]$m){Write-Host "[ OK ] $m"}
function Info([string]$m){Write-Host "[INFO] $m"}
function Fail([string]$m){throw "[FAIL] $m"}
try {
    Write-Host ''
    Write-Host '============================================================'
    Write-Host "Inu $Version bootstrap"
    Write-Host '============================================================'
    Write-Host "[INFO] Root: $Root"
    foreach ($p in @(
        'src\Common\Inu.Cli\Inu.Cli.csproj',
        'targets\X64\PC\UEFI\Target.json',
        'toolchains\Inu.Toolchain.json'
    )) { $full=Join-Path $Root $p; if(-not(Test-Path -LiteralPath $full)){Fail "Required source is missing: $full"} }
    foreach($tool in @($DotNet,$Clang,$LldLink)){if(-not(Test-Path -LiteralPath $tool -PathType Leaf)){Fail "Private tool is missing: $tool"}}
    New-Item -ItemType Directory -Force -Path $Bin | Out-Null
    $cliProject=Join-Path $Root 'src\Common\Inu.Cli\Inu.Cli.csproj'
    $out=Join-Path $Root 'Artifacts\Bootstrap\Inu'
    New-Item -ItemType Directory -Force -Path $out | Out-Null
    $Configuration = 'Release'
    $stageHelper = Join-Path $Root 'SDK\scripts\StageCache.ps1'
    . $stageHelper
    $stable=Join-Path $Bin 'inu.exe'
    $arguments=@('publish',$cliProject,'-c','Release','-r','win-x64','--self-contained','true','-p:PublishSingleFile=true','-p:DebugType=None','-p:DebugSymbols=false','-o',$out)
    $inputs=@($stageHelper,$DotNet,(Join-Path $Root 'Bootstrap-Inu.ps1')) + @(Get-InuProjectStageInputs -ProjectFile $cliProject) + @((Get-InuStageFiles -Paths @((Join-Path (Split-Path -Parent $DotNet) 'sdk'),(Join-Path (Split-Path -Parent $DotNet) 'host'),(Join-Path (Split-Path -Parent $DotNet) 'shared')) -Outputs).FullName)
    Invoke-InuCachedAction -Stage 'Inu bootstrap CLI' -CacheDirectory (Join-Path $Root 'Artifacts\StageCache') -Inputs $inputs -Outputs @($stable,$out) -KeyArguments $arguments -Force:$ForceRebuild -Action {
        & $DotNet @arguments
        if($LASTEXITCODE -ne 0){Fail "Bootstrap Inu CLI build failed with exit code $LASTEXITCODE."}
        $built=Join-Path $out 'inu.exe'; if(-not(Test-Path -LiteralPath $built)){Fail 'Bootstrap publish did not produce inu.exe.'}
        Copy-Item -LiteralPath $built -Destination $stable -Force
    }
    $reported=(& $stable --version | Select-Object -First 1).Trim()
    if($LASTEXITCODE -ne 0 -or $reported -ne $Version){Fail "inu.exe version mismatch. Expected $Version, got '$reported'."}
    Ok "Bin\inu.exe reports $reported."
    Info 'Running the Inu build for normal products...'
    $buildArguments=@('build','--root',$Root)
    if($ForceRebuild){$buildArguments+='--force-rebuild'}
    & $stable @buildArguments
    exit $LASTEXITCODE
} catch { Write-Host $_.Exception.Message; exit 1 }
