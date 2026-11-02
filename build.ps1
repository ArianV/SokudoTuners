# Builds the mod. The csproj copies CarTuning.dll into the game's Mods folder after every build.
# Usage: .\build.ps1            (Debug)
#        .\build.ps1 Release
param([string]$Configuration = "Debug")

dotnet build "$PSScriptRoot\CarTuning.csproj" -c $Configuration
$code = $LASTEXITCODE

# MelonLoader reads mods into memory at startup, so the copy works with the game open,
# but the running game keeps the old version until I restart it.
if ($code -eq 0 -and (Get-Process -Name "Schedule I" -ErrorAction SilentlyContinue)) {
    Write-Host "Schedule I is running. Restart it to load the new build." -ForegroundColor Yellow
}
exit $code
