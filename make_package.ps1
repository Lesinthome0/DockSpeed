# Builds the Thunderstore zip from the Release build output.
# Run after building in Release mode (Visual Studio: Release -> Build).
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$manifestPath = Join-Path $root "thunderstore\manifest.json"
$manifest = Get-Content $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$dll = Join-Path $root "release\plugins\DockSpeed.dll"
if (-not (Test-Path $dll)) { throw "Not found: $dll . Build in Release mode first." }

$stage = Join-Path $root "release\package"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Path (Join-Path $stage "plugins") | Out-Null
Copy-Item $dll (Join-Path $stage "plugins")
Copy-Item (Join-Path $root "thunderstore\manifest.json") $stage
Copy-Item (Join-Path $root "thunderstore\icon.png") $stage
Copy-Item (Join-Path $root "thunderstore\README.md") $stage
Copy-Item (Join-Path $root "CHANGELOG.md") $stage
Copy-Item (Join-Path $root "LICENSE") $stage

$zip = Join-Path $root ("release\sinthome-" + $manifest.name + "-" + $manifest.version_number + ".zip")
if (Test-Path $zip) { Remove-Item $zip }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($stage, $zip)
Write-Host "Created: $zip"
