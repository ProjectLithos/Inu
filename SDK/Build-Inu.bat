@echo off
setlocal
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build-Inu-Progress.ps1" %*
exit /b %ERRORLEVEL%
