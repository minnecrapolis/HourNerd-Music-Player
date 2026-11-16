[CmdletBinding()]
param([string]$CompilerPath, [switch]$UseExistingDistribution)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$version = '1.1.4'
if (!$CompilerPath) {
    $compilerCommand = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($compilerCommand) { $CompilerPath = $compilerCommand.Source }
    if (!$CompilerPath) {
        $roots = @(${env:ProgramFiles(x86)}, $env:ProgramFiles,
            (Join-Path $env:LOCALAPPDATA 'Programs'), 'E:\Program Files', 'E:\Program Files (x86)') |
            Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -Unique
        $candidates = @($roots | ForEach-Object {
            Get-ChildItem -LiteralPath $_ -Directory -Filter 'Inno Setup*' -ErrorAction SilentlyContinue |
                ForEach-Object { Join-Path $_.FullName 'ISCC.exe' }
        })
        $CompilerPath = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    }
}
if (!$CompilerPath -or !(Test-Path -LiteralPath $CompilerPath)) {
    throw 'Install Inno Setup 6 once: winget install --id JRSoftware.InnoSetup -e -s winget -i. Then run this script again. Download alternative: https://jrsoftware.org/isdl.php'
}
# Default: fresh tested public build. Recovery mode reuses only the public ZIP,
# still checking its complete manifest below; never copy arbitrary build output.
if (!$UseExistingDistribution) { & "$PSScriptRoot\Publish-HourNerdMusicPlayer.ps1" }
$distribution = Join-Path $PSScriptRoot "dist\HourNerd-Music-Player-$version-Windows.zip"
if (!(Test-Path -LiteralPath $distribution)) { throw 'The validated public distribution was not produced.' }
$work = Join-Path ([IO.Path]::GetTempPath()) ('HourNerd-Installer-' + [Guid]::NewGuid().ToString('N'))
$dist = Join-Path $PSScriptRoot 'dist'
$setup = Join-Path $dist "HourNerd-Music-Player-$version-Setup.exe"
New-Item -ItemType Directory -Path $work -Force | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
try {
    [IO.Compression.ZipFile]::ExtractToDirectory($distribution, $work)
    # ZipFile.CreateFromDirectory(stage, output) omits the source base folder.
    $payload = $work
    foreach ($required in @('HourNerdMusicPlayer.exe','public-distribution.flag','SOURCE.zip','LICENSE','MANIFEST.json')) {
        if (!(Test-Path -LiteralPath (Join-Path $payload $required))) { throw "Public payload is missing $required." }
    }
    $manifest = Get-Content -LiteralPath (Join-Path $payload 'MANIFEST.json') -Raw | ConvertFrom-Json
    if ($manifest.version -ne $version) { throw 'Public payload version does not match installer version.' }
    $root = [IO.Path]::GetFullPath($payload).TrimEnd('\') + '\'
    $listed = @{}
    foreach ($entry in $manifest.files) {
        $path = [IO.Path]::GetFullPath((Join-Path $payload $entry.file))
        if ([IO.Path]::IsPathRooted($entry.file) -or !$path.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid payload manifest path.' }
        if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $entry.sha256) { throw "Payload verification failed: $($entry.file)" }
        $listed[$path] = $true
    }
    foreach ($file in (Get-ChildItem -LiteralPath $payload -Recurse -File)) {
        if ($file.Name -ne 'MANIFEST.json' -and !$listed.ContainsKey($file.FullName)) { throw 'Unexpected file in public payload.' }
    }
    if (Test-Path -LiteralPath $setup) { Remove-Item -LiteralPath $setup -Force }
    & $CompilerPath "/DPayloadDir=$payload" "/DInstallerOutput=$dist" "/DProductVersion=$version" "$PSScriptRoot\installer\HourNerdMusicPlayer.iss"
    if ($LASTEXITCODE -ne 0 -or !(Test-Path -LiteralPath $setup)) { throw 'Windows installer compilation failed. See the compiler error above.' }
    $bytes = [IO.File]::ReadAllBytes($setup)
    if ($bytes.Length -lt 2 -or $bytes[0] -ne 0x4D -or $bytes[1] -ne 0x5A) { throw 'The compiler did not produce a Windows executable.' }
    $hash = (Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash
    Set-Content -LiteralPath "$setup.sha256" -Value "$hash  $([IO.Path]::GetFileName($setup))" -Encoding ASCII
    Write-Host "Installer built: $setup"
    Write-Host 'Give recipients this Setup.exe. They do not need the source project, PowerShell scripts, Visual Studio or Inno Setup.'
    Write-Host 'Test station switching, losing focus, restart, updates and uninstall on Windows before public release.'
} finally {
    Remove-Item -LiteralPath $work -Recurse -Force
}
