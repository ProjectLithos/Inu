[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ProjectDirectory,
    [string]$OutputDirectory,
    [string]$Configuration = 'Release'
)
$ErrorActionPreference='Stop'
$root=(Resolve-Path -LiteralPath $ProjectDirectory).Path
$manifestPath=Join-Path $root 'Inu.Driver.json'
$manifest=Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if (-not $OutputDirectory) { $OutputDirectory=Join-Path $root 'Artifacts\Drivers' }
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$payload=Join-Path $root "bin\$Configuration"
if (-not (Test-Path -LiteralPath $payload -PathType Container)) { throw "Driver build output not found: $payload. Build the driver first." }
$temp=Join-Path ([IO.Path]::GetTempPath()) ("InuDriver-"+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temp | Out-Null
try {
    Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $temp 'Inu.Driver.json')
    $payloadOut=Join-Path $temp 'payload'; New-Item -ItemType Directory -Path $payloadOut | Out-Null
    Get-ChildItem -LiteralPath $payload -Recurse -File | Where-Object { $_.Extension -in '.dll','.exe','.pdb','.json' } | ForEach-Object {
        $relative=$_.FullName.Substring($payload.Length).TrimStart('\','/'); $dest=Join-Path $payloadOut $relative; New-Item -ItemType Directory -Force -Path (Split-Path $dest) | Out-Null; Copy-Item -LiteralPath $_.FullName -Destination $dest
    }
    $safeId=([string]$manifest.id -replace '[^A-Za-z0-9._-]','_'); $name="$safeId-$($manifest.version).nodrv"
    $zip=Join-Path $OutputDirectory ($name+'.zip'); $final=Join-Path $OutputDirectory $name
    if (Test-Path $zip) { Remove-Item $zip -Force }; if (Test-Path $final) { Remove-Item $final -Force }
    Compress-Archive -Path (Join-Path $temp '*') -DestinationPath $zip -CompressionLevel Optimal
    Move-Item -LiteralPath $zip -Destination $final
    Write-Host "[ OK ] Inu driver package: $final" -ForegroundColor Green
} finally { if (Test-Path $temp) { Remove-Item $temp -Recurse -Force } }
