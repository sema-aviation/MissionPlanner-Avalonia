#!/usr/bin/env bash
set -euo pipefail

# Wrap a `dotnet publish` output directory into a macOS .app bundle.
# Usage: make-app.sh <publish-dir> <version> <output-app-path>

PUBLISH_DIR="$1"
VERSION="$2"
APP="$3"
HERE="$(cd "$(dirname "$0")" && pwd)"
EXE="MissionPlannerAvalonia"

rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"

cp -R "$PUBLISH_DIR"/. "$APP/Contents/MacOS/"
cp "$HERE/AppIcon.icns" "$APP/Contents/Resources/AppIcon.icns"
sed "s/__VERSION__/$VERSION/g" "$HERE/Info.plist" > "$APP/Contents/Info.plist"
chmod +x "$APP/Contents/MacOS/$EXE"

if command -v codesign >/dev/null 2>&1; then
  codesign --remove-signature "$APP/Contents/MacOS/$EXE" 2>/dev/null || true
  codesign --force --deep --sign - --identifier com.semaaviation.missionplanneravalonia "$APP"
fi

echo "Built $APP"
