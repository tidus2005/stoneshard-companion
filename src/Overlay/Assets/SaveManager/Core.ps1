#requires -Version 7.0
param([switch]$LibraryOnly)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$SaveRoot = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'StoneShard'
$BackupRoot = $PSScriptRoot
$LatestArchive = Join-Path $BackupRoot 'Stoneshard-latest.zip'
$SaveParent = Split-Path -Parent $SaveRoot

[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new()
try { $Host.UI.RawUI.WindowTitle = 'Stoneshard 存档管理器' } catch {}

function Pause-Menu {
    param([string]$Message = '按任意键返回主菜单...')
    Write-Host "`n$Message" -ForegroundColor DarkGray
    [void][Console]::ReadKey($true)
}

function Select-Menu {
    param(
        [Parameter(Mandatory)][string]$Title,
        [Parameter(Mandatory)][string[]]$Items,
        [string]$Hint = '↑/↓ 选择，Enter 确认，Esc 返回'
    )

    $selected = 0
    while ($true) {
        Clear-Host
        Write-Host ' STONESHARD 存档管理器 ' -ForegroundColor Black -BackgroundColor DarkCyan
        Write-Host "`n$Title`n"

        $pageSize = 10
        $pageStart = [math]::Floor($selected / $pageSize) * $pageSize
        $pageEnd = [math]::Min($pageStart + $pageSize, $Items.Count)
        for ($i = $pageStart; $i -lt $pageEnd; $i++) {
            if ($i -eq $selected) {
                Write-Host ("  > " + $Items[$i]) -ForegroundColor Black -BackgroundColor Cyan
            } else {
                Write-Host ("    " + $Items[$i])
            }
        }
        if ($Items.Count -gt $pageSize) {
            Write-Host ("`n第 {0}/{1} 项" -f ($selected + 1), $Items.Count) -ForegroundColor DarkGray
        }

        Write-Host "`n$Hint" -ForegroundColor DarkGray
        $key = [Console]::ReadKey($true).Key
        switch ($key) {
            'UpArrow'   { $selected = ($selected - 1 + $Items.Count) % $Items.Count }
            'DownArrow' { $selected = ($selected + 1) % $Items.Count }
            'Enter'     { return $selected }
            'Escape'    { return -1 }
        }
    }
}

function Get-TreeSignature {
    param([Parameter(Mandatory)][string]$Root)

    if (-not (Test-Path -LiteralPath $Root -PathType Container)) {
        throw "目录不存在：$Root"
    }

    $rootFull = [IO.Path]::GetFullPath($Root).TrimEnd('\')
    $files = @(Get-ChildItem -LiteralPath $rootFull -Recurse -Force -File | Sort-Object FullName)
    if ((Get-Item -LiteralPath $rootFull).Attributes -band [IO.FileAttributes]::ReparsePoint) {
        throw "存档根目录不能是链接：$rootFull"
    }
    if (@(Get-ChildItem -LiteralPath $rootFull -Recurse -Force | Where-Object {
        $_.Attributes -band [IO.FileAttributes]::ReparsePoint
    }).Count -gt 0) { throw '存档中存在目录链接或文件链接，已停止操作。' }
    $lines = foreach ($file in $files) {
        $relative = $file.FullName.Substring($rootFull.Length).TrimStart('\')
        $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        "$relative|$($file.Length)|$hash"
    }

    $payload = [Text.Encoding]::UTF8.GetBytes(($lines -join "`n"))
    $treeHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($payload))
    $writePayload = [Text.Encoding]::UTF8.GetBytes((@($files | ForEach-Object {
        "$($_.FullName.Substring($rootFull.Length))|$($_.LastWriteTimeUtc.Ticks)"
    }) -join "`n"))
    [pscustomobject]@{
        Files = $files.Count
        Bytes = ($files | Measure-Object Length -Sum).Sum
        Hash  = $treeHash
        WriteHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($writePayload))
    }
}

function Test-SameTree {
    param(
        [Parameter(Mandatory)]$Left,
        [Parameter(Mandatory)]$Right
    )
    return ($Left.Files -eq $Right.Files -and $Left.Bytes -eq $Right.Bytes -and $Left.Hash -eq $Right.Hash)
}

function Remove-SafeWorkDirectory {
    param([Parameter(Mandatory)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) { return }
    $rootFull = [IO.Path]::GetFullPath($BackupRoot).TrimEnd('\') + '\'
    $pathFull = [IO.Path]::GetFullPath($Path)
    $leaf = Split-Path -Leaf $pathFull
    if (-not $pathFull.StartsWith($rootFull, [StringComparison]::OrdinalIgnoreCase) -or
        -not $leaf.StartsWith('.work-', [StringComparison]::OrdinalIgnoreCase)) {
        throw "拒绝清理未验证的目录：$pathFull"
    }
    Remove-Item -LiteralPath $pathFull -Recurse -Force
}

function Write-ArchiveHash {
    param([Parameter(Mandatory)][string]$ArchivePath)
    $hash = (Get-FileHash -LiteralPath $ArchivePath -Algorithm SHA256).Hash
    $line = "$hash  $([IO.Path]::GetFileName($ArchivePath))$([Environment]::NewLine)"
    [IO.File]::WriteAllText("$ArchivePath.sha256", $line, [Text.UTF8Encoding]::new($false))
    return $hash
}

function Test-ArchiveSidecar {
    param([Parameter(Mandatory)][string]$ArchivePath)
    $sidecar = "$ArchivePath.sha256"
    if (-not (Test-Path -LiteralPath $sidecar -PathType Leaf)) { return }
    $expected = ((Get-Content -LiteralPath $sidecar -Raw).Trim() -split '\s+')[0].ToUpperInvariant()
    $actual = (Get-FileHash -LiteralPath $ArchivePath -Algorithm SHA256).Hash
    if ($expected -ne $actual) {
        throw "备份校验失败，文件可能已损坏：$ArchivePath"
    }
}

function New-FullBackup {
    param(
        [string]$Prefix = 'Stoneshard-all-saves',
        [switch]$UpdateLatest
    )

    if (-not (Test-Path -LiteralPath $SaveRoot -PathType Container)) {
        throw "找不到 Stoneshard 存档目录：$SaveRoot"
    }

    New-Item -ItemType Directory -Path $BackupRoot -Force | Out-Null
    $stamp = Get-Date -Format 'yyyy-MM-dd_HH-mm-ss-fff'
    $archivePath = Join-Path $BackupRoot "$Prefix-$stamp.zip"
    $work = Join-Path $BackupRoot ('.work-' + [guid]::NewGuid().ToString('N'))
    $snapshot = Join-Path $work 'StoneShard'
    $verified = $false
    $temporaryZip = $null
    $latestTemp = $null

    try {
        for ($attempt = 1; $attempt -le 3; $attempt++) {
            if (Test-Path -LiteralPath $work) { Remove-SafeWorkDirectory -Path $work }
            New-Item -ItemType Directory -Path $work -Force | Out-Null

            Write-Host "正在读取存档（第 $attempt 次）..." -ForegroundColor Cyan
            $before = Get-TreeSignature -Root $SaveRoot
            Copy-Item -LiteralPath $SaveRoot -Destination $work -Recurse -Force
            $after = Get-TreeSignature -Root $SaveRoot
            $copied = Get-TreeSignature -Root $snapshot

            if ((Test-SameTree $before $after) -and (Test-SameTree $after $copied)) {
                $verified = $true
                break
            }
            Write-Host '检测到游戏正在改写存档，自动重试...' -ForegroundColor Yellow
        }

        if (-not $verified) {
            throw '连续三次都检测到存档变化。请先回到主菜单或关闭游戏后再备份。'
        }

        $temporaryZip = Join-Path $BackupRoot ('.work-' + [guid]::NewGuid().ToString('N') + '.zip')
        [IO.Compression.ZipFile]::CreateFromDirectory($snapshot, $temporaryZip, [IO.Compression.CompressionLevel]::Optimal, $true)

        $verifyFolder = Join-Path $work 'verify'
        Expand-Archive -LiteralPath $temporaryZip -DestinationPath $verifyFolder
        $expanded = Join-Path $verifyFolder 'StoneShard'
        $expandedSignature = Get-TreeSignature -Root $expanded
        $snapshotSignature = Get-TreeSignature -Root $snapshot
        if (-not (Test-SameTree $snapshotSignature $expandedSignature)) {
            throw '压缩包解压校验失败，未保留该备份。'
        }

        Move-Item -LiteralPath $temporaryZip -Destination $archivePath
        $archiveHash = Write-ArchiveHash -ArchivePath $archivePath

        if ($UpdateLatest) {
            $latestTemp = Join-Path $BackupRoot ('.work-latest-' + [guid]::NewGuid().ToString('N') + '.zip')
            Copy-Item -LiteralPath $archivePath -Destination $latestTemp
            if ((Get-FileHash -LiteralPath $latestTemp -Algorithm SHA256).Hash -ne $archiveHash) {
                Remove-Item -LiteralPath $latestTemp -Force
                throw '刷新最新备份时复制校验失败。'
            }
            Move-Item -LiteralPath $latestTemp -Destination $LatestArchive -Force
            [void](Write-ArchiveHash -ArchivePath $LatestArchive)
        }

        [pscustomobject]@{
            Archive = $archivePath
            Files = $snapshotSignature.Files
            Bytes = $snapshotSignature.Bytes
            Hash = $archiveHash
            LatestUpdated = [bool]$UpdateLatest
            Signature = $snapshotSignature
        }
    }
    finally {
        if (Test-Path -LiteralPath $work) { Remove-SafeWorkDirectory -Path $work }
        foreach ($temporaryFile in @($temporaryZip, $latestTemp)) {
            if ($temporaryFile -and (Test-Path -LiteralPath $temporaryFile -PathType Leaf)) {
                Remove-Item -LiteralPath $temporaryFile -Force
            }
        }
    }
}

function Get-BackupArchives {
    $archives = @(Get-ChildItem -LiteralPath $BackupRoot -Filter 'Stoneshard*.zip' -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending)
    $history = @($archives | Where-Object Name -NotLike 'Stoneshard-before-restore-*')
    $safety = @($archives | Where-Object Name -Like 'Stoneshard-before-restore-*')
    return @($history + $safety)
}

function Get-QuietSaveState {
    # The user keeps the game on the death/menu screen until restore completes.
    $exists = Test-Path -LiteralPath $SaveRoot -PathType Container
    $first = if ($exists) { Get-TreeSignature -Root $SaveRoot } else { $null }
    Start-Sleep -Milliseconds 1200
    $existsNow = Test-Path -LiteralPath $SaveRoot -PathType Container
    if ($exists -ne $existsNow) { throw '游戏正在创建或删除存档，请停留在死亡界面或主菜单后重试。' }
    $second = if ($existsNow) { Get-TreeSignature -Root $SaveRoot } else { $null }
    if ($exists -and ((-not (Test-SameTree $first $second)) -or $first.WriteHash -ne $second.WriteHash)) {
        throw '游戏仍在写入存档。请停留在死亡界面或主菜单，稍等后重试。'
    }
    [pscustomobject]@{ Exists = $exists; Signature = $second }
}

function Assert-RestoreTarget {
    $parent = [IO.Path]::GetFullPath($SaveParent).TrimEnd('\')
    $target = [IO.Path]::GetFullPath($SaveRoot).TrimEnd('\')
    if ($target -ine (Join-Path $parent 'StoneShard')) {
        throw "还原目标必须是已指定目录下的 StoneShard：$target"
    }
    $item = Get-Item -LiteralPath $parent -Force
    while ($null -ne $item) {
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw "还原路径不能经过目录链接：$($item.FullName)"
        }
        $item = $item.Parent
    }
    if (Test-Path -LiteralPath $target) {
        $item = Get-Item -LiteralPath $target -Force
        if (-not $item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw "还原目标不是普通目录：$target"
        }
    }
}

function Assert-SaveArchive {
    param([Parameter(Mandatory)][string]$ArchivePath)
    $archive = [IO.Compression.ZipFile]::OpenRead($ArchivePath)
    try {
        if ($archive.Entries.Count -eq 0) { throw '备份是空的。' }
        foreach ($entry in $archive.Entries) {
            $name = $entry.FullName.Replace('\', '/')
            if ($name.StartsWith('/') -or $name.Contains(':') -or
                @($name.Split('/') | Where-Object { $_ -eq '..' -or $_ -eq '.' }).Count -gt 0) {
                throw '压缩包包含不安全的路径，已停止。'
            }
        }
    }
    finally { $archive.Dispose() }
}

function Remove-RestoreWork {
    param([Parameter(Mandatory)][string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return }
    $full = [IO.Path]::GetFullPath($Path)
    $parent = [IO.Path]::GetFullPath($SaveParent).TrimEnd('\')
    if ((Split-Path -Parent $full) -ine $parent -or
        (Split-Path -Leaf $full) -notmatch '^\.StoneShard-restore-[a-f0-9]{32}$') {
        throw "拒绝清理未经验证的还原工作目录：$full"
    }
    if ((Get-Item -LiteralPath $full -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
        throw '还原工作目录已被替换为链接，停止清理。'
    }
    Remove-Item -LiteralPath $full -Recurse -Force
}

function Install-StagedSaveFiles {
    param([Parameter(Mandatory)][string]$StagedRoot, [Parameter(Mandatory)][ref]$Changed)
    Assert-RestoreTarget
    [void](Get-TreeSignature -Root $StagedRoot)
    $desired = @{}
    # Save payloads first, slot metadata next, character list last.
    $files = @(Get-ChildItem -LiteralPath $StagedRoot -Recurse -Force -File | Sort-Object @{
        Expression = {
            if ($_.Name -eq 'characters.map') { 3 }
            elseif ($_.Name -eq 'character.map') { 2 }
            elseif ($_.Name -eq 'save.map') { 1 }
            else { 0 }
        }
    }, FullName)
    foreach ($file in $files) {
        $relative = [IO.Path]::GetRelativePath($StagedRoot, $file.FullName)
        $desired[$relative] = $file
        if (Test-Path -LiteralPath (Join-Path $SaveRoot $relative) -PathType Container) {
            throw "目标文件路径被目录占用：$relative"
        }
    }
    $existing = if (Test-Path -LiteralPath $SaveRoot -PathType Container) {
        @(Get-ChildItem -LiteralPath $SaveRoot -Recurse -Force -File)
    } else { @() }
    $probes = [Collections.Generic.List[IO.FileStream]]::new()
    try {
        foreach ($file in $existing) {
            $probes.Add([IO.File]::Open($file.FullName, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::Read))
        }
    } catch { throw "存档文件被占用，当前存档未改动：$($_.Exception.Message)" }
    finally { foreach ($probe in $probes) { $probe.Dispose() } }

    New-Item -ItemType Directory -Path $SaveRoot -Force | Out-Null
    foreach ($file in $files) {
        $relative = [IO.Path]::GetRelativePath($StagedRoot, $file.FullName)
        $target = Join-Path $SaveRoot $relative
        if ((Test-Path -LiteralPath $target -PathType Leaf) -and
            (Get-FileHash -LiteralPath $target).Hash -eq (Get-FileHash -LiteralPath $file.FullName).Hash) {
            continue
        }
        New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
        $Changed.Value = $true
        # Rename a verified staged file over its target; never rename the save directory.
        [IO.File]::Move($file.FullName, $target, $true)
    }
    foreach ($file in $existing) {
        $relative = [IO.Path]::GetRelativePath($SaveRoot, $file.FullName)
        if (-not $desired.ContainsKey($relative)) {
            $retained = Join-Path (Split-Path -Parent $StagedRoot) ('removed-' + [guid]::NewGuid().ToString('N'))
            $Changed.Value = $true
            [IO.File]::Move($file.FullName, $retained)
        }
    }
}

function Restore-SaveArchive {
    param([Parameter(Mandatory)][string]$ArchivePath)

    Assert-RestoreTarget
    $archiveFull = (Get-Item -LiteralPath $ArchivePath -ErrorAction Stop).FullName
    $backupPrefix = [IO.Path]::GetFullPath($BackupRoot).TrimEnd('\') + '\'
    if (-not $archiveFull.StartsWith($backupPrefix, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetExtension($archiveFull) -ine '.zip') {
        throw '请选择备份目录内的 ZIP 存档。'
    }

    $work = Join-Path $BackupRoot ('.work-restore-' + [guid]::NewGuid().ToString('N'))
    $localWork = Join-Path $SaveParent ('.StoneShard-restore-' + [guid]::NewGuid().ToString('N'))
    $staged = Join-Path $localWork 'StoneShard'
    $previous = Join-Path $localWork 'previous'
    $safety = $null
    $keepRecovery = $false
    $archiveGuard = $null

    try {
        # Hold the selected archive read-only until verification finishes.
        $archiveGuard = [IO.File]::Open($archiveFull, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
        Test-ArchiveSidecar -ArchivePath $archiveFull
        Assert-SaveArchive -ArchivePath $archiveFull
        $archiveHashBefore = (Get-FileHash -LiteralPath $archiveFull -Algorithm SHA256).Hash
        New-Item -ItemType Directory -Path $work -ErrorAction Stop | Out-Null
        Expand-Archive -LiteralPath $archiveFull -DestinationPath $work
        $restoreRoot = Join-Path $work 'StoneShard'
        if (-not (Test-Path -LiteralPath $restoreRoot -PathType Container)) {
            if (Test-Path -LiteralPath (Join-Path $work 'characters_v1') -PathType Container) {
                $restoreRoot = $work
            } else { throw '备份内没有识别到 StoneShard 存档目录。' }
        }
        $saveFiles = @(Get-ChildItem -LiteralPath $restoreRoot -Recurse -Force -File -Filter '*.sav')
        if ($saveFiles.Count -eq 0 -or
            ((-not (Test-Path -LiteralPath (Join-Path $restoreRoot 'characters_v1') -PathType Container)) -and
             (-not (Test-Path -LiteralPath (Join-Path $restoreRoot 'characters') -PathType Container)))) {
            throw '备份不包含角色存档，已停止还原。'
        }
        $expected = Get-TreeSignature -Root $restoreRoot

        # Stage on the target volume; retain all live directory identities.
        New-Item -ItemType Directory -Path $localWork -ErrorAction Stop | Out-Null
        Copy-Item -LiteralPath $restoreRoot -Destination $staged -Recurse -Force
        if (-not (Test-SameTree $expected (Get-TreeSignature -Root $staged))) {
            throw '待还原文件校验失败，当前存档未改动。'
        }

        Write-Host '正在确认存档已停止写入，请保持游戏停留在菜单...' -ForegroundColor Cyan
        $quiet = Get-QuietSaveState
        if ($quiet.Exists) {
            Write-Host '正在备份还原前状态...' -ForegroundColor Cyan
            $safety = New-FullBackup -Prefix 'Stoneshard-before-restore'
            if (-not (Test-SameTree $quiet.Signature $safety.Signature)) {
                throw '准备期间存档发生变化，已停止还原；请停留在菜单后重试。'
            }
        }
        $ready = Get-QuietSaveState
        if ($ready.Exists -ne $quiet.Exists -or
            ($ready.Exists -and (-not (Test-SameTree $ready.Signature $quiet.Signature)))) {
            throw '存档在准备期间改变，当前存档未被替换。'
        }
        Assert-RestoreTarget
        if ($ready.Exists) {
            Copy-Item -LiteralPath $SaveRoot -Destination $previous -Recurse -Force
            if (-not (Test-SameTree $ready.Signature (Get-TreeSignature -Root $previous))) {
                throw '回退副本校验失败，当前存档未改动。'
            }
        }
        $changed = $false
        Write-Host '正在替换存档文件，游戏会保持运行...' -ForegroundColor Cyan
        try {
            Install-StagedSaveFiles -StagedRoot $staged -Changed ([ref]$changed)
        }
        catch {
            $switchError = $_.Exception.Message
            if ($changed) {
                $keepRecovery = $true
                if ($ready.Exists -and (Test-Path -LiteralPath $previous -PathType Container)) {
                    try {
                        $rollbackChanged = $false
                        Install-StagedSaveFiles -StagedRoot $previous -Changed ([ref]$rollbackChanged)
                        $keepRecovery = -not (Test-SameTree $ready.Signature (Get-TreeSignature -Root $SaveRoot))
                    } catch { $keepRecovery = $true }
                }
            }
            if ($keepRecovery) {
                $safetyPath = if ($null -ne $safety) { $safety.Archive } else { '还原前没有存档目录' }
                throw "还原未完成；恢复材料保留在 $localWork，保险备份：$safetyPath。$switchError"
            }
            throw "存档文件被占用或无法写入，本次还原已取消或回退。请返回游戏主菜单后重试。$switchError"
        }

        # Verify twice while the user remains in the menu. This does not modify game memory.
        $keepRecovery = $true
        if (-not (Test-SameTree $expected (Get-TreeSignature -Root $SaveRoot))) {
            throw "替换后存档被改写；请回到游戏主菜单重新还原。恢复材料保留在 $localWork"
        }
        Start-Sleep -Milliseconds 1200
        if (-not (Test-SameTree $expected (Get-TreeSignature -Root $SaveRoot))) {
            throw "游戏在还原后又写入了存档；请回到主菜单重新还原。恢复材料保留在 $localWork"
        }
        if ($archiveHashBefore -ne (Get-FileHash -LiteralPath $archiveFull -Algorithm SHA256).Hash) {
            throw "备份在操作期间改变，恢复材料保留在 $localWork"
        }
        $keepRecovery = $false
        [pscustomobject]@{
            Archive = $archiveFull
            Files = $expected.Files
            Hash = $expected.Hash
            SafetyArchive = if ($null -ne $safety) { $safety.Archive } else { $null }
            GameRunning = [bool](Get-Process StoneShard -ErrorAction SilentlyContinue)
            ArchiveUnchanged = $true
        }
    }
    finally {
        if ($null -ne $archiveGuard) { $archiveGuard.Dispose() }
        if (Test-Path -LiteralPath $work) { Remove-SafeWorkDirectory -Path $work }
        if (-not $keepRecovery -and (Test-Path -LiteralPath $localWork)) {
            Remove-RestoreWork -Path $localWork
        }
    }
}

function Invoke-Restore {
    $archives = @(Get-BackupArchives)
    if ($archives.Count -eq 0) { throw "没有找到 ZIP 备份：$BackupRoot" }
    $labels = @(foreach ($archive in $archives) {
        $kind = if ($archive.Name -like 'Stoneshard-before-restore-*') { '[保险] ' }
                elseif ($archive.Name -eq 'Stoneshard-latest.zip') { '[固定副本] ' } else { '' }
        "$kind$($archive.Name)"
    })
    $labels += '返回主菜单'
    $choice = Select-Menu -Title '选择备份（普通备份按时间排序；保险备份在后）' -Items $labels
    if ($choice -lt 0 -or $choice -eq $archives.Count) { return }

    $selected = $archives[$choice]
    Clear-Host
    Write-Host "还原：$($selected.Name)" -ForegroundColor Cyan
    Write-Host '请保持游戏停留在死亡界面或主菜单，直到显示“文件还原完成”。'
    Write-Host '游戏会保持运行；还原前会自动备份当前状态。'
    Write-Host '按 Enter 开始，其他键返回。' -ForegroundColor Yellow
    if ([Console]::ReadKey($true).Key -ne 'Enter') { return }

    $result = Restore-SaveArchive -ArchivePath $selected.FullName
    Write-Host "`n文件还原完成，$($result.Files) 个文件校验一致，原备份保持不变。" -ForegroundColor Green
    if ($result.GameRunning) {
        Write-Host '可以切回游戏尝试“继续游戏”；文件校验通过不等于游戏已成功读档。'
        Write-Host '如果继续按钮灰色或仍读旧档，在游戏内返回主菜单，重新进入“开始游戏 → 加载游戏”选择存档。' -ForegroundColor Yellow
        Write-Host '如果出现“存档文件损坏 / 加载中止”，请停止反复加载，保留现有备份再排查；脚本不能直接刷新游戏内存。' -ForegroundColor DarkGray
    }
    Write-Host "还原前备份：$($result.SafetyArchive)" -ForegroundColor DarkGray
    Pause-Menu
}

function Get-StatusText {
    $running = if (Get-Process StoneShard -ErrorAction SilentlyContinue) { '运行中' } else { '已关闭' }
    $fileCount = if (Test-Path -LiteralPath $SaveRoot) {
        @(Get-ChildItem -LiteralPath $SaveRoot -Recurse -Force -File).Count
    } else { 0 }
    $latestHistory = @(Get-BackupArchives | Where-Object Name -NotLike 'Stoneshard-before-restore-*') | Select-Object -First 1
    $latestText = if ($null -ne $latestHistory) {
        "$($latestHistory.Name)（$($latestHistory.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss'))）"
    } else { '暂无' }

    "游戏状态：$running（还原不退出游戏）`n当前存档文件：$fileCount 个`n最近备份：$latestText"
}

if ($LibraryOnly) { return }

$managerLock = $null
try {
    $managerLock = [IO.File]::Open((Join-Path $BackupRoot '.manager.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
} catch {
    Write-Host '另一个存档管理器已打开，请使用原窗口，或先关闭它。' -ForegroundColor Yellow
    Pause-Menu -Message '按任意键退出...'
    return
}
try {
while ($true) {
    $mainChoice = Select-Menu -Title (Get-StatusText) -Items @(
        '新建完整备份（保留一个新的历史版本）',
        '还原存档（保持游戏运行）',
        '刷新最新备份（新建历史版本并更新 Stoneshard-latest.zip）',
        '刷新状态',
        '退出'
    ) -Hint '↑/↓ 选择，Enter 确认'

    try {
        switch ($mainChoice) {
            0 {
                Clear-Host
                if (Get-Process StoneShard -ErrorAction SilentlyContinue) {
                    Write-Host '游戏正在运行；备份的是最近一次已经写入磁盘的存档。' -ForegroundColor Yellow
                }
                $result = New-FullBackup
                Write-Host "`n备份成功。" -ForegroundColor Green
                Write-Host "文件数：$($result.Files)"
                Write-Host "位置：$($result.Archive)"
                Write-Host "SHA256：$($result.Hash)"
                Pause-Menu
            }
            1 { Invoke-Restore }
            2 {
                Clear-Host
                if (Get-Process StoneShard -ErrorAction SilentlyContinue) {
                    Write-Host '游戏正在运行；刷新的是最近一次已经写入磁盘的存档。' -ForegroundColor Yellow
                }
                $result = New-FullBackup -UpdateLatest
                Write-Host "`n最新备份已刷新，同时保留了新的历史版本。" -ForegroundColor Green
                Write-Host "历史版本：$($result.Archive)"
                Write-Host "最新副本：$LatestArchive"
                Write-Host "文件数：$($result.Files)"
                Pause-Menu
            }
            3 { continue }
            4 { return }
            -1 { return }
        }
    }
    catch {
        Write-Host "`n操作失败：$($_.Exception.Message)" -ForegroundColor Red
        Pause-Menu
    }
}
} finally {
    if ($null -ne $managerLock) { $managerLock.Dispose() }
}
