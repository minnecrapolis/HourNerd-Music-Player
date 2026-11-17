[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$MsiPath)
$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class MsiPreviewChecks {
 [UnmanagedFunctionPointer(CallingConvention.Winapi, CharSet=CharSet.Unicode)]
 public delegate int ErrorHandler(IntPtr context, uint type, string text);
 private static ErrorHandler handler = delegate(IntPtr context, uint type, string text) { Console.WriteLine("MSI UI diagnostic: " + text); return 1; };
 [DllImport("msi.dll", CharSet=CharSet.Unicode)] private static extern IntPtr MsiSetExternalUI(ErrorHandler handler, uint filter, IntPtr context);
 public static void CaptureErrors() { MsiSetExternalUI(handler, 3, IntPtr.Zero); }
 public static void ClearErrors() { MsiSetExternalUI(null, 0, IntPtr.Zero); }

 [DllImport("msi.dll", CharSet=CharSet.Unicode)] public static extern uint MsiOpenDatabase(string path, IntPtr mode, out uint database);
 [DllImport("msi.dll")] public static extern uint MsiEnableUIPreview(uint database, out uint preview);
 [DllImport("msi.dll", CharSet=CharSet.Unicode)] public static extern uint MsiPreviewDialog(uint preview, string dialog);
 [DllImport("msi.dll")] public static extern uint MsiCloseHandle(uint handle);
 [DllImport("msi.dll")] public static extern uint MsiGetLastErrorRecord();
 [DllImport("msi.dll", CharSet=CharSet.Unicode)] public static extern uint MsiFormatRecord(uint install, uint record, StringBuilder text, ref uint length);
 public static string LastError() {
   uint record=MsiGetLastErrorRecord(); if(record==0) return "No extended error record";
   try { uint size=4096; var text=new StringBuilder((int)size); MsiFormatRecord(0,record,text,ref size); return text.ToString(); }
   finally { MsiCloseHandle(record); }
 }
}
'@
[MsiPreviewChecks]::CaptureErrors()
[uint32]$database = 0; [uint32]$preview = 0
try {
    $result = [MsiPreviewChecks]::MsiOpenDatabase([IO.Path]::GetFullPath($MsiPath), [IntPtr]::Zero, [ref]$database)
    if ($result -ne 0) { throw "MSI open failed ($result): $([MsiPreviewChecks]::LastError())" }
    $result = [MsiPreviewChecks]::MsiEnableUIPreview($database, [ref]$preview)
    if ($result -ne 0) { throw "MSI UI preview initialization failed ($result): $([MsiPreviewChecks]::LastError())" }
    foreach ($dialog in 'WelcomeDlg','FinishedDlg') {
        $result = [MsiPreviewChecks]::MsiPreviewDialog($preview, $dialog)
        if ($result -ne 0) { throw "MSI dialog $dialog failed ($result): $([MsiPreviewChecks]::LastError())" }
        Write-Host "PASS native MSI dialog preview: $dialog"
        [MsiPreviewChecks]::MsiPreviewDialog($preview, $null) | Out-Null
    }
} finally {
    [MsiPreviewChecks]::ClearErrors()
    if ($preview) { [MsiPreviewChecks]::MsiCloseHandle($preview) | Out-Null }
    if ($database) { [MsiPreviewChecks]::MsiCloseHandle($database) | Out-Null }
}
