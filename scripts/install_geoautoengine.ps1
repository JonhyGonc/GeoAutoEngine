param(
    [ValidateSet("2024", "2025", "2026")]
    [string]$Version = "2026",

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

# Compatibility entry point: installation is deliberately scoped to one host.
& (Join-Path $PSScriptRoot "deploy.ps1") `
    -Version $Version `
    -Configuration $Configuration `
    -SkipBuild:$SkipBuild

exit $LASTEXITCODE
