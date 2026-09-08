#requires -Version 7.0
param()
$ErrorActionPreference = 'Stop'
. (Join-Path (Split-Path -Parent $PSScriptRoot) 'src\Overlay\Assets\SaveManager\Core.ps1') -LibraryOnly

$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('Stoneshard-manager-tests-' + [guid]::NewGuid().ToString('N'))
$SaveParent = Join-Path $testRoot 'local'
$SaveRoot = Join-Path $SaveParent 'StoneShard'
$BackupRoot = Join-Path $testRoot 'backups'
$LatestArchive = Join-Path $BackupRoot 'Stoneshard-latest.zip'
$gameBefore = @(Get-Process StoneShard -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id)
$passed = [Collections.Generic.List[string]]::new()

function Assert-That([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw "ASSERTION FAILED: $Message" }
}
function Expect-Failure([scriptblock]$Action, [string]$Expected) {
    $failed = $false
    try { & $Action | Out-Null } catch {
        $failed = $true
        Assert-That ($_.Exception.Message -like "*$Expected*") "Unexpected error: $($_.Exception.Message)"
    }
    Assert-That $failed 'The operation should have refused the restore'
}

try {
    New-Item -ItemType Directory -Path (Join-Path $SaveRoot 'characters_v1\character_3\exitsave_1'),$BackupRoot -Force | Out-Null
    $dataFile = Join-Path $SaveRoot 'characters_v1\character_3\exitsave_1\data.sav'
    [IO.File]::WriteAllText($dataFile, 'ALIVE_01')
    [IO.File]::WriteAllText((Join-Path $SaveRoot 'characters_v1\characters.map'), 'CHARACTER_3')
    [IO.File]::WriteAllText((Join-Path $SaveRoot 'settings.ini'), 'settings')
    $hidden = Join-Path $SaveRoot 'hidden.sav'
    [IO.File]::WriteAllText($hidden, 'hidden-save')
    (Get-Item -LiteralPath $hidden).Attributes = [IO.FileAttributes]::Hidden
    $base = New-FullBackup -UpdateLatest
    Assert-That ($base.Files -eq 4) 'Hidden files must be included in the ZIP and verified'
    $passed.Add('Full backup including hidden file; ZIP extraction/hash validation; latest refresh')

    $oldTime = (Get-Item -LiteralPath $dataFile).LastWriteTimeUtc
    [IO.File]::WriteAllText($dataFile, 'DEAD__01')
    (Get-Item -LiteralPath $dataFile).LastWriteTimeUtc = $oldTime
    $extra = Join-Path $SaveRoot 'death-only.sav'
    [IO.File]::WriteAllText($extra, 'extra')
    $deadSignature = Get-TreeSignature $SaveRoot
    $directoryIdBefore = (& fsutil.exe file queryfileid $SaveRoot | Out-String).Trim()
    Assert-That ($LASTEXITCODE -eq 0) 'Can inspect original directory identity'
    $restore = Restore-SaveArchive -ArchivePath $base.Archive
    $directoryIdAfter = (& fsutil.exe file queryfileid $SaveRoot | Out-String).Trim()
    Assert-That ($directoryIdBefore -eq $directoryIdAfter) 'Running game must keep the same save directory identity'
    Assert-That (Test-SameTree $base.Signature (Get-TreeSignature $SaveRoot)) 'Full tree equality after restore'
    Assert-That (-not (Test-Path -LiteralPath $extra)) 'Extra files must not survive restore'
    Assert-That ((Get-FileHash -LiteralPath $base.Archive).Hash -eq $base.Hash) 'Original archive immutable'
    $safetyCheck = Join-Path $testRoot 'safety-check'
    Expand-Archive -LiteralPath $restore.SafetyArchive -DestinationPath $safetyCheck
    Assert-That (Test-SameTree $deadSignature (Get-TreeSignature (Join-Path $safetyCheck 'StoneShard'))) 'Pre-restore backup matches dead state'
    $passed.Add('Live file replacement preserving directory identity; same-size/same-time change; extra removal; safety backup')

    $ordered = @(Get-BackupArchives)
    Assert-That ($ordered[0].Name -notlike 'Stoneshard-before-restore-*') 'Safety backup must not be default'
    (Get-Item -LiteralPath $LatestArchive).LastWriteTimeUtc = [datetime]::UtcNow.AddDays(-2)
    $ordered = @(Get-BackupArchives)
    Assert-That ($ordered[0].Name -eq [IO.Path]::GetFileName($base.Archive)) 'Old latest alias must not outrank newer history'
    $passed.Add('Backup sorting excludes newest safety snapshot and stale latest alias from default selection')

    $beforeInvalid = Get-TreeSignature $SaveRoot
    $bad = Join-Path $BackupRoot 'Stoneshard-bad.zip'
    Copy-Item -LiteralPath $base.Archive -Destination $bad
    [IO.File]::WriteAllText("$bad.sha256", ('0' * 64))
    Expect-Failure { Restore-SaveArchive $bad } '校验失败'
    Assert-That (Test-SameTree $beforeInvalid (Get-TreeSignature $SaveRoot)) 'Corrupt backup must not touch live saves'
    $passed.Add('Checksum rejection leaves current save intact')

    $missingCopy = Join-Path $testRoot 'removed-save'
    [IO.Directory]::Move($SaveRoot, $missingCopy)
    $missingResult = Restore-SaveArchive -ArchivePath $base.Archive
    Assert-That (Test-SameTree $base.Signature (Get-TreeSignature $SaveRoot)) 'Restore when whole save directory is missing'
    Assert-That ($null -eq $missingResult.SafetyArchive) 'No fake safety backup for absent source'
    $passed.Add('Restore after complete save-directory deletion')

    # An active writer must prevent replacement rather than yield a mixed snapshot.
    $writerJob = Start-ThreadJob -ArgumentList $dataFile -ScriptBlock {
        param($File)
        for ($i = 0; $i -lt 160; $i++) {
            [IO.File]::WriteAllText($File, "WRITER_$i")
            Start-Sleep -Milliseconds 40
        }
    }
    try {
        Start-Sleep -Milliseconds 400
        Expect-Failure { Restore-SaveArchive -ArchivePath $base.Archive } '写入存档'
    } finally {
        Stop-Job $writerJob -ErrorAction SilentlyContinue
        Remove-Job $writerJob -Force -ErrorAction SilentlyContinue
    }
    $passed.Add('Concurrent writer detected before file replacement')

    $traversalZip = Join-Path $BackupRoot 'Stoneshard-traversal.zip'
    $zip = [IO.Compression.ZipFile]::Open($traversalZip, [IO.Compression.ZipArchiveMode]::Create)
    try { [void]$zip.CreateEntry('../outside.sav') } finally { $zip.Dispose() }
    Expect-Failure { Restore-SaveArchive $traversalZip } '不安全的路径'
    Assert-That (-not (Test-Path -LiteralPath (Join-Path $BackupRoot 'outside.sav'))) 'Path traversal must not extract'
    $passed.Add('Archive path traversal rejected before extraction')

    # A file opened without delete sharing prevents the old tree from moving.
    $handle = [IO.File]::Open($dataFile, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    $lockedBefore = Get-TreeSignature $SaveRoot
    try { Expect-Failure { Restore-SaveArchive $base.Archive } '占用' }
    finally { $handle.Dispose() }
    Assert-That (Test-SameTree $lockedBefore (Get-TreeSignature $SaveRoot)) 'Locked-file failure preserves original tree'
    $passed.Add('Locked file fails safely without closing game or changing saves')

    $gameAfter = @(Get-Process StoneShard -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id)
    foreach ($gameId in $gameBefore) { Assert-That ($gameId -in $gameAfter) 'Existing game process must remain alive' }
    Assert-That (@(Get-ChildItem -LiteralPath $SaveParent -Force -Filter '.StoneShard-restore-*').Count -eq 0) 'No leaked restore work directories'
    Assert-That (@(Get-ChildItem -LiteralPath $BackupRoot -Force -Filter '.work-*').Count -eq 0) 'No leaked archive work directories'
    [pscustomobject]@{ Passed = $passed.Count; Checks = $passed.ToArray(); ExistingGameProcessesPreserved = $gameBefore; TestRoot = $testRoot } | ConvertTo-Json -Depth 4
}
finally {
    $tempParent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
    $resolvedTest = [IO.Path]::GetFullPath($testRoot)
    if ((Split-Path -Parent $resolvedTest) -ieq $tempParent -and
        (Split-Path -Leaf $resolvedTest) -match '^Stoneshard-manager-tests-[a-f0-9]{32}$' -and
        (Test-Path -LiteralPath $resolvedTest)) {
        Remove-Item -LiteralPath $resolvedTest -Recurse -Force
    }
}
