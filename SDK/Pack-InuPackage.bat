@echo off
setlocal
set "DOTNET=%~dp0.toolchain\DotNet\dotnet.exe"
if not exist "%DOTNET%" (
  echo [FAIL] Inu pinned .NET toolchain was not found: %DOTNET%
  exit /b 1
)
"%DOTNET%" run --project "%~dp0src\Inu.PackagePacker\Inu.PackagePacker.csproj" --configuration Release -- %*
exit /b %ERRORLEVEL%
