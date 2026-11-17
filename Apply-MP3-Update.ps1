[CmdletBinding()]
param([string]$InstallPath = (Join-Path $env:LOCALAPPDATA 'Programs\HourNerd Music Player'))
$ErrorActionPreference='Stop'
try {
    if(Get-Process HourNerdMusicPlayer -ErrorAction SilentlyContinue){throw 'Close HourNerd Music Player completely, including its tray icon, then run this update again.'}
    $exe=Join-Path $InstallPath 'HourNerdMusicPlayer.exe'
    if(!(Test-Path $exe)){throw "Existing player not found in $InstallPath. Use -InstallPath followed by the actual installation folder."}
    $hashFile=Join-Path $PSScriptRoot 'SHA256SUMS.txt'
    if(!(Test-Path $hashFile)){throw 'Extract the entire ZIP before applying the update.'}
    foreach($line in Get-Content $hashFile){
        if($line -notmatch '^([A-Fa-f0-9]{64})\s+(.+)$'){throw 'Invalid update checksum file.'}
        $hash=$Matches[1];$relative=$Matches[2]
        if([IO.Path]::IsPathRooted($relative) -or $relative -match '(^|[\\/])\.\.([\\/]|$)'){throw 'Invalid update checksum path.'}
        if((Get-FileHash (Join-Path $PSScriptRoot $relative) -Algorithm SHA256).Hash -ne $hash){throw 'Update verification failed. Download and extract the ZIP again.'}
    }
    $backup=Join-Path $InstallPath ('MP3-update-backup-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[guid]::NewGuid().ToString('N').Substring(0,6))
    New-Item -ItemType Directory $backup | Out-Null
    $spotifyTarget=Join-Path $InstallPath 'Spotify'
    if(Test-Path $spotifyTarget){Copy-Item $spotifyTarget $backup -Recurse}
    $replaced=@();$spotifyTouched=$false
    try {
        $files=@('HourNerdMusicPlayer.exe','NAudio.Core.dll','NAudio.Wasapi.dll','NAudio-LICENSE.txt')
        if(Test-Path (Join-Path $PSScriptRoot 'developer-tip-email.txt')){$files+='developer-tip-email.txt'}
        foreach($name in $files){
            $target=Join-Path $InstallPath $name
            if(Test-Path $target){Copy-Item $target $backup}
            $replaced+=$name
            Copy-Item (Join-Path $PSScriptRoot $name) $target -Force
        }
        $spotifyTouched=$true
        if(Test-Path $spotifyTarget){Remove-Item $spotifyTarget -Recurse -Force}
        Copy-Item (Join-Path $PSScriptRoot 'Spotify') $InstallPath -Recurse -Force
        foreach($relative in @('HourNerdMusicPlayer.exe','Spotify\hournerd-spotify.exe')){
            if((Get-FileHash (Join-Path $InstallPath $relative) -Algorithm SHA256).Hash -ne (Get-FileHash (Join-Path $PSScriptRoot $relative) -Algorithm SHA256).Hash){throw "Installed file verification failed: $relative"}
        }
    }catch{
        $failure=$_.Exception.Message
        try {
            foreach($name in $replaced){$original=Join-Path $backup $name;$target=Join-Path $InstallPath $name;if(Test-Path $original){Copy-Item $original $target -Force}else{Remove-Item $target -Force -ErrorAction SilentlyContinue}}
            if($spotifyTouched){if(Test-Path $spotifyTarget){Remove-Item $spotifyTarget -Recurse -Force};if(Test-Path (Join-Path $backup 'Spotify')){Copy-Item (Join-Path $backup 'Spotify') $InstallPath -Recurse -Force}}
        }catch{Write-Host "Automatic rollback could not finish. Restore files from $backup" -ForegroundColor Red}
        throw $failure
    }
    Write-Host "Updated EXE: $exe"
    Write-Host ('Installed version: '+(Get-Item $exe).VersionInfo.FileVersion)
    Write-Host 'Standalone Spotify update installed. Run Start-HourNerd-Standalone.cmd, select Spotify and sign in.'
    Write-Host "Backup: $backup"
}catch{Write-Host $_.Exception.Message -ForegroundColor Red;exit 1}
