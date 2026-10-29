#!/bin/bash
# Fast compile check of every script without Unity, one assembly at a time like Unity does, so a
# reference across the wrong boundary fails here too:
#   Epacenur.Core     (Assets/Scripts/Core)     no Unity at all
#   Epacenur.Content  (Assets/Scripts/Content)  no Unity; uses Core
#   Epacenur.Runtime  (Assets/Scripts/Runtime)  Unity (stubbed in UnityStubs.cs) + Core + Content
#   Epacenur.Editor   (Assets/Editor)           all of the above
# When the game starts using a Unity member the stubs lack, add it to UnityStubs.cs.
# Needs mono: apt-get install mono-mcs
cd "$(dirname "$0")/../.."
tmp=/tmp/epacenur-check; mkdir -p $tmp
flags="-langversion:7.2 -nowarn:414,169,649,219 -r:System.Numerics -target:library"
build() {  # build <name> <refs> <files...>
    local name=$1 refs=$2; shift 2
    local out
    out=$(mcs $flags $refs -out:$tmp/$name.dll "$@" 2>&1)
    local status=$?
    local errors=$(echo "$out" | grep "error")
    if [ $status -ne 0 ] || [ -n "$errors" ]; then echo "$name:"; echo "${errors:-$out}"; exit 1; fi
}
files() { find "$@" -name '*.cs' 2>/dev/null; }
build Epacenur.Core "" $(files Assets/Scripts/Core)
content=$(files Assets/Scripts/Content)
refs="-r:$tmp/Epacenur.Core.dll"
if [ -n "$content" ]; then build Epacenur.Content "$refs" $content; refs="$refs -r:$tmp/Epacenur.Content.dll"; fi
build UnityStubs "" Tools/CompileCheck/UnityStubs.cs
build Epacenur.Runtime "$refs -r:$tmp/UnityStubs.dll" $(files Assets/Scripts/Runtime)
build Epacenur.Editor "$refs -r:$tmp/UnityStubs.dll -r:$tmp/Epacenur.Runtime.dll" $(files Assets/Editor)

# The stubs put every Unity type in one assembly, but in Unity some types live in modules that must be in
# Packages/manifest.json, or Unity fails with CS1069. Check the ones the game uses.
need() {  # need <module> <regex of types from it>
    if grep -rqE "$2" Assets/Scripts --include=*.cs && ! grep -q "\"$1\"" Packages/manifest.json; then
        echo "FAILED: the scripts use $1 (matched '$2') but Packages/manifest.json doesn't include it."; exit 1
    fi
}
need com.unity.modules.audio 'Audio(Listener|Source|Clip)'
need com.unity.modules.physics 'Physics\.|Collider|CharacterController|RaycastHit'
need com.unity.modules.imgui 'GUI(Layout|Style)?\.|OnGUI'
need com.unity.modules.jsonserialize 'JsonUtility'
echo "OK: compiles"
