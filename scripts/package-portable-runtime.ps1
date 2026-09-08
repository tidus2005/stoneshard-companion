param([Parameter(Mandatory)][string]$ReleaseFolder)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$spec = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'portable-runtime.json') -Raw | ConvertFrom-Json
$cache = Join-Path $projectRoot '.tools\portable-runtime'
New-Item -ItemType Directory -Path $cache -Force | Out-Null
$archive = Join-Path $cache "PowerShell-$($spec.version)-win-x64.zip"
if (-not (Test-Path -LiteralPath $archive)) {
    $partial = $archive + '.partial'
    Invoke-WebRequest -Uri $spec.url -OutFile $partial
    if ((Get-FileHash -LiteralPath $partial -Algorithm SHA256).Hash -ine $spec.sha256) { throw 'PowerShell download checksum mismatch.' }
    Move-Item -LiteralPath $partial -Destination $archive -Force
}
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ine $spec.sha256) { throw 'Cached PowerShell checksum mismatch.' }
$runtime = Join-Path $ReleaseFolder 'Runtime\PowerShell'
New-Item -ItemType Directory -Path $runtime -Force | Out-Null
Expand-Archive -LiteralPath $archive -DestinationPath $runtime -Force
foreach ($required in @('pwsh.exe','LICENSE.txt','ThirdPartyNotices.txt')) {
    if (-not (Test-Path -LiteralPath (Join-Path $runtime $required))) { throw "Missing portable runtime file: $required" }
}
Write-Host "Bundled PowerShell $($spec.version) (verified SHA256)."
