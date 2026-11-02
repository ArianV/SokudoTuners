# Release build + Thunderstore zip in dist\. The same zip works for Nexus.
$ErrorActionPreference = "Stop"

dotnet build "$PSScriptRoot\SokudoTuners.csproj" -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$manifest = Get-Content "$PSScriptRoot\thunderstore\manifest.json" -Raw | ConvertFrom-Json
$version = $manifest.version_number
$csprojVersion = ([xml](Get-Content "$PSScriptRoot\SokudoTuners.csproj")).Project.PropertyGroup.Version
if ($csprojVersion -ne $version) {
    Write-Host "Version mismatch: manifest $version, csproj $csprojVersion" -ForegroundColor Red
    exit 1
}

$dist = "$PSScriptRoot\dist"
New-Item -ItemType Directory -Force $dist | Out-Null
$zipPath = "$dist\SokudoTuners-$version.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath }

# Compress-Archive in PowerShell 5 writes backslash paths, which Thunderstore rejects.
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::Open($zipPath, "Create")
try {
    $files = @{
        "manifest.json"         = "$PSScriptRoot\thunderstore\manifest.json"
        "icon.png"              = "$PSScriptRoot\assets\icon.png"
        "README.md"             = "$PSScriptRoot\README.md"
        "CHANGELOG.md"          = "$PSScriptRoot\CHANGELOG.md"
        "Mods/SokudoTuners.dll" = "$PSScriptRoot\bin\Release\net472\SokudoTuners.dll"
    }
    foreach ($entry in $files.GetEnumerator()) {
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $entry.Value, $entry.Key) | Out-Null
    }
}
finally {
    $zip.Dispose()
}

Write-Host "Packaged $zipPath" -ForegroundColor Green
