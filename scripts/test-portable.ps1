param([Parameter(Mandatory)][string]$ZipPath)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$zip = (Resolve-Path -LiteralPath $ZipPath).Path
$acceptanceRoot = Join-Path $projectRoot ('.tools\portable check 中文 [Mac] & #; ' + [guid]::NewGuid().ToString('N'))
Expand-Archive -LiteralPath $zip -DestinationPath $acceptanceRoot
$package = @(Get-ChildItem -LiteralPath $acceptanceRoot -Directory)
if ($package.Count -ne 1) { throw 'Expected one application folder in the archive.' }
$packageRoot = $package[0].FullName
if (Test-Path -LiteralPath (Join-Path $packageRoot 'Runtime\PowerShell')) { throw 'The new package must not require or ship PowerShell.' }
$manifest = Get-Content -LiteralPath (Join-Path $packageRoot 'SHA256.json') -Raw | ConvertFrom-Json
foreach ($entry in $manifest.PSObject.Properties) {
    if ((Get-FileHash -LiteralPath (Join-Path $packageRoot $entry.Name) -Algorithm SHA256).Hash -ine $entry.Value) { throw "Package hash mismatch: $($entry.Name)" }
}
$files = @(Get-ChildItem -LiteralPath $packageRoot -Recurse -File)
if ($files.Count -ne @($manifest.PSObject.Properties).Count + 1) { throw 'Unexpected or missing package files.' }
# The developer harness creates junction fixtures. On some hosts the standalone
# test EXE cannot issue FSCTL_SET_REPARSE_POINT. Use the existing SDK host for
# fixture setup; every save operation still re-enters the extracted self-contained
# application, with PATH stripped and PowerShell deliberately unavailable.
$probeHost = (Get-Command dotnet -ErrorAction Stop).Source
dotnet build (Join-Path $projectRoot 'src\Probe\StoneshardCompanion.Probe.csproj') -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Portable verification harness build failed.' }
$probeAssembly = Join-Path $projectRoot 'src\Probe\bin\Release\net8.0-windows\StoneshardCompanion.Probe.dll'
$start = [Diagnostics.ProcessStartInfo]::new($probeHost)
$start.ArgumentList.Add($probeAssembly)
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
$start.RedirectStandardOutput = $start.RedirectStandardError = $true
$start.StandardOutputEncoding = $start.StandardErrorEncoding = [Text.Encoding]::UTF8
$start.Environment['PATH'] = Join-Path $env:SystemRoot 'System32'
$start.Environment['PSModulePath'] = Join-Path $acceptanceRoot 'missing PS modules'
$start.Environment['PSHOME'] = Join-Path $acceptanceRoot 'missing PS home'
foreach ($arg in @('OfflineTest', $projectRoot, (Join-Path $packageRoot 'StoneshardCompanion.exe'))) { $start.ArgumentList.Add($arg) }
$process = [Diagnostics.Process]::Start($start)
$stdout = $process.StandardOutput.ReadToEndAsync()
$stderr = $process.StandardError.ReadToEndAsync()
$hostsSeen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
try {
    while (-not $process.WaitForExit(200)) {
        foreach ($worker in (Get-CimInstance Win32_Process -Filter "ParentProcessId = $($process.Id)")) {
            # The console-subsystem test probe may have its own Windows console
            # host. It is not an archive worker or an external runtime dependency.
            if ($worker.ExecutablePath -ieq (Join-Path $env:SystemRoot 'System32\conhost.exe')) { continue }
            if ($worker.ExecutablePath) { [void]$hostsSeen.Add($worker.ExecutablePath) }
        }
    }
    $stdout.GetAwaiter().GetResult() | Set-Content -LiteralPath (Join-Path $projectRoot 'artifacts\portable-test.log') -Encoding utf8
    if ($process.ExitCode -ne 0) { throw ('Portable checks failed: ' + $stderr.GetAwaiter().GetResult()) }
} finally { $process.Dispose() }
$expectedHost = Join-Path $packageRoot 'StoneshardCompanion.exe'
if ($hostsSeen.Count -ne 1 -or -not $hostsSeen.Contains($expectedHost)) { throw ('Save checks did not exclusively use the packaged application worker. Observed: ' + ($hostsSeen -join ', ') + '; expected: ' + $expectedHost) }
[ordered]@{
    ArchiveSHA256 = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
    PackageFiles = $files.Count
    PackagedSaveWorkerObserved = $true
    PowerShellNotRequired = $true
    InvalidPowerShellEnvironment = $true
    SpecialCharacterPackagePath = $true
    DeveloperToolsRemovedFromChildPath = $true
    DeveloperHarnessCreatesJunctionFixtures = $true
    GameStarted = $false
    RealSavesModified = $false
} | ConvertTo-Json | Tee-Object -FilePath (Join-Path $projectRoot 'artifacts\portable-verification.json')
