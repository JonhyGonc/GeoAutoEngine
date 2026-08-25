param(
    [Parameter(Mandatory = $true)]
    [string]$SourceDrawing,

    [Parameter(Mandatory = $true)]
    [string[]]$SourceCsv,

    [ValidateSet("ACAD", "C3D")]
    [string]$Product = "C3D",

    [string]$OutputDirectory,

    [switch]$RemoveExistingGeneratedEntities
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$coreConsole = Join-Path $env:ProgramFiles "Autodesk\AutoCAD 2026\accoreconsole.exe"
$plugin = Join-Path $repositoryRoot "artifacts\release\bundle\GeoAutoEngine.bundle\Contents\2026\GeoAutoEngine.Plugin.dll"
$source = (Resolve-Path -LiteralPath $SourceDrawing).Path
$csvs = @($SourceCsv | ForEach-Object { (Resolve-Path -LiteralPath $_).Path })
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repositoryRoot ("artifacts\real-dwg-layout\" + (Get-Date -Format "yyyyMMdd-HHmmss"))
}

$output = [IO.Path]::GetFullPath($OutputDirectory)
foreach ($required in @($coreConsole, $plugin, $source) + $csvs) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Required file not found: $required"
    }
}

$resolvedRoot = [IO.Path]::GetFullPath($repositoryRoot)
if (-not $output.StartsWith($resolvedRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputDirectory must remain inside the repository: $output"
}

function Get-ShortPath {
    param([Parameter(Mandatory = $true)][string]$Path)
    $short = (& $env:ComSpec /d /c "for %I in (`"$Path`") do @echo %~sI").Trim()
    if (-not (Test-Path -LiteralPath $short)) {
        throw "Could not resolve an ASCII-safe path: $Path"
    }

    return $short
}

function Read-Events {
    param([Parameter(Mandatory = $true)][string]$Path)
    $parsed = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    return @($parsed)
}

function Assert-ZeroInvariantMetrics {
    param([Parameter(Mandatory = $true)]$Statistics)
    foreach ($property in @(
        "OrderInvariantViolations",
        "RotationInvariantViolations",
        "SideInvariantViolations",
        "GroupIntegrityViolations",
        "UnnecessaryMoves")) {
        if ([int]$Statistics.$property -ne 0) {
            throw "$property must be zero, actual=$($Statistics.$property)."
        }
    }
}

function Invoke-Pass {
    param(
        [Parameter(Mandatory = $true)][int]$Pass,
        [Parameter(Mandatory = $true)][string]$Drawing,
        [Parameter(Mandatory = $true)][string]$PluginPath,
        [Parameter(Mandatory = $true)][bool]$RemoveExistingGeneratedEntities
    )

    $script = Join-Path $output "pass-$Pass.scr"
    $marker = Join-Path $output "pass-$Pass-events.json"
    $audit = Join-Path $output "pass-$Pass-reality-audit.json"
    $stdout = Join-Path $output "pass-$Pass-stdout.log"
    $commands = @(
        "FILEDIA", "0", "CMDECHO", "1", "TILEMODE", "1", "SECURELOAD", "0",
        "_.NETLOAD", ('"' + $PluginPath + '"')
    )
    if ($RemoveExistingGeneratedEntities) {
        $commands += "(progn (setq gae_ss (ssget `"_X`" '((-3 (`"GEOAUTOENGINE`"))))) (if gae_ss (command `"_.ERASE`" gae_ss `"`")) (princ))"
    }

    $commands += @("GEOAUTO3IMPORT", "GEOAUTO3VERIFY", "_.QSAVE", "_.QUIT", "_Y")
    $commands | Set-Content -LiteralPath $script -Encoding ascii
    $env:GEOAUTOENGINE_STAGE4_INPUTS = [string]::Join([IO.Path]::PathSeparator, $csvs)
    $env:GEOAUTOENGINE_STAGE4_SMOKE_MARKER = $marker
    $env:GEOAUTOENGINE_ANNOTATION_REALITY_AUDIT_PATH = $audit
    try {
        & $coreConsole /i $Drawing /s $script /product $Product /l en-US /p "<<Unnamed Profile>>" /safemode `
            /isolate ("real-layout-$Pass") $output *> $stdout
        if ($LASTEXITCODE -ne 0) {
            throw "AutoCAD Core Console pass $Pass failed with exit code $LASTEXITCODE. See $stdout"
        }
    }
    finally {
        Remove-Item Env:GEOAUTOENGINE_STAGE4_INPUTS -ErrorAction SilentlyContinue
        Remove-Item Env:GEOAUTOENGINE_STAGE4_SMOKE_MARKER -ErrorAction SilentlyContinue
        Remove-Item Env:GEOAUTOENGINE_ANNOTATION_REALITY_AUDIT_PATH -ErrorAction SilentlyContinue
    }

    foreach ($required in @($marker, $audit)) {
        if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
            throw "Pass $Pass did not create evidence: $required"
        }
    }

    $events = Read-Events $marker
    $import = @($events | Where-Object { $_.Command -eq "GEOAUTO3IMPORT" })[-1]
    $verify = @($events | Where-Object { $_.Command -eq "GEOAUTO3VERIFY" })[-1]
    if ($null -eq $import -or $null -eq $import.ApplicationResult -or -not $import.ApplicationResult.Succeeded) {
        throw "Pass $Pass import did not succeed. See $marker"
    }

    $reality = Get-Content -LiteralPath $audit -Raw | ConvertFrom-Json
    Assert-ZeroInvariantMetrics $reality.Metrics
    return [pscustomobject]@{
        Pass = $Pass
        Events = $events
        Import = $import
        Verify = $verify
        Reality = $reality
        AuditPath = $audit
        MarkerPath = $marker
    }
}

New-Item -ItemType Directory -Path $output -Force | Out-Null
$drawing = Join-Path $output "legacy-restored-real.dwg"
Copy-Item -LiteralPath $source -Destination $drawing
$sourceHashBefore = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant()
$pluginForScript = Get-ShortPath $plugin

$first = Invoke-Pass -Pass 1 -Drawing $drawing -PluginPath $pluginForScript `
    -RemoveExistingGeneratedEntities ([bool]$RemoveExistingGeneratedEntities)
$second = Invoke-Pass -Pass 2 -Drawing $drawing -PluginPath $pluginForScript -RemoveExistingGeneratedEntities $false
$sourceHashAfter = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant()
if ($sourceHashBefore -ne $sourceHashAfter) {
    throw "Source DWG hash changed."
}

$firstEntities = @($first.Verify.Drawing.Entities) | Sort-Object StableKey | ForEach-Object {
    [ordered]@{
        StableKey = $_.StableKey
        EntityType = $_.EntityType
        Position = $_.Position
        Scale = $_.Scale
        Rotation = $_.Rotation
        Radius = $_.Radius
        VertexCount = $_.VertexCount
    }
} | ConvertTo-Json -Depth 6 -Compress
$secondEntities = @($second.Verify.Drawing.Entities) | Sort-Object StableKey | ForEach-Object {
    [ordered]@{
        StableKey = $_.StableKey
        EntityType = $_.EntityType
        Position = $_.Position
        Scale = $_.Scale
        Rotation = $_.Rotation
        Radius = $_.Radius
        VertexCount = $_.VertexCount
    }
} | ConvertTo-Json -Depth 6 -Compress
if ($firstEntities -ne $secondEntities) {
    throw "Generated entity geometry changed between pass 1 and pass 2."
}

$summary = [ordered]@{
    SchemaVersion = 1
    SourceDrawing = $source
    SourceSha256 = $sourceHashBefore
    SourceHashUnchanged = $true
    SourceCsv = $csvs
    Product = $Product
    OutputDrawing = $drawing
    Pass1 = [ordered]@{
        ApplicationResult = $first.Import.ApplicationResult
        Statistics = $first.Reality.Metrics
        AnnotationCount = @($first.Reality.Annotations).Count
    }
    Pass2 = [ordered]@{
        ApplicationResult = $second.Import.ApplicationResult
        Statistics = $second.Reality.Metrics
        AnnotationCount = @($second.Reality.Annotations).Count
    }
    GeneratedGeometryStableAcrossPasses = $true
}
$summaryPath = Join-Path $output "summary.json"
$summary | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $summaryPath -Encoding utf8
foreach ($pass in @("Pass1", "Pass2")) {
    $metrics = $summary[$pass].Statistics
    Write-Output (
        "${pass}: annotations=$($metrics.AnnotationCount) existing=$($metrics.KeptExistingCount) " +
        "legacy=$($metrics.KeptLegacyCount) corrected=$($metrics.CorrectedCount) " +
        "review=$($metrics.UnresolvedCount) order=$($metrics.OrderInvariantViolations) " +
        "rotation=$($metrics.RotationInvariantViolations) side=$($metrics.SideInvariantViolations) " +
        "group=$($metrics.GroupIntegrityViolations) unnecessary=$($metrics.UnnecessaryMoves)")
}
Write-Output "Generated geometry stable across passes: true"
Write-Output "Evidence: $summaryPath"
