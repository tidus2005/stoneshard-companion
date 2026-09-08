param([switch]$Publish)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location -LiteralPath $projectRoot
try {
    $zigCompiler = Join-Path $projectRoot '.tools\zig-windows-x86_64-0.13.0\zig.exe'
    if (-not (Test-Path -LiteralPath $zigCompiler)) {
        $zigCommand = Get-Command zig -ErrorAction SilentlyContinue
        if (-not $zigCommand) { throw 'Zig 0.13.0 is required. Place it in .tools or add zig to PATH.' }
        $zigCompiler = $zigCommand.Source
    }
    New-Item -ItemType Directory -Force -Path 'artifacts\native' | Out-Null
    & $zigCompiler cc -target x86_64-windows-gnu -shared -O2 native\Bridge\bridge.c -o artifacts\native\StoneshardBridge.dll -luser32 -lkernel32
    if ($LASTEXITCODE -ne 0) { throw 'Native bridge build failed.' }
    dotnet build src\Probe\StoneshardCompanion.Probe.csproj -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Probe build failed.' }
    if ($Publish) {
        $version = ([xml](Get-Content -LiteralPath 'src\Overlay\StoneshardCompanion.csproj' -Raw)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
        $releaseFolder = Join-Path $projectRoot "artifacts\release\StoneshardCompanion-$version"
        dotnet publish src\Overlay\StoneshardCompanion.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o $releaseFolder --nologo
        if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
        & "$PSScriptRoot\package-portable-runtime.ps1" -ReleaseFolder $releaseFolder
        [IO.File]::WriteAllText((Join-Path $releaseFolder 'Start.cmd'), "@echo off`r`nstart `"`" `"%~dp0StoneshardCompanion.exe`"`r`n", [Text.Encoding]::ASCII)
        Copy-Item -LiteralPath 'docs\使用说明.md' -Destination (Join-Path $releaseFolder '使用说明.md')
        foreach ($notice in @('LICENSE', 'THIRD_PARTY_NOTICES.md')) {
            Copy-Item -LiteralPath $notice -Destination (Join-Path $releaseFolder $notice)
        }
        $releaseHashes = [ordered]@{}
        foreach ($releaseFile in (Get-ChildItem -LiteralPath $releaseFolder -Recurse -File | Where-Object Name -ne 'SHA256.json' | Sort-Object FullName)) {
            $releaseRelative = [IO.Path]::GetRelativePath($releaseFolder, $releaseFile.FullName)
            $releaseHashes[$releaseRelative] = (Get-FileHash -LiteralPath $releaseFile.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        }
        $releaseHashes | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $releaseFolder 'SHA256.json') -Encoding utf8
        $zip = Join-Path $projectRoot "artifacts\release\StoneshardCompanion-$version-win-x64.zip"
        Compress-Archive -LiteralPath $releaseFolder -DestinationPath $zip -Force
        $zipHash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
        "$zipHash  $([IO.Path]::GetFileName($zip))" | Set-Content -LiteralPath ($zip + '.sha256') -Encoding ascii
        Write-Host "Ready: $releaseFolder\StoneshardCompanion.exe"
        Write-Host "Package: $zip"
    } else {
        dotnet build src\Overlay\StoneshardCompanion.csproj -c Release --nologo
        if ($LASTEXITCODE -ne 0) { throw 'Overlay build failed.' }
    }
} finally { Pop-Location }
