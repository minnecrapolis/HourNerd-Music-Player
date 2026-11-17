[CmdletBinding()]
param([string]$InstallPath = (Join-Path $env:LOCALAPPDATA 'Programs\HourNerd Music Player'))
$ErrorActionPreference='Stop'
try {
    if(Get-Process HourNerdMusicPlayer -ErrorAction SilentlyContinue){throw 'Close HourNerd Music Player completely, including its tray icon, then run this update again.'}
    $exe=Join-Path $InstallPath 'HourNerdMusicPlayer.exe'
    if(!(Test-Path $exe)){throw "Existing player not found in $InstallPath. Run this script with -InstallPath followed by your installation folder."}
    $hashFile=Join-Path $PSScriptRoot 'SHA256SUMS.txt'
    if(!(Test-Path $hashFile)){throw 'Extract the entire ZIP before applying the update.'}
    foreach($line in Get-Content $hashFile){
        if($line -notmatch '^([A-Fa-f0-9]{64})\s+(.+)$'){throw 'Invalid update checksum file.'}
        $hash=$Matches[1];$relative=$Matches[2]
        if([IO.Path]::IsPathRooted($relative) -or $relative -match '(^|[\\/])\.\.([\\/]|$)'){throw 'Invalid update checksum path.'}
        if((Get-FileHash (Join-Path $PSScriptRoot $relative) -Algorithm SHA256).Hash -ne $hash){throw 'Update verification failed. Download and extract the ZIP again.'}
    }
    $backup=Join-Path $InstallPath ('MP3-update-backup-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
    New-Item -ItemType Directory $backup | Out-Null
    $replaced=@()
    try {
        $files=@('HourNerdMusicPlayer.exe','NAudio.Core.dll','NAudio.Wasapi.dll')
        if(Test-Path (Join-Path $PSScriptRoot 'developer-tip-email.txt')){$files+='developer-tip-email.txt'}
        foreach($name in $files){
            $target=Join-Path $InstallPath $name
            if(Test-Path $target){Copy-Item $target $backup}
            $replaced+=$name
            Copy-Item (Join-Path $PSScriptRoot $name) $target -Force
        }
    }catch{
        foreach($name in $replaced){$original=Join-Path $backup $name;$target=Join-Path $InstallPath $name;if(Test-Path $original){Copy-Item $original $target -Force}else{Remove-Item $target -Force}}
        throw
    }
    if(Test-Path (Join-Path $PSScriptRoot 'Spotify')){
        $spotifyTarget=Join-Path $InstallPath 'Spotify'
        if(Test-Path $spotifyTarget){Copy-Item $spotifyTarget $backup -Recurse}
        New-Item -ItemType Directory $spotifyTarget -Force | Out-Null
        if(Test-Path (Join-Path $PSScriptRoot 'Spotify\standalone.json')){Remove-Item $spotifyTarget -Recurse -Force;New-Item -ItemType Directory $spotifyTarget -Force | Out-Null}
        Copy-Item (Join-Path $PSScriptRoot 'Spotify\*') $spotifyTarget -Recurse -Force
    }
    Copy-Item (Join-Path $PSScriptRoot 'NAudio-LICENSE.txt') $InstallPath -Force
    if((Get-FileHash $exe -Algorithm SHA256).Hash -ne (Get-FileHash (Join-Path $PSScriptRoot 'HourNerdMusicPlayer.exe') -Algorithm SHA256).Hash){throw 'The installed EXE verification failed.'}
    Write-Host "Updated EXE: $exe"
    Write-Host 'Update installed. Open HourNerd. Use Settings for separate Pandora and Spotify accounts, or choose Spotify from the service dropdown.'
    Write-Host "Backup: $backup"
}catch{Write-Host $_.Exception.Message -ForegroundColor Red;exit 1}

