#!/usr/bin/env bash
# One-command SB2 HOTAS setup for Linux / Steam Deck: downloads the latest release into ~/SB2-HOTAS and installs it.
#   curl -fsSL https://raw.githubusercontent.com/hubertdungen/SpaceBourne2-HOTAS/main/linux-setup.sh | bash
# Extra arguments go to install.sh (e.g. the game folder). SB2HOTAS_DIR=<folder> changes where it is kept.
set -euo pipefail
dest="${SB2HOTAS_DIR:-$HOME/SB2-HOTAS}"
die() { echo "error: $*" >&2; exit 1; }
command -v curl >/dev/null || die "curl is required."

echo "Looking up the latest SB2 HOTAS release..."
url=$(curl -fsSL https://api.github.com/repos/hubertdungen/SpaceBourne2-HOTAS/releases/latest |
    sed -n 's/.*"browser_download_url": *"\([^"]*\.zip\)".*/\1/p' | head -n 1)
[[ -n "$url" ]] || die "could not find the latest release on GitHub."

tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT
echo "Downloading $url"
curl -fL --progress-bar "$url" -o "$tmp/sb2hotas.zip"

# unzip is not always installed (e.g. on SteamOS); Python's zipfile does the same job.
if command -v unzip >/dev/null; then unzip -q "$tmp/sb2hotas.zip" -d "$tmp/x"
elif command -v python3 >/dev/null; then python3 -m zipfile -e "$tmp/sb2hotas.zip" "$tmp/x"
else die "unzip or python3 is required to extract the download."
fi

mkdir -p "$dest"
cp -r "$tmp"/x/SB2-HOTAS-*/. "$dest/"
chmod +x "$dest"/*.sh
echo "SB2 HOTAS files are in $dest"
bash "$dest/install.sh" "$@"
