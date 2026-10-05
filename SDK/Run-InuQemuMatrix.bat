@echo off
setlocal
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Run-InuQemuMatrix.ps1" %*
exit /b %ERRORLEVEL%
