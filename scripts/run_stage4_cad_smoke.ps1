param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [ValidateSet("ACAD", "C3D")]
    [string[]]$Products = @("ACAD", "C3D"),

    [ValidateSet("success", "rollback", "undo")]
    [string[]]$Scenarios = @("rollback", "undo", "success"),

    [switch]$UseInstalledBundle
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$dotnet = Join-Path $repositoryRoot ".dotnet\dotnet.exe"
$pluginProject = Join-Path $repositoryRoot "src\GeoAutoEngine.Plugin\GeoAutoEngine.Plugin.csproj"
$bundleSource = Join-Path $repositoryRoot "artifacts\bundle\GeoAutoEngine.bundle"
$bundleInstall = Join-Path $env:APPDATA "Autodesk\ApplicationPlugins\GeoAutoEngine.bundle"
$cadExecutable = Join-Path $env:ProgramFiles "Autodesk\AutoCAD 2026\acad.exe"
$sourceDrawing = Join-Path $env:ProgramFiles "Autodesk\AutoCAD 2026\C3D\PushPins.dwg"
$sourceCsvFiles = @(
    (Join-Path $repositoryRoot "samples\stage4_cad_smoke_day1.csv"),
    (Join-Path $repositoryRoot "samples\stage4_cad_smoke_day2.csv")
)
$evidenceDirectory = Join-Path $repositoryRoot "docs\evidence\stage4\cad"
$preexistingCadProcessIds = @(Get-Process acad -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id)

$env:DOTNET_CLI_HOME = Join-Path $repositoryRoot ".dotnet_cli_home"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_NOLOGO = "1"

function Read-ConsoleLog {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return "" }
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $probeLength = [Math]::Min(100, $bytes.Length)
    if ($probeLength -gt 0 -and $bytes[0..($probeLength - 1)] -contains 0) {
        return [System.Text.Encoding]::Unicode.GetString($bytes)
    }
    return [System.Text.Encoding]::UTF8.GetString($bytes)
}

function Assert-Check {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

function Test-Near {
    param([double]$Actual, [double]$Expected, [double]$Tolerance = 0.00000001)
    return [Math]::Abs($Actual - $Expected) -le $Tolerance
}

function Invoke-Stage4Scenario {
    param(
        [string]$Product,
        [ValidateSet("success", "rollback", "undo")]
        [string]$Scenario
    )

    $temporaryDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ("geoautoengine-stage4-" + $Product.ToLowerInvariant() + "-" + $Scenario + "-" + [guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $temporaryDirectory | Out-Null
    try {
        $drawing = Join-Path $temporaryDirectory "stage4-smoke.dwg"
        $script = Join-Path $temporaryDirectory "stage4-smoke.scr"
        $marker = Join-Path $temporaryDirectory "stage4-smoke.json"
        $stdoutPath = Join-Path $temporaryDirectory "stdout.log"
        $stderrPath = Join-Path $temporaryDirectory "stderr.log"
        Copy-Item -LiteralPath $sourceDrawing -Destination $drawing

        $commands = @("FILEDIA", "0", "CMDECHO", "1", "GEOAUTO3IMPORT")
        if ($Scenario -eq "success") {
            $commands += "GEOAUTO3IMPORT"
        }
        elseif ($Scenario -eq "undo") {
            $commands += @("_.UNDO", "1")
        }
        $commands += @("GEOAUTO3VERIFY", "_.QSAVE", "_.QUIT", "_N")
        $commands | Set-Content -LiteralPath $script -Encoding ascii

        $env:GEOAUTOENGINE_STAGE4_INPUTS = [string]::Join([System.IO.Path]::PathSeparator, $sourceCsvFiles)
        $env:GEOAUTOENGINE_STAGE4_SMOKE_MARKER = $marker
        if ($Scenario -eq "rollback") {
            $env:GEOAUTOENGINE_STAGE4_INJECT_FAILURE_AFTER = "2"
        }
        else {
            Remove-Item Env:GEOAUTOENGINE_STAGE4_INJECT_FAILURE_AFTER -ErrorAction SilentlyContinue
        }

        $arguments = @($drawing, "/b", $script, "/product", $Product, "/language", "en-US", "/nologo")
        $started = Get-Date
        $process = Start-Process `
            -FilePath $cadExecutable `
            -ArgumentList $arguments `
            -WorkingDirectory $repositoryRoot `
            -WindowStyle Hidden `
            -RedirectStandardOutput $stdoutPath `
            -RedirectStandardError $stderrPath `
            -PassThru
        if (-not $process.WaitForExit(180000)) {
            $process.Kill($true)
            throw "AutoCAD desktop host timed out for $Product/$Scenario."
        }
        $process.WaitForExit()
        $process.Refresh()
        $durationSeconds = ((Get-Date) - $started).TotalSeconds
        $exitCode = [int]$process.ExitCode

        $suffix = "2026_" + $Product.ToLowerInvariant() + "_" + $Scenario
        $logPath = Join-Path $evidenceDirectory ("stage4_smoke_" + $suffix + ".log")
        $combinedLog = (Read-ConsoleLog $stdoutPath) + (Read-ConsoleLog $stderrPath)
        Set-Content -LiteralPath $logPath -Value $combinedLog -Encoding utf8
        Assert-Check ($exitCode -eq 0) "AutoCAD returned $exitCode for $Product/$Scenario; inspect $logPath."
        Assert-Check (Test-Path -LiteralPath $marker -PathType Leaf) "Stage 4 marker was not created for $Product/$Scenario."

        $markerEvidence = Join-Path $evidenceDirectory ("stage4_smoke_" + $suffix + ".json")
        Copy-Item -LiteralPath $marker -Destination $markerEvidence -Force
        $drawingEvidence = Join-Path $evidenceDirectory ("stage4_output_" + $suffix + ".dwg")
        Copy-Item -LiteralPath $drawing -Destination $drawingEvidence -Force

        $parsedEvents = Get-Content -LiteralPath $marker -Raw | ConvertFrom-Json
        $events = @()
        foreach ($event in $parsedEvents) { $events += $event }
        $import = $events[0]
        $verificationEvent = $events[-1]
        Assert-Check ($import.Command -eq "GEOAUTO3IMPORT") "Import command evidence missing for $Product/$Scenario."
        Assert-Check ($verificationEvent.Command -eq "GEOAUTO3VERIFY") "Verification command evidence missing for $Product/$Scenario."

        if ($Scenario -eq "success") {
            Assert-Check ($events.Count -eq 3) "Success scenario must contain two imports and one verification."
            $second = $events[1]
            Assert-Check ([bool]$import.ApplicationResult.Succeeded) "First application failed for $Product."
            Assert-Check ($import.ApplicationResult.CreatedItemCount -eq 7) "First application must create seven entities for $Product."
            Assert-Check ($import.ApplicationResult.PendingItemCount -eq 0) "First application must resolve or safely fallback every FollowNetwork item."
            Assert-Check ([bool]$import.ApplicationResult.Regenerated) "First application must perform one regeneration."
            Assert-Check ([bool]$second.ApplicationResult.Succeeded) "Second application failed for $Product."
            Assert-Check ($second.ApplicationResult.CreatedItemCount -eq 0) "Second application created duplicates for $Product."
            Assert-Check ($second.ApplicationResult.SkippedExistingItemCount -eq 7) "Second application did not identify all existing items for $Product."
            Assert-Check (-not [bool]$second.ApplicationResult.Regenerated) "Second no-op application regenerated unexpectedly for $Product."
            Assert-Check ($verificationEvent.Drawing.GeneratedEntityCount -eq 7) "Expected seven generated entities after idempotence check for $Product."
            foreach ($pair in @{ DBText = 3; BlockReference = 2; Polyline = 1; Circle = 1 }.GetEnumerator()) {
                Assert-Check ($verificationEvent.Drawing.CountsByType.($pair.Key) -eq $pair.Value) "Unexpected $($pair.Key) count for $Product."
            }

            $text = @($verificationEvent.Drawing.Entities | Where-Object { $_.EntityType -eq "DBText" -and $_.Layer -eq "-Texto" })[0]
            $block = @($verificationEvent.Drawing.Entities | Where-Object { $_.EntityType -eq "BlockReference" -and $_.BlockName -eq "pv_esgoto_sanitario" })[0]
            $polyline = @($verificationEvent.Drawing.Entities | Where-Object EntityType -eq "Polyline")[0]
            $circle = @($verificationEvent.Drawing.Entities | Where-Object EntityType -eq "Circle")[0]
            $expectedBoundaryText = "N" + [char]0x00BA + "113"
            Assert-Check ($text.Text -eq $expectedBoundaryText -and $text.Layer -eq "-Texto") "Boundary text properties differ for $Product."
            Assert-Check ((Test-Near $text.Position[0] 500) -and (Test-Near $text.Position[1] 1000)) "Boundary text position differs for $Product."
            Assert-Check ($block.BlockName -eq "pv_esgoto_sanitario" -and $block.Layer -eq "GF-Rede Esgoto") "Block properties differ for $Product."
            Assert-Check ((Test-Near $block.Position[0] 501) -and (Test-Near $block.Position[1] 1001)) "Block position differs for $Product."
            Assert-Check (($block.Scale | Where-Object { -not (Test-Near $_ 1) }).Count -eq 0) "Block scale differs for $Product."
            Assert-Check ($polyline.Layer -eq "GF-Grelha" -and $polyline.VertexCount -eq 4) "Rain structure properties differ for $Product."
            Assert-Check ($circle.Layer -eq "GF-Diagnostico" -and (Test-Near $circle.Radius 0.75) -and $circle.ColorIndex -eq 1) "Alert properties differ for $Product."
            Assert-Check ((Test-Near $circle.Position[0] 520) -and (Test-Near $circle.Position[1] 1020)) "Alert position differs for $Product."
            $expectedWaterLayer = "GF-Rede " + [char]0x00C1 + "gua"
            Assert-Check ($verificationEvent.Drawing.Layers -contains $expectedWaterLayer) "Layer for pending FollowNetwork block was not guaranteed for $Product."
            Assert-Check ($verificationEvent.Drawing.TextStyles -contains "GEO_ARIAL") "Main text style was not guaranteed for $Product."
            Assert-Check ($verificationEvent.Drawing.TextStyles -contains "GEO_ARIAL_BOLD") "Boundary text style was not guaranteed for $Product."
            Assert-Check ($verificationEvent.Drawing.RegisteredApplications -contains "GEOAUTOENGINE") "XData application missing for $Product."
            foreach ($entity in $verificationEvent.Drawing.Entities) {
                Assert-Check (-not [string]::IsNullOrWhiteSpace($entity.XDataValues.REGISTRO)) "Stable XData identity missing for $Product."
                Assert-Check (-not [string]::IsNullOrWhiteSpace($entity.XDataValues.PLAN_ID)) "Plan XData identity missing for $Product."
                Assert-Check (-not [string]::IsNullOrWhiteSpace($entity.XDataValues.SOURCE_FILE)) "Source XData missing for $Product."
                Assert-Check (-not [string]::IsNullOrWhiteSpace($entity.XDataValues.SOURCE_LINE)) "Source line XData missing for $Product."
            }
        }
        elseif ($Scenario -eq "rollback") {
            Assert-Check (-not [bool]$import.ApplicationResult.Succeeded) "Injected failure unexpectedly succeeded for $Product."
            Assert-Check ([bool]$import.ApplicationResult.RolledBack) "Injected failure did not report rollback for $Product."
            Assert-Check ($verificationEvent.Drawing.GeneratedEntityCount -eq 0) "Rollback left generated entities for $Product."
        }
        else {
            Assert-Check ([bool]$import.ApplicationResult.Succeeded -and $import.ApplicationResult.CreatedItemCount -eq 7) "Undo setup application failed for $Product."
            Assert-Check ($verificationEvent.Drawing.GeneratedEntityCount -eq 0) "UNDO did not remove the transaction entities for $Product."
        }

        $preexistingSessionsPreserved = @($preexistingCadProcessIds | Where-Object { Get-Process -Id $_ -ErrorAction SilentlyContinue }).Count -eq $preexistingCadProcessIds.Count
        return [ordered]@{
            host = $Product
            scenario = $Scenario
            passed = $true
            generated_entities = $verificationEvent.Drawing.GeneratedEntityCount
            timing_seconds = [Math]::Round($durationSeconds, 6)
            preexisting_sessions_preserved = $preexistingSessionsPreserved
            drawing_sha256 = (Get-FileHash -LiteralPath $drawingEvidence -Algorithm SHA256).Hash.ToLowerInvariant()
            evidence_sha256 = (Get-FileHash -LiteralPath $markerEvidence -Algorithm SHA256).Hash.ToLowerInvariant()
            log_sha256 = (Get-FileHash -LiteralPath $logPath -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    }
    finally {
        Remove-Item Env:GEOAUTOENGINE_STAGE4_INPUTS -ErrorAction SilentlyContinue
        Remove-Item Env:GEOAUTOENGINE_STAGE4_SMOKE_MARKER -ErrorAction SilentlyContinue
        Remove-Item Env:GEOAUTOENGINE_STAGE4_INJECT_FAILURE_AFTER -ErrorAction SilentlyContinue
        if (Test-Path -LiteralPath $temporaryDirectory) {
            $resolvedTemporary = (Resolve-Path -LiteralPath $temporaryDirectory).Path
            $expectedTemporary = [System.IO.Path]::GetFullPath($temporaryDirectory)
            if (-not [string]::Equals($resolvedTemporary, $expectedTemporary, [StringComparison]::OrdinalIgnoreCase)) {
                throw "Refusing cleanup because temporary path changed: $resolvedTemporary"
            }
            Remove-Item -LiteralPath $resolvedTemporary -Recurse -Force
        }
    }
}

foreach ($required in @($dotnet, $pluginProject, $cadExecutable, $sourceDrawing) + $sourceCsvFiles) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { throw "Required file not found: $required" }
}

& $dotnet build $pluginProject --configuration $Configuration --no-restore --nologo
if ($LASTEXITCODE -ne 0) { throw "Plugin build failed with exit code $LASTEXITCODE." }

$forbiddenAssemblies = Get-ChildItem -LiteralPath (Join-Path $bundleSource "Contents\Windows") -File |
    Where-Object { $_.Name -in @("AcCoreMgd.dll", "AcDbMgd.dll", "AcMgd.dll", "AcWindows.dll", "AdWindows.dll", "AeccDbMgd.dll") }
if ($forbiddenAssemblies) { throw "Autodesk assemblies must not be copied into the bundle: $($forbiddenAssemblies.Name -join ', ')" }
$blockAssets = @(Get-ChildItem -LiteralPath (Join-Path $bundleSource "Contents\Assets\Blocks") -Filter *.dwg -File)
Assert-Check ($blockAssets.Count -eq 7) "Bundle must contain exactly seven approved block assets."
if ((Test-Path -LiteralPath $bundleInstall) -and -not $UseInstalledBundle) { throw "Refusing to overwrite an existing installed bundle: $bundleInstall" }
if (-not (Test-Path -LiteralPath $bundleInstall) -and $UseInstalledBundle) { throw "Installed bundle not found: $bundleInstall" }

New-Item -ItemType Directory -Path $evidenceDirectory -Force | Out-Null
$installedBySmoke = $false
if (-not $UseInstalledBundle) {
    New-Item -ItemType Directory -Path (Split-Path -Parent $bundleInstall) -Force | Out-Null
    Copy-Item -LiteralPath $bundleSource -Destination $bundleInstall -Recurse
    $installedBySmoke = $true
}

$results = @()
try {
    foreach ($product in $Products) {
        foreach ($scenario in $Scenarios) {
            $results += Invoke-Stage4Scenario -Product $product -Scenario $scenario
        }
    }

    Assert-Check (($results | Where-Object { -not $_.preexisting_sessions_preserved }).Count -eq 0) "A preexisting AutoCAD session was not preserved."
    $summaryPath = Join-Path $evidenceDirectory "STAGE4_CAD_SMOKE_SUMMARY.json"
    [ordered]@{
        schema_version = 1
        target = "AutoCAD 2026 / Civil 3D 2026"
        configuration = $Configuration
        results = $results
    } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $summaryPath -Encoding utf8
    Write-Output "Stage 4 CAD smoke passed for products [$($Products -join ', ')] and scenarios [$($Scenarios -join ', ')]."
}
finally {
    Remove-Item Env:GEOAUTOENGINE_STAGE4_INPUTS -ErrorAction SilentlyContinue
    Remove-Item Env:GEOAUTOENGINE_STAGE4_SMOKE_MARKER -ErrorAction SilentlyContinue
    Remove-Item Env:GEOAUTOENGINE_STAGE4_INJECT_FAILURE_AFTER -ErrorAction SilentlyContinue
    if ($installedBySmoke -and (Test-Path -LiteralPath $bundleInstall)) {
        $resolvedInstall = (Resolve-Path -LiteralPath $bundleInstall).Path
        $expectedInstall = [System.IO.Path]::GetFullPath($bundleInstall)
        if (-not [string]::Equals($resolvedInstall, $expectedInstall, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing cleanup because installed bundle path changed: $resolvedInstall"
        }
        Remove-Item -LiteralPath $resolvedInstall -Recurse -Force
    }
}
