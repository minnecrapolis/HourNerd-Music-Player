$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$version = '1.1.3'
$bin = Join-Path $PSScriptRoot 'HourNerdMusicPlayer\bin\Release'
if (Get-Process HourNerdMusicPlayer -ErrorAction SilentlyContinue) {
    throw 'Close HourNerd Music Player, including its system tray icon, before publishing.'
}
& "$PSScriptRoot\Build-HourNerdMusicPlayer.ps1" -Test -Distribution

$work = Join-Path ([IO.Path]::GetTempPath()) ('HourNerd-Publish-' + [Guid]::NewGuid().ToString('N'))
$stage = Join-Path $work 'HourNerd Music Player'
$sourceStage = Join-Path $work 'source'
New-Item -ItemType Directory -Path $stage,$sourceStage -Force | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
try {
    # Explicit runtime list: never copy the user's application-data directory,
    # arbitrary output files, diagnostic reports, test runner or debug symbols.
    $required = @(
        'HourNerdMusicPlayer.exe', 'HourNerdMusicPlayer.exe.config',
        'Bass.Net.dll', 'BassPlayer.dll', 'bass.dll', 'bass_aac.dll',
        'Util.dll', 'PandoraSharp.dll', 'PandoraSharpPlayer.dll',
        'PandoraSharpScrobbler.dll', 'PlayerControlQuery.dll',
        'Lpfm.LastFmScrobbler.dll', 'Kayak.dll', 'Newtonsoft.Json.dll',
        'FontAwesome.WPF.dll', 'MahApps.Metro.dll', 'ControlzEx.dll', 'MaterialDesignColors.dll',
        'MaterialDesignThemes.Wpf.dll', 'Microsoft.WindowsAPICodePack.dll',
        'Microsoft.WindowsAPICodePack.Shell.dll', 'System.Windows.Interactivity.dll'
    )
    $optional = @('Microsoft.Xaml.Behaviors.dll')
    foreach ($name in $required) {
        $path = Join-Path $bin $name
        if (!(Test-Path $path)) { throw "A required runtime dependency was not produced: $name" }
        Copy-Item $path (Join-Path $stage $name)
    }
    foreach ($name in $optional) {
        $path = Join-Path $bin $name
        if (Test-Path $path) { Copy-Item $path (Join-Path $stage $name) }
    }
    # Reject developer-machine path leakage in the compiled runtime, including
    # CodeView/debug records. Public credits and the requested tipping address
    # are intentional product information and are retained.
    $privatePaths = @($PSScriptRoot, $env:USERPROFILE) | Where-Object { $_ -and $_.Length -gt 7 }
    foreach ($file in (Get-ChildItem $stage -File | Where-Object { $_.Extension -in @('.exe','.dll') })) {
        $bytes = [IO.File]::ReadAllBytes($file.FullName)
        $utf8 = [Text.Encoding]::UTF8.GetString($bytes)
        $utf16 = [Text.Encoding]::Unicode.GetString($bytes)
        foreach ($privatePath in $privatePaths) {
            if ($utf8.IndexOf($privatePath, [StringComparison]::OrdinalIgnoreCase) -ge 0 -or
                $utf16.IndexOf($privatePath, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                throw "A developer-machine path was found in $($file.Name). Distribution stopped."
            }
        }
    }
    Set-Content (Join-Path $stage 'public-distribution.flag') -Value 'Public build: use a separate per-user profile; do not import development or legacy account settings.' -Encoding UTF8
    Copy-Item "$PSScriptRoot\DISTRIBUTION-README.txt" (Join-Path $stage 'README.txt')
    Copy-Item "$PSScriptRoot\Install-HourNerdMusicPlayer.ps1" $stage
    Copy-Item "$PSScriptRoot\Install-HourNerdMusicPlayer.cmd" $stage
    Copy-Item "$PSScriptRoot\LICENSE" (Join-Path $stage 'LICENSE')
    Copy-Item "$PSScriptRoot\AUTHORS.md" (Join-Path $stage 'AUTHORS.md')
    Copy-Item "$PSScriptRoot\THIRD-PARTY-NOTICES.txt" (Join-Path $stage 'THIRD-PARTY-NOTICES.txt')

    # Include available component notices, preserving their relative locations.
    foreach ($folder in 'Libs','packages') {
        $root = Join-Path $PSScriptRoot $folder
        Get-ChildItem $root -Recurse -File | Where-Object { $_.Name -match '^(LICENSE|LICENCE|COPYING|NOTICE)(\..*)?$' } | ForEach-Object {
            $relative = $_.FullName.Substring($PSScriptRoot.Length + 1)
            $destination = Join-Path $stage (Join-Path 'ThirdParty' $relative)
            New-Item -ItemType Directory -Path (Split-Path $destination) -Force | Out-Null
            Copy-Item $_.FullName $destination
        }
    }

    # Include corresponding source. Filter the repository itself, never AppData.
    $sourceExtensions = @('.cs','.xaml','.csproj','.sln','.ps1','.targets','.props',
        '.config','.resx','.settings','.txt','.md','.rtf','.xml','.html','.htm',
        '.js','.css','.svg','.png','.jpg','.jpeg','.ico','.gif','.wav',
        '.dll','.vdproj','.sed','.iss','.url','.sh','.bat','.cmd','.rl','.yml','.yaml')
    Get-ChildItem $PSScriptRoot -Recurse -File | ForEach-Object {
        $relative = $_.FullName.Substring($PSScriptRoot.Length + 1)
        $first = ($relative -split '[\\/]')[0]
        $rootFiles = @('README.md','START-HERE.txt','AUTHORS.md','ChangeLog.txt','CONTRIBUTING.md',
            'DISTRIBUTION-README.txt','THIRD-PARTY-NOTICES.txt','LICENSE','Directory.Build.targets',
            'HourNerdMusicPlayer.sln','Build-HourNerdMusicPlayer.ps1','Publish-HourNerdMusicPlayer.ps1',
            'Install-HourNerdMusicPlayer.ps1','Install-HourNerdMusicPlayer.cmd','INSTALL-WINDOWS.txt',
            'Build-Windows-Installer.ps1','Build-Windows-Installer.cmd',
            'Build-Windows-Installers.ps1','Build-Windows-Installers.cmd','Build-Windows-MSI.ps1',
            'GITHUB-RELEASE.txt',
            'Check-Pandora.ps1','Start-HourNerdMusicPlayer-Diagnostics.ps1',
            'Show-HourNerdMusicPlayer-Timings.ps1','Show-HourNerdMusicPlayer-Crash.ps1','.gitignore','.gitattributes')
        if ($first -notin @('HourNerdMusicPlayer','Libs','rebuild','upstream','installer','.github') -and $relative -notin $rootFiles) { return }
        if ($_.Name -eq 'ReleaseData.cs' -or $_.Name -match '(?i)(secrets|\.local\.)') { return }
        if ($relative -match '(^|[\\/])(bin|obj|packages|dist|\.git|\.vs)([\\/]|$)') { return }
        if ($_.Name -match '\.(pdb|log|user|suo|pfx|p12|zip)$' -or $_.Name -match '^(elpis|hournerdmusicplayer)\.config$') { return }
        if ($sourceExtensions -notcontains $_.Extension.ToLowerInvariant() -and $_.Name -notin @('LICENSE','COPYING','NOTICE','.gitignore','.gitattributes')) { return }
        if ($_.Extension -eq '.config' -and $_.Name -notin @('packages.config','app.config','App.config','Elpis-version.config')) { return }
        # Original bundled DLLs are source dependencies; compiled outputs are excluded above.
        if ($_.Extension -eq '.dll' -and !$relative.StartsWith('Libs\', [StringComparison]::OrdinalIgnoreCase)) { return }
        $destination = Join-Path $sourceStage $relative
        New-Item -ItemType Directory -Path (Split-Path $destination) -Force | Out-Null
        Copy-Item $_.FullName $destination
    }
    [IO.Compression.ZipFile]::CreateFromDirectory($sourceStage, (Join-Path $stage 'SOURCE.zip'))

    $forbidden = Get-ChildItem $stage -Recurse -File | Where-Object {
        $_.Extension -in @('.pdb','.log','.user','.suo','.pfx','.p12') -or
        ($_.Extension -eq '.config' -and $_.Name -ne 'HourNerdMusicPlayer.exe.config')
    }
    if ($forbidden) { throw 'Distribution validation found files that should not be shared.' }
    $manifest = Get-ChildItem $stage -Recurse -File | ForEach-Object {
        [ordered]@{ file = $_.FullName.Substring($stage.Length + 1); sha256 = (Get-FileHash $_.FullName -Algorithm SHA256).Hash }
    }
    [ordered]@{ product = 'HourNerd Music Player'; version = $version; files = @($manifest) } |
        ConvertTo-Json -Depth 5 | Set-Content (Join-Path $stage 'MANIFEST.json') -Encoding UTF8
    $dist = Join-Path $PSScriptRoot 'dist'
    New-Item -ItemType Directory -Path $dist -Force | Out-Null
    $output = Join-Path $dist "HourNerd-Music-Player-$version-Windows.zip"
    if (Test-Path $output) { Remove-Item $output }
    [IO.Compression.ZipFile]::CreateFromDirectory($stage, $output)
    Write-Host "Ready to share: $output"
    Write-Host 'Recipients extract this ZIP and run Install-HourNerdMusicPlayer.cmd. No Visual Studio or compilation is required for them.'
} finally {
    if (Test-Path $work) { Remove-Item $work -Recurse -Force }
}
