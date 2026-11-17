[CmdletBinding()]
param([string]$TipEmail)
$ErrorActionPreference='Stop'
Set-Location $PSScriptRoot
$out=Join-Path $PSScriptRoot 'dist\spotify-standalone'
if(Test-Path $out){Remove-Item $out -Recurse -Force}
New-Item -ItemType Directory (Join-Path $out 'Spotify') -Force | Out-Null
foreach($name in @('HourNerdMusicPlayer.exe','NAudio.Core.dll','NAudio.Wasapi.dll')){Copy-Item (Join-Path $PSScriptRoot "HourNerdMusicPlayer\bin\Release\$name") $out}
Copy-Item 'native-spotify\target\x86_64-pc-windows-msvc\release\hournerd-spotify.exe' (Join-Path $out 'Spotify')
Copy-Item 'native-spotify\README.txt' (Join-Path $out 'Spotify\SPOTIFY-README.txt')
Copy-Item 'native-spotify\LIBRESPOT-LICENSE.txt' (Join-Path $out 'Spotify')
'{"engine":"native","protocol":1,"build":"1.2.0-standalone-test"}' | Set-Content (Join-Path $out 'Spotify\standalone.json') -Encoding UTF8
foreach($name in @('Apply-MP3-Update.cmd','Apply-MP3-Update.ps1','MP3-UPDATE-README.txt','Start-HourNerd-Standalone.cmd')){Copy-Item (Join-Path $PSScriptRoot $name) $out}
Copy-Item 'upstream-notices\NAudio-LICENSE.txt' $out
if($TipEmail){
    $parsed=New-Object System.Net.Mail.MailAddress($TipEmail)
    if($parsed.Address -ne $TipEmail){throw 'Invalid private payment configuration.'}
    [IO.File]::WriteAllText((Join-Path $out 'developer-tip-email.txt'),$TipEmail,(New-Object Text.UTF8Encoding($false)))
}
$base=[IO.Path]::GetFullPath($out).TrimEnd('\')+'\'
$checksums=Get-ChildItem $out -Recurse -File | ForEach-Object { '{0}  {1}' -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash,$_.FullName.Substring($base.Length) }
$checksums | Set-Content (Join-Path $out 'SHA256SUMS.txt') -Encoding UTF8
Write-Host 'Prepared standalone Spotify test update. No Spotify Desktop, Spicetify, or ad-blocking extension dependency.'
