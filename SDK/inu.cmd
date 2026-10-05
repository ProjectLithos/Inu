@echo off
setlocal
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0inu.ps1" %*
exit /b %ERRORLEVEL%
