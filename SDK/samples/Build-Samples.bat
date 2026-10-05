@echo off
setlocal
set "SDKROOT=%~dp0.."
set "DOTNET=%SDKROOT%\.toolchain\DotNet\dotnet.exe"
if not exist "%DOTNET%" set "DOTNET=dotnet"
for %%D in (HelloKernel InterruptHandler PciDriver UsbDriver VirtioDriver Filesystem NetworkClient UserlandProcess Syscall Service TrueTypeRenderer) do (
 echo [INFO] Building %%D
 "%DOTNET%" build "%~dp0%%D\Inu.Sample.%%D.csproj" --configuration Debug --nologo
 if errorlevel 1 exit /b 1
)
echo [ OK ] All Inu SDK samples built.
