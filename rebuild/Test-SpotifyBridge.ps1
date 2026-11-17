$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$bin=Join-Path $root 'HourNerdMusicPlayer\bin\Release'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
$runner=Join-Path $bin 'HourNerdMusicPlayer.SpotifyTests.exe'
& $compiler /nologo /target:exe /platform:x86 "/out:$runner" /reference:System.Web.Extensions.dll (Join-Path $root 'HourNerdMusicPlayer\Spotify\SpotifyBridge.cs') (Join-Path $PSScriptRoot 'SpotifyBridgeTests.cs')
if($LASTEXITCODE -ne 0){throw 'Spotify bridge tests did not compile.'}
& $runner
if($LASTEXITCODE -ne 0){throw 'Spotify bridge tests failed.'}
