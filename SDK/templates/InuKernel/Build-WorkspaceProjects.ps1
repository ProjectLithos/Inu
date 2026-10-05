[CmdletBinding()]
param(
    [string]$SdkRoot = "",
    [ValidateSet("Debug", "Release")][string]$Configuration = "Release"
)
$ErrorActionPreference = "Stop"
$workspaceRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($SdkRoot)) { $SdkRoot = $env:INU_SDK_ROOT }
if ([string]::IsNullOrWhiteSpace($SdkRoot)) { $SdkRoot = "C:\Inu" }
$dotnet = Join-Path $SdkRoot ".toolchain\DotNet\dotnet.exe"
if (-not (Test-Path -LiteralPath $dotnet -PathType Leaf)) { throw "Inu repository-pinned dotnet.exe was not found: $dotnet" }
$graphFile = Join-Path $workspaceRoot "Configuration\WorkspaceProjects.txt"
if (-not (Test-Path -LiteralPath $graphFile -PathType Leaf)) {
    Write-Host "[INFO] No configured workspace graph exists. Apply Inu Configuration Pages first."
    exit 0
}
$projects = @(Get-Content -LiteralPath $graphFile |
    ForEach-Object { $_.Trim() } |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
    ForEach-Object { Join-Path $workspaceRoot $_ } |
    Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
    Sort-Object -Unique)
if ($projects.Count -eq 0) { Write-Host "[INFO] Configured workspace contains no independent projects."; exit 0 }
Write-Host "[INFO] Building $($projects.Count) configured independent Inu project(s)."
foreach ($project in $projects) {
    Write-Host "[INFO] Configured project build: $project"
    & $dotnet build $project --configuration $Configuration --nologo
    if ($LASTEXITCODE -ne 0) { throw "Configured workspace project build failed with exit code $LASTEXITCODE: $project" }
}
Write-Host "[ OK ] Configured Inu workspace projects built."
exit 0
