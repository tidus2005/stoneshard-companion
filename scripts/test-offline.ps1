# No game/UI operations: pure policies, mocked native API, temporary save trees.
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location -LiteralPath $projectRoot
try {
    & "$PSScriptRoot\build.ps1"
    $zigCompiler = Join-Path $projectRoot '.tools\zig-windows-x86_64-0.13.0\zig.exe'
    if (-not (Test-Path -LiteralPath $zigCompiler)) { $zigCompiler = (Get-Command zig -ErrorAction Stop).Source }
    & $zigCompiler cc -target x86_64-windows-gnu -O2 native\Tests\supplies_test.c -o artifacts\supplies_test.exe
    if ($LASTEXITCODE -ne 0) { throw 'Native offline test build failed.' }
    & artifacts\supplies_test.exe | Tee-Object -FilePath artifacts\native-offline-v03.log
    if ($LASTEXITCODE -ne 0) { throw 'Native offline tests failed.' }
    & $zigCompiler cc -target x86_64-windows-gnu -O2 native\Tests\interaction_test.c -o artifacts\interaction_test.exe
    if ($LASTEXITCODE -ne 0) { throw 'Interaction test build failed.' }
    & artifacts\interaction_test.exe | Tee-Object -FilePath artifacts\interaction-offline-v032.log
    if ($LASTEXITCODE -ne 0) { throw 'Native interaction tests failed.' }
    & $zigCompiler cc -target x86_64-windows-gnu -O2 native\Tests\walk_native_test.c -o artifacts\walk_native_test.exe
    if ($LASTEXITCODE -ne 0) { throw 'Native walk adapter test build failed.' }
    & artifacts\walk_native_test.exe | Tee-Object -FilePath artifacts\walk-native-offline.log
    if ($LASTEXITCODE -ne 0) { throw 'Native walk adapter tests failed.' }
    foreach ($case in @('walk_click','fodder')) {
        & $zigCompiler cc -target x86_64-windows-gnu -O2 "native/Tests/${case}_test.c" -o "artifacts/${case}_test.exe"
        if ($LASTEXITCODE -ne 0) { throw "${case} test build failed" }
        & "./artifacts/${case}_test.exe"
        if ($LASTEXITCODE -ne 0) { throw "${case} tests failed" }
    }
    dotnet src\Probe\bin\Release\net8.0-windows\StoneshardCompanion.Probe.dll OfflineTest $projectRoot | Tee-Object -FilePath artifacts\offline-v03.log
    if ($LASTEXITCODE -ne 0) { throw 'Managed offline tests failed.' }
    & pwsh -NoLogo -NoProfile -File tests\Test-SaveManager.ps1 | Tee-Object -FilePath artifacts\save-core-v03.log
    if ($LASTEXITCODE -ne 0) { throw 'Save core regression tests failed.' }
} finally { Pop-Location }
