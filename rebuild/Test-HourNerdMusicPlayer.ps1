$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$bin = Join-Path $root 'HourNerdMusicPlayer\bin\Release'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
foreach ($dll in 'PandoraSharp.dll','Util.dll','Newtonsoft.Json.dll') {
    if (!(Test-Path (Join-Path $bin $dll))) { throw "Missing $dll. Run Build-HourNerdMusicPlayer.ps1 first." }
}
$runner = Join-Path $bin 'HourNerdMusicPlayer.ApiTests.exe'
& $compiler /nologo /target:exe /platform:x86 "/out:$runner" "/reference:$bin\PandoraSharp.dll" "/reference:$bin\Util.dll" "/reference:$bin\Newtonsoft.Json.dll" (Join-Path $PSScriptRoot 'ApiTests.cs') (Join-Path $root 'HourNerdMusicPlayer\ProfileMigration.cs') (Join-Path $root 'HourNerdMusicPlayer\DistributionPolicy.cs')
if ($LASTEXITCODE -ne 0) { throw 'API regression test compilation failed.' }
Copy-Item (Join-Path $bin 'HourNerdMusicPlayer.exe.config') "$runner.config" -Force
& $runner
if ($LASTEXITCODE -ne 0) { throw 'API regression tests failed.' }

$audioRunner = Join-Path $bin 'HourNerdMusicPlayer.AudioLifetimeTests.exe'
& $compiler /nologo /target:exe /platform:x86 "/out:$audioRunner" "/reference:$bin\BassPlayer.dll" "/reference:$bin\Bass.Net.dll" (Join-Path $PSScriptRoot 'AudioLifetimeTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Audio lifecycle regression test compilation failed.' }
Copy-Item (Join-Path $bin 'HourNerdMusicPlayer.exe.config') "$audioRunner.config" -Force
& $audioRunner
if ($LASTEXITCODE -ne 0) { throw 'Native audio lifecycle regression tests failed.' }
