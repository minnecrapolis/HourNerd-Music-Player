[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw 'Build this MSI on Windows.' }
$version = '1.1.3'
$dist = Join-Path $PSScriptRoot 'dist'
$zip = Join-Path $dist "HourNerd-Music-Player-$version-Windows.zip"
$output = Join-Path $dist "HourNerd-Music-Player-$version-Setup.msi"
if (!(Test-Path -LiteralPath $zip)) { throw 'Build the public Windows ZIP first with Publish-HourNerdMusicPlayer.ps1.' }
$work = Join-Path ([IO.Path]::GetTempPath()) ('HourNerd-MSI-' + [Guid]::NewGuid().ToString('N'))
$payload = Join-Path $work 'payload'
$cabRoot = Join-Path $work 'cab'
$databasePath = Join-Path $work 'setup.msi'
$installer = $null
$database = $null
$upgradeCode = '{28A6E5C5-D828-4F8B-8B89-95112003DC13}'

function Get-ComArguments([object[]]$Values) {
    # PowerShell may wrap cmdlet results (such as Join-Path) in PSObject.
    # The COM reflection binder needs the underlying string/integer/COM value.
    $result = New-Object object[] $Values.Count
    for ($index = 0; $index -lt $Values.Count; $index++) {
        $value = $Values[$index]
        if ($null -ne $value) { $value = $value.PSObject.BaseObject }
        $result[$index] = $value
    }
    return ,$result
}
function Invoke-Com($Object, [string]$Name, [object[]]$Arguments = @()) {
    $nativeArguments = Get-ComArguments $Arguments
    try {
        $Object.GetType().InvokeMember($Name, [Reflection.BindingFlags]::InvokeMethod, $null, $Object, $nativeArguments)
    } catch {
        throw "Windows Installer method $Name failed: $($_.Exception.Message)"
    }
}
function Set-Com($Object, [string]$Name, [object[]]$Arguments) {
    $nativeArguments = Get-ComArguments $Arguments
    try {
        $Object.GetType().InvokeMember($Name, [Reflection.BindingFlags]::SetProperty, $null, $Object, $nativeArguments) | Out-Null
    } catch {
        throw "Windows Installer property $Name failed: $($_.Exception.Message)"
    }
}
function Release-Com($Object) {
    if ($null -ne $Object -and [Runtime.InteropServices.Marshal]::IsComObject($Object)) {
        [Runtime.InteropServices.Marshal]::FinalReleaseComObject($Object) | Out-Null
    }
}
function Execute-Sql([string]$Sql) {
    $view = Invoke-Com $database 'OpenView' @($Sql)
    try { Invoke-Com $view 'Execute' | Out-Null; Invoke-Com $view 'Close' | Out-Null }
    finally { Release-Com $view }
}
function Add-Row([string]$Table, [string[]]$Columns, [object[]]$Values) {
    if ($Columns.Count -ne $Values.Count) { throw "Invalid $Table row length." }
    $names = ($Columns | ForEach-Object { '`' + $_ + '`' }) -join ','
    $marks = (@($Columns | ForEach-Object { '?' })) -join ','
    $sql = 'INSERT INTO `{0}` ({1}) VALUES ({2})' -f $Table, $names, $marks
    $view = Invoke-Com $database 'OpenView' @($sql)
    $record = Invoke-Com $installer 'CreateRecord' @($Values.Count)
    try {
        for ($i = 0; $i -lt $Values.Count; $i++) {
            if ($null -eq $Values[$i]) { continue }
            if ($Values[$i] -is [int]) { Set-Com $record 'IntegerData' @(($i + 1), $Values[$i]) }
            else { Set-Com $record 'StringData' @(($i + 1), [string]$Values[$i]) }
        }
        Invoke-Com $view 'Execute' @($record) | Out-Null
        Invoke-Com $view 'Close' | Out-Null
    } finally { Release-Com $record; Release-Com $view }
}
function Add-Stream([string]$Table, [string]$Name, [string]$File) {
    $view = Invoke-Com $database 'OpenView' @(('INSERT INTO `{0}` (`Name`,`Data`) VALUES (?,?)' -f $Table))
    $record = Invoke-Com $installer 'CreateRecord' @(2)
    try {
        Set-Com $record 'StringData' @(1, $Name)
        Invoke-Com $record 'SetStream' @(2, $File) | Out-Null
        Invoke-Com $view 'Execute' @($record) | Out-Null
        Invoke-Com $view 'Close' | Out-Null
    } finally { Release-Com $record; Release-Com $view }
}
function Stable-Id([string]$Value) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($Value.ToLowerInvariant())))).Replace('-', '').Substring(0, 30) }
    finally { $sha.Dispose() }
}
function Stable-Guid([string]$Value) {
    $h = (Stable-Id ('HourNerd MSI component v1/' + $Value)) + '00'
    ([Guid]::ParseExact($h, 'N')).ToString('B').ToUpperInvariant()
}
function Msi-Name([string]$Value) {
    # Windows Installer Filename columns explicitly carry both 8.3 and long names.
    'N' + (Stable-Id $Value).Substring(0, 7) + '|' + $Value
}

try {
    New-Item -ItemType Directory -Path $payload,$cabRoot -Force | Out-Null
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::ExtractToDirectory($zip, $payload)
    $manifest = Get-Content -LiteralPath (Join-Path $payload 'MANIFEST.json') -Raw | ConvertFrom-Json
    if ($manifest.product -ne 'HourNerd Music Player' -or $manifest.version -ne $version) { throw 'Unexpected public distribution identity.' }
    foreach ($required in @('HourNerdMusicPlayer.exe','public-distribution.flag','SOURCE.zip','LICENSE')) {
        if (!(Test-Path -LiteralPath (Join-Path $payload $required))) { throw "Public payload is missing $required." }
    }
    $root = [IO.Path]::GetFullPath($payload).TrimEnd('\') + '\'
    $listed = @{}
    foreach ($entry in $manifest.files) {
        $path = [IO.Path]::GetFullPath((Join-Path $payload $entry.file))
        if ([IO.Path]::IsPathRooted($entry.file) -or !$path.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid public payload path.' }
        if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $entry.sha256) { throw "Payload hash mismatch: $($entry.file)" }
        $listed[$path] = $true
    }
    $files = @(Get-ChildItem -LiteralPath $payload -Recurse -File | Sort-Object FullName)
    foreach ($file in $files) {
        if ($file.Name -ne 'MANIFEST.json' -and !$listed.ContainsKey($file.FullName)) { throw 'Unexpected file in public payload.' }
    }
    # Same runtime/docs as the EXE; legacy script installers are not installed.
    $files = @($files | Where-Object { $_.Name -notin @('Install-HourNerdMusicPlayer.ps1','Install-HourNerdMusicPlayer.cmd') })
    if ($files.Count -gt 30000) { throw 'This MSI builder supports at most 30000 files.' }
    $installer = New-Object -ComObject WindowsInstaller.Installer
    $database = Invoke-Com $installer 'OpenDatabase' @([string]$databasePath, [int]3)
    $schema = @(
        'CREATE TABLE `Property` (`Property` CHAR(72) NOT NULL, `Value` CHAR(0) NOT NULL LOCALIZABLE PRIMARY KEY `Property`)',
        'CREATE TABLE `Directory` (`Directory` CHAR(72) NOT NULL, `Directory_Parent` CHAR(72), `DefaultDir` CHAR(255) NOT NULL LOCALIZABLE PRIMARY KEY `Directory`)',
        'CREATE TABLE `Component` (`Component` CHAR(72) NOT NULL, `ComponentId` CHAR(38), `Directory_` CHAR(72) NOT NULL, `Attributes` SHORT NOT NULL, `Condition` CHAR(255), `KeyPath` CHAR(72) PRIMARY KEY `Component`)',
        'CREATE TABLE `Feature` (`Feature` CHAR(38) NOT NULL, `Feature_Parent` CHAR(38), `Title` CHAR(64) LOCALIZABLE, `Description` CHAR(255) LOCALIZABLE, `Display` SHORT, `Level` SHORT NOT NULL, `Directory_` CHAR(72), `Attributes` SHORT NOT NULL PRIMARY KEY `Feature`)',
        'CREATE TABLE `FeatureComponents` (`Feature_` CHAR(38) NOT NULL, `Component_` CHAR(72) NOT NULL PRIMARY KEY `Feature_`,`Component_`)',
        'CREATE TABLE `File` (`File` CHAR(72) NOT NULL, `Component_` CHAR(72) NOT NULL, `FileName` CHAR(255) NOT NULL LOCALIZABLE, `FileSize` LONG NOT NULL, `Version` CHAR(72), `Language` CHAR(20), `Attributes` SHORT, `Sequence` SHORT NOT NULL PRIMARY KEY `File`)',
        'CREATE TABLE `Media` (`DiskId` SHORT NOT NULL, `LastSequence` SHORT NOT NULL, `DiskPrompt` CHAR(64) LOCALIZABLE, `Cabinet` CHAR(255), `VolumeLabel` CHAR(32), `Source` CHAR(72) PRIMARY KEY `DiskId`)',
        'CREATE TABLE `Registry` (`Registry` CHAR(72) NOT NULL, `Root` SHORT NOT NULL, `Key` CHAR(255) NOT NULL LOCALIZABLE, `Name` CHAR(255) LOCALIZABLE, `Value` CHAR(0) LOCALIZABLE, `Component_` CHAR(72) NOT NULL PRIMARY KEY `Registry`)',
        'CREATE TABLE `Shortcut` (`Shortcut` CHAR(72) NOT NULL, `Directory_` CHAR(72) NOT NULL, `Name` CHAR(128) NOT NULL LOCALIZABLE, `Component_` CHAR(72) NOT NULL, `Target` CHAR(255) NOT NULL, `Arguments` CHAR(255), `Description` CHAR(255) LOCALIZABLE, `Hotkey` SHORT, `Icon_` CHAR(72), `IconIndex` SHORT, `ShowCmd` SHORT, `WkDir` CHAR(72) PRIMARY KEY `Shortcut`)',
        'CREATE TABLE `CreateFolder` (`Directory_` CHAR(72) NOT NULL, `Component_` CHAR(72) NOT NULL PRIMARY KEY `Directory_`,`Component_`)',
        'CREATE TABLE `Icon` (`Name` CHAR(72) NOT NULL, `Data` OBJECT NOT NULL PRIMARY KEY `Name`)',
        'CREATE TABLE `AppSearch` (`Property` CHAR(72) NOT NULL, `Signature_` CHAR(72) NOT NULL PRIMARY KEY `Property`,`Signature_`)',
        'CREATE TABLE `RegLocator` (`Signature_` CHAR(72) NOT NULL, `Root` SHORT NOT NULL, `Key` CHAR(255) NOT NULL, `Name` CHAR(255), `Type` SHORT PRIMARY KEY `Signature_`)',
        'CREATE TABLE `LaunchCondition` (`Condition` CHAR(255) NOT NULL, `Description` CHAR(255) NOT NULL LOCALIZABLE PRIMARY KEY `Condition`)',
        'CREATE TABLE `Upgrade` (`UpgradeCode` CHAR(38) NOT NULL, `VersionMin` CHAR(20), `VersionMax` CHAR(20), `Language` CHAR(255), `Attributes` SHORT NOT NULL, `Remove` CHAR(255), `ActionProperty` CHAR(72) NOT NULL PRIMARY KEY `UpgradeCode`,`VersionMin`,`VersionMax`,`Language`,`Attributes`)',
        'CREATE TABLE `TextStyle` (`TextStyle` CHAR(72) NOT NULL, `FaceName` CHAR(32) NOT NULL, `Size` SHORT NOT NULL, `Color` LONG, `StyleBits` SHORT PRIMARY KEY `TextStyle`)',
        'CREATE TABLE `Dialog` (`Dialog` CHAR(72) NOT NULL, `HCentering` SHORT NOT NULL, `VCentering` SHORT NOT NULL, `Width` SHORT NOT NULL, `Height` SHORT NOT NULL, `Attributes` LONG, `Title` CHAR(128) LOCALIZABLE, `Control_First` CHAR(50) NOT NULL, `Control_Default` CHAR(50), `Control_Cancel` CHAR(50) PRIMARY KEY `Dialog`)',
        'CREATE TABLE `Control` (`Dialog_` CHAR(72) NOT NULL, `Control` CHAR(50) NOT NULL, `Type` CHAR(20) NOT NULL, `X` SHORT NOT NULL, `Y` SHORT NOT NULL, `Width` SHORT NOT NULL, `Height` SHORT NOT NULL, `Attributes` LONG, `Property` CHAR(72), `Text` CHAR(0) LOCALIZABLE, `Control_Next` CHAR(50), `Help` CHAR(50) LOCALIZABLE PRIMARY KEY `Dialog_`,`Control`)',
        'CREATE TABLE `ControlEvent` (`Dialog_` CHAR(72) NOT NULL, `Control_` CHAR(50) NOT NULL, `Event` CHAR(50) NOT NULL, `Argument` CHAR(255), `Condition` CHAR(255), `Ordering` SHORT PRIMARY KEY `Dialog_`,`Control_`,`Event`,`Argument`,`Condition`)',
        'CREATE TABLE `InstallExecuteSequence` (`Action` CHAR(72) NOT NULL, `Condition` CHAR(255), `Sequence` SHORT PRIMARY KEY `Action`)',
        'CREATE TABLE `InstallUISequence` (`Action` CHAR(72) NOT NULL, `Condition` CHAR(255), `Sequence` SHORT PRIMARY KEY `Action`)',
        'CREATE TABLE `AdminExecuteSequence` (`Action` CHAR(72) NOT NULL, `Condition` CHAR(255), `Sequence` SHORT PRIMARY KEY `Action`)',
        'CREATE TABLE `AdminUISequence` (`Action` CHAR(72) NOT NULL, `Condition` CHAR(255), `Sequence` SHORT PRIMARY KEY `Action`)'
    )
    foreach ($sql in $schema) { Execute-Sql $sql }
    $properties = @{
        ProductCode = ([Guid]::NewGuid().ToString('B').ToUpperInvariant()); ProductName = 'HourNerd Music Player'
        ProductVersion = $version; Manufacturer = 'Chris Dahlkvist / HourNerd'; ProductLanguage = '1033'
        UpgradeCode = $upgradeCode; ARPPRODUCTICON = 'HourNerd.ico'; ARPNOMODIFY = '1'; ARPNOREPAIR = '1'
        SecureCustomProperties = 'OLDPRODUCTS;NEWERPRODUCTS'; MSIRESTARTMANAGERCONTROL = 'DisableShutdown'
        DefaultUIFont = 'DlgFont'
    }
    foreach ($key in $properties.Keys) { Add-Row 'Property' @('Property','Value') @($key,$properties[$key]) }
    Add-Row 'Feature' @('Feature','Title','Display','Level','Attributes') @('Main','HourNerd Music Player',2,1,0)
    $directories = @(
        @('TARGETDIR',$null,'SourceDir'), @('LocalAppDataFolder','TARGETDIR','.'),
        @('ProgramsDir','LocalAppDataFolder','Programs'), @('INSTALLDIR','ProgramsDir','HourNerd Music Player'),
        @('WorkRoot','LocalAppDataFolder','HourNerd Music Player'), @('WorkFolder','WorkRoot','Working'),
        @('WorkPublic','WorkFolder','Public'), @('ProgramMenuFolder','TARGETDIR','.'), @('DesktopFolder','TARGETDIR','.')
    )
    foreach ($row in $directories) {
        if ($row[2] -notin @('SourceDir','.')) { $row[2] = Msi-Name $row[2] }
        Add-Row 'Directory' @('Directory','Directory_Parent','DefaultDir') $row
    }
    $dirIds = @{ '' = 'INSTALLDIR' }
    $ddf = [Collections.Generic.List[string]]::new()
    foreach ($line in @('.OPTION EXPLICIT','.Set Cabinet=on','.Set Compress=on','.Set CompressionType=MSZIP',
        '.Set MaxDiskSize=0','.Set CabinetNameTemplate=payload.cab','.Set DiskDirectoryTemplate=.')) { $ddf.Add($line) }
    $sequence = 0
    $mainFileId = $null
    foreach ($file in $files) {
        $relative = $file.FullName.Substring($root.Length)
        $relativeDir = [IO.Path]::GetDirectoryName($relative)
        $parent = 'INSTALLDIR'; $accumulated = ''
        if ($relativeDir) {
            foreach ($part in ($relativeDir -split '\\')) {
                $accumulated = if ($accumulated) { $accumulated + '\' + $part } else { $part }
                if (!$dirIds.ContainsKey($accumulated)) {
                    $id = 'd' + (Stable-Id $accumulated)
                    Add-Row 'Directory' @('Directory','Directory_Parent','DefaultDir') @($id,$parent,(Msi-Name $part))
                    $dirIds[$accumulated] = $id
                }
                $parent = $dirIds[$accumulated]
            }
        }
        $id = Stable-Id $relative; $component = 'c' + $id; $fileId = 'f' + $id; $regId = 'r' + $id
        Add-Row 'Component' @('Component','ComponentId','Directory_','Attributes','KeyPath') @($component,(Stable-Guid $relative),$parent,4,$regId)
        Add-Row 'Registry' @('Registry','Root','Key','Name','Value','Component_') @($regId,1,'Software\HourNerd\Music Player\MSIFiles',$id,$version,$component)
        Add-Row 'FeatureComponents' @('Feature_','Component_') @('Main',$component)
        $sequence++
        $fileVersion = $null
        if ($file.Extension -in @('.exe','.dll')) {
            $candidate = [Diagnostics.FileVersionInfo]::GetVersionInfo($file.FullName).FileVersion
            if ($candidate -match '^\d+\.\d+\.\d+\.\d+$') { $fileVersion = $candidate }
        }
        if ($file.Length -gt [int]::MaxValue) { throw 'MSI file exceeds the supported size.' }
        Add-Row 'File' @('File','Component_','FileName','FileSize','Version','Attributes','Sequence') @($fileId,$component,(Msi-Name $file.Name),[int]$file.Length,$fileVersion,16896,$sequence)
        Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $cabRoot $fileId)
        $ddf.Add(('"{0}" {0}' -f $fileId))
        if ($relative -eq 'HourNerdMusicPlayer.exe') { $mainFileId = $fileId }
    }
    if (!$mainFileId) { throw 'Main executable not mapped into MSI.' }
    Add-Row 'Component' @('Component','ComponentId','Directory_','Attributes','KeyPath') @('Shortcuts',(Stable-Guid 'shortcuts'),'INSTALLDIR',4,'InstallerType')
    Add-Row 'Registry' @('Registry','Root','Key','Name','Value','Component_') @('InstallerType',1,'Software\HourNerd\Music Player\Installer','Type','MSI','Shortcuts')
    Add-Row 'FeatureComponents' @('Feature_','Component_') @('Main','Shortcuts')
    Add-Row 'CreateFolder' @('Directory_','Component_') @('WorkPublic','Shortcuts')
    foreach ($row in @(@('StartShortcut','ProgramMenuFolder'),@('DesktopShortcut','DesktopFolder'))) {
        Add-Row 'Shortcut' @('Shortcut','Directory_','Name','Component_','Target','Description','Icon_','IconIndex','ShowCmd','WkDir') @($row[0],$row[1],'HOURNERD|HourNerd Music Player','Shortcuts',('[#' + $mainFileId + ']'),'HourNerd Music Player','HourNerd.ico',0,1,'WorkPublic')
    }
    Add-Stream 'Icon' 'HourNerd.ico' (Join-Path $PSScriptRoot 'HourNerdMusicPlayer\Resources\main_icon.ico')
    foreach ($search in @(
        @('NET481','NetRelease',2,'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full','Release'),
        @('EXEINSTALLED','ExeInstall',1,'Software\Microsoft\Windows\CurrentVersion\Uninstall\HourNerdMusicPlayer_is1','InstallLocation'),
        @('SCRIPTINSTALLED','ScriptInstall',1,'Software\Microsoft\Windows\CurrentVersion\Uninstall\HourNerdMusicPlayer','InstallLocation')
    )) {
        Add-Row 'AppSearch' @('Property','Signature_') @($search[0],$search[1])
        Add-Row 'RegLocator' @('Signature_','Root','Key','Name','Type') @($search[1],[int]$search[2],$search[3],$search[4],2)
    }
    Add-Row 'LaunchCondition' @('Condition','Description') @('Installed OR (NET481 AND NET481 >= "#533320")','Install the Microsoft .NET Framework 4.8.1 runtime, then run Setup again.')
    Add-Row 'LaunchCondition' @('Condition','Description') @('Installed OR (NOT EXEINSTALLED AND NOT SCRIPTINSTALLED)','Uninstall the existing EXE/script installation before installing the MSI. Your saved accounts and settings will be preserved.')
    Add-Row 'LaunchCondition' @('Condition','Description') @('Installed OR NOT NEWERPRODUCTS','A newer HourNerd Music Player MSI is already installed.')
    Add-Row 'Upgrade' @('UpgradeCode','VersionMax','Attributes','ActionProperty') @($upgradeCode,$version,512,'OLDPRODUCTS')
    Add-Row 'Upgrade' @('UpgradeCode','VersionMin','Attributes','ActionProperty') @($upgradeCode,$version,2,'NEWERPRODUCTS')
    Add-Row 'TextStyle' @('TextStyle','FaceName','Size','StyleBits') @('DlgFont','Segoe UI',9,0)
    Add-Row 'TextStyle' @('TextStyle','FaceName','Size','StyleBits') @('HeaderFont','Segoe UI',16,1)
    foreach ($dialog in @('WelcomeDlg','FinishedDlg')) {
        $isWelcome = $dialog -eq 'WelcomeDlg'
        $first = if ($isWelcome) { 'Install' } else { 'Finish' }
        $cancel = if ($isWelcome) { 'Cancel' } else { 'Finish' }
        Add-Row 'Dialog' @('Dialog','HCentering','VCentering','Width','Height','Attributes','Title','Control_First','Control_Default','Control_Cancel') @($dialog,50,50,370,200,3,'HourNerd Music Player Setup',$first,$first,$cancel)
        $heading = if ($isWelcome) { '{\HeaderFont}HourNerd Music Player' } else { '{\HeaderFont}Installation complete' }
        $body = if ($isWelcome) {
            'Install HourNerd Music Player for your Windows account. Setup includes the player, supporting files, Start menu and desktop shortcuts. Close any running copy before continuing. Audio: BASS / BASS.NET. Based on Elpis; see About and the installed license notices.'
        } else { 'HourNerd Music Player is installed. Open it from the Start menu and sign in with your own Pandora account. Your stations will appear after sign-in.' }
        Add-Row 'Control' @('Dialog_','Control','Type','X','Y','Width','Height','Attributes','Text') @($dialog,'Heading','Text',15,15,340,28,3,$heading)
        Add-Row 'Control' @('Dialog_','Control','Type','X','Y','Width','Height','Attributes','Text') @($dialog,'Body','Text',15,55,340,90,3,$body)
        $label = if ($isWelcome) { 'Install' } else { 'Finish' }
        Add-Row 'Control' @('Dialog_','Control','Type','X','Y','Width','Height','Attributes','Text','Control_Next') @($dialog,$first,'PushButton',185,165,80,23,3,$label,$cancel)
        Add-Row 'ControlEvent' @('Dialog_','Control_','Event','Argument','Condition','Ordering') @($dialog,$first,'EndDialog','Return','1',1)
        if ($isWelcome) {
            Add-Row 'Control' @('Dialog_','Control','Type','X','Y','Width','Height','Attributes','Text','Control_Next') @($dialog,'Cancel','PushButton',275,165,80,23,3,'Cancel','Install')
            Add-Row 'ControlEvent' @('Dialog_','Control_','Event','Argument','Condition','Ordering') @($dialog,'Cancel','EndDialog','Exit','1',1)
        }
    }
    Add-Row 'InstallUISequence' @('Action','Condition','Sequence') @('WelcomeDlg','NOT Installed',1280)
    Add-Row 'InstallUISequence' @('Action','Condition','Sequence') @('FinishedDlg','NOT Installed',-1)
    foreach ($row in @(@('FindRelatedProducts',25),@('AppSearch',50),@('LaunchConditions',100),@('ExecuteAction',1300))) {
        Add-Row 'InstallUISequence' @('Action','Sequence') @($row[0],[int]$row[1])
    }
    $actions = @(
        @('FindRelatedProducts',25),@('AppSearch',50),@('LaunchConditions',100),@('CostInitialize',800),@('FileCost',900),@('CostFinalize',1000),
        @('InstallValidate',1400),@('InstallInitialize',1500),@('RemoveExistingProducts',1550),@('ProcessComponents',1600),
        @('UnpublishFeatures',1800),@('RemoveRegistryValues',2600),@('RemoveShortcuts',3200),@('RemoveFiles',3500),@('RemoveFolders',3600),
        @('CreateFolders',3700),@('InstallFiles',4000),@('CreateShortcuts',4500),@('WriteRegistryValues',5000),
        @('RegisterUser',6000),@('RegisterProduct',6100),@('PublishFeatures',6300),@('PublishProduct',6400),@('InstallFinalize',6600)
    )
    foreach ($row in $actions) {
        $condition = if ($row[0] -eq 'RemoveExistingProducts') { 'OLDPRODUCTS' } else { $null }
        Add-Row 'InstallExecuteSequence' @('Action','Condition','Sequence') @($row[0],$condition,[int]$row[1])
    }
    foreach ($row in @(@('CostInitialize',800),@('FileCost',900),@('CostFinalize',1000),@('InstallValidate',1400),
        @('InstallInitialize',1500),@('InstallAdminPackage',3900),@('InstallFiles',4000),@('InstallFinalize',6600))) {
        Add-Row 'AdminExecuteSequence' @('Action','Sequence') @($row[0],[int]$row[1])
    }
    Add-Row 'AdminUISequence' @('Action','Sequence') @('ExecuteAction',1300)
    Add-Row 'Media' @('DiskId','LastSequence','Cabinet') @(1,$sequence,'#payload.cab')
    # CAB staging names and DDF are ASCII; original file names live in the MSI database.
    $ddf | Set-Content -LiteralPath (Join-Path $cabRoot 'payload.ddf') -Encoding ASCII
    Push-Location $cabRoot
    try {
        & "$env:SystemRoot\System32\makecab.exe" /F payload.ddf
        if ($LASTEXITCODE -ne 0) { throw 'Windows cabinet creation failed.' }
    } finally { Pop-Location }
    Add-Stream '_Streams' 'payload.cab' (Join-Path $cabRoot 'payload.cab')
    $summary = $database.GetType().InvokeMember('SummaryInformation', [Reflection.BindingFlags]::GetProperty, $null, $database, @(20))
    try {
        $summaryValues = @{ 1 = [int16]1252; 2 = 'Installation Database'; 3 = 'HourNerd Music Player';
            4 = 'Chris Dahlkvist / HourNerd'; 7 = 'Intel;1033'; 9 = ([Guid]::NewGuid().ToString('B').ToUpperInvariant());
            14 = 400; 15 = 10; 18 = 'HourNerd Windows MSI Builder' }
        foreach ($key in $summaryValues.Keys) { Set-Com $summary 'Property' @([int]$key,$summaryValues[$key]) }
        Invoke-Com $summary 'Persist' | Out-Null
    } finally { Release-Com $summary }
    Invoke-Com $database 'Commit' | Out-Null
    Release-Com $database; $database = $null
    # Verify actual Windows Installer extraction before publishing the MSI. This
    # is an administrative image in a temporary folder, not an app installation:
    # no live shortcuts, accounts or installed-app registration are changed.
    $verify = Join-Path $work 'verify'
    # Administrative images follow the Directory table beneath TARGETDIR.
    $verifyApp = Join-Path $verify 'Programs\HourNerd Music Player'
    $log = Join-Path $dist 'MSI-build-validation.log'
    $arguments = '/a "{0}" TARGETDIR="{1}" /qn /norestart /L*v "{2}"' -f $databasePath, $verify, $log
    $check = Start-Process -FilePath "$env:SystemRoot\System32\msiexec.exe" -ArgumentList $arguments -Wait -PassThru
    if ($check.ExitCode -notin @(0,3010)) { throw "MSI extraction validation failed ($($check.ExitCode)). Details: $log" }
    foreach ($file in $files) {
        $relative = $file.FullName.Substring($root.Length)
        $extracted = Join-Path $verifyApp $relative
        if (!(Test-Path -LiteralPath $extracted) -or
            (Get-FileHash -LiteralPath $extracted -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash) {
            throw "MSI extraction verification failed for $relative. Details: $log"
        }
    }
    Copy-Item -LiteralPath $databasePath -Destination $output -Force
    $hash = (Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash
    Set-Content -LiteralPath "$output.sha256" -Value "$hash  $([IO.Path]::GetFileName($output))" -Encoding ASCII
    Write-Host "MSI built: $output"
    Write-Host ('Install with Windows progress UI: msiexec.exe /i "{0}" /qb' -f $output)
    Write-Host 'Test MSI install, repair, upgrade and uninstall on a clean Windows account before distribution.'
} finally {
    Release-Com $database
    Release-Com $installer
    if (Test-Path -LiteralPath $work) { Remove-Item -LiteralPath $work -Recurse -Force }
}
