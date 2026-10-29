#!/bin/bash
# Runs the rules suite (Assets/Tests/EditMode/CoreRulesSuite.cs) outside Unity, against the Core and Content
# assemblies, which don't use Unity. Needs mono: apt-get install mono-mcs mono-runtime
cd "$(dirname "$0")/../.."
out=/tmp/epacenur-tests.exe
src=$(find Assets/Scripts/Core Assets/Scripts/Content -name '*.cs' 2>/dev/null)
mcs -langversion:7.2 -r:System.Numerics -out:$out Tools/Tests/Program.cs Assets/Tests/EditMode/CoreRulesSuite.cs $src || exit 1
mono $out
