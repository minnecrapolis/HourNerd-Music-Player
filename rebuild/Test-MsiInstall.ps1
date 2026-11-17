[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$MsiPath, [switch]$FullUI)
$ErrorActionPreference = 'Stop'
$MsiPath = [IO.Path]::GetFullPath($MsiPath)
$logs = Join-Path (Split-Path $MsiPath) 'MSI-install-validation.log'
$uninstallLog = Join-Path (Split-Path $MsiPath) 'MSI-uninstall-validation.log'
$installed = Join-Path $env:LOCALAPPDATA 'Programs\HourNerd Music Player\HourNerdMusicPlayer.exe'
$msiexec = Join-Path $env:SystemRoot 'System32\msiexec.exe'
try {
    if ($FullUI) {
        Add-Type -AssemblyName UIAutomationClient
        Add-Type -AssemblyName UIAutomationTypes
        Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class MsiButtonClick {
 [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint message, IntPtr w, IntPtr l);
}
'@
        $process = Start-Process -FilePath $msiexec -ArgumentList ('/i "{0}" /norestart /L*v "{1}"' -f $MsiPath,$logs) -PassThru
        $deadline = (Get-Date).AddSeconds(120)
        $seen = @{}
        while (!$process.HasExited -and (Get-Date) -lt $deadline) {
            $windows = [System.Windows.Automation.AutomationElement]::RootElement.FindAll([System.Windows.Automation.TreeScope]::Children,[System.Windows.Automation.Condition]::TrueCondition)
            foreach ($window in $windows) {
                try {
                    if ($window.Current.Name -notmatch 'HourNerd|Windows Installer') { continue }
                    $all = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition)
                    $names = @($all | ForEach-Object { $_.Current.Name })
                    $summary = $names -join ' | '
                    if (!$seen.ContainsKey($summary)) { Write-Host "MSI UI: $summary"; $seen[$summary] = $true }
                    foreach ($control in $all) {
                        if ($control.Current.ControlType -ne [System.Windows.Automation.ControlType]::Button -or !$control.Current.IsEnabled) { continue }
                        if ($control.Current.Name -notmatch '^&?(Install|Finish|OK)$') { continue }
                        Write-Host "Invoking MSI button: $($control.Current.Name)"
                        if ($control.Current.NativeWindowHandle) {
                            [MsiButtonClick]::PostMessage([IntPtr]$control.Current.NativeWindowHandle,0x00F5,[IntPtr]::Zero,[IntPtr]::Zero) | Out-Null
                        } else { throw 'MSI button has no native window handle.' }
                        break
                    }
                } catch [System.Windows.Automation.ElementNotAvailableException] { }
            }
            Start-Sleep -Milliseconds 250
            $process.Refresh()
        }
        if (!$process.HasExited) {
            $process.Kill()
            Get-Content -LiteralPath $logs -Tail 160 | Write-Host
            throw 'MSI full-UI test timed out. Inspect UI and installation logs.'
        }
        $process.WaitForExit()
    } else {
        $process = Start-Process -FilePath $msiexec -ArgumentList ('/i "{0}" /qn /norestart /L*v "{1}"' -f $MsiPath,$logs) -Wait -PassThru
    }
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
