#requires -Version 7.0
param([Parameter(Mandatory)][string]$RequestPath)
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$managerLock = $null
try {
    $request = Get-Content -LiteralPath $RequestPath -Raw | ConvertFrom-Json
    if ($request.Operation -notin @('Backup','Latest','Restore')) { throw '不支持的存档操作。' }
    . (Join-Path $PSScriptRoot 'Core.ps1') -LibraryOnly
    $SaveRoot = [IO.Path]::GetFullPath([string]$request.SaveRoot).TrimEnd('\')
    $BackupRoot = [IO.Path]::GetFullPath([string]$request.BackupRoot).TrimEnd('\')
    $SaveParent = Split-Path -Parent $SaveRoot
    $LatestArchive = Join-Path $BackupRoot 'Stoneshard-latest.zip'
    if ((Split-Path -Leaf $SaveRoot) -ne 'StoneShard') { throw '存档目录名称必须为 StoneShard。' }
    if ($BackupRoot.StartsWith($SaveRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or $SaveRoot -ieq $BackupRoot) { throw '备份目录不能位于存档目录中。' }
    New-Item -ItemType Directory -Force -Path $BackupRoot | Out-Null
    try { $managerLock = [IO.File]::Open((Join-Path $BackupRoot '.manager.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None) }
    catch { throw '另一个存档管理器正在操作，请关闭原命令行管理器或等待其完成。' }
    switch ($request.Operation) {
        'Backup' { $result = New-FullBackup }
        'Latest' { $result = New-FullBackup -UpdateLatest }
        'Restore' { $result = Restore-SaveArchive -ArchivePath ([string]$request.Archive) }
    }
    [Console]::WriteLine('@@RESULT@@' + (@{ Ok=$true; Result=$result } | ConvertTo-Json -Depth 8 -Compress))
} catch {
    [Console]::WriteLine('@@RESULT@@' + (@{ Ok=$false; Error=$_.Exception.Message } | ConvertTo-Json -Compress))
    exit 1
} finally {
    if ($null -ne $managerLock) { $managerLock.Dispose() }
}
