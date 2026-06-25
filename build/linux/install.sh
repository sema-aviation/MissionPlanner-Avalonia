#!/usr/bin/env bash
set -euo pipefail

DIR="$(cd "$(dirname "$0")" && pwd)"
APPS="$HOME/.local/share/applications"

mkdir -p "$APPS"
sed "s|@INSTALL_DIR@|$DIR|g" "$DIR/missionplanner-avalonia.desktop" \
  > "$APPS/missionplanner-avalonia.desktop"
chmod +x "$DIR/MissionPlannerAvalonia"
update-desktop-database "$APPS" 2>/dev/null || true

echo "Installed. Find 'Mission Planner' in your launcher (or run ./MissionPlannerAvalonia)."
echo
echo "Serial access to a flight controller needs the dialout group:"
echo "  sudo usermod -aG dialout \$USER   # then log out and back in"
