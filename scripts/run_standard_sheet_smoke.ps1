param(
    [ValidateSet("ACAD", "C3D")]
    [string]$Product = "C3D"
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$acad = Join-Path $env:ProgramFiles "Autodesk\AutoCAD 2026\acad.exe"
$sourceDrawing = Join-Path $env:ProgramFiles "Autodesk\AutoCAD 2026\C3D\PushPins.dwg"
$bundle = Join-Path $env:APPDATA "Autodesk\ApplicationPlugins\GeoAutoEngine.bundle"
$settingsDirectory = Join-Path $env:APPDATA "GeoAutoEngine3"
$settingsPath = Join-Path $settingsDirectory "drawing-presentation.json"
$csvFiles = @(
    (Join-Path $root "samples\stage4_cad_smoke_day1.csv"),
    (Join-Path $root "samples\stage4_cad_smoke_day2.csv")
)
foreach ($path in @($acad, $sourceDrawing, $bundle) + $csvFiles) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Required path missing: $path" }
}
if (Get-Process acad -ErrorAction SilentlyContinue) { throw "Close AutoCAD/Civil 3D before this smoke." }

$temporaryDirectory = Join-Path ([IO.Path]::GetTempPath()) ("geoautoengine-sheet-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $temporaryDirectory | Out-Null
$originalSettings = if (Test-Path -LiteralPath $settingsPath) { [IO.File]::ReadAllBytes($settingsPath) } else { $null }
$smokeSucceeded = $false
try {
    function Set-Presentation([int]$Scale, [bool]$AddSheet) {
        New-Item -ItemType Directory -Path $settingsDirectory -Force | Out-Null
        [ordered]@{
            ScaleDenominator = $Scale
            PrintedTextHeightMillimeters = 2.0
            BlockScaleMode = 0
            ShowRevisionMarks = $true
            AddStandardSheet = $AddSheet
        } | ConvertTo-Json | Set-Content -LiteralPath $settingsPath -Encoding utf8
    }

    function Invoke-CadRun([string]$Drawing, [string]$Marker, [string[]]$Commands) {
        $script = Join-Path $temporaryDirectory ("run-" + [guid]::NewGuid().ToString("N") + ".scr")
        $stdout = "$script.out"
        $stderr = "$script.err"
        (@("FILEDIA", "0", "CMDECHO", "1") + $Commands + @("_.QSAVE", "_.QUIT", "_N")) |
            Set-Content -LiteralPath $script -Encoding ascii
        $env:GEOAUTOENGINE_STAGE4_INPUTS = [string]::Join([IO.Path]::PathSeparator, $csvFiles)
        $env:GEOAUTOENGINE_STAGE4_SMOKE_MARKER = $Marker
        $process = Start-Process -FilePath $acad `
            -ArgumentList @($Drawing, "/b", $script, "/product", $Product, "/language", "en-US", "/nologo") `
            -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr -PassThru
        if (-not $process.WaitForExit(180000)) {
            $process.Kill($true)
            throw "Civil 3D standard sheet smoke timed out."
        }
        $process.WaitForExit()
        $process.Refresh()
        if ([int]$process.ExitCode -ne 0) { throw "Civil 3D returned $($process.ExitCode)." }
        Start-Sleep -Seconds 5
        $deadline = [DateTime]::UtcNow.AddSeconds(175)
        while (Get-Process acad -ErrorAction SilentlyContinue) {
            if ([DateTime]::UtcNow -ge $deadline) {
                Get-Process acad -ErrorAction SilentlyContinue | Stop-Process -Force
                throw "Civil 3D child process did not finish the sheet smoke script."
            }
            Start-Sleep -Milliseconds 500
        }
    }

    $drawing = Join-Path $temporaryDirectory "with-sheet.dwg"
    $disabledDrawing = Join-Path $temporaryDirectory "without-sheet.dwg"
    $marker = Join-Path $temporaryDirectory "with-sheet.json"
    $disabledMarker = Join-Path $temporaryDirectory "without-sheet.json"
    Copy-Item -LiteralPath $sourceDrawing -Destination $drawing
    Copy-Item -LiteralPath $sourceDrawing -Destination $disabledDrawing

    Set-Presentation 250 $true
    Invoke-CadRun $drawing $marker @("GEOAUTO3IMPORT", "GEOAUTO3VERIFY", "GEOAUTO3IMPORT", "GEOAUTO3VERIFY")
    Set-Presentation 500 $true
    Invoke-CadRun $drawing $marker @("GEOAUTO3IMPORT", "GEOAUTO3VERIFY")
    Set-Presentation 250 $false
    Invoke-CadRun $disabledDrawing $disabledMarker @("GEOAUTO3IMPORT", "GEOAUTO3VERIFY")

    $events = Get-Content -LiteralPath $marker -Raw | ConvertFrom-Json
    $disabled = Get-Content -LiteralPath $disabledMarker -Raw | ConvertFrom-Json
    if ($events.Count -ne 6 -or $disabled.Count -ne 2) { throw "Unexpected sheet smoke evidence count." }
    $first = $events[0].Drawing
    $second = $events[2].Drawing
    $scaled = $events[4].Drawing
    $withoutSheet = $disabled[0].Drawing
    if ([string]::IsNullOrWhiteSpace($first.StandardSheetLayout)) { throw "Standard sheet marker was not created." }
    if (@($first.StandardSheetScaleTexts | Where-Object { $_ -eq "H=1:250" }).Count -lt 1) {
        throw "The imported standard sheet does not show H=1:250."
    }
    if ($first.PaperLayouts.Count -ne $second.PaperLayouts.Count -or
        $first.StandardSheetLayout -ne $second.StandardSheetLayout) {
        throw "A second standard sheet was created during reprocessing."
    }
    if (@($scaled.StandardSheetScaleTexts | Where-Object { $_ -eq "H=1:500" }).Count -lt 1) {
        throw "The standard sheet scale was not synchronized to H=1:500."
    }
    if ($scaled.PaperLayouts.Count -ne $first.PaperLayouts.Count) {
        throw "Scale synchronization duplicated the standard sheet."
    }
    if (-not [string]::IsNullOrWhiteSpace($withoutSheet.StandardSheetLayout)) {
        throw "Disabled option imported a standard sheet."
    }
    if ($first.PaperLayouts.Count -ne ($withoutSheet.PaperLayouts.Count + 1)) {
        throw "The enabled option did not add exactly one Layout."
    }

    $smokeSucceeded = $true
    Write-Output "Civil 3D 2026 standard sheet smoke passed: import, H=1:250 -> H=1:500, disabled option and idempotence."
}
finally {
    Remove-Item Env:GEOAUTOENGINE_STAGE4_INPUTS -ErrorAction SilentlyContinue
    Remove-Item Env:GEOAUTOENGINE_STAGE4_SMOKE_MARKER -ErrorAction SilentlyContinue
    if ($null -ne $originalSettings) {
        [IO.File]::WriteAllBytes($settingsPath, $originalSettings)
    }
    elseif (Test-Path -LiteralPath $settingsPath) {
        Remove-Item -LiteralPath $settingsPath -Force
    }
    if ($smokeSucceeded -and (Test-Path -LiteralPath $temporaryDirectory)) {
        Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force
    }
    elseif (-not $smokeSucceeded) {
        Write-Warning "Smoke evidence preserved at $temporaryDirectory"
    }
}
