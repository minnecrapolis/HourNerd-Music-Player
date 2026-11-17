$ErrorActionPreference='Stop'
try {
    Write-Host 'HourNerd Spotify setup: adds a local control bridge and ad-blocking extension.'
    Write-Host 'Spotify will restart. Sign in to Spotify normally; no developer API key is needed.'
    $keyFile=Join-Path $env:APPDATA 'HourNerd Music Player\SpotifyBridge\bridge.key'
    if(!(Test-Path $keyFile)){throw 'Open HourNerd and choose Spotify from the dropdown once, then run setup again.'}
    $key=(Get-Content $keyFile -Raw).Trim()
    if($key -notmatch '^[a-f0-9]{64}$'){throw 'The local bridge key is invalid. Close HourNerd, delete its SpotifyBridge\bridge.key file, reopen it and select Spotify again.'}
    $cli=Get-ChildItem (Join-Path $PSScriptRoot 'cli') -Filter spicetify.exe -Recurse | Select-Object -First 1 -ExpandProperty FullName
    if(!$cli){throw 'The bundled Spicetify tool was not found. Extract and apply the whole update ZIP.'}
    function Run-Spicetify([string[]]$Arguments){ & $cli @Arguments; if($LASTEXITCODE -ne 0){throw 'Spicetify could not finish. Read its message above; your Spotify version may need a compatible Spicetify update.'} }
    $data=((& $cli path userdata) | Out-String).Trim()
    if($LASTEXITCODE -ne 0 -or !$data){throw 'Spicetify could not find its configuration directory. Install the Spotify desktop app and sign in first.'}
    $config=Join-Path $data 'config-xpui.ini'
    $backup=Join-Path $data ('HourNerd-backup-'+(Get-Date -Format yyyyMMdd-HHmmss))
    New-Item -ItemType Directory $backup -Force | Out-Null
    if(Test-Path $config){Copy-Item $config $backup}
    $extensions=Join-Path $data 'Extensions';New-Item -ItemType Directory $extensions -Force | Out-Null
    foreach($name in @('hournerd-bridge.js','hournerd-adblock.js')){if(Test-Path (Join-Path $extensions $name)){Copy-Item (Join-Path $extensions $name) $backup}}
    $script=(Get-Content (Join-Path $PSScriptRoot 'hournerd-bridge.js') -Raw).Replace('__HOURNERD_BRIDGE_KEY__',$key)
    [IO.File]::WriteAllText((Join-Path $extensions 'hournerd-bridge.js'),$script,(New-Object Text.UTF8Encoding($false)))
    Copy-Item (Join-Path $PSScriptRoot 'adblock.js') (Join-Path $extensions 'hournerd-adblock.js') -Force
    Run-Spicetify @('config','extensions','hournerd-bridge.js-','hournerd-adblock.js-')
    Run-Spicetify @('config','extensions','hournerd-bridge.js')
    Run-Spicetify @('config','extensions','hournerd-adblock.js')
    Run-Spicetify @('backup','apply')
    Write-Host 'Setup finished. Leave Spotify running and return to HourNerd.'
    Write-Host "Your previous configuration is backed up in $backup"
}catch{Write-Host $_.Exception.Message -ForegroundColor Red;exit 1}
