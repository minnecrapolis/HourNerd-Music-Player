$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$bin=Join-Path $root 'HourNerdMusicPlayer\bin\Release'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
$wpf=Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\WPF'
$runner=Join-Path $bin 'HourNerdMusicPlayer.SpotifyUiTests.exe'
& $compiler /nologo /target:exe /platform:x86 "/out:$runner" "/reference:$bin\HourNerdMusicPlayer.exe" "/reference:$wpf\PresentationFramework.dll" "/reference:$wpf\PresentationCore.dll" "/reference:$wpf\WindowsBase.dll" /reference:System.Xaml.dll (Join-Path $PSScriptRoot 'SpotifyUiTests.cs')
if($LASTEXITCODE -ne 0){throw 'Native Spotify UI tests did not compile.'}
if(Test-Path "$bin\HourNerdMusicPlayer.exe.config"){Copy-Item "$bin\HourNerdMusicPlayer.exe.config" "$runner.config" -Force}
$process=Start-Process $runner -PassThru -NoNewWindow
if(!$process.WaitForExit(90000)){Stop-Process -Id $process.Id -Force;throw 'Native Spotify UI tests timed out.'}
if($process.ExitCode -ne 0){throw 'Native Spotify UI tests failed.'}
Copy-Item (Join-Path $bin 'spotify-preview.png') (Join-Path $root 'spotify-preview.png') -Force
