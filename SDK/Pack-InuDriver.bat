@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Pack-InuDriver.ps1" %*
exit /b %ERRORLEVEL%
