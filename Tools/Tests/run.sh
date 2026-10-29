#!/bin/bash
# Runs the rule tests (Assets/Scripts/Core needs no Unity). Needs mono: apt-get install mono-mcs mono-runtime
cd "$(dirname "$0")/../.."
out=/tmp/epacenur-tests.exe
mcs -langversion:7.2 -r:System.Numerics -out:$out Tools/Tests/CoreTests.cs Assets/Scripts/Core/*.cs || exit 1
mono $out
