#!/bin/zsh
set -e
cd "${0:A:h}"
source Launcher/Environment.zsh
exec "$GODOT_BIN" --editor --path "$PWD"
