#!/usr/bin/env bash
# Build Skylines Agent Bridge on macOS and install it into the CS1 mods folder.
# Requires Mono (brew install mono). Override GAME_DIR if CS1 is not in the default Steam library.
set -euo pipefail

repo="$(cd "$(dirname "$0")/.." && pwd)"
src="$repo/src"
out="$repo/bin"
game="${GAME_DIR:-$HOME/Library/Application Support/Steam/steamapps/common/Cities_Skylines}"
managed="${MANAGED_DIR:-$game/Cities.app/Contents/Resources/Data/Managed}"
mod_dir="$HOME/Library/Application Support/Colossal Order/Cities_Skylines/Addons/Mods/SkylinesAgentBridge"

if ! command -v mcs >/dev/null 2>&1; then
    echo "mcs was not found. Install Mono first: brew install mono" >&2
    exit 1
fi

if [ ! -f "$managed/ICities.dll" ]; then
    echo "Cities: Skylines managed DLLs were not found at $managed" >&2
    echo "Set GAME_DIR (or MANAGED_DIR) to your install location." >&2
    exit 1
fi

mkdir -p "$out"
target="$out/SkylinesAgentBridge.dll"

# Compile against the game's own .NET 3.5-era corlib so the DLL loads in CS1's Mono runtime.
mcs \
    -nologo \
    -target:library \
    -out:"$target" \
    -optimize+ \
    -define:TRACE \
    -nostdlib \
    -noconfig \
    -r:"$managed/mscorlib.dll" \
    -r:"$managed/System.dll" \
    -r:"$managed/System.Core.dll" \
    -r:"$managed/ICities.dll" \
    -r:"$managed/Assembly-CSharp.dll" \
    -r:"$managed/Assembly-CSharp-firstpass.dll" \
    -r:"$managed/ColossalManaged.dll" \
    -r:"$managed/UnityEngine.dll" \
    "$src"/*.cs

mkdir -p "$mod_dir"
cp -f "$target" "$mod_dir/SkylinesAgentBridge.dll"

echo "Built $target"
echo "Copied to $mod_dir"
