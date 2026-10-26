#!/bin/bash
# Builds BAM.Sim with the tests and runs them; exit code 1 on a failed check.
set -e
cd "$(dirname "$0")"
OUT=${TMPDIR:-/tmp}/bam-simtests
dotnet build -nologo -v q -o "$OUT" simtests.csproj >/dev/null
dotnet "$OUT/simtests.dll"
