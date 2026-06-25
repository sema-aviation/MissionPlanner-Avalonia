#!/usr/bin/env bash
set -euo pipefail

# Regenerate every icon asset from the single vector source build/icon/AppIcon.svg.
# Needs: rsvg-convert, ImageMagick (magick), iconutil (macOS).

HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../.." && pwd)"
SVG="$HERE/AppIcon.svg"
TMP="$(mktemp -d)"

for s in 16 32 48 64 128 256 512 1024; do
  rsvg-convert -w "$s" -h "$s" "$SVG" -o "$TMP/icon_$s.png"
done

cp "$TMP/icon_1024.png" "$HERE/AppIcon.png"
cp "$TMP/icon_256.png"  "$ROOT/build/linux/missionplanner-avalonia.png"
magick "$TMP/icon_16.png" "$TMP/icon_32.png" "$TMP/icon_48.png" "$TMP/icon_64.png" \
       "$TMP/icon_128.png" "$TMP/icon_256.png" "$ROOT/src/MissionPlannerAvalonia/Assets/appicon.ico"

ICONSET="$TMP/AppIcon.iconset"; mkdir -p "$ICONSET"
cp "$TMP/icon_16.png" "$ICONSET/icon_16x16.png";    cp "$TMP/icon_32.png"  "$ICONSET/icon_16x16@2x.png"
cp "$TMP/icon_32.png" "$ICONSET/icon_32x32.png";    cp "$TMP/icon_64.png"  "$ICONSET/icon_32x32@2x.png"
cp "$TMP/icon_128.png" "$ICONSET/icon_128x128.png"; cp "$TMP/icon_256.png" "$ICONSET/icon_128x128@2x.png"
cp "$TMP/icon_256.png" "$ICONSET/icon_256x256.png"; cp "$TMP/icon_512.png" "$ICONSET/icon_256x256@2x.png"
cp "$TMP/icon_512.png" "$ICONSET/icon_512x512.png"; cp "$TMP/icon_1024.png" "$ICONSET/icon_512x512@2x.png"
iconutil -c icns "$ICONSET" -o "$ROOT/build/macos/AppIcon.icns"

rm -rf "$TMP"
echo "Regenerated ICO / ICNS / PNGs from AppIcon.svg"
