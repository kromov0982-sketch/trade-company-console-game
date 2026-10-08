#!/bin/zsh
set -e
cd "${0:A:h}"
source Launcher/Environment.zsh
dotnet build TradeCompanyGodot.csproj --nologo
exec "$GODOT_BIN" --path "$PWD"
