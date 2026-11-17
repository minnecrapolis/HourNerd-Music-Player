[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$MsiPath)
$ErrorActionPreference = 'Stop'
$MsiPath = [IO.Path]::GetFullPath($MsiPath)
$logs = Join-Path (Split-Path $MsiPath) 'MSI-install-validation.log'
$uninstallLog = Join-Path (Split-Path $MsiPath) 'MSI-uninstall-validation.log'
$installed = Join-Path $env:LOCALAPPDATA 'Programs\HourNerd Music Player\HourNerdMusicPlayer.exe'
$msiexec = Join-Path $env:SystemRoot 'System32\msiexec.exe'
try {
    $process = Start-Process -FilePath $msiexec -ArgumentList ('/i "{0}" /qn /norestart /L*v "{1}"' -f $MsiPath,$logs) -Wait -PassThru
    if ($process.ExitCode -notin @(0,3010)) {
        Get-Content -LiteralPath $logs -Tail 160 | Write-Host
        throw "Real MSI install failed ($($process.ExitCode)); see $logs"
    }
    if (!(Test-Path -LiteralPath $installed)) { throw 'Real install did not create the expected per-user executable.' }
    Write-Host 'PASS real per-user MSI install'
} finally {
    $remove = Start-Process -FilePath $msiexec -ArgumentList ('/x "{0}" /qn /norestart /L*v "{1}"' -f $MsiPath,$uninstallLog) -Wait -PassThru
    if ($remove.ExitCode -notin @(0,3010,1605)) {
        Get-Content -LiteralPath $uninstallLog -Tail 100 | Write-Host
        throw "MSI uninstall failed ($($remove.ExitCode))."
    }
}
if (Test-Path -LiteralPath $installed) { throw 'Uninstall left the executable behind.' }
Write-Host 'PASS real per-user MSI uninstall'
