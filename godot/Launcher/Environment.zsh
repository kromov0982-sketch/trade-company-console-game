# Shared discovery for Finder launchers. Override GODOT_BIN for a custom Godot install.
export PATH="/opt/homebrew/bin:/usr/local/bin:$PATH"
if [[ -z "${DOTNET_ROOT:-}" ]]; then
  for candidate in "$HOME/Library/dotnet" "$HOME/.dotnet" /usr/local/share/dotnet /opt/homebrew/opt/dotnet/libexec; do
    if [[ -x "$candidate/dotnet" ]]; then export DOTNET_ROOT="$candidate"; break; fi
  done
fi
[[ -n "${DOTNET_ROOT:-}" ]] && export PATH="$DOTNET_ROOT:$PATH"
if [[ -z "${GODOT_BIN:-}" ]]; then
  for candidate in "$HOME/Applications/Godot_mono.app/Contents/MacOS/Godot" /Applications/Godot_mono.app/Contents/MacOS/Godot /Applications/Godot.app/Contents/MacOS/Godot; do
    if [[ -x "$candidate" ]]; then export GODOT_BIN="$candidate"; break; fi
  done
fi
if [[ -z "${GODOT_BIN:-}" ]] && command -v godot-mono >/dev/null; then export GODOT_BIN="$(command -v godot-mono)"; fi
if ! command -v dotnet >/dev/null || [[ ! -x "${GODOT_BIN:-}" ]]; then
  print -u2 'Install .NET 10 SDK and Godot .NET 4.7.2. Set GODOT_BIN if Godot is installed elsewhere.'
  exit 1
fi
