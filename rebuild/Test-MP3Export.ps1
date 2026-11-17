$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$bin=Join-Path $root 'HourNerdMusicPlayer\bin\Release'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
$runner=Join-Path $bin 'HourNerdMusicPlayer.MP3Tests.exe'
& $compiler /nologo /target:exe /platform:x86 "/out:$runner" /reference:System.Net.Http.dll "/reference:$bin\NAudio.Core.dll" "/reference:$bin\NAudio.Wasapi.dll" "/reference:$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\Facades\netstandard.dll" (Join-Path $root 'HourNerdMusicPlayer\MP3Exporter.cs') (Join-Path $PSScriptRoot 'MP3ExportTests.cs')
if($LASTEXITCODE -ne 0){throw 'MP3 regression test compilation failed.'}
& $runner
if($LASTEXITCODE -ne 0){throw 'MP3 regression tests failed.'}
