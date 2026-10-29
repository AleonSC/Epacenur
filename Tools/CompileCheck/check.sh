#!/bin/bash
# Fast compile check of every script without Unity (Unity's API is stubbed in UnityStubs.cs).
# Needs mono: apt-get install mono-mcs
cd "$(dirname "$0")/../.."
out=$(mcs -langversion:7.2 -nowarn:414,169,649,219 -r:System.Numerics -target:library -out:/tmp/epacenur-check.dll \
    Tools/CompileCheck/UnityStubs.cs Assets/Scripts/Core/*.cs Assets/Scripts/Runtime/*.cs Assets/Editor/*.cs 2>&1)
status=$?
errors=$(echo "$out" | grep "error")
if [ $status -ne 0 ] || [ -n "$errors" ]; then echo "${errors:-$out}"; exit 1; fi
echo "OK: compiles"
