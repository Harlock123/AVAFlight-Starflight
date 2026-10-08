#!/usr/bin/env bash
# Regenerates docs/screenshots/*.png with Avalonia's headless Skia renderer.
set -euo pipefail
cd "$(dirname "$0")/.."
AVAFLIGHT_SCREENSHOTS="$PWD/docs/screenshots" dotnet test tests/AVAFlight.Tests --filter "FullyQualifiedName~ScreenshotTests"
ls -1 docs/screenshots
