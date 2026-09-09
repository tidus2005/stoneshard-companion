param([Parameter(Mandatory)][string]$PackageFolder)
$ErrorActionPreference='Stop'
$runningGame = @(Get-Process -Name StoneShard -ErrorAction SilentlyContinue)
if ($runningGame.Count) {
    $runningGame | ForEach-Object { $_.Dispose() }
    Write-Output 'SKIP packaged update helper while game is running: the production guard would show a dialog. Run this test on game-free CI.'
    return
}
$projectRoot=Split-Path -Parent $PSScriptRoot
$package=(Resolve-Path -LiteralPath $PackageFolder).Path
$fixture=Join-Path $projectRoot 'artifacts\update-fixture'
New-Item -ItemType Directory -Force -Path $fixture | Out-Null
@'
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0-windows</TargetFramework><ImplicitUsings>enable</ImplicitUsings><AssemblyName>StoneshardCompanion</AssemblyName></PropertyGroup></Project>
'@ | Set-Content -LiteralPath (Join-Path $fixture 'fixture.csproj')
@'
if(args.Contains("--wait")){
    while(!File.Exists(Path.Combine(AppContext.BaseDirectory,"exit.signal")))await Task.Delay(100);
}else File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"restarted.txt"),"OK");
'@ | Set-Content -LiteralPath (Join-Path $fixture 'Program.cs')
dotnet publish (Join-Path $fixture 'fixture.csproj') -r win-x64 --self-contained true -p:PublishSingleFile=true -p:DebugType=None -o (Join-Path $fixture 'published') --nologo
if($LASTEXITCODE -ne 0){throw 'Fixture build failed'}
$updates=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'StoneshardCompanion\Updates'
$work=Join-Path $updates ([guid]::NewGuid().ToString('N'))
$target=Join-Path $fixture ([guid]::NewGuid().ToString('N'))
$staged=Join-Path $work 'package'
New-Item -ItemType Directory -Force -Path $staged,$target | Out-Null
# Copy only test fixture files; the user's running application is never targeted.
Copy-Item -LiteralPath (Join-Path $package 'StoneshardCompanion.exe') -Destination (Join-Path $work 'UpdateHelper.exe')
Copy-Item -LiteralPath (Join-Path $fixture 'published\StoneshardCompanion.exe') -Destination $target
$manifest=Get-Content -LiteralPath (Join-Path $package 'SHA256.json') -Raw | ConvertFrom-Json
$hashes=[ordered]@{}
foreach($entry in $manifest.PSObject.Properties){
    $path=Join-Path $staged $entry.Name
    New-Item -ItemType Directory -Force -Path ([IO.Path]::GetDirectoryName($path)) | Out-Null
    if($entry.Name -eq 'StoneshardCompanion.exe'){Copy-Item -LiteralPath (Join-Path $fixture 'published\StoneshardCompanion.exe') -Destination $path}
    else{[IO.File]::WriteAllText($path,'FIXTURE_NEW')}
    $hashes[$entry.Name]=(Get-FileHash -LiteralPath $path).Hash
}
$hashes | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $staged 'SHA256.json')
$parentStart=[Diagnostics.ProcessStartInfo]::new((Join-Path $target 'StoneshardCompanion.exe'))
$parentStart.UseShellExecute=$false;$parentStart.CreateNoWindow=$true;$parentStart.WindowStyle='Hidden'
$parentStart.ArgumentList.Add('--wait')
$parent=[Diagnostics.Process]::Start($parentStart)
$request=Join-Path $work 'request.json'
@{Parent=$parent.Id;Started=$parent.StartTime.ToFileTimeUtc();Target=$target;Package=$staged} | ConvertTo-Json | Set-Content -LiteralPath $request
$helperStart=[Diagnostics.ProcessStartInfo]::new((Join-Path $work 'UpdateHelper.exe'))
$helperStart.UseShellExecute=$false;$helperStart.CreateNoWindow=$true;$helperStart.WindowStyle='Hidden'
$helperStart.ArgumentList.Add('--apply-update');$helperStart.ArgumentList.Add($request)
$helper=[Diagnostics.Process]::Start($helperStart)
try{
    $watch=[Diagnostics.Stopwatch]::StartNew()
    while(-not (Test-Path -LiteralPath (Join-Path $work 'ready'))){
        if($helper.HasExited -or $watch.Elapsed.TotalSeconds -gt 20){throw 'Update helper failed to signal readiness'}
        Start-Sleep -Milliseconds 100
    }
    [IO.File]::WriteAllText((Join-Path $target 'exit.signal'),'exit')
    if(-not $parent.WaitForExit(10000)){throw 'Fixture parent did not exit'}
    $parent.Dispose()
    if(-not $helper.WaitForExit(20000) -or $helper.ExitCode -ne 0){throw 'Update helper failed'}
    $watch.Restart()
    while(-not (Test-Path -LiteralPath (Join-Path $target 'restarted.txt'))){
        if($watch.Elapsed.TotalSeconds -gt 10){throw 'Updated executable did not restart'}
        Start-Sleep -Milliseconds 100
    }
    foreach($entry in $hashes.GetEnumerator()){
        if((Get-FileHash -LiteralPath (Join-Path $target $entry.Key)).Hash -ne $entry.Value){throw 'Installed fixture hash mismatch'}
    }
    if(-not (Test-Path -LiteralPath (Join-Path $work 'rollback\StoneshardCompanion.exe'))){throw 'Rollback copy missing'}
    'PASS packaged update helper: ready handshake, parent exit, replacement, all hashes, recovery copy, automatic restart'
}finally{
    [IO.File]::WriteAllText((Join-Path $target 'exit.signal'),'exit')
    $parent.Dispose();$helper.Dispose()
    # Leave these isolated fixtures for inspection; never remove a running executable.
}
