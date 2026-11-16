# Show only known timing/count lines, not URLs, passwords or account details.
$ErrorActionPreference = 'Stop'
$log = Join-Path $env:APPDATA 'HourNerd Music Player\hournerdmusicplayer-diagnostics.log'
$logPublic = Join-Path $env:APPDATA 'HourNerd Music Player\Public\hournerdmusicplayer-diagnostics.log'
if (Test-Path $logPublic) { $log = $logPublic }
if (!(Test-Path $log)) { throw 'Start HourNerdMusicPlayer with Start-HourNerdMusicPlayer-Diagnostics.ps1 first.' }
Get-Content $log | Select-String -Pattern '^\[\d{2}:\d{2}:\d{2}\.\d{3}\] (API station\.getPlaylist: \d+ ms|Playlist preparation: \d+ ms; playable tracks: \d+|Audio engine startup: \d+ ms)$' | Select-Object -Last 15 | ForEach-Object { $_.Line }
