[CmdletBinding()]
param([string]$SourceDirectory, [switch]$Uninstall)
$ErrorActionPreference = 'Stop'
$name = 'HourNerd Music Player'
$install = Join-Path $env:LOCALAPPDATA 'Programs\HourNerd Music Player'
$support = Join-Path $env:LOCALAPPDATA 'HourNerd Music Player Setup'
$working = Join-Path $env:LOCALAPPDATA 'HourNerd Music Player\Working'
$registry = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\HourNerdMusicPlayer'
$exeName = 'HourNerdMusicPlayer.exe'
$installedExe = Join-Path $install $exeName
$manifestPath = Join-Path $support 'installed-files.json'
$startLink = Join-Path ([Environment]::GetFolderPath('Programs')) "$name.lnk"
$desktopLink = Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) "$name.lnk"
if (Get-Process HourNerdMusicPlayer -ErrorAction SilentlyContinue) {
    throw 'Close HourNerd Music Player, including its system tray icon, then try again.'
}

# Only manifest-listed files beneath our fixed installation directory may be removed.
function Get-ManagedPath([string]$Relative) {
    $root = [IO.Path]::GetFullPath($install).TrimEnd('\') + '\'
    $path = [IO.Path]::GetFullPath((Join-Path $install $Relative))
    if (![IO.Path]::IsPathRooted($Relative) -and $path.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) { return $path }
    throw 'Invalid installation file manifest. No files were removed.'
}

if ($Uninstall) {
    if (!(Test-Path -LiteralPath $manifestPath)) { throw 'Installation manifest is missing; application files were left in place.' }
    $files = @(Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json)
    $paths = @($files | ForEach-Object { Get-ManagedPath ([string]$_) })
    foreach ($path in $paths) {
        if (Test-Path -LiteralPath $path -PathType Leaf) { Remove-Item -LiteralPath $path -Force }
    }
    # Remove empty directories only. Unrecognized files and all user profiles stay intact.
    if (Test-Path -LiteralPath $install) {
        Get-ChildItem -LiteralPath $install -Directory -Recurse | Sort-Object FullName -Descending | ForEach-Object {
            if (!(Get-ChildItem -LiteralPath $_.FullName -Force)) { Remove-Item -LiteralPath $_.FullName }
        }
        if (!(Get-ChildItem -LiteralPath $install -Force)) { Remove-Item -LiteralPath $install }
    }
    $shell = New-Object -ComObject WScript.Shell
    foreach ($link in @($startLink, $desktopLink)) {
        if (Test-Path -LiteralPath $link) {
            if ($shell.CreateShortcut($link).TargetPath -eq $installedExe) { Remove-Item -LiteralPath $link -Force }
        }
    }
    if (Test-Path $registry) { Remove-Item $registry -Recurse -Force }
    Remove-Item -LiteralPath $manifestPath -Force
    Write-Host 'HourNerd Music Player uninstalled. Saved accounts and settings were preserved.'
    exit 0
}

if (!$SourceDirectory) {
    $candidates = @($PSScriptRoot, (Join-Path $PSScriptRoot 'HourNerdMusicPlayer\bin\Release'),
        (Join-Path $PSScriptRoot 'HourNerd Music Player'))
    $SourceDirectory = $candidates | Where-Object { Test-Path -LiteralPath (Join-Path $_ $exeName) } | Select-Object -First 1
}
if (!$SourceDirectory) { throw 'Put this script beside HourNerdMusicPlayer.exe or in your built source project folder, then run it again.' }
$SourceDirectory = (Resolve-Path -LiteralPath $SourceDirectory).Path
$sourceExe = Join-Path $SourceDirectory $exeName
foreach ($required in @($exeName, "$exeName.config", 'PandoraSharp.dll', 'PandoraSharpPlayer.dll', 'bass.dll', 'bass_aac.dll')) {
    if (!(Test-Path -LiteralPath (Join-Path $SourceDirectory $required))) { throw "Missing runtime file: $required. Build the project or extract the complete distribution first." }
}
# Never harvest arbitrary files or account data from the source/build folder.
$payload = @(Get-ChildItem -LiteralPath $SourceDirectory -File | Where-Object {
    $_.Extension -eq '.dll' -or $_.Name -in @($exeName, "$exeName.config", 'public-distribution.flag',
        'README.txt', 'LICENSE', 'AUTHORS.md', 'THIRD-PARTY-NOTICES.txt', 'SOURCE.zip', 'MANIFEST.json')
})
$thirdParty = Join-Path $SourceDirectory 'ThirdParty'
if (Test-Path -LiteralPath $thirdParty) { $payload += @(Get-ChildItem -LiteralPath $thirdParty -Recurse -File) }
New-Item -ItemType Directory -Path $install,$support -Force | Out-Null
$previousFiles = @()
if (Test-Path -LiteralPath $manifestPath) { $previousFiles = @(Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json) }
$newFiles = @()
foreach ($file in $payload) {
    $relative = $file.FullName.Substring($SourceDirectory.TrimEnd('\').Length + 1)
    $destination = Get-ManagedPath $relative
    New-Item -ItemType Directory -Path (Split-Path $destination) -Force | Out-Null
    if ($file.FullName -ne $destination) { Copy-Item -LiteralPath $file.FullName -Destination $destination -Force }
    $newFiles += $relative
}
$allFiles = @($previousFiles + $newFiles | Sort-Object -Unique)
ConvertTo-Json -InputObject $allFiles | Set-Content -LiteralPath $manifestPath -Encoding UTF8
$uninstaller = Join-Path $support 'Install-HourNerdMusicPlayer.ps1'
if ($PSCommandPath -ne $uninstaller) { Copy-Item -LiteralPath $PSCommandPath -Destination $uninstaller -Force }
$shell = New-Object -ComObject WScript.Shell
if (Test-Path -LiteralPath (Join-Path $install 'public-distribution.flag')) { $working = Join-Path $working 'Public' }
New-Item -ItemType Directory -Path $working -Force | Out-Null
foreach ($link in @($startLink, $desktopLink)) {
    $shortcut = $shell.CreateShortcut($link)
    $shortcut.TargetPath = $installedExe
    $shortcut.WorkingDirectory = $working
    $shortcut.IconLocation = "$installedExe,0"
    $shortcut.Description = $name
    $shortcut.Save()
}
$powershell = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
$values = @{
    DisplayName = $name
    DisplayVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($installedExe).FileVersion
    Publisher = 'Chris Dahlkvist / HourNerd'
    InstallLocation = $install
    DisplayIcon = "$installedExe,0"
    UninstallString = ('"{0}" -NoProfile -ExecutionPolicy Bypass -File "{1}" -Uninstall' -f $powershell, $uninstaller)
    InstallDate = (Get-Date -Format 'yyyyMMdd')
}
New-Item -Path $registry -Force | Out-Null
foreach ($key in $values.Keys) { New-ItemProperty -Path $registry -Name $key -Value $values[$key] -PropertyType String -Force | Out-Null }
foreach ($key in @('NoModify','NoRepair')) { New-ItemProperty -Path $registry -Name $key -Value 1 -PropertyType DWord -Force | Out-Null }
$size = [int][Math]::Ceiling((($payload | Measure-Object Length -Sum).Sum) / 1KB)
New-ItemProperty -Path $registry -Name EstimatedSize -Value $size -PropertyType DWord -Force | Out-Null
Write-Host "Installed: $installedExe"
Write-Host 'Start menu and desktop shortcuts created. Reopen Windows Installed Apps or Programs and Features to see the entry.'
Write-Host 'Updates: close the player and rerun this installer from the new build or distribution.'
