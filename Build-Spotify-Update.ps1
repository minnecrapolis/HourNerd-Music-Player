[CmdletBinding()]
param([string]$TipEmail)
$ErrorActionPreference='Stop'
Set-Location $PSScriptRoot
$out=Join-Path $PSScriptRoot 'dist\spotify-update'
if(Test-Path $out){Remove-Item $out -Recurse -Force}
New-Item -ItemType Directory (Join-Path $out 'Spotify') -Force | Out-Null
foreach($name in @('HourNerdMusicPlayer.exe','NAudio.Core.dll','NAudio.Wasapi.dll')){Copy-Item (Join-Path $PSScriptRoot "HourNerdMusicPlayer\bin\Release\$name") $out}
foreach($name in @('Apply-MP3-Update.cmd','Apply-MP3-Update.ps1','MP3-UPDATE-README.txt')){Copy-Item (Join-Path $PSScriptRoot $name) $out}
Copy-Item 'upstream-notices\NAudio-LICENSE.txt' $out
Copy-Item 'spotify\*' (Join-Path $out 'Spotify') -Recurse -Force
$zip=Join-Path $env:TEMP ('hournerd-spicetify-'+[guid]::NewGuid().ToString('N')+'.zip')
try{
    Invoke-WebRequest -UseBasicParsing 'https://github.com/spicetify/cli/releases/download/v2.46.7/spicetify-2.46.7-windows-x64.zip' -OutFile $zip
    if((Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant() -ne 'c5a723d31eae529ba13f6922a7a027124238c7f3da7edd182a73eab03c447e9e'){throw 'Spicetify archive checksum did not match the pinned release.'}
    Expand-Archive $zip (Join-Path $out 'Spotify\cli') -Force
}finally{if(Test-Path $zip){Remove-Item $zip -Force}}
if($TipEmail){
    $parsed=New-Object System.Net.Mail.MailAddress($TipEmail)
    if($parsed.Address -ne $TipEmail){throw 'Invalid private payment configuration.'}
    [IO.File]::WriteAllText((Join-Path $out 'developer-tip-email.txt'),$TipEmail,(New-Object Text.UTF8Encoding($false)))
}
$base=[IO.Path]::GetFullPath($out).TrimEnd('\')+'\'
Get-ChildItem $out -Recurse -File | ForEach-Object { '{0}  {1}' -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash,$_.FullName.Substring($base.Length) } | Set-Content (Join-Path $out 'SHA256SUMS.txt') -Encoding UTF8
Write-Host 'Prepared the Spotify test update with a checksum-verified Spicetify distribution.'
