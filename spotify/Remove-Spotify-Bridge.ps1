$ErrorActionPreference='Stop'
try{
    $cli=Get-ChildItem (Join-Path $PSScriptRoot 'cli') -Filter spicetify.exe -Recurse | Select-Object -First 1 -ExpandProperty FullName
    if(!$cli){throw 'The bundled Spicetify tool was not found.'}
    & $cli config extensions 'hournerd-bridge.js-' 'hournerd-adblock.js-'
    if($LASTEXITCODE -ne 0){throw 'Could not remove the HourNerd extension entries.'}
    & $cli apply
    if($LASTEXITCODE -ne 0){throw 'Could not apply the restored extension list.'}
    Write-Host 'HourNerd Spotify extensions disabled. Other Spicetify extensions were kept.'
}catch{Write-Host $_.Exception.Message -ForegroundColor Red;exit 1}
