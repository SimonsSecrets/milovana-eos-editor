@echo off
REM Regenerate tease.json for TheFuckingMachine-Introduction from its script.md.
REM Double-click this, or run it from a terminal. Thin wrapper over the generic Build-Tease.ps1.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build-Tease.ps1" -TeaseDir "%~dp0..\Teases\TheFuckingMachine-Introduction"
echo.
pause
