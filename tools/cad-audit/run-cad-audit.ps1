param(
    [Parameter(Mandatory = $true)]
    [string]$SourceDrawing,

    [Parameter(Mandatory = $true)]
    [string]$OutputJson,

    [ValidateSet("ACAD", "C3D")]
    [string]$Product = "C3D"
)

$ErrorActionPreference = "Stop"
$source = (Resolve-Path -LiteralPath $SourceDrawing).Path
$output = [System.IO.Path]::GetFullPath($OutputJson)
$coreConsole = Join-Path $env:ProgramFiles "Autodesk\AutoCAD 2026\accoreconsole.exe"
$plugin = Join-Path $PSScriptRoot "bin\Release\net8.0-windows\GeoAutoEngine.CadAudit.dll"

foreach ($required in @($source, $coreConsole, $plugin)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Required file not found: $required"
    }
}

$pluginForScript = (& $env:ComSpec /d /c "for %I in (`"$plugin`") do @echo %~sI").Trim()
if (-not (Test-Path -LiteralPath $pluginForScript -PathType Leaf)) {
    throw "Could not resolve an ASCII-safe plugin path: $pluginForScript"
}

$outputDirectory = Split-Path -Parent $output
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$temporaryDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ("geoauto-cad-audit-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $temporaryDirectory | Out-Null

try {
    $drawingCopy = Join-Path $temporaryDirectory ([System.IO.Path]::GetFileName($source))
    $script = Join-Path $temporaryDirectory "audit.scr"
    $isolateDirectory = Join-Path $temporaryDirectory "isolate"
    New-Item -ItemType Directory -Path $isolateDirectory | Out-Null
    Copy-Item -LiteralPath $source -Destination $drawingCopy
    $sourceHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $source).Hash
    $copyHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $drawingCopy).Hash
    if ($sourceHash -ne $copyHash) {
        throw "Temporary copy hash differs from source."
    }

    @(
        "FILEDIA"
        "0"
        "CMDECHO"
        "1"
        "_.NETLOAD"
        ('"' + $pluginForScript + '"')
        "GEOAUTOCADAUDIT"
        "_.QUIT"
        "_Y"
    ) | Set-Content -LiteralPath $script -Encoding ascii

    $env:GEOAUTO_CAD_AUDIT_OUTPUT = $output
    $env:GEOAUTO_CAD_AUDIT_SOURCE = $source
    $env:GEOAUTO_CAD_AUDIT_SHA256 = $sourceHash
    & $coreConsole /i $drawingCopy /s $script /product $Product /l en-US /readonly /isolate ("cad-audit-" + [guid]::NewGuid().ToString("N")) $isolateDirectory
    $exitCode = $LASTEXITCODE
    Remove-Item Env:GEOAUTO_CAD_AUDIT_OUTPUT -ErrorAction SilentlyContinue
    Remove-Item Env:GEOAUTO_CAD_AUDIT_SOURCE -ErrorAction SilentlyContinue
    Remove-Item Env:GEOAUTO_CAD_AUDIT_SHA256 -ErrorAction SilentlyContinue

    if ($exitCode -ne 0) {
        throw "AutoCAD Core Console failed with exit code $exitCode."
    }
    if (-not (Test-Path -LiteralPath $output -PathType Leaf)) {
        throw "Audit output was not created: $output"
    }
    $afterHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $source).Hash
    if ($afterHash -ne $sourceHash) {
        throw "SOURCE DRAWING HASH CHANGED: $source"
    }
    [pscustomobject]@{
        Source = $source
        SourceSha256 = $sourceHash
        SourceHashUnchanged = $true
        Output = $output
        OutputBytes = (Get-Item -LiteralPath $output).Length
    } | ConvertTo-Json -Depth 3
}
finally {
    Remove-Item Env:GEOAUTO_CAD_AUDIT_OUTPUT -ErrorAction SilentlyContinue
    Remove-Item Env:GEOAUTO_CAD_AUDIT_SOURCE -ErrorAction SilentlyContinue
    Remove-Item Env:GEOAUTO_CAD_AUDIT_SHA256 -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $temporaryDirectory) {
        $resolvedTemporaryDirectory = [System.IO.Path]::GetFullPath($temporaryDirectory)
        $resolvedTemporaryRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
        if (-not $resolvedTemporaryDirectory.StartsWith($resolvedTemporaryRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to delete unexpected temporary path: $resolvedTemporaryDirectory"
        }
        for ($attempt = 0; $attempt -lt 10; $attempt++) {
            try {
                [System.IO.Directory]::Delete($resolvedTemporaryDirectory, $true)
                break
            }
            catch [System.IO.IOException] {
                if ($attempt -eq 9) {
                    Write-Warning "Temporary audit directory is still locked and could not be removed: $resolvedTemporaryDirectory"
                    break
                }
                Start-Sleep -Milliseconds 250
            }
        }
    }
}
