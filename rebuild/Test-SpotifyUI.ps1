$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes,System.Drawing,System.Windows.Forms
$root=Split-Path $PSScriptRoot
$process=Start-Process (Join-Path $root 'HourNerdMusicPlayer\bin\Release\HourNerdMusicPlayer.exe') -PassThru
try{
    $window=$null
    for($attempt=0;$attempt -lt 30 -and !$window;$attempt++){
        Start-Sleep -Milliseconds 500
        if($process.HasExited){throw 'The player exited before its window opened.'}
        $window=[System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children,(New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty,$process.Id)))
    }
    if(!$window){throw 'Player window did not appear.'}
    function Find-Name([string]$name){$window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,(New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty,$name)))}
    function Find-ServiceItem([string]$name){
        $conditions=[System.Windows.Automation.Condition[]]@(
            (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty,$name)),
            (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty,$process.Id)),
            (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::IsSelectionItemPatternAvailableProperty,$true))
        )
        $condition=New-Object System.Windows.Automation.AndCondition(,$conditions)
        [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$condition)
    }

    $combo=$window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,(New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty,'MusicService')))
    if(!$combo){throw 'The music service dropdown was not found.'}
    $expand=$combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
    $expand.Expand();Start-Sleep -Milliseconds 300
    $spotify=Find-ServiceItem 'Spotify'
    if(!$spotify){throw 'Spotify was not available in the dropdown.'}
    $spotify.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select();$expand.Collapse()
    Start-Sleep -Milliseconds 1200
    if(!(Find-Name 'Set up Spotify')){throw 'Spotify panel did not appear.'}
    if(!(Find-Name 'YOUR SPOTIFY MUSIC')){throw 'Spotify sidebar heading did not update.'}
    Write-Host 'PASS selecting Spotify displays its native panel and sidebar'
    $keyFile=Join-Path $env:APPDATA 'HourNerd Music Player\SpotifyBridge\bridge.key'
    $key=(Get-Content $keyFile -Raw).Trim()
    $fixture='{"title":"Fixture song","artist":"Fixture artist","entries":[{"name":"Fixture playlist","uri":"spotify:playlist:1234567890123456789012"}]}'
    $null=Invoke-WebRequest -UseBasicParsing -Method Post -Uri 'http://127.0.0.1:48372/exchange' -Headers @{'Origin'='https://xpui.app.spotify.com';'X-HourNerd-Bridge'=$key} -Body $fixture -ContentType 'application/json'
    Start-Sleep -Milliseconds 1000
    if(!(Find-Name 'Fixture song')){throw 'Authenticated song metadata did not appear.'}

    $bounds=$window.Current.BoundingRectangle
    $bitmap=New-Object Drawing.Bitmap([int]$bounds.Width,[int]$bounds.Height)
    $graphics=[Drawing.Graphics]::FromImage($bitmap)
    try{$graphics.CopyFromScreen([int]$bounds.Left,[int]$bounds.Top,0,0,$bitmap.Size);$bitmap.Save((Join-Path $root 'spotify-preview.png'),[Drawing.Imaging.ImageFormat]::Png)}finally{$graphics.Dispose();$bitmap.Dispose()}
    $settings=Find-Name 'Settings'
    $settings.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 500
    foreach($accountName in @('Pandora account','Spotify account','Sign in / change Pandora account','Open Spotify / change account','Set up Spotify connection')) {
        if(!(Find-Name $accountName)){throw "Missing separate account control: $accountName"}
    }
    Write-Host 'PASS Settings offers separate Pandora and Spotify account controls'
    $expand.Expand();Start-Sleep -Milliseconds 300
    $pandora=Find-ServiceItem 'Pandora' ;$pandora.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select();$expand.Collapse()
    Start-Sleep -Milliseconds 500
    if(!(Find-Name 'YOUR STATIONS')){throw 'Switching back did not restore Pandora sidebar.'}
    Write-Host 'PASS selecting Pandora restores its station interface'
    $expand.Expand();Start-Sleep -Milliseconds 300
    $spotify=Find-ServiceItem 'Spotify';$spotify.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select();$expand.Collapse()
    Start-Sleep -Milliseconds 500
    $sidebar=$window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,(New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty,'SidebarStationList')))
    $playlist=$sidebar.FindFirst([System.Windows.Automation.TreeScope]::Descendants,(New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty,'Fixture playlist')))
    if(!$playlist){throw 'Spotify library was lost when switching back from Pandora.'}
    if((Get-Content $keyFile -Raw).Trim() -ne $key){throw 'Changing music services changed the Spotify connection identity.'}
    Write-Host 'PASS switching services preserves the Spotify library and connection identity'

}finally{if(!$process.HasExited){Stop-Process -Id $process.Id -Force}}
