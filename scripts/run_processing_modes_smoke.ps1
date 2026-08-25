param(
    [ValidateSet("ACAD", "C3D")]
    [string]$Product = "C3D",

    [ValidateSet("2025", "2026")]
    [string]$CadVersion = "2026"
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$acad = Join-Path $env:ProgramFiles "Autodesk\AutoCAD $CadVersion\acad.exe"
$sourceDrawing = Join-Path $env:ProgramFiles "Autodesk\AutoCAD $CadVersion\C3D\PushPins.dwg"
$bundle = Join-Path $env:APPDATA "Autodesk\ApplicationPlugins\GeoAutoEngine.bundle"
$csvFiles = @(
    (Join-Path $root "samples\stage4_cad_smoke_day1.csv"),
    (Join-Path $root "samples\stage4_cad_smoke_day2.csv"),
    (Join-Path $root "samples\processing_modes_attention.csv")
)
foreach ($path in @($acad, $sourceDrawing, $bundle) + $csvFiles) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Required path missing: $path" }
}
if (Get-Process acad -ErrorAction SilentlyContinue) { throw "Close AutoCAD/Civil 3D before this smoke." }

$temporaryDirectory = Join-Path ([IO.Path]::GetTempPath()) ("geoautoengine-modes-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $temporaryDirectory | Out-Null
$smokeSucceeded = $false
try {
    $drawing = Join-Path $temporaryDirectory "processing-modes.dwg"
    $marker = Join-Path $temporaryDirectory "processing-modes.json"
    Copy-Item -LiteralPath $sourceDrawing -Destination $drawing

    function Invoke-CadRun([string]$Mode, [string[]]$Commands) {
        $script = Join-Path $temporaryDirectory ("run-" + [guid]::NewGuid().ToString("N") + ".scr")
        $stdout = "$script.out"
        $stderr = "$script.err"
        (@("FILEDIA", "0", "CMDECHO", "1") + $Commands + @("_.QSAVE", "_.QUIT", "_N")) |
            Set-Content -LiteralPath $script -Encoding ascii
        $env:GEOAUTOENGINE_PROCESSING_MODE = $Mode
        $process = Start-Process -FilePath $acad `
            -ArgumentList @($drawing, "/b", $script, "/product", $Product, "/language", "en-US", "/nologo") `
            -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr -PassThru
        if (-not $process.WaitForExit(180000)) {
            $process.Kill($true)
            throw "Civil 3D processing mode smoke timed out."
        }
        $process.WaitForExit()
        $process.Refresh()
        if ([int]$process.ExitCode -ne 0) { throw "Civil 3D returned $($process.ExitCode)." }
        Start-Sleep -Seconds 5
        $deadline = [DateTime]::UtcNow.AddSeconds(175)
        while (Get-Process acad -ErrorAction SilentlyContinue) {
            if ([DateTime]::UtcNow -ge $deadline) {
                Get-Process acad -ErrorAction SilentlyContinue | Stop-Process -Force
                throw "Civil 3D child process did not finish the smoke script."
            }
            Start-Sleep -Milliseconds 500
        }
    }

    $env:GEOAUTOENGINE_STAGE4_INPUTS = [string]::Join([IO.Path]::PathSeparator, $csvFiles)
    $env:GEOAUTOENGINE_STAGE4_SMOKE_MARKER = $marker
    Invoke-CadRun "BlocksOnly" @("GEOAUTO3IMPORT")
    Invoke-CadRun "TextsOnly" @(
        "_.-LAYER", "_M", "REDE_ESGOTO", "",
        "_.LINE", "495,1002", "510,1002", "",
        "_.-LAYER", "_M", "AGUA", "",
        "_.LINE", "525,1030", "540,1030", "",
        "GEOAUTO3IMPORT", "GEOAUTO3VERIFY"
    )
    Invoke-CadRun "TextsOnly" @("GEOAUTO3IMPORT", "GEOAUTO3VERIFY")

    $events = Get-Content -LiteralPath $marker -Raw | ConvertFrom-Json
    if ($events.Count -ne 5) { throw "Unexpected smoke evidence event count: $($events.Count)." }
    if ($events[0].ApplicationResult.CreatedItemCount -ne 4 -or $events[0].Drawing.GeneratedEntityCount -ne 4) {
        throw "BlocksOnly did not create two blocks, one structure and its attention point."
    }
    if (@($events[0].Drawing.Entities | Where-Object EntityType -eq "DBText").Count -ne 0) {
        throw "BlocksOnly materialized text."
    }
    $blockAttention = @($events[0].Drawing.Entities | Where-Object EntityType -eq "Circle")
    if ($blockAttention.Count -ne 1 -or $blockAttention[0].StableKey -notlike "*rain-review*") {
        throw "BlocksOnly did not materialize only the block attention point."
    }
    if ($events[1].ApplicationResult.CreatedItemCount -ne 4 -or $events[1].Drawing.GeneratedEntityCount -ne 8) {
        throw "TextsOnly did not add three canonical texts and its attention point."
    }
    $allAttention = @($events[1].Drawing.Entities | Where-Object EntityType -eq "Circle")
    if ($allAttention.Count -ne 2 -or
        @($allAttention | Where-Object { $_.StableKey -like "*unknown-code*" }).Count -ne 1) {
        throw "TextsOnly did not materialize exactly the text attention point."
    }
    if ($events[3].ApplicationResult.CreatedItemCount -ne 0 -or
        $events[3].ApplicationResult.SkippedExistingItemCount -ne 4 -or
        $events[4].Drawing.GeneratedEntityCount -ne 8) {
        throw "TextsOnly reprocessing was not idempotent."
    }
    $networkTexts = @($events[4].Drawing.Entities |
        Where-Object { $_.EntityType -eq "DBText" -and $_.Layer -eq "GF-Rede Esgoto" })
    if ($networkTexts.Count -ne 2) { throw "FollowNetwork texts were not materialized." }

    $smokeSucceeded = $true
    Write-Output "Civil 3D $CadVersion processing modes smoke passed: BlocksOnly -> TextsOnly, FollowNetwork and idempotence."
}
finally {
    Remove-Item Env:GEOAUTOENGINE_STAGE4_INPUTS -ErrorAction SilentlyContinue
    Remove-Item Env:GEOAUTOENGINE_STAGE4_SMOKE_MARKER -ErrorAction SilentlyContinue
    Remove-Item Env:GEOAUTOENGINE_PROCESSING_MODE -ErrorAction SilentlyContinue
    if ($smokeSucceeded -and (Test-Path -LiteralPath $temporaryDirectory)) {
        Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force
    }
    elseif (-not $smokeSucceeded) {
        Write-Warning "Smoke evidence preserved at $temporaryDirectory"
    }
}
