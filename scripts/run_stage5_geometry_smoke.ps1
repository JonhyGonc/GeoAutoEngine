param(
    [ValidateSet("ACAD", "C3D")]
    [string]$Product = "C3D"
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$acad = Join-Path $env:ProgramFiles "Autodesk\AutoCAD 2026\acad.exe"
$sourceDrawing = Join-Path $env:ProgramFiles "Autodesk\AutoCAD 2026\C3D\PushPins.dwg"
$bundle = Join-Path $env:APPDATA "Autodesk\ApplicationPlugins\GeoAutoEngine.bundle"
$csvFiles = @(
    (Join-Path $root "samples\stage4_cad_smoke_day1.csv"),
    (Join-Path $root "samples\stage4_cad_smoke_day2.csv")
)
foreach ($path in @($acad, $sourceDrawing, $bundle) + $csvFiles) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Required path missing: $path" }
}

$temporaryDirectory = Join-Path ([IO.Path]::GetTempPath()) ("geoautoengine-stage5-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $temporaryDirectory | Out-Null
try {
    $drawing = Join-Path $temporaryDirectory "geometry-smoke.dwg"
    $script = Join-Path $temporaryDirectory "geometry-smoke.scr"
    $marker = Join-Path $temporaryDirectory "geometry-smoke.json"
    $stdout = Join-Path $temporaryDirectory "stdout.log"
    $stderr = Join-Path $temporaryDirectory "stderr.log"
    Copy-Item -LiteralPath $sourceDrawing -Destination $drawing
    @(
        "FILEDIA", "0", "CMDECHO", "1",
        "_.-LAYER", "_M", "REDE_ESGOTO", "",
        "_.LINE", "495,1002", "510,1002", "",
        "_.-LAYER", "_M", "AGUA", "",
        "_.LINE", "525,1030", "540,1030", "",
        "GEOAUTO3IMPORT", "GEOAUTO3IMPORT", "GEOAUTO3VERIFY",
        "_.QUIT", "_N"
    ) | Set-Content -LiteralPath $script -Encoding ascii

    $env:GEOAUTOENGINE_STAGE4_INPUTS = [string]::Join([IO.Path]::PathSeparator, $csvFiles)
    $env:GEOAUTOENGINE_STAGE4_SMOKE_MARKER = $marker
    $process = Start-Process -FilePath $acad `
        -ArgumentList @($drawing, "/b", $script, "/product", $Product, "/language", "en-US", "/nologo") `
        -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr -PassThru
    if (-not $process.WaitForExit(180000)) {
        $process.Kill($true)
        throw "Civil 3D geometry smoke timed out."
    }
    $process.WaitForExit()
    $process.Refresh()
    $exitCode = [int]$process.ExitCode
    if ($exitCode -ne 0) { throw "Civil 3D returned $exitCode." }
    if (-not (Test-Path -LiteralPath $marker)) { throw "Geometry smoke evidence was not created." }

    $parsedEvents = Get-Content -LiteralPath $marker -Raw | ConvertFrom-Json
    $events = @()
    foreach ($event in $parsedEvents) { $events += $event }
    $first = $events[0].ApplicationResult
    $second = $events[1].ApplicationResult
    $drawingState = $events[2].Drawing
    Write-Output "First: created=$($first.CreatedItemCount) skipped=$($first.SkippedExistingItemCount) pending=$($first.PendingItemCount)"
    Write-Output "Second: created=$($second.CreatedItemCount) skipped=$($second.SkippedExistingItemCount) pending=$($second.PendingItemCount)"
    if (-not $first.Succeeded -or $first.CreatedItemCount -ne 7 -or $first.PendingItemCount -ne 0) {
        throw "First geometry application did not create all seven entities."
    }
    if (-not $second.Succeeded -or $second.CreatedItemCount -ne 0 -or $second.SkippedExistingItemCount -ne 7) {
        throw "Second geometry application was not idempotent."
    }
    if ($drawingState.GeneratedEntityCount -ne 7) { throw "Unexpected generated entity count." }
    $networkTexts = @($drawingState.Entities | Where-Object { $_.EntityType -eq "DBText" -and $_.Layer -eq "GF-Rede Esgoto" })
    $waterBlock = @($drawingState.Entities | Where-Object { $_.EntityType -eq "BlockReference" -and $_.BlockName -eq "rg_agua_potavel" })
    if ($networkTexts.Count -ne 2 -or $waterBlock.Count -ne 1) { throw "FollowNetwork entities were not created." }
    if (@($networkTexts | Where-Object { [Math]::Abs([double]$_.Rotation) -gt 0.00000001 }).Count -ne 0) {
        throw "Horizontal network text rotation differs from baseline."
    }
    if ([Math]::Abs([double]$waterBlock[0].Rotation) -gt 0.00000001) {
        throw "Horizontal FollowNetwork block rotation differs from baseline."
    }
    Write-Output "Stage 5 geometry smoke passed: association, rotation, writer and idempotence."
}
finally {
    Remove-Item Env:GEOAUTOENGINE_STAGE4_INPUTS -ErrorAction SilentlyContinue
    Remove-Item Env:GEOAUTOENGINE_STAGE4_SMOKE_MARKER -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $temporaryDirectory) {
        Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force
    }
}
