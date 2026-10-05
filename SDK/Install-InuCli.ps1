[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$sdkRoot=$PSScriptRoot
$bin=Join-Path $env:LOCALAPPDATA 'Inu\bin'
New-Item -ItemType Directory -Force -Path $bin | Out-Null
$shim=Join-Path $bin 'inu.cmd'
$target=Join-Path $sdkRoot 'inu.cmd'
Set-Content -LiteralPath $shim -Encoding Ascii -Value "@echo off`r`ncall `"$target`" %*`r`nexit /b %ERRORLEVEL%`r`n"
$userPath=[Environment]::GetEnvironmentVariable('Path','User')
$parts=@($userPath -split ';' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
if($parts -notcontains $bin){
    $newPath=if([string]::IsNullOrWhiteSpace($userPath)){$bin}else{$userPath.TrimEnd(';')+';'+$bin}
    [Environment]::SetEnvironmentVariable('Path',$newPath,'User')
    Write-Host "[ OK ] Added Inu CLI directory to user PATH: $bin"
    Write-Host '[INFO] Open a new terminal before using inu by name.'
}else{Write-Host "[ OK ] Inu CLI directory is already on user PATH: $bin"}
Write-Host "[ OK ] Installed inu command shim: $shim"
