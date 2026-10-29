#!/bin/bash
# BAM Precision Test Suite: builds BAM.Sim + the lockstep with the precision tests and runs them.
# Prints one line per test (PASS / FAIL P0-P3 / INFO) and writes the same as Markdown to $1 (default: $TMPDIR/bam-precision/precision-report.md).
# Exit code 0 even with FAILs (they are findings for review); pass --strict as $2 to exit 1 on any FAIL.
set -e
cd "$(dirname "$0")"
OUT=${TMPDIR:-/tmp}/bam-precision
dotnet build -nologo -v q -o "$OUT" precision.csproj >/dev/null
dotnet "$OUT/precision.dll" "${1:-$OUT/precision-report.md}" "$2"
