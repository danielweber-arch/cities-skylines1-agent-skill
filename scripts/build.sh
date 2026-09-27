#!/usr/bin/env bash
#
# Build SkylinesAgentBridge.dll with Mono and install it into the local
# Cities: Skylines 1 mod folder on macOS.
#
# Overrides (all optional):
#   MCS=/path/to/mcs            explicit Mono C# compiler
#   CS1_GAME_DIR=/path/to/dir   Steam "common/Cities_Skylines" folder, or Cities.app itself
#   CS1_MANAGED=/path/to/dir    the Managed folder directly; skips all detection
#
set -euo pipefail

repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
src="$repo/src"
out="$repo/bin"
target="$out/SkylinesAgentBridge.dll"
mod_dir="$HOME/Library/Application Support/Colossal Order/Cities_Skylines/Addons/Mods/SkylinesAgentBridge"

die() {
    echo "error: $*" >&2
    exit 1
}

# --- Mono C# compiler -------------------------------------------------------

find_mcs() {
    if [ -n "${MCS:-}" ]; then
        [ -x "$MCS" ] || die "MCS is set to '$MCS' but that is not executable."
        printf '%s' "$MCS"
        return
    fi

    local candidate
    for candidate in \
        "$(command -v mcs 2>/dev/null || true)" \
        "/Library/Frameworks/Mono.framework/Versions/Current/bin/mcs" \
        "/opt/homebrew/bin/mcs" \
        "/usr/local/bin/mcs"
    do
        if [ -n "$candidate" ] && [ -x "$candidate" ]; then
            printf '%s' "$candidate"
            return
        fi
    done

    die "mcs was not found. Install Mono with 'brew install mono', or set MCS=/path/to/mcs."
}

# --- Cities: Skylines managed assemblies ------------------------------------

# Every extra Steam library root the user has configured, one per line.
steam_library_roots() {
    local vdf="$HOME/Library/Application Support/Steam/steamapps/libraryfolders.vdf"
    echo "$HOME/Library/Application Support/Steam"
    [ -f "$vdf" ] || return 0
    sed -n 's/^[[:space:]]*"path"[[:space:]]*"\(.*\)"[[:space:]]*$/\1/p' "$vdf"
}

# Every place a Managed folder could plausibly live, one per line.
managed_candidates() {
    if [ -n "${CS1_GAME_DIR:-}" ]; then
        # Accept either the install folder or the app bundle itself.
        echo "$CS1_GAME_DIR/Cities.app/Contents/Resources/Data/Managed"
        echo "$CS1_GAME_DIR/Contents/Resources/Data/Managed"
        return 0
    fi

    local root
    while IFS= read -r root; do
        [ -n "$root" ] || continue
        echo "$root/steamapps/common/Cities_Skylines/Cities.app/Contents/Resources/Data/Managed"
    done < <(steam_library_roots)

    echo "/Applications/Cities_Skylines/Cities.app/Contents/Resources/Data/Managed"
    echo "/Applications/Cities.app/Contents/Resources/Data/Managed"
}

find_managed() {
    if [ -n "${CS1_MANAGED:-}" ]; then
        [ -f "$CS1_MANAGED/ICities.dll" ] || die "CS1_MANAGED is set to '$CS1_MANAGED' but ICities.dll is not there."
        printf '%s' "$CS1_MANAGED"
        return
    fi

    local candidate
    while IFS= read -r candidate; do
        if [ -f "$candidate/ICities.dll" ]; then
            printf '%s' "$candidate"
            return
        fi
    done < <(managed_candidates)

    {
        echo "error: the Cities: Skylines managed assemblies were not found."
        echo "Looked in:"
        managed_candidates | sed 's/^/  /'
        echo "Set CS1_MANAGED to the folder containing ICities.dll, or CS1_GAME_DIR to the install folder."
    } >&2
    exit 1
}

mcs="$(find_mcs)"
managed="$(find_managed)"

for assembly in mscorlib.dll System.dll System.Core.dll ICities.dll \
                Assembly-CSharp.dll Assembly-CSharp-firstpass.dll \
                ColossalManaged.dll UnityEngine.dll
do
    [ -f "$managed/$assembly" ] || die "$assembly is missing from $managed"
done

# --- Compile ----------------------------------------------------------------

mkdir -p "$out"

# -nostdlib plus explicit references pin the build to the game's own .NET 3.5
# assemblies. Without it mcs silently links against the host Mono's 4.x BCL and
# the resulting DLL fails to load inside Cities: Skylines.
# -langversion:3 matches the .NET 3.5 csc the Windows build used.
"$mcs" \
    -nostdlib \
    -noconfig \
    -target:library \
    -langversion:3 \
    -optimize+ \
    -define:TRACE \
    -out:"$target" \
    -lib:"$managed" \
    -r:mscorlib.dll \
    -r:System.dll \
    -r:System.Core.dll \
    -r:System.Xml.dll \
    -r:ICities.dll \
    -r:Assembly-CSharp.dll \
    -r:Assembly-CSharp-firstpass.dll \
    -r:ColossalManaged.dll \
    -r:UnityEngine.dll \
    "$src"/*.cs

# --- Install ----------------------------------------------------------------

mkdir -p "$mod_dir"
cp -f "$target" "$mod_dir/SkylinesAgentBridge.dll"

echo "Managed assemblies: $managed"
echo "Compiler:           $mcs"
echo "Built    $target"
echo "Copied to $mod_dir"
echo
echo "Enable 'Skylines Agent Bridge' in the CS1 content manager, load a city, then:"
echo "  curl http://127.0.0.1:32123/health"
