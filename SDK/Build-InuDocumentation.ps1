[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")][string]$Configuration = "Release",
    [switch]$Strict
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$dotnet = Join-Path $root ".toolchain\DotNet\dotnet.exe"
if (-not (Test-Path -LiteralPath $dotnet -PathType Leaf)) {
    throw "Repository-pinned dotnet.exe was not found: $dotnet. Run Install-InuToolchain.bat first."
}

$projectDirectory = Join-Path $root "src\Inu.DocumentationGenerator"
$project = Join-Path $projectDirectory "Inu.DocumentationGenerator.csproj"
$config = Join-Path $root "docs\Inu.Documentation.json"
if (-not (Test-Path -LiteralPath $config -PathType Leaf)) {
    throw "Documentation configuration was not found: $config"
}
$configData = Get-Content -LiteralPath $config -Raw | ConvertFrom-Json
$outputDirectory = [string]$configData.outputDirectory
if ([string]::IsNullOrWhiteSpace($outputDirectory)) {
    throw "Documentation configuration does not define outputDirectory: $config"
}
$siteRoot = if ([IO.Path]::IsPathRooted($outputDirectory)) {
    [IO.Path]::GetFullPath($outputDirectory)
} else {
    [IO.Path]::GetFullPath((Join-Path $root $outputDirectory))
}

Write-Host "[INFO] Building Inu SDK documentation generator."
& $dotnet build $project --configuration $Configuration --nologo
if ($LASTEXITCODE -ne 0) { throw "Documentation generator build failed with exit code $LASTEXITCODE." }

$generatorName = "Inu.DocumentationGenerator.dll"
$generatorCandidates = @(Get-ChildItem -LiteralPath (Join-Path $projectDirectory "bin") -Filter $generatorName -File -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match "[\\/]$([Regex]::Escape($Configuration))[\\/]net10\.0[\\/]$([Regex]::Escape($generatorName))$" } |
    Sort-Object LastWriteTimeUtc -Descending)
if ($generatorCandidates.Count -eq 0) {
    throw "Documentation generator was not produced beneath $projectDirectory\bin for configuration $Configuration."
}
$generator = $generatorCandidates[0].FullName
Write-Host "[ OK ] Documentation generator: $generator"

$arguments = @("generate", "--root", $root, "--configuration", $config)
if ($Strict) { $arguments += "--validate" }
Write-Host "[INFO] Generating Inu SDK usage site."
& $dotnet $generator @arguments
if ($LASTEXITCODE -ne 0) { throw "Documentation generation failed with exit code $LASTEXITCODE." }

$index = Join-Path $siteRoot "index.html"
$search = Join-Path $siteRoot "assets\search-index.js"
$apiIndex = Join-Path $siteRoot "api\index.html"
$assemblyIndex = Join-Path $siteRoot "assemblies\index.html"
if (-not (Test-Path -LiteralPath $index -PathType Leaf) -or -not (Test-Path -LiteralPath $search -PathType Leaf) -or -not (Test-Path -LiteralPath $apiIndex -PathType Leaf) -or -not (Test-Path -LiteralPath $assemblyIndex -PathType Leaf)) {
    throw "Documentation generator did not produce the required current API site outputs beneath $siteRoot."
}
Write-Host "[ OK ] Inu SDK usage site: $index"
