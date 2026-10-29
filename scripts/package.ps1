# Builds the plugin in Release and zips a Thunderstore-ready package to dist/.
# Usage (from the repo root):  powershell -File scripts/package.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$manifest = Get-Content (Join-Path $root 'thunderstore/manifest.json') -Raw | ConvertFrom-Json

# PATH may point at a runtime-only dotnet (e.g. the x86 install); use the first one that actually has an SDK.
$dotnet = @('dotnet', "$env:ProgramFiles\dotnet\dotnet.exe") | Where-Object {
    (Get-Command $_ -ErrorAction SilentlyContinue) -and (& $_ --list-sdks 2>$null)
} | Select-Object -First 1
if (-not $dotnet) { throw 'No .NET SDK found. Install the .NET 8 SDK.' }

& $dotnet build (Join-Path $root 'src/ItemRoulettePatcher/ItemRoulettePatcher.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }

$stage = Join-Path $root "dist/stage"
Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $stage | Out-Null

$bin = Join-Path $root 'src/ItemRoulettePatcher/bin/Release/netstandard2.1'
Copy-Item (Join-Path $bin 'ItemRoulettePatcher.dll') $stage
foreach ($f in 'artifact_on.png', 'artifact_off.png') {
    $icon = Join-Path $root "assets/$f"
    if (Test-Path $icon) { Copy-Item $icon $stage }
}
foreach ($f in 'manifest.json', 'README.md', 'CHANGELOG.md', 'icon.png') {
    Copy-Item (Join-Path $root "thunderstore/$f") $stage
}

$zip = Join-Path $root "dist/$($manifest.name)-$($manifest.version_number).zip"
Remove-Item $zip -Force -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip
Remove-Item $stage -Recurse -Force
Write-Host "Created $zip"
