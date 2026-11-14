#!/usr/bin/env bash
# Baut die EXE und veröffentlicht sie als GitHub-Release (Version aus der .csproj).
# Aufruf: tools/release.sh "Was ist neu …"
set -euo pipefail
cd "$(dirname "$0")/.."
VERSION=$(grep -oP '(?<=<Version>)[^<]+' src/SteuerPilot.Gui/SteuerPilot.Gui.csproj)
OUT=artifacts/release
rm -rf "$OUT" && mkdir -p "$OUT"
dotnet test
dotnet publish src/SteuerPilot.Gui -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true \
  -p:DebugType=none -o "$OUT/publish"
cp "$OUT/publish/SteuerPilot.exe" "$OUT/"
(cd "$OUT" && sha256sum SteuerPilot.exe > SteuerPilot.exe.sha256 && zip -q -9 SteuerPilot.zip SteuerPilot.exe)
gh release create "v$VERSION" --title "SteuerPilot $VERSION" --notes "${1:-}" \
  "$OUT/SteuerPilot.exe" "$OUT/SteuerPilot.exe.sha256" "$OUT/SteuerPilot.zip"
