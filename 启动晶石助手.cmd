@echo off
set "companion=%~dp0artifacts\release\StoneshardCompanion-0.3.5\StoneshardCompanion.exe"
if not exist "%companion%" (
  echo Please run scripts\build.ps1 -Publish first.
  pause
  exit /b 1
)
start "" "%companion%"
