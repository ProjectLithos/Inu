@echo off
setlocal
set "ROOT=%~dp0"
set "DOTNET=%ROOT%.toolchain\DotNet\dotnet.exe"
if not exist "%DOTNET%" (
  echo [FAIL] Inu pinned dotnet was not found: %DOTNET%
  exit /b 1
)
"%DOTNET%" run --project "%ROOT%tests\Inu.DotNetConformance.Tests\Inu.DotNetConformance.Tests.csproj" -c Release
exit /b %ERRORLEVEL%
