#!/usr/bin/env bash
# Opens the SB2 HOTAS bridge inside SpaceBourne 2's Proton prefix. It has to run there to share the game's
# %LOCALAPPDATA% (where it talks to the mod) and to see the game window and send it key presses.
# Start the game first. Needs protontricks (native package or the Flatpak).
set -euo pipefail
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
exe="$here/bridge/SB2-HOTAS.exe"
appid=1646850
[[ -f "$exe" ]] || { echo "error: $exe not found." >&2; exit 1; }

if command -v protontricks-launch >/dev/null 2>&1; then
    exec protontricks-launch --appid "$appid" "$exe"
elif command -v flatpak >/dev/null 2>&1 && flatpak info com.github.Matoking.protontricks >/dev/null 2>&1; then
    # The Flatpak only sees folders it was given; allow this one once.
    flatpak override --user --filesystem="$here" com.github.Matoking.protontricks
    exec flatpak run --command=protontricks-launch com.github.Matoking.protontricks --appid "$appid" "$exe"
else
    echo "error: protontricks is not installed." >&2
    echo "Install it from your package manager, or: flatpak install flathub com.github.Matoking.protontricks" >&2
    exit 1
fi
