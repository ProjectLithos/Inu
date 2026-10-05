@echo off
setlocal
set "ROOT=%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%ROOT%Build-InuDocumentation.ps1" %*
exit /b %ERRORLEVEL%
