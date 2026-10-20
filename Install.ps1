# Installs (or removes) the SB2 HOTAS mod in a SpaceBourne 2 folder.
# The game executable is never modified: UE4SS loads through dwmapi.dll next to it and the mod lives in Mods\SB2HOTAS.
param(
    [Parameter(Mandatory)] [string]$Game,     # the "SpaceBourne 2" folder
    [string]$UE4SS,                           # folder with UE4SS.dll, dwmapi.dll and UE4SS-settings.ini
    [switch]$Uninstall
)
$ErrorActionPreference = 'Stop'
# Default set here, not in param(): Windows PowerShell 5.1 leaves $PSScriptRoot empty there when a parameter is Mandatory.
if (-not $UE4SS) { $UE4SS = Join-Path $PSScriptRoot 'ue4ss' }
$win64 = Join-Path (Resolve-Path $Game).Path 'SB2\Binaries\Win64'
if (-not (Test-Path (Join-Path $win64 'SB2-Win64-Shipping.exe'))) {
    throw "SpaceBourne 2 not found in '$Game' (expected SB2\Binaries\Win64\SB2-Win64-Shipping.exe)."
}
$marker = Join-Path $win64 'SB2HOTAS-installed-UE4SS.txt'   # present only if this script installed UE4SS
$entry  = '^\s*SB2HOTAS\s*:'
# UE4SS 3.0.0 (bundled) keeps UE4SS.dll and Mods next to the exe; later 3.x releases move them into "ue4ss\".
$existing = if (Test-Path (Join-Path $win64 'ue4ss\UE4SS.dll')) { Join-Path $win64 'ue4ss\Mods' }
            elseif (Test-Path (Join-Path $win64 'UE4SS.dll')) { Join-Path $win64 'Mods' }

if ($Uninstall) {
    foreach ($mods in (Join-Path $win64 'Mods'), (Join-Path $win64 'ue4ss\Mods')) {
        if (Test-Path (Join-Path $mods 'SB2HOTAS')) { Remove-Item (Join-Path $mods 'SB2HOTAS') -Recurse -Force }
        $modsTxt = Join-Path $mods 'mods.txt'
        if (Test-Path $modsTxt) { Set-Content $modsTxt -Value @(Get-Content $modsTxt | Where-Object { $_ -notmatch $entry }) -Encoding ASCII }
    }
    if (Test-Path $marker) {
        $ours = Join-Path $win64 'Mods'
        if (Get-ChildItem $ours -Directory -ErrorAction SilentlyContinue) {
            Write-Warning "Other UE4SS mods are installed, so UE4SS itself was left in place."
        } else {
            'dwmapi.dll', 'UE4SS.dll', 'UE4SS-settings.ini', 'UE4SS.log' |
                ForEach-Object { Remove-Item (Join-Path $win64 $_) -ErrorAction SilentlyContinue }
            Remove-Item $ours -Recurse -Force -ErrorAction SilentlyContinue
            Remove-Item $marker
        }
    }
    Write-Host "SB2 HOTAS removed from $win64"
    return
}

# Reuse an existing UE4SS (the player may have other mods); otherwise install ours with its console window off.
if ($existing) { $mods = $existing }
else {
    if (Test-Path (Join-Path $win64 'dwmapi.dll')) { throw "The game folder already has a dwmapi.dll from another mod loader. Remove it, or install UE4SS yourself first." }
    if (-not (Test-Path (Join-Path $UE4SS 'UE4SS.dll'))) { throw "UE4SS not found in '$UE4SS'. Pass -UE4SS <folder>." }
    Copy-Item (Join-Path $UE4SS 'dwmapi.dll'), (Join-Path $UE4SS 'UE4SS.dll') $win64 -Force
    (Get-Content (Join-Path $UE4SS 'UE4SS-settings.ini')) -replace '^GuiConsoleEnabled\s*=.*', 'GuiConsoleEnabled = 0' |
        Set-Content (Join-Path $win64 'UE4SS-settings.ini') -Encoding ASCII
    'UE4SS was installed by SB2 HOTAS. "Install.ps1 -Uninstall" removes it.' | Set-Content $marker -Encoding ASCII
    $mods = Join-Path $win64 'Mods'
}
New-Item -ItemType Directory -Force (Join-Path $mods 'SB2HOTAS\scripts') | Out-Null
Copy-Item "$PSScriptRoot\mod\SB2HOTAS\scripts\*.lua" (Join-Path $mods 'SB2HOTAS\scripts') -Force

# Enable the mod (re-enabling a "SB2HOTAS : 0"), keeping UE4SS's built-in Keybinds entry last as it requires.
$modsTxt = Join-Path $mods 'mods.txt'
$lines = [Collections.Generic.List[string]]@(if (Test-Path $modsTxt) { Get-Content $modsTxt })
$at = $lines.FindIndex([Predicate[string]]{ param($l) $l -match $entry })
if ($at -ge 0) { $lines[$at] = 'SB2HOTAS : 1' }
else {
    $keybinds = $lines.FindIndex([Predicate[string]]{ param($l) $l -match '^\s*Keybinds\s*:' })
    if ($keybinds -ge 0) { $lines.Insert($keybinds, 'SB2HOTAS : 1') } else { $lines.Add('SB2HOTAS : 1') }
}
Set-Content $modsTxt -Value $lines -Encoding ASCII

# The bridge reads controllers through the SDL2.dll that ships with the game.
$bridge = Join-Path $PSScriptRoot 'bridge'
if ((Test-Path $bridge) -and -not (Test-Path (Join-Path $bridge 'SDL2.dll')) -and (Test-Path (Join-Path $win64 'SDL2.dll'))) {
    Copy-Item (Join-Path $win64 'SDL2.dll') $bridge
}
Write-Host "SB2 HOTAS installed in $win64"
