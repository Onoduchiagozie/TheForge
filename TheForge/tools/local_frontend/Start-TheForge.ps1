[CmdletBinding()]
param([switch]$NoBrowser, [switch]$Stop, [switch]$SkipBuild)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$runtimeDir = Join-Path $PSScriptRoot '.runtime'
$stateFile = Join-Path $runtimeDir 'frontend.json'
$url = 'http://127.0.0.1:5157/'
$backend = 'https://emerald-nativity-guacamole.ngrok-free.dev/'
$appProcess = $null

if (Test-Path -LiteralPath $stateFile) {
    $record = Get-Content -LiteralPath $stateFile -Raw | ConvertFrom-Json
    $candidate = Get-Process -Id $record.Id -ErrorAction SilentlyContinue
    if ($candidate -and $candidate.StartTime.ToUniversalTime() -eq ([datetime]$record.StartedUtc).ToUniversalTime()) {
        $appProcess = $candidate
    }
    if (!$appProcess) { Remove-Item -LiteralPath $stateFile }
}
if ($Stop) {
    if ($appProcess) {
        Stop-Process -InputObject $appProcess
        Remove-Item -LiteralPath $stateFile
        Write-Output 'Stopped the local TheForge frontend. Oracle keeps running.'
    } else { Write-Output 'No frontend started by this launcher is running.' }
    return
}
if ($appProcess) {
    Write-Output "TheForge is already running: $url"
    if (!$NoBrowser) { Start-Process $url }
    return
}

$listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 5157)
try { $listener.Start() } catch { throw 'Port 5157 is occupied. Stop the other local TheForge instance before using this launcher.' }
finally { $listener.Stop() }

$dotnetExe = (Get-Command dotnet.exe -ErrorAction Stop).Source
$project = Join-Path $projectRoot 'TheForge.csproj'
if (!$SkipBuild) {
    & $dotnetExe build $project --nologo --configuration OracleLocal
    if ($LASTEXITCODE -ne 0) { throw 'TheForge build failed. See the build output above.' }
}
$dll = Join-Path $projectRoot 'bin/OracleLocal/net8.0/TheForge.dll'
if (!(Test-Path -LiteralPath $dll)) { throw 'The compiled frontend is missing. Run the launcher without -SkipBuild.' }
New-Item -ItemType Directory -Path $runtimeDir -Force | Out-Null
$appProcess = Start-Process -FilePath $dotnetExe -ArgumentList @(
    ('"' + $dll + '"'), '--Urls', $url, '--environment', 'Development', '--FastApi:BaseUrl', $backend
) -WorkingDirectory $projectRoot -WindowStyle Hidden -PassThru `
    -RedirectStandardOutput (Join-Path $runtimeDir 'frontend.log') `
    -RedirectStandardError (Join-Path $runtimeDir 'frontend-error.log')
@{ Id = $appProcess.Id; StartedUtc = $appProcess.StartTime.ToUniversalTime().ToString('o'); Url = $url } |
    ConvertTo-Json | Set-Content -LiteralPath $stateFile

$watch = [Diagnostics.Stopwatch]::StartNew()
$ready = $false
while ($watch.Elapsed.TotalSeconds -lt 40) {
    $appProcess.Refresh()
    if ($appProcess.HasExited) { throw "TheForge exited. Check $runtimeDir/frontend-error.log" }
    try {
        $health = Invoke-RestMethod -Uri "${url}api/health" -TimeoutSec 5
        if ($health.status -in @('online', 'ok', 'healthy')) { $ready = $true; break }
    } catch { }
    Start-Sleep -Milliseconds 250
}
if ($ready) { Write-Output "TheForge is connected to Oracle: $url" }
else { Write-Warning "TheForge is running at $url, but Oracle's health check failed. Check the frontend logs." }
Write-Output 'This frontend keeps running when Codex or this launcher window closes. Use Stop TheForge.cmd to stop it.'
if (!$NoBrowser) { Start-Process $url }
