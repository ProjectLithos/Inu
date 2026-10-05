@echo off
setlocal
set "SDK_ROOT=%INU_SDK_ROOT%"
if not defined SDK_ROOT set "SDK_ROOT=C:\Inu"
if not exist "%SDK_ROOT%\Build-Inu.bat" (
    echo [FAIL] Inu SDK was not found at "%SDK_ROOT%".
    echo [INFO] Set INU_SDK_ROOT to the SDK directory and run this file again.
    exit /b 1
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build-WorkspaceProjects.ps1" -SdkRoot "%SDK_ROOT%" -Configuration Release
if errorlevel 1 exit /b %ERRORLEVEL%
call "%SDK_ROOT%\Build-Inu.bat" -Project "%~dp0InuProject.json" -Run
exit /b %ERRORLEVEL%
