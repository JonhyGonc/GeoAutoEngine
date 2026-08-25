$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$acad = Join-Path $env:ProgramFiles "Autodesk\AutoCAD 2026\acad.exe"
$drawingSource = Join-Path $env:ProgramFiles "Autodesk\AutoCAD 2026\C3D\PushPins.dwg"
$csv = Join-Path $root "samples\stage5_curve_types.csv"
$bundle = Join-Path $env:APPDATA "Autodesk\ApplicationPlugins\GeoAutoEngine.bundle"
foreach ($path in @($acad, $drawingSource, $csv, $bundle)) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Required path missing: $path" }
}

$temporaryDirectory = Join-Path ([IO.Path]::GetTempPath()) ("geoautoengine-curves-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $temporaryDirectory | Out-Null
try {
    $drawing = Join-Path $temporaryDirectory "curve-types.dwg"
    $script = Join-Path $temporaryDirectory "curve-types.scr"
    $marker = Join-Path $temporaryDirectory "curve-types.json"
    $stdout = Join-Path $temporaryDirectory "stdout.log"
    $stderr = Join-Path $temporaryDirectory "stderr.log"
    Copy-Item -LiteralPath $drawingSource -Destination $drawing
    @(
        "FILEDIA", "0", "CMDECHO", "1",
        "_.-LAYER", "_M", "REDE_ESGOTO", "",
        "_.LINE", "2000,2000", "2010,2000", "",
        "PLINETYPE", "1", "_.PLINE", "2020,2000", "2030,2010", "",
        "PLINETYPE", "0", "_.PLINE", "2040,2000", "2050,2010", "",
        "_.3DPOLY", "2060,2000,0", "2070,2010,10", "",
        "_.ARC", "_C", "2080,2000", "2090,2000", "2080,2010",
        "_.SPLINE", "2100,2000", "2105,2005", "2110,2000", "", "", "",
        "GEOAUTO3IMPORT", "GEOAUTO3VERIFY", "_.QUIT", "_N"
    ) | Set-Content -LiteralPath $script -Encoding ascii

    $env:GEOAUTOENGINE_STAGE4_INPUTS = $csv
    $env:GEOAUTOENGINE_STAGE4_SMOKE_MARKER = $marker
    $process = Start-Process -FilePath $acad `
        -ArgumentList @($drawing, "/b", $script, "/product", "C3D", "/language", "en-US", "/nologo") `
        -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr -PassThru
    if (-not $process.WaitForExit(180000)) {
        $process.Kill($true)
        throw "Curve type smoke timed out."
    }
    $process.WaitForExit()
    $process.Refresh()
    if ([int]$process.ExitCode -ne 0) { throw "Civil 3D returned $($process.ExitCode)." }
    if (-not (Test-Path -LiteralPath $marker)) { throw "Curve type evidence was not created." }

    $parsed = Get-Content -LiteralPath $marker -Raw | ConvertFrom-Json
    $events = @()
    foreach ($event in $parsed) { $events += $event }
    $result = $events[0].ApplicationResult
    $drawingState = $events[1].Drawing
    if (-not $result.Succeeded -or $result.CreatedItemCount -ne 12 -or $result.PendingItemCount -ne 0) {
        throw "Not all curve-type texts were applied."
    }
    $texts = @($drawingState.Entities | Where-Object EntityType -eq "DBText")
    if ($texts.Count -ne 12) { throw "Unexpected text count: $($texts.Count)." }
    $expected = @{
        "2" = 0.0
        "3" = [Math]::PI / 4
        "4" = [Math]::PI / 4
        "5" = [Math]::PI / 4
        "6" = -[Math]::PI / 4
        "7" = 0.0
    }
    foreach ($line in $expected.Keys) {
        $rotations = @($texts | Where-Object { $_.XDataValues.SOURCE_LINE -eq $line } | Select-Object -ExpandProperty Rotation)
        if ($rotations.Count -ne 2) { throw "Missing text pair for source line $line." }
        foreach ($rotation in $rotations) {
            $difference = [Math]::Abs([Math]::Atan2([Math]::Sin([double]$rotation - $expected[$line]), [Math]::Cos([double]$rotation - $expected[$line])))
            if ($difference -gt 0.02) { throw "Rotation mismatch on source line ${line}: $rotation." }
        }
    }
    Write-Output "Stage 5 curve smoke passed: Line, Polyline, Polyline2d, Polyline3d, Arc and Spline."
}
finally {
    Remove-Item Env:GEOAUTOENGINE_STAGE4_INPUTS -ErrorAction SilentlyContinue
    Remove-Item Env:GEOAUTOENGINE_STAGE4_SMOKE_MARKER -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $temporaryDirectory) {
        Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force
    }
}
