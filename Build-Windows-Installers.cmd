@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build-Windows-Installers.ps1"
if errorlevel 1 (
  echo Installer build did not complete. See the error above.
  pause
  exit /b 1
)
pause
