#!/bin/bash
# Fast compile check of every script without Unity (Unity's API is stubbed in UnityStubs.cs).
# Needs mono: apt-get install mono-mcs
cd "$(dirname "$0")/../.."
out=$(mcs -langversion:7.2 -nowarn:414,169,649,219 -r:System.Numerics -target:library -out:/tmp/epacenur-check.dll \
    Tools/CompileCheck/UnityStubs.cs Assets/Scripts/Core/*.cs Assets/Scripts/Runtime/*.cs Assets/Editor/*.cs 2>&1)
status=$?
errors=$(echo "$out" | grep "error")
if [ $status -ne 0 ] || [ -n "$errors" ]; then echo "${errors:-$out}"; exit 1; fi
# The stubs put every type in one assembly, but in Unity some types live in modules that must be in
# Packages/manifest.json, or Unity fails with CS1069. Check the ones the game uses.
need() {  # need <module> <regex of types from it>
    if grep -qE "$2" Assets/Scripts/*/*.cs && ! grep -q "\"$1\"" Packages/manifest.json; then
        echo "FAILED: the scripts use $1 (matched '$2') but Packages/manifest.json doesn't include it."; exit 1
    fi
}
need com.unity.modules.audio 'Audio(Listener|Source|Clip)'
need com.unity.modules.physics 'Physics\.|Collider|CharacterController|RaycastHit'
need com.unity.modules.imgui 'GUI(Layout|Style)?\.|OnGUI'
need com.unity.modules.jsonserialize 'JsonUtility'
echo "OK: compiles"
