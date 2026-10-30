#!/bin/bash
# Animation Foundation 1.0 tests: builds BAM.Sim + Art/Anim/AnimTiming.cs with the tests and runs them.
# One line per test (PASS / FAIL / INFO); exit code 1 on any FAIL.
set -e
cd "$(dirname "$0")"
OUT=${TMPDIR:-/tmp}/bam-animtests
dotnet build -nologo -v q -o "$OUT" anim.csproj >/dev/null
dotnet "$OUT/anim.dll" "$@"
