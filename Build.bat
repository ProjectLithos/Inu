@echo off
setlocal EnableExtensions EnableDelayedExpansion
set "ROOT=%~dp0"
set "ROOT=%ROOT:~0,-1%"
set /p INU_VERSION=<"%ROOT%\VERSION"
if not defined INU_VERSION (
    echo [FAIL] Inu VERSION is missing.
    exit /b 1
)
for %%I in ("%ROOT%\..") do set "PRODUCT_ROOT=%%~fI"
set "KATH_ROOT=%PRODUCT_ROOT%\Kath"

echo.
echo ============================================================
echo Inu Build %INU_VERSION%
echo ============================================================
echo [INFO] Root: %ROOT%
echo.

if not exist "%ROOT%\src\Common\Inu.Cli\Inu.Cli.csproj" (
    echo [FAIL] Required source is missing: %ROOT%\src\Common\Inu.Cli\Inu.Cli.csproj
    exit /b 1
)
if not exist "%ROOT%\targets\X64\PC\UEFI\Target.json" (
    echo [FAIL] Required source is missing: %ROOT%\targets\X64\PC\UEFI\Target.json
    exit /b 1
)
if not exist "%ROOT%\toolchains\Inu.Toolchain.json" (
    echo [FAIL] Required source is missing: %ROOT%\toolchains\Inu.Toolchain.json
    exit /b 1
)
if not exist "%KATH_ROOT%\Build-Kath.bat" (
    echo [FAIL] Required source is missing: %KATH_ROOT%\Build-Kath.bat
    exit /b 1
)
if not exist "%KATH_ROOT%\VERSION" (
    echo [FAIL] Required source is missing: %KATH_ROOT%\VERSION
    exit /b 1
)

set "KATH_VERSION="
set /p KATH_VERSION=<"%KATH_ROOT%\VERSION"
if /I not "%KATH_VERSION%"=="%INU_VERSION%" (
    echo [FAIL] Inu/Kath version mismatch. Inu=%INU_VERSION% Kath=%KATH_VERSION%
    exit /b 1
)
if exist "%ROOT%\Repos" (
    echo [FAIL] Obsolete Inu\Repos still exists. Run the root Build.bat to clean the installation.
    exit /b 1
)


powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "%ROOT%\Test-Toolchain.ps1" -Root "%ROOT%"
if errorlevel 1 (
    echo [FAIL] Inu private toolchain is missing, incomplete, or incompatible.
    echo [INFO] Build.bat never downloads or installs toolchains.
    echo [INFO] Restore the prepared %PRODUCT_ROOT%\Inu\.toolchain or run Install-Toolchain.bat explicitly if you intend to install it.
    exit /b 1
) else (
    echo [ OK ] Inu private toolchain is installed.
)

if exist "%ROOT%\SDK\scripts\Build-Usings.ps1" del /q "%ROOT%\SDK\scripts\Build-Usings.ps1" >nul 2>nul

if not exist "%ROOT%\SDK\scripts\Build-References.ps1" (
    echo [FAIL] SDK central-reference build script is missing.
    exit /b 1
)
powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "%ROOT%\SDK\scripts\Build-References.ps1" -SdkRoot "%ROOT%\SDK" -DotNet "%ROOT%\.toolchain\DotNet\dotnet.exe" -Configuration Release
if errorlevel 1 (
    echo [FAIL] SDK central-reference compilation failed.
    exit /b 1
)

powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "%ROOT%\Bootstrap-Inu.ps1" -Root "%ROOT%" %*
set "RC=%ERRORLEVEL%"
if not "%RC%"=="0" (
    echo.
    echo [FAIL] Inu build failed with exit code %RC%.
    exit /b %RC%
)

if not exist "%ROOT%\Bin\inu.exe" ( echo [FAIL] Bin\inu.exe is missing after build. & exit /b 1 )
if not exist "%KATH_ROOT%\Bin\Kath.exe" ( echo [FAIL] Kath\Bin\Kath.exe is missing after build. & exit /b 1 )

echo.
echo [ OK ] Inu %INU_VERSION% and Kath %INU_VERSION% build completed successfully.
echo [INFO] Inu:  %ROOT%\Bin\inu.exe
echo [INFO] Kath: %KATH_ROOT%\Bin\Kath.exe

exit /b 0
