@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-HourNerdMusicPlayer.ps1"
if errorlevel 1 (
  echo Installation did not complete. See the error above.
  pause
  exit /b 1
)
pause
