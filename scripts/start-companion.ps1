param([switch]$CheckOnly)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$stage = 'locate application'
try {
    $appFile = Join-Path $projectRoot 'artifacts\development\0.3.31\app\StoneshardCompanion.dll'
    if (-not (Test-Path -LiteralPath $appFile)) {
        $appFile = Join-Path $projectRoot 'src\Overlay\bin\Release\net8.0-windows\StoneshardCompanion.dll'
    }
    if (-not (Test-Path -LiteralPath $appFile)) { throw 'Application files are missing. Run scripts/build.ps1 first.' }
    $stage = 'create launcher logs'
    $logFolder = Join-Path $projectRoot 'artifacts\launcher-logs'
    New-Item -ItemType Directory -Path $logFolder -Force | Out-Null
    $launchId = (Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '-' + [guid]::NewGuid().ToString('N')
    $stdout = Join-Path $logFolder ($launchId + '.stdout.log')
    $stderr = Join-Path $logFolder ($launchId + '.stderr.log')
    if ($CheckOnly) { Write-Output ('Launcher check OK: ' + $appFile); exit 0 }
    $stage = 'start application'
    $runtimePath = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
    if (-not (Test-Path -LiteralPath $runtimePath)) { $runtimePath = (Get-Command dotnet.exe -ErrorAction Stop).Source }
    $appProcess = Start-Process -FilePath $runtimePath -ArgumentList ('"' + $appFile + '"') -WorkingDirectory (Split-Path -Parent $appFile) -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr -PassThru
    # Keep the native handle alive so Windows PowerShell retains the exit code
    # when a duplicate instance acknowledges activation and exits immediately.
    $null = $appProcess.Handle
    if ($appProcess.WaitForExit(2500) -and $appProcess.ExitCode -ne 0) {
        $detail = Get-Content -LiteralPath $stderr -Raw -ErrorAction SilentlyContinue
        throw "Application exited with code $($appProcess.ExitCode). $detail`nLog: $stderr"
    }
    exit 0
} catch {
    Write-Host ('Unable to start Stoneshard Companion: ' + $_.Exception.Message) -ForegroundColor Red
    Write-Host ('Stage: ' + $stage)
    Write-Host ('Application: ' + $appFile)
    Write-Host ('Exception: ' + $_.Exception.GetType().FullName)
    Write-Host ('HRESULT: 0x{0:X8}' -f $_.Exception.HResult)
    Write-Host ($_ | Out-String)
    exit 1
}
