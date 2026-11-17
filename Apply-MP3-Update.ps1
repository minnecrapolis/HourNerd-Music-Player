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
        if($line -notmatch '^([A-Fa-f0-9]{64})\s+([^\\/]+)$'){throw 'Invalid update checksum file.'}
        if((Get-FileHash (Join-Path $PSScriptRoot $Matches[2]) -Algorithm SHA256).Hash -ne $Matches[1]){throw 'Update verification failed. Download and extract the ZIP again.'}
    }
    $backup=Join-Path $InstallPath ('MP3-update-backup-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
    New-Item -ItemType Directory $backup | Out-Null
    $replaced=@()
    try {
        foreach($name in @('HourNerdMusicPlayer.exe','NAudio.Core.dll','NAudio.Wasapi.dll','developer-tip-email.txt')){
            $target=Join-Path $InstallPath $name
            if(Test-Path $target){Copy-Item $target $backup}
            Copy-Item (Join-Path $PSScriptRoot $name) $target -Force
            $replaced+=$name
        }
    }catch{
        foreach($name in $replaced){$original=Join-Path $backup $name;$target=Join-Path $InstallPath $name;if(Test-Path $original){Copy-Item $original $target -Force}else{Remove-Item $target -Force}}
        throw
    }
    Copy-Item (Join-Path $PSScriptRoot 'NAudio-LICENSE.txt') $InstallPath -Force
    Write-Host 'MP3 update installed. Open HourNerd and use Save MP3 on the now-playing screen.'
    Write-Host "Backup: $backup"
}catch{Write-Host $_.Exception.Message -ForegroundColor Red;exit 1}
