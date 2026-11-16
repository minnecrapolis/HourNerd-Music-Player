# One partner-login request. No personal credentials or auth tokens are printed.
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::SystemDefault
$url = 'https://tuner.pandora.com/services/json/?method=auth.partnerLogin'
$partnerPassword = $env:HOURNERD_PANDORA_PARTNER_PASSWORD
if ([string]::IsNullOrWhiteSpace($partnerPassword)) { throw 'Set HOURNERD_PANDORA_PARTNER_PASSWORD locally before running this developer diagnostic.' }
$body = @{username='android'; password=$partnerPassword; deviceModel='android-generic'; version='5'; includeUrls=$true} | ConvertTo-Json -Compress
$request = [Net.HttpWebRequest]::Create($url)
$request.Method = 'POST'
$request.ContentType = 'text/plain; charset=utf-8'
$request.UserAgent = 'pianobar-2022.04.01'
$request.Timeout = 30000
$request.ReadWriteTimeout = 30000
$request.AllowAutoRedirect = $false
$request.ServicePoint.Expect100Continue = $false
$bytes = [Text.Encoding]::UTF8.GetBytes($body)
$request.ContentLength = $bytes.Length
$response = $null
try {
    $stream = $request.GetRequestStream()
    try { $stream.Write($bytes,0,$bytes.Length) } finally { $stream.Dispose() }
    $response = $request.GetResponse()
    Write-Host "HTTP status: $([int]$response.StatusCode)"
    $reader = New-Object IO.StreamReader($response.GetResponseStream())
    try { $text = $reader.ReadToEnd() } finally { $reader.Dispose() }
    try { $data = $text | ConvertFrom-Json } catch { Write-Host 'Response was not JSON. Possible proxy or access block page.'; exit 2 }
    if ($data.stat -eq 'ok' -and $data.result.partnerAuthToken -and $data.result.syncTime) {
        Write-Host 'Partner login: OK. The endpoint accepted the existing HourNerdMusicPlayer partner profile.'
        Write-Host 'Next: build HourNerdMusicPlayer and test your account login and playback. This check does not test either.'
    } elseif ($data.stat -eq 'fail') {
        Write-Host "Partner login: REJECTED. Pandora API code: $($data.code)"
        Write-Host 'The rejection occurred before any personal account login.'
        exit 3
    } else { Write-Host 'Unexpected response structure.'; exit 2 }
} catch [Net.WebException] {
    $failure = $_.Exception
    if ($failure.Response) {
        Write-Host "HTTP status: $([int]$failure.Response.StatusCode)"
        $failure.Response.Dispose()
    }
    Write-Host "Transport status: $($failure.Status)"
    Write-Host '401/403 = API access denied; 429 = rate limited. Do not repeatedly retry.'
    exit 4
} finally {
    if ($response) { $response.Dispose() }
}
