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
using System.Text;
using System.Collections.Generic;
public static class MsiButtonClick {
 public delegate bool EnumProc(IntPtr hwnd, IntPtr l);
 [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb,IntPtr l);
 [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr hwnd,EnumProc cb,IntPtr l);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr hwnd,StringBuilder b,int n);
 [DllImport("user32.dll")] public static extern bool IsWindowEnabled(IntPtr hwnd);
 public static string Title(IntPtr h) { var b=new StringBuilder(2048); GetWindowText(h,b,b.Capacity); return b.ToString(); }
 public static string Pump() {
   var messages=new List<string>();
   EnumWindows((h,l)=> {
     var title=Title(h);
     if (title.IndexOf("HourNerd",StringComparison.OrdinalIgnoreCase)<0 && title.IndexOf("Windows Installer",StringComparison.OrdinalIgnoreCase)<0) return true;
     messages.Add("Window: "+title);
     EnumChildWindows(h,(c,x)=> {
       var text=Title(c); if(text.Length>0) messages.Add(text);
       var label=text.Replace("&","");
       if (IsWindowEnabled(c) && (label=="Install" || label=="Finish" || label=="OK")) PostMessage(c,0x00F5,IntPtr.Zero,IntPtr.Zero);
       return true;
     },IntPtr.Zero);
     return true;
   },IntPtr.Zero);
   return String.Join(" | ",messages);
 }
 [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint message, IntPtr w, IntPtr l);
}
'@
        $process = Start-Process -FilePath $msiexec -ArgumentList ('/i "{0}" /norestart /L*v "{1}"' -f $MsiPath,$logs) -PassThru
        $deadline = (Get-Date).AddSeconds(120)
        $seen = @{}
        while (!$process.HasExited -and (Get-Date) -lt $deadline) {
            $nativeSummary = [MsiButtonClick]::Pump()
            if ($nativeSummary -and !$seen.ContainsKey($nativeSummary)) { Write-Host "MSI native UI: $nativeSummary"; $seen[$nativeSummary] = $true }
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
