$ErrorActionPreference = 'Stop'
$exe = Join-Path $PSScriptRoot 'HourNerdMusicPlayer\bin\Release\HourNerdMusicPlayer.exe'
if (!(Test-Path $exe)) { throw 'Build HourNerdMusicPlayer first with Build-HourNerdMusicPlayer.ps1 -Test.' }
if (Get-Process HourNerdMusicPlayer -ErrorAction SilentlyContinue) { throw 'Close HourNerdMusicPlayer, including its system tray icon, before starting diagnostics.' }
$previousFlag = $env:HOURNERDMUSICPLAYER_DIAGNOSTICS
try {
    $env:HOURNERDMUSICPLAYER_DIAGNOSTICS = '1'
    Start-Process $exe
} finally { $env:HOURNERDMUSICPLAYER_DIAGNOSTICS = $previousFlag }
Write-Host 'Load a station, then switch to another. After both have loaded, run:'
Write-Host 'powershell -NoProfile -ExecutionPolicy Bypass -File .\Show-HourNerdMusicPlayer-Timings.ps1'
