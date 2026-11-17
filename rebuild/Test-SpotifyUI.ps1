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
    $combo=$window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,(New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty,'MusicService')))
    if(!$combo){throw 'The music service dropdown was not found.'}
    $expand=$combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
    $expand.Expand();Start-Sleep -Milliseconds 300
    $spotify=Find-Name 'Spotify'
    if(!$spotify){throw 'Spotify was not available in the dropdown.'}
    $spotify.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select();$expand.Collapse()
    Start-Sleep -Milliseconds 1200
    if(!(Find-Name 'Set up Spotify')){throw 'Spotify panel did not appear.'}
    if(!(Find-Name 'YOUR SPOTIFY MUSIC')){throw 'Spotify sidebar heading did not update.'}
    Write-Host 'PASS selecting Spotify displays its native panel and sidebar'
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
    $pandora=Find-Name 'Pandora' ;$pandora.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select();$expand.Collapse()
    Start-Sleep -Milliseconds 500
    if(!(Find-Name 'YOUR STATIONS')){throw 'Switching back did not restore Pandora sidebar.'}
    Write-Host 'PASS selecting Pandora restores its station interface'
}finally{if(!$process.HasExited){Stop-Process -Id $process.Id -Force}}
