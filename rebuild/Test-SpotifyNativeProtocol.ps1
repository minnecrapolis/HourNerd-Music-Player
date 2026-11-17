$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$process=New-Object Diagnostics.Process
$process.StartInfo.FileName=Join-Path $root 'native-spotify\target\release\hournerd-spotify.exe'
$process.StartInfo.UseShellExecute=$false
$process.StartInfo.CreateNoWindow=$true
$process.StartInfo.RedirectStandardInput=$true
$process.StartInfo.RedirectStandardOutput=$true
$process.StartInfo.RedirectStandardError=$true
$process.StartInfo.StandardOutputEncoding=[Text.Encoding]::UTF8
$null=$process.Start()
try {
    function Read-State {
        $line=$process.StandardOutput.ReadLineAsync()
        if(!$line.Wait(8000)){throw 'Native engine protocol timed out.'}
        $message=$line.Result | ConvertFrom-Json
        if($message.type -ne 'state'){throw 'Unexpected native engine protocol message.'}
        return $message.snapshot
    }
    $state=Read-State
    if($state.connected -or $state.playing){throw 'Native engine started without sign-in in an invalid state.'}
    Write-Host 'PASS native engine starts independently without Spotify Desktop or account credentials'
    $process.StandardInput.WriteLine('{"name":"playUri","value":"file:///secret"}');$process.StandardInput.Flush()
    $state=Read-State
    if($state.playing -or !$state.error){throw 'Invalid native playback URI was not refused.'}
    Write-Host 'PASS native process refuses non-Spotify playback requests'
    $process.StandardInput.WriteLine('{"name":"logout"}');$process.StandardInput.Flush()
    $state=Read-State
    if($state.connected -or $state.playing){throw 'Native logout did not clear connection state.'}
    $process.StandardInput.Close()
    if(!$process.WaitForExit(8000)){throw 'Native engine did not stop when its owner closed the protocol pipe.'}
    if($process.ExitCode -ne 0){throw 'Native engine did not exit cleanly.'}
    Write-Host 'PASS native engine clears account state and shuts down with its owner'
}finally{if(!$process.HasExited){$process.Kill()};$process.Dispose()}
