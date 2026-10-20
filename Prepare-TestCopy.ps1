# Developer helper: builds the bridge and installs the mod into the isolated test copy (work\sandbox\Game).
# It never writes to the Steam install, and finishes by checking that the original executables are untouched.
param([string]$Game = "$PSScriptRoot\..\..\work\sandbox\Game")
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$work = (Resolve-Path "$root\..\..\work").Path
$Game = (Resolve-Path $Game).Path
if ($Game -match '\\steamapps\\') { throw "'$Game' is a Steam install, not the test copy." }
if (Get-Process SB2-HOTAS -ErrorAction SilentlyContinue) { throw "Close SB2-HOTAS (the bridge) first." }

Write-Host "1/3 Building the bridge..."
dotnet publish "$root\source\Bridge\Bridge.csproj" -c Release -o "$root\bridge" --nologo -v q
if ($LASTEXITCODE) { throw "Bridge build failed." }

Write-Host "2/3 Installing UE4SS and the mod into the test copy..."
& "$root\Install.ps1" -Game $Game -UE4SS "$work\tools\ue4ss"
# Lets the copy start directly instead of Steam relaunching the original install.
"1646850" | Set-Content (Join-Path $Game 'SB2\Binaries\Win64\steam_appid.txt') -Encoding ASCII

Write-Host "3/3 Checking the original executables..."
foreach ($o in (Get-Content "$work\inspection\original-executables.json" -Raw | ConvertFrom-Json)) {
    if ((Get-FileHash $o.Path -Algorithm SHA256).Hash -eq $o.Hash) { Write-Host "  untouched: $($o.Path)" }
    else { Write-Warning "Changed (Steam update?): $($o.Path)" }
}
Write-Host "`nDone. Run bridge\SB2-HOTAS.exe to set up your controllers; play the test copy with Start-TestCopy.ps1." -ForegroundColor Green
