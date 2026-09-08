param([Parameter(Mandatory)][string]$ZipPath)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$zip = (Resolve-Path -LiteralPath $ZipPath).Path
$acceptanceRoot = Join-Path $projectRoot ('.tools\portable check 中文 ' + [guid]::NewGuid().ToString('N'))
Expand-Archive -LiteralPath $zip -DestinationPath $acceptanceRoot
$package = @(Get-ChildItem -LiteralPath $acceptanceRoot -Directory)
if ($package.Count -ne 1) { throw 'Expected one application folder in the archive.' }
$packageRoot = $package[0].FullName
$manifest = Get-Content -LiteralPath (Join-Path $packageRoot 'SHA256.json') -Raw | ConvertFrom-Json
foreach ($entry in $manifest.PSObject.Properties) {
    if ((Get-FileHash -LiteralPath (Join-Path $packageRoot $entry.Name) -Algorithm SHA256).Hash -ine $entry.Value) { throw "Package hash mismatch: $($entry.Name)" }
}
$files = @(Get-ChildItem -LiteralPath $packageRoot -Recurse -File)
if ($files.Count -ne @($manifest.PSObject.Properties).Count + 1) { throw 'Unexpected or missing package files.' }
$probeFolder = Join-Path $projectRoot 'artifacts\portable-probe'
dotnet publish (Join-Path $projectRoot 'src\Probe\StoneshardCompanion.Probe.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $probeFolder --nologo
if ($LASTEXITCODE -ne 0) { throw 'Portable probe build failed.' }
$start = [Diagnostics.ProcessStartInfo]::new((Join-Path $probeFolder 'StoneshardCompanion.Probe.exe'))
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
$start.RedirectStandardOutput = $start.RedirectStandardError = $true
$start.StandardOutputEncoding = $start.StandardErrorEncoding = [Text.Encoding]::UTF8
$start.Environment['PATH'] = Join-Path $env:SystemRoot 'System32'
foreach ($arg in @('OfflineTest', $projectRoot, (Join-Path $packageRoot 'Assets\SaveManager\Invoke.ps1'))) { $start.ArgumentList.Add($arg) }
$process = [Diagnostics.Process]::Start($start)
$stdout = $process.StandardOutput.ReadToEndAsync()
$stderr = $process.StandardError.ReadToEndAsync()
$hostsSeen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
try {
    while (-not $process.WaitForExit(200)) {
        foreach ($worker in (Get-CimInstance Win32_Process -Filter "ParentProcessId = $($process.Id) AND Name = 'pwsh.exe'")) {
            if ($worker.ExecutablePath) { [void]$hostsSeen.Add($worker.ExecutablePath) }
        }
    }
    $stdout.GetAwaiter().GetResult() | Set-Content -LiteralPath (Join-Path $projectRoot 'artifacts\portable-test.log') -Encoding utf8
    if ($process.ExitCode -ne 0) { throw ('Portable checks failed: ' + $stderr.GetAwaiter().GetResult()) }
} finally { $process.Dispose() }
$expectedHost = Join-Path $packageRoot 'Runtime\PowerShell\pwsh.exe'
if ($hostsSeen.Count -ne 1 -or -not $hostsSeen.Contains($expectedHost)) { throw 'Save checks did not exclusively use the bundled PowerShell host.' }
[ordered]@{
    ArchiveSHA256 = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
    PackageFiles = $files.Count
    BundledPowerShellObserved = $true
    DeveloperToolsRemovedFromChildPath = $true
    GameStarted = $false
    RealSavesModified = $false
} | ConvertTo-Json | Tee-Object -FilePath (Join-Path $projectRoot 'artifacts\portable-verification.json')
