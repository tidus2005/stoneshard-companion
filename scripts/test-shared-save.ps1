# Developer check only. Uses an EXISTING writable share; never creates a share,
# changes credentials, opens the game, or accesses personal saves.
#requires -Version 7.0
param([Parameter(Mandatory)][string]$PackageFolder,[Parameter(Mandatory)][string]$ShareRoot)
$ErrorActionPreference = 'Stop'
$package = (Resolve-Path -LiteralPath $PackageFolder).ProviderPath
$share = [IO.Path]::TrimEndingDirectorySeparator((Resolve-Path -LiteralPath $ShareRoot).ProviderPath)
if (-not ([Uri]$share).IsUnc) { throw 'ShareRoot must be an existing UNC directory.' }
if (-not (Test-Path -LiteralPath (Join-Path $package 'StoneshardCompanion.exe'))) { throw 'Packaged application missing.' }
$testLocal = Join-Path ([IO.Path]::GetTempPath()) ('Stoneshard-share-check-' + [guid]::NewGuid().ToString('N'))
$testShare = Join-Path $share ('Stoneshard-share-check-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testLocal, $testShare | Out-Null
try {
    $app = Join-Path $testShare 'app'
    Copy-Item -LiteralPath $package -Destination $app -Recurse
    $saves = Join-Path $testLocal 'StoneShard'
    $data = Join-Path $saves 'characters_v1\character_3\exitsave_1\data.sav'
    New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($data)) | Out-Null
    [IO.File]::WriteAllText($data, 'ALIVE_01')
    $backupFolder = Join-Path $testShare 'backups [Mac] 中文'
    function Invoke-TestSave([string]$Operation, [string]$Archive) {
        $request = Join-Path $testLocal 'request.json'
        @{ Operation=$Operation; SaveRoot=$saves; BackupRoot=$backupFolder; Archive=$Archive } | ConvertTo-Json | Set-Content -LiteralPath $request -Encoding utf8
        $start = [Diagnostics.ProcessStartInfo]::new((Join-Path $app 'StoneshardCompanion.exe'))
        $start.UseShellExecute = $false; $start.CreateNoWindow = $true; $start.WindowStyle = 'Hidden'
        $start.WorkingDirectory = $testLocal
        $start.RedirectStandardOutput = $start.RedirectStandardError = $true
        $start.StandardOutputEncoding = $start.StandardErrorEncoding = [Text.Encoding]::UTF8
        foreach ($arg in @('--save-worker', $request)) { $start.ArgumentList.Add($arg) }
        $process = [Diagnostics.Process]::Start($start)
        try {
            $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync()
            $process.WaitForExit()
            $line = ($stdout.GetAwaiter().GetResult() -split "`n" | Where-Object { $_.StartsWith('@@RESULT@@') } | Select-Object -Last 1)
            if (-not $line) { throw ('Worker returned no result: ' + $stderr.GetAwaiter().GetResult()) }
            $result = $line.Substring(10) | ConvertFrom-Json
            if (-not $result.Ok -or $process.ExitCode -ne 0) { throw $result.Error }
            return $result.Result
        } finally { $process.Dispose() }
    }
    $first = Invoke-TestSave 'Backup' ''
    $hash = (Get-FileHash -LiteralPath $first.Archive).Hash
    [IO.File]::WriteAllText($data, 'DEAD__01')
    $restored = Invoke-TestSave 'Restore' $first.Archive
    if ([IO.File]::ReadAllText($data) -ne 'ALIVE_01' -or -not $restored.SafetyArchive) { throw 'Shared restore verification failed.' }
    if ((Get-FileHash -LiteralPath $first.Archive).Hash -ne $hash) { throw 'Original archive changed.' }
    [ordered]@{ UncExecutable=$true; UncBackupDirectory=$true; BackupAndRestoreVerified=$true; OriginalArchiveUnchanged=$true; GameStarted=$false; PersonalSavesModified=$false } | ConvertTo-Json
} finally {
    foreach ($item in @(@{ Path=$testLocal; Parent=[IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetTempPath()) }, @{ Path=$testShare; Parent=$share })) {
        $target = [IO.Path]::GetFullPath($item.Path)
        if ([IO.Path]::GetDirectoryName($target) -ine [IO.Path]::GetFullPath($item.Parent) -or -not [IO.Path]::GetFileName($target).StartsWith('Stoneshard-share-check-')) { throw 'Unsafe shared test cleanup target.' }
        Remove-Item -LiteralPath $target -Recurse -Force
    }
}
