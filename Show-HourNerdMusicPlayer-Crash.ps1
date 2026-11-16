$ErrorActionPreference = 'Stop'
$path = Join-Path $env:APPDATA 'HourNerd Music Player\hournerdmusicplayer-crash.txt'
$pathPublic = Join-Path $env:APPDATA 'HourNerd Music Player\Public\hournerdmusicplayer-crash.txt'
if (Test-Path $pathPublic) { $path = $pathPublic }
if (Test-Path $path) {
    Write-Host 'Managed crash report:'
    Get-Content $path -Tail 100
} else { Write-Host 'No managed crash report was written.' }
Write-Host 'Recent Windows crash events for HourNerdMusicPlayer:'
try {
    $events = Get-WinEvent -FilterHashtable @{LogName='Application';Id=1000,1026;StartTime=(Get-Date).AddHours(-1)} -ErrorAction Stop | Where-Object { $_.Message -match '(?i)HourNerdMusicPlayer\.exe' } | Select-Object -First 2
    if (!$events) { Write-Host 'No matching Windows crash events found in the past hour.' }
    foreach ($event in $events) {
        Write-Host "$($event.TimeCreated) - $($event.ProviderName)"
        $event.Message
    }
} catch { Write-Host 'No matching Windows crash events were available.' }
