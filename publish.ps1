# Publishes AVAFlight as self-contained single-file executables.
# Usage: ./publish.ps1 [-Rids win-x64,win-arm64,linux-x64,linux-arm64,osx-x64,osx-arm64]
param([string[]]$Rids = @("win-x64", "win-arm64", "linux-x64", "linux-arm64", "osx-x64", "osx-arm64"))
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot
foreach ($rid in $Rids) {
    Write-Host "==> Publishing $rid"
    if (Test-Path "publish/$rid") { Remove-Item -Recurse -Force "publish/$rid" }
    dotnet publish src/AVAFlight.Avalonia/AVAFlight.Avalonia.csproj -c Release -r $rid `
        --self-contained -p:PublishSingleFile=true -o "publish/$rid" -nologo -v:q
    if ($LASTEXITCODE -ne 0) { throw "publish failed for $rid" }
    Get-ChildItem "publish/$rid"
}
Write-Host "Done. Verify each binary with: <binary> --smoke-test  (prints AVAFLIGHT_SMOKE_OK and exits 0)"
