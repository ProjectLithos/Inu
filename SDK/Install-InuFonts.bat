@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-InuFonts.ps1" %*
exit /b %ERRORLEVEL%
