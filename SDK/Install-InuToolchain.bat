@echo off
setlocal
set "SCRIPT=%~dp0Install-InuToolchain.ps1"
if not exist "%SCRIPT%" (
  echo [FAIL] Missing %SCRIPT%
  exit /b 1
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT%"
exit /b %ERRORLEVEL%
