$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$package=Join-Path $root 'dist\spotify-standalone'
$temporary=Join-Path $env:TEMP ('hournerd-update-test-'+[guid]::NewGuid().ToString('N'))
$install=Join-Path $temporary 'installed'
New-Item -ItemType Directory (Join-Path $install 'Spotify') -Force | Out-Null
try {
    foreach($name in @('HourNerdMusicPlayer.exe','NAudio.Core.dll','NAudio.Wasapi.dll','PandoraSharp.dll','BassPlayer.dll')){[IO.File]::WriteAllText((Join-Path $install $name),"old-test-$name")}
    [IO.File]::WriteAllText((Join-Path $install 'Spotify\legacy-bridge.txt'),'previous Spotify bridge')
    [IO.File]::WriteAllText((Join-Path $install 'developer-tip-email.txt'),'existing-owner-config')
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $package 'Apply-MP3-Update.ps1') -InstallPath $install
    if($LASTEXITCODE -ne 0){throw 'Standalone updater could not update an existing installation.'}
    foreach($name in @('PandoraSharp.dll','BassPlayer.dll')){if((Get-Content (Join-Path $install $name) -Raw) -ne "old-test-$name"){throw "Existing playback repair replaced: $name"}}
    if((Get-Content (Join-Path $install 'developer-tip-email.txt') -Raw) -ne 'existing-owner-config'){throw 'Existing tip configuration was not preserved.'}
    if(Test-Path (Join-Path $install 'Spotify\legacy-bridge.txt')){throw 'Obsolete Spotify bridge files remain in the active folder.'}
    $backup=Get-ChildItem $install -Directory -Filter 'MP3-update-backup-*' | Select-Object -First 1
    if(!(Test-Path (Join-Path $backup.FullName 'Spotify\legacy-bridge.txt'))){throw 'Previous Spotify configuration was not backed up.'}
    $expected=(Get-FileHash (Join-Path $package 'HourNerdMusicPlayer.exe')).Hash
    if((Get-FileHash (Join-Path $install 'HourNerdMusicPlayer.exe')).Hash -ne $expected){throw 'Wrong EXE installed.'}
    if(!(Test-Path (Join-Path $install 'Spotify\hournerd-spotify.exe'))){throw 'Native engine was not installed.'}
    Write-Host 'PASS standalone update targets the actual EXE, preserves Pandora/audio fixes and account configuration, and replaces/backups the old bridge'
    $corrupt=Join-Path $temporary 'corrupt'
    Copy-Item $package $corrupt -Recurse
    Add-Content (Join-Path $corrupt 'Spotify\SPOTIFY-README.txt') 'changed'
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $corrupt 'Apply-MP3-Update.ps1') -InstallPath $install
    if($LASTEXITCODE -eq 0){throw 'Corrupted update was accepted.'}
    if((Get-FileHash (Join-Path $install 'HourNerdMusicPlayer.exe')).Hash -ne $expected){throw 'Failed verification changed the installed EXE.'}
    Write-Host 'PASS checksum failure leaves the installed player unchanged'
}finally{Remove-Item $temporary -Recurse -Force -ErrorAction SilentlyContinue}
