# .\build.ps1 [Release]
param([string]$Configuration = "Debug")

dotnet build "$PSScriptRoot\SokudoTuners.csproj" -c $Configuration
$code = $LASTEXITCODE

# The copy works with the game open, but it keeps running the old DLL until a restart.
if ($code -eq 0 -and (Get-Process -Name "Schedule I" -ErrorAction SilentlyContinue)) {
    Write-Host "Schedule I is running. Restart it to load the new build." -ForegroundColor Yellow
}
exit $code
