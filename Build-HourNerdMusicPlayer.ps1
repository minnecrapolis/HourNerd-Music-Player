param([switch]$Run, [switch]$Test, [switch]$Distribution)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (!(Test-Path $vswhere)) { throw 'Install Visual Studio 2022 with the .NET desktop development workload.' }
$msbuild = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (!$msbuild) { throw 'MSBuild was not found. Add the .NET desktop development workload to Visual Studio.' }
# Restore Microsoft's 4.8.1 reference assemblies with the other dependencies.
# Directory.Build.targets imports them for every project; no machine targeting
# pack installation is required. These are build references, not the runtime.
# Separate restore and build evaluations are required for packages.config.
& $msbuild 'HourNerdMusicPlayer.sln' /t:Restore /p:RestorePackagesConfig=true "/p:RestoreRepositoryPath=$PSScriptRoot\packages" /p:Configuration=Release /p:Platform=x86 /nologo /v:minimal
if ($LASTEXITCODE -ne 0) { throw 'Package restore failed. See the first error above.' }
$referenceTargets = Join-Path $PSScriptRoot 'packages\Microsoft.NETFramework.ReferenceAssemblies.net481.1.0.3\build\Microsoft.NETFramework.ReferenceAssemblies.net481.targets'
if (!(Test-Path $referenceTargets)) { throw 'NuGet did not restore the Microsoft 4.8.1 reference-assembly package. Verify that HourNerdMusicPlayer\packages.config and Directory.Build.targets are present in this HourNerd Music Player project.' }
$releaseOptions = @()
if ($Distribution) { $releaseOptions = @('/p:DebugSymbols=false', '/p:DebugType=none', "/p:PathMap=$PSScriptRoot=/_/src") }
& $msbuild 'HourNerdMusicPlayer.sln' /t:Rebuild /p:Configuration=Release /p:Platform=x86 /m /nologo /v:minimal @releaseOptions
if ($LASTEXITCODE -ne 0) { throw 'Build failed. See the first error above.' }
$exe = Join-Path $PSScriptRoot 'HourNerdMusicPlayer\bin\Release\HourNerdMusicPlayer.exe'
if (!(Test-Path $exe)) { throw 'Build finished without the expected HourNerdMusicPlayer.exe.' }
foreach ($native in 'bass.dll','bass_aac.dll') {
    Copy-Item (Join-Path $PSScriptRoot "Libs\BassPlayer\$native") (Split-Path $exe) -Force
}
if ($Test) { & "$PSScriptRoot\rebuild\Test-HourNerdMusicPlayer.ps1" }
Write-Host "Built: $exe"
if ($Run) { Start-Process $exe }
