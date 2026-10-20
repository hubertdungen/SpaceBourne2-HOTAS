# Developer helper: starts the bridge and the isolated test copy.
# The copy shares the original game's save folder, so the saves are backed up first.
$ErrorActionPreference = 'Stop'
$root   = $PSScriptRoot
$work   = (Resolve-Path "$root\..\..\work").Path
$exe    = Join-Path $work 'sandbox\Game\SB2\Binaries\Win64\SB2-Win64-Shipping.exe'
$bridge = Join-Path $root 'bridge\SB2-HOTAS.exe'
if (-not (Test-Path $exe) -or -not (Test-Path $bridge)) { throw "Run Prepare-TestCopy.ps1 first." }
if (Get-Process SB2-Win64-Shipping -ErrorAction SilentlyContinue) {
    throw "SpaceBourne 2 is already running. Save and quit it before starting the test copy."
}

$saves = "$env:LOCALAPPDATA\SB2\Saved\SaveGames"
if (Test-Path $saves) {
    $backup = Join-Path $work ("backup\SaveGames-" + (Get-Date -Format 'yyyyMMdd-HHmmss'))
    Copy-Item $saves $backup -Recurse
    Write-Host "Saves backed up to $backup"
}
if (-not (Get-Process SB2-HOTAS -ErrorAction SilentlyContinue)) { Start-Process $bridge -WorkingDirectory (Split-Path $bridge) }
Start-Process $exe -WorkingDirectory (Split-Path $exe)
Write-Host "Test copy starting. Click ARM in the bridge once you are in your ship; Ctrl+Alt+F12 disarms."
