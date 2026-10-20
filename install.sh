#!/usr/bin/env bash
# Installs (or removes) SB2 HOTAS for SpaceBourne 2 running through Steam Proton on Linux / Steam Deck.
# The game executable is never modified: UE4SS loads through dwmapi.dll next to it and the mod lives in Mods/SB2HOTAS.
# Usage: bash install.sh [--uninstall] ["/path/to/SpaceBourne 2"]     (UE4SS=<folder> overrides the bundled ue4ss/)
set -euo pipefail
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
die() { echo "error: $*" >&2; exit 1; }

uninstall=0
if [[ "${1:-}" == "--uninstall" ]]; then uninstall=1; shift; fi

find_game() {
    local libs=("$HOME/.steam/steam" "$HOME/.local/share/Steam" "$HOME/.var/app/com.valvesoftware.Steam/data/Steam")
    local base vdf lib path
    for base in "${libs[@]}"; do
        vdf="$base/steamapps/libraryfolders.vdf"
        [[ -f "$vdf" ]] || continue
        while IFS= read -r path; do libs+=("$path"); done < <(sed -n 's/.*"path"[[:space:]]*"\(.*\)".*/\1/p' "$vdf")
    done
    for lib in "${libs[@]}"; do
        if [[ -f "$lib/steamapps/common/SpaceBourne 2/SB2/Binaries/Win64/SB2-Win64-Shipping.exe" ]]; then
            echo "$lib/steamapps/common/SpaceBourne 2"; return
        fi
    done
}

game="${1:-$(find_game)}"
[[ -n "$game" ]] || die 'SpaceBourne 2 not found in your Steam libraries. Pass its folder: bash install.sh "/path/to/SpaceBourne 2"'
win64="$game/SB2/Binaries/Win64"
[[ -f "$win64/SB2-Win64-Shipping.exe" ]] || die "SpaceBourne 2 not found in '$game' (expected SB2/Binaries/Win64/SB2-Win64-Shipping.exe)."
marker="$win64/SB2HOTAS-installed-UE4SS.txt"   # present only if we installed UE4SS
entry='^[[:space:]]*SB2HOTAS[[:space:]]*:'
desktop="${XDG_DATA_HOME:-$HOME/.local/share}/applications/sb2-hotas-bridge.desktop"

if (( uninstall )); then
    for mods in "$win64/Mods" "$win64/ue4ss/Mods"; do
        rm -rf "$mods/SB2HOTAS"
        [[ -f "$mods/mods.txt" ]] && sed -i "/$entry/d" "$mods/mods.txt"
    done
    if [[ -f "$marker" ]]; then
        if [[ -n "$(find "$win64/Mods" -mindepth 1 -maxdepth 1 -type d 2>/dev/null)" ]]; then
            echo "warning: other UE4SS mods are installed, so UE4SS itself was left in place." >&2
        else
            rm -f "$win64/dwmapi.dll" "$win64/UE4SS.dll" "$win64/UE4SS-settings.ini" "$win64/UE4SS.log" "$marker"
            rm -rf "$win64/Mods"
        fi
    fi
    rm -f "$desktop"
    echo "SB2 HOTAS removed from $win64"
    echo 'You can also remove WINEDLLOVERRIDES="dwmapi=n,b" from the game'"'"'s Steam launch options.'
    exit 0
fi

# Reuse an existing UE4SS (the player may have other mods); otherwise install ours with its console window off.
# UE4SS 3.0.0 (bundled) keeps UE4SS.dll and Mods next to the exe; later 3.x releases move them into "ue4ss/".
if [[ -f "$win64/ue4ss/UE4SS.dll" ]]; then mods="$win64/ue4ss/Mods"
elif [[ -f "$win64/UE4SS.dll" ]]; then mods="$win64/Mods"
else
    [[ -f "$win64/dwmapi.dll" ]] && die "The game folder already has a dwmapi.dll from another mod loader. Remove it, or install UE4SS yourself first."
    ue4ss="${UE4SS:-$here/ue4ss}"
    [[ -f "$ue4ss/UE4SS.dll" ]] || die "UE4SS not found in '$ue4ss'. Set UE4SS=<folder>."
    cp "$ue4ss/dwmapi.dll" "$ue4ss/UE4SS.dll" "$win64/"
    sed 's/^GuiConsoleEnabled[[:space:]]*=.*/GuiConsoleEnabled = 0/' "$ue4ss/UE4SS-settings.ini" > "$win64/UE4SS-settings.ini"
    echo 'UE4SS was installed by SB2 HOTAS. "bash install.sh --uninstall" removes it.' > "$marker"
    mods="$win64/Mods"
fi
mkdir -p "$mods/SB2HOTAS/scripts"
cp "$here"/mod/SB2HOTAS/scripts/*.lua "$mods/SB2HOTAS/scripts/"

# Enable the mod (re-enabling a "SB2HOTAS : 0"), keeping UE4SS's built-in Keybinds entry last as it requires.
modstxt="$mods/mods.txt"
touch "$modstxt"
if grep -q "$entry" "$modstxt"; then
    sed -i "s/$entry.*/SB2HOTAS : 1/" "$modstxt"
elif grep -q '^[[:space:]]*Keybinds[[:space:]]*:' "$modstxt"; then
    sed -i '0,/^[[:space:]]*Keybinds[[:space:]]*:/s//SB2HOTAS : 1\n&/' "$modstxt"
else
    [[ -s "$modstxt" && -n "$(tail -c1 "$modstxt")" ]] && echo >> "$modstxt"
    echo "SB2HOTAS : 1" >> "$modstxt"
fi

# The bridge reads controllers through the SDL2.dll that ships with the game.
if [[ -d "$here/bridge" && ! -f "$here/bridge/SDL2.dll" && -f "$win64/SDL2.dll" ]]; then cp "$win64/SDL2.dll" "$here/bridge/"; fi

# An app-menu entry for the bridge, so it's one click away (handy in Steam Deck desktop mode).
mkdir -p "$(dirname "$desktop")"
cat > "$desktop" <<EOF
[Desktop Entry]
Type=Application
Name=SB2 HOTAS bridge
Comment=Joystick, pedals and head tracking for SpaceBourne 2 (start the game first)
Exec=bash "$here/run-bridge.sh"
Terminal=true
Categories=Game;
EOF

if command -v protontricks-launch >/dev/null 2>&1 || { command -v flatpak >/dev/null 2>&1 && flatpak info com.github.Matoking.protontricks >/dev/null 2>&1; }; then
    pt="protontricks: found."
else
    pt="protontricks: NOT FOUND. The bridge needs it. Install it with your package manager, or:
         flatpak install flathub com.github.Matoking.protontricks
     (On Steam Deck: Discover store > search \"Protontricks\".)"
fi

cat <<EOF
SB2 HOTAS installed in $win64

Next:
  1. In Steam: SpaceBourne 2 > Properties > Launch Options, enter:
         WINEDLLOVERRIDES="dwmapi=n,b" %command%
     (Proton only loads UE4SS with this.)
  2. Start the game, then open "SB2 HOTAS bridge" from your app menu
     (or run: bash "$here/run-bridge.sh").
  $pt
EOF
