param(
    [ValidateSet("Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
& (Join-Path $PSScriptRoot "build-release.ps1") -Configuration $Configuration
exit $LASTEXITCODE
