#!/usr/bin/env bash
# Publishes AVAFlight as self-contained single-file executables.
# Usage: ./publish.sh [rid ...]     (default: win-x64 linux-x64 osx-x64 osx-arm64)
# Output: publish/<rid>/AVAFlight[.exe]
set -euo pipefail
cd "$(dirname "$0")"
RIDS=("$@")
if [ ${#RIDS[@]} -eq 0 ]; then RIDS=(win-x64 linux-x64 osx-x64 osx-arm64); fi
for rid in "${RIDS[@]}"; do
  echo "==> Publishing $rid"
  rm -rf "publish/$rid"
  dotnet publish src/AVAFlight.Avalonia/AVAFlight.Avalonia.csproj -c Release -r "$rid" \
    --self-contained -p:PublishSingleFile=true -o "publish/$rid" -nologo -v:q
  ls -la "publish/$rid"
done
echo "Done. Verify each binary with:  <binary> --smoke-test   (prints AVAFLIGHT_SMOKE_OK and exits 0)"
