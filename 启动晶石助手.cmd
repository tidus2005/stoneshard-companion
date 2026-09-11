@echo off
setlocal
cd /d "%~dp0"
set "runtime=%~dp0.tools\dotnet\dotnet.exe"
set "companion=%~dp0src\Overlay\bin\Release\net8.0-windows\StoneshardCompanion.dll"
if not exist "%runtime%" set "runtime=dotnet"
if not exist "%companion%" (
  echo Build the development application first: scripts\build.ps1
  pause
  exit /b 1
)
start "" "%runtime%" "%companion%"
