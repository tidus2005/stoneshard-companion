@echo off
setlocal
set "COMPANION_APP=%~dp0artifacts\development\0.3.24\app\StoneshardCompanion.dll"
if not exist "%COMPANION_APP%" set "COMPANION_APP=%~dp0src\Overlay\bin\Release\net8.0-windows\StoneshardCompanion.dll"
if not exist "%COMPANION_APP%" goto missing_app
if "%~1"=="--check" goto check_app
start "" /min /d "%~dp0" "C:\Program Files\dotnet\dotnet.exe" "%COMPANION_APP%"
if errorlevel 1 goto launch_failed
exit /b 0
:check_app
echo Application exists: %COMPANION_APP%
exit /b 0
:missing_app
echo [Stoneshard Companion] Application files are missing. Run scripts/build.ps1.
pause
exit /b 1
:launch_failed
echo [Stoneshard Companion] Windows could not start:
echo %COMPANION_APP%
echo Please check the error dialog and application logs.
pause
exit /b 1