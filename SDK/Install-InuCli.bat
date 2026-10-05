@echo off
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-InuCli.ps1"
exit /b %ERRORLEVEL%
