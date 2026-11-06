[CmdletBinding()]
param(
    [int]$TunnelPort = 18010,
    [int]$ConsolePort = 18011,
    [switch]$NoBrowser,
    [switch]$Stop
)

$ErrorActionPreference = 'Stop'
$runtimeDir = Join-Path $PSScriptRoot '.runtime'
$stateFile = Join-Path $runtimeDir "processes-$ConsolePort.json"
$consoleUrl = "http://127.0.0.1:$ConsolePort/"

function Get-OwnedProcess($record) {
    $process = Get-Process -Id $record.Id -ErrorAction SilentlyContinue
    if ($process -and $process.StartTime.ToUniversalTime() -eq ([datetime]$record.StartedUtc).ToUniversalTime()) {
        return $process
    }
}

if (Test-Path -LiteralPath $stateFile) {
    $state = Get-Content -LiteralPath $stateFile -Raw | ConvertFrom-Json
    $server = Get-OwnedProcess $state.Console
    $tunnel = Get-OwnedProcess $state.Tunnel
    if ($Stop) {
        if ($server) { Stop-Process -InputObject $server }
        if ($tunnel) { Stop-Process -InputObject $tunnel }
        Remove-Item -LiteralPath $stateFile
        Write-Output 'Stopped the console and tunnel recorded by this launcher.'
        return
    }
    if ($server -and $tunnel) {
        Write-Output "Console already running: $consoleUrl"
        if (!$NoBrowser) { Start-Process $consoleUrl }
        return
    }
    # Recover after a reboot or an interrupted helper, stopping only a
    # surviving process whose ID and start time match this launcher's record.
    if ($server) { Stop-Process -InputObject $server }
    if ($tunnel) { Stop-Process -InputObject $tunnel }
    Remove-Item -LiteralPath $stateFile
}
if ($Stop) { Write-Output 'No launcher session recorded.'; return }
if ($TunnelPort -eq $ConsolePort) { throw 'Choose different console and tunnel ports.' }

$sshExe = (Get-Command ssh.exe -ErrorAction Stop).Source
$nodeCommand = Get-Command node.exe -ErrorAction SilentlyContinue
$nodeExe = if ($nodeCommand) { $nodeCommand.Source } else {
    Join-Path $env:USERPROFILE '.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe'
}
if (!(Test-Path -LiteralPath $nodeExe)) { throw 'Node.js is required. Install Node.js or add the existing executable to PATH.' }
$keyPath = Join-Path $env:USERPROFILE '.ssh\oracle_dell'
if (!(Test-Path -LiteralPath $keyPath)) { throw 'The configured Oracle SSH key is unavailable on this computer.' }

# A listener must bind successfully before any helper starts. Never stop
# an unknown process that happens to occupy the desired port.
foreach ($port in @($TunnelPort, $ConsolePort)) {
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $port)
    try { $listener.Start() } catch { throw "Port $port is already in use. Choose unused ports." }
    finally { $listener.Stop() }
}
New-Item -ItemType Directory -Path $runtimeDir -Force | Out-Null
$tunnel = $null
$server = $null
$previousTunnel = $env:ORACLE_TUNNEL_PORT
$previousConsole = $env:ORACLE_TEST_PORT
try {
    $sshArgs = @('-N','-L',"127.0.0.1:${TunnelPort}:127.0.0.1:8000",
        '-o','BatchMode=yes','-o','IdentitiesOnly=yes','-o','StrictHostKeyChecking=yes',
        '-o','ExitOnForwardFailure=yes','-o','ConnectTimeout=15',
        '-o','ServerAliveInterval=30','-o','ServerAliveCountMax=3',
        '-i',('"' + $keyPath + '"'),'ubuntu@129.151.163.64')
    $tunnel = Start-Process -FilePath $sshExe -ArgumentList $sshArgs -WindowStyle Hidden -PassThru `
        -RedirectStandardError (Join-Path $runtimeDir "tunnel-$ConsolePort.log")
    $env:ORACLE_TUNNEL_PORT = "$TunnelPort"
    $env:ORACLE_TEST_PORT = "$ConsolePort"
    $server = Start-Process -FilePath $nodeExe -ArgumentList 'server.cjs' -WorkingDirectory $PSScriptRoot `
        -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $runtimeDir "console-$ConsolePort.log") `
        -RedirectStandardError (Join-Path $runtimeDir "console-$ConsolePort-error.log")
    $ready = $false
    for ($attempt = 0; $attempt -lt 20; $attempt++) {
        $tunnel.Refresh(); $server.Refresh()
        if ($tunnel.HasExited -or $server.HasExited) { throw "A helper exited. Check logs in $runtimeDir" }
        try {
            $health = Invoke-RestMethod -Uri "${consoleUrl}api/health" -TimeoutSec 2
            if ($health.status -eq 'online') { $ready = $true; break }
        } catch { }
        Start-Sleep -Milliseconds 250
    }
    if (!$ready) { throw "Oracle did not become reachable. Check logs in $runtimeDir" }
    @{
        Console = @{ Id = $server.Id; StartedUtc = $server.StartTime.ToUniversalTime().ToString('o') }
        Tunnel = @{ Id = $tunnel.Id; StartedUtc = $tunnel.StartTime.ToUniversalTime().ToString('o') }
        Url = $consoleUrl
    } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $stateFile
    Write-Output "Oracle online. Console: $consoleUrl"
    Write-Output 'Helpers run independently of this PowerShell window and Codex. Run this script with -Stop to stop them.'
    if (!$NoBrowser) { Start-Process $consoleUrl }
} catch {
    if ($server -and !$server.HasExited) { Stop-Process -InputObject $server }
    if ($tunnel -and !$tunnel.HasExited) { Stop-Process -InputObject $tunnel }
    throw
} finally {
    $env:ORACLE_TUNNEL_PORT = $previousTunnel
    $env:ORACLE_TEST_PORT = $previousConsole
}
