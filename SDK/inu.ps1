[CmdletBinding(PositionalBinding=$false)]
param(
    [Parameter(Position=0)][string]$Command = "help",
    [Parameter(ValueFromRemainingArguments=$true)][string[]]$Arguments
)

$ErrorActionPreference = 'Stop'
$SdkRoot = $PSScriptRoot
$CliVersion = '0.30.1'

function Write-Usage {
    Write-Host "Inu SDK CLI $CliVersion"
    Write-Host ""
    Write-Host "Usage: inu <command> [options]"
    Write-Host ""
    Write-Host "Commands:"
    Write-Host "  new <name> [--output <dir>]          Create a Inu OS project"
    Write-Host "  build [--project <dir>]              Build the selected/current project"
    Write-Host "  run [--project <dir>]                Build and run under the configured target"
    Write-Host "  debug [--project <dir>]              Debug-build and run with debugger transport"
    Write-Host "  test [test-runner options]           Run Inu SDK/project tests"
    Write-Host "  pack <manifest> <payload> <zip>      Build a Inu ZIP package"
    Write-Host "  doctor                               Verify SDK/toolchain health"
    Write-Host "  version                              Print CLI/SDK version information"
    Write-Host "  help                                 Show this help"
    Write-Host ""
    Write-Host "Common build options: --configuration Debug|Release, --boot-timeout <seconds>"
}

function Fail([string]$Message, [int]$Code = 1) {
    Write-Host "[FAIL] $Message"
    exit $Code
}

function Get-OptionValue([string[]]$Items, [string]$Name, [string]$Default = '') {
    for ($i=0; $i -lt $Items.Count; $i++) {
        if ($Items[$i] -eq $Name) {
            if ($i + 1 -ge $Items.Count) { Fail "Missing value for $Name" 2 }
            return $Items[$i + 1]
        }
        if ($Items[$i].StartsWith($Name + '=', [StringComparison]::OrdinalIgnoreCase)) {
            return $Items[$i].Substring($Name.Length + 1)
        }
    }
    return $Default
}

function Get-Remaining([string[]]$Items, [string[]]$NamesWithValue) {
    $result = New-Object System.Collections.Generic.List[string]
    for ($i=0; $i -lt $Items.Count; $i++) {
        $item = $Items[$i]
        $consumed = $false
        foreach ($name in $NamesWithValue) {
            if ($item -eq $name) {
                if ($i + 1 -ge $Items.Count) { Fail "Missing value for $name" 2 }
                $i++
                $consumed = $true
                break
            }
            if ($item.StartsWith($name + '=', [StringComparison]::OrdinalIgnoreCase)) {
                $consumed = $true
                break
            }
        }
        if (-not $consumed) { $result.Add($item) }
    }
    return $result.ToArray()
}

function Resolve-DotNet {
    $p = Join-Path $SdkRoot '.toolchain\DotNet\dotnet.exe'
    if (Test-Path -LiteralPath $p -PathType Leaf) { return $p }
    $cmd = Get-Command dotnet.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    Fail "Inu .NET toolchain is unavailable. Run Install-InuToolchain.bat." 3
}

function Run-Script([string]$Name, [string[]]$ScriptArgs) {
    $path = Join-Path $SdkRoot $Name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { Fail "Required SDK script is missing: $Name" 3 }
    & $path @ScriptArgs
    if ($null -eq $LASTEXITCODE) { return 0 }
    return [int]$LASTEXITCODE
}

function Resolve-Project([string[]]$Items) {
    $project = Get-OptionValue $Items '--project' ''
    if ([string]::IsNullOrWhiteSpace($project)) { $project = (Get-Location).Path }
    return [IO.Path]::GetFullPath($project)
}

function Build-Arguments([string[]]$Items, [bool]$Run, [bool]$DebugMode) {
    $project = Resolve-Project $Items
    $configuration = Get-OptionValue $Items '--configuration' $(if ($DebugMode) { 'Debug' } else { 'Release' })
    if ($configuration -ne 'Debug' -and $configuration -ne 'Release') { Fail "--configuration must be Debug or Release." 2 }
    $bootTimeout = Get-OptionValue $Items '--boot-timeout' '30'
    $n = 0
    if (-not [int]::TryParse($bootTimeout, [ref]$n) -or $n -lt 5 -or $n -gt 300) { Fail "--boot-timeout must be between 5 and 300 seconds." 2 }
    $args = @('-Project', $project, '-Configuration', $configuration, '-BootTimeoutSeconds', [string]$n)
    if ($Run) { $args += '-Run' } else { $args += '-NoRun' }
    return $args
}

function Invoke-New([string[]]$Items) {
    $output = Get-OptionValue $Items '--output' ''
    $remaining = @(Get-Remaining $Items @('--output'))
    $name = ''
    foreach ($item in $remaining) { if (-not $item.StartsWith('-')) { $name = $item; break } }
    if ([string]::IsNullOrWhiteSpace($output)) {
        if ([string]::IsNullOrWhiteSpace($name)) { Fail "new requires a project name or --output <directory>." 2 }
        $output = Join-Path (Get-Location).Path $name
    }
    $output = [IO.Path]::GetFullPath($output)
    $dotnet = Resolve-DotNet
    $project = Join-Path $SdkRoot 'src\Inu.ProjectCreator\Inu.ProjectCreator.csproj'
    Write-Host "[INFO] Creating Inu project: $output"
    & $dotnet run --project $project --configuration Release -- create --output $output --sdk-root $SdkRoot
    if ($LASTEXITCODE -ne 0) { return $LASTEXITCODE }
    Write-Host "[ OK ] Inu project created: $output"
    return 0
}

$cmd = $Command.ToLowerInvariant()
switch ($cmd) {
    'new' { exit (Invoke-New $Arguments) }
    'build' { exit (Run-Script 'Build-Inu.ps1' (Build-Arguments $Arguments $false $false)) }
    'run' { exit (Run-Script 'Build-Inu.ps1' (Build-Arguments $Arguments $true $false)) }
    'debug' { exit (Run-Script 'Build-Inu.ps1' (Build-Arguments $Arguments $true $true)) }
    'pack' {
        if ($Arguments.Count -ne 3) { Fail 'Usage: inu pack <Inu.Package.json> <payload-directory> <output.zip>' 2 }
        $dotnet = Resolve-DotNet
        $project = Join-Path $SdkRoot 'src\Inu.PackagePacker\Inu.PackagePacker.csproj'
        & $dotnet run --project $project --configuration Release -- $Arguments[0] $Arguments[1] $Arguments[2]
        exit $LASTEXITCODE
    }
    'version' {
        Write-Host "Inu CLI $CliVersion"
        $manifestPath = Join-Path $SdkRoot 'Inu.SdkManifest.json'
        if (Test-Path -LiteralPath $manifestPath) { $m=Get-Content -LiteralPath $manifestPath -Raw|ConvertFrom-Json; Write-Host "Inu SDK $($m.sdkVersion), API $($m.apiVersion), ABI $($m.abiVersion)" }
        exit 0
    }
    'help' { Write-Usage; exit 0 }
    '--help' { Write-Usage; exit 0 }
    '-h' { Write-Usage; exit 0 }
    default { Write-Usage; Fail "Unknown command: $Command" 2 }
}
