#!/usr/bin/env bash
set -euo pipefail

# Profiles MkvRenameWizard.Cli using the CoreCLR 'Profile' configuration
# (NativeAOT strips EventPipe, so Profile uses CoreCLR + Release optimizations).
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CLI_PROJ="$ROOT_DIR/MkvRenameWizard/MkvRenameWizard.Cli/MkvRenameWizard.Cli.csproj"
TEST_MEDIA="$ROOT_DIR/Tests/Integration/TestMedia"
OUT_DIR="${1:-/tmp/opencode/mkv-rename-profile}"

mkdir -p "$OUT_DIR"

echo "Building MkvRenameWizard.Cli (Profile / CoreCLR)..."
dotnet build "$CLI_PROJ" -c Profile

CLI_DLL="$ROOT_DIR/MkvRenameWizard/MkvRenameWizard.Cli/bin/Profile/net10.0/mkv-rename-cli.dll"

echo "Collecting CPU trace into $OUT_DIR/scan.nettrace ..."
dotnet-trace collect \
  --profile cpu-sampling \
  --output "$OUT_DIR/scan.nettrace" \
  -- dotnet "$CLI_DLL" scan "$TEST_MEDIA" --json > /dev/null

echo "Trace saved to: $OUT_DIR/scan.nettrace"
echo "To capture a GC heap dump of a live stdio session:"
echo "  dotnet \"$CLI_DLL\" serve --stdio & PID=\$!; dotnet-gcdump collect -p \$PID -o \"$OUT_DIR/stdio.gcdump\"; kill \$PID"
