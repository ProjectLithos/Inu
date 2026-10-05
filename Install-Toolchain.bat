@echo off
setlocal EnableExtensions

set "ROOT=%~dp0"
set "ROOT=%ROOT:~0,-1%"

echo.
echo ============================================================
echo Inu Toolchain Installer
echo ============================================================
echo [INFO] Root: %ROOT%
echo.

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%ROOT%\Install-Toolchain.ps1" -Root "%ROOT%"
exit /b %ERRORLEVEL%
