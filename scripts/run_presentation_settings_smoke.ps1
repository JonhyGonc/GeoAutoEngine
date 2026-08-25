param(
    [ValidateSet("Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$dotnet = Join-Path $repositoryRoot ".dotnet\dotnet.exe"
$pluginProject = Join-Path $repositoryRoot "src\GeoAutoEngine.Plugin\GeoAutoEngine.Plugin.csproj"
$cadExecutable = Join-Path $env:ProgramFiles "Autodesk\AutoCAD 2026\acad.exe"
$sourceDrawing = Join-Path $env:ProgramFiles "Autodesk\AutoCAD 2026\C3D\PushPins.dwg"
$bundleInstall = Join-Path $env:APPDATA "Autodesk\ApplicationPlugins\GeoAutoEngine.bundle"
$settingsDirectory = Join-Path $env:APPDATA "GeoAutoEngine3"
$settingsPath = Join-Path $settingsDirectory "drawing-presentation.json"
$sourceCsvFiles = @(
    (Join-Path $repositoryRoot "samples\stage4_cad_smoke_day1.csv"),
    (Join-Path $repositoryRoot "samples\stage4_cad_smoke_day2.csv")
)

$env:DOTNET_CLI_HOME = Join-Path $repositoryRoot ".dotnet_cli_home"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_NOLOGO = "1"

foreach ($required in @($dotnet, $pluginProject, $cadExecutable, $sourceDrawing) + $sourceCsvFiles) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { throw "Required file not found: $required" }
}
if (-not (Test-Path -LiteralPath $bundleInstall -PathType Container)) { throw "Installed bundle not found: $bundleInstall" }
if (Get-Process acad -ErrorAction SilentlyContinue) { throw "Close AutoCAD/Civil 3D before this smoke." }

& $dotnet build $pluginProject --configuration $Configuration --no-restore --nologo
if ($LASTEXITCODE -ne 0) { throw "Plugin build failed with exit code $LASTEXITCODE." }

$settingsExisted = Test-Path -LiteralPath $settingsPath -PathType Leaf
$settingsBackup = if ($settingsExisted) { [System.IO.File]::ReadAllBytes($settingsPath) } else { $null }
$temporaryDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ("geoautoengine-presentation-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $temporaryDirectory | Out-Null

try {
    New-Item -ItemType Directory -Path $settingsDirectory -Force | Out-Null
    $settingsJson = [ordered]@{
        ScaleDenominator = 500
        PrintedTextHeightMillimeters = 2.0
        BlockScaleMode = 0
        ShowRevisionMarks = $false
    } | ConvertTo-Json
    [System.IO.File]::WriteAllText($settingsPath, $settingsJson, [System.Text.UTF8Encoding]::new($false))

    $drawing = Join-Path $temporaryDirectory "presentation-smoke.dwg"
    $script = Join-Path $temporaryDirectory "presentation-smoke.scr"
    $marker = Join-Path $temporaryDirectory "presentation-smoke.json"
    Copy-Item -LiteralPath $sourceDrawing -Destination $drawing
    @("FILEDIA", "0", "CMDECHO", "1", "GEOAUTO3IMPORT", "GEOAUTO3VERIFY", "_.QSAVE", "_.QUIT", "_N") |
        Set-Content -LiteralPath $script -Encoding ascii

    $env:GEOAUTOENGINE_STAGE4_INPUTS = [string]::Join([System.IO.Path]::PathSeparator, $sourceCsvFiles)
    $env:GEOAUTOENGINE_STAGE4_SMOKE_MARKER = $marker
    $arguments = @($drawing, "/b", $script, "/product", "C3D", "/language", "en-US", "/nologo")
    $process = Start-Process -FilePath $cadExecutable -ArgumentList $arguments -WorkingDirectory $repositoryRoot `
        -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(180000)) {
        $process.Kill($true)
        throw "Civil 3D presentation settings smoke timed out."
    }
    $process.WaitForExit()
    $process.Refresh()
    if ($process.ExitCode -ne 0) { throw "Civil 3D returned $($process.ExitCode)." }
    if (-not (Test-Path -LiteralPath $marker -PathType Leaf)) { throw "Presentation smoke marker was not created." }

    $parsedEvents = Get-Content -LiteralPath $marker -Raw | ConvertFrom-Json
    $events = @()
    foreach ($event in $parsedEvents) { $events += $event }
    $import = $events[0]
    $verification = $events[-1]
    if (-not [bool]$import.ApplicationResult.Succeeded) { throw "Presentation smoke application failed." }
    if ($import.PlanSummary.CircleAlertCount -ne 0) { throw "Revision circles were not disabled." }
    if ($import.PlanSummary.WarningCount -lt 1) { throw "Internal diagnostics were incorrectly removed." }
    if ($verification.Drawing.GeneratedEntityCount -ne 6) { throw "Expected six generated entities without the revision circle." }
    if (@($verification.Drawing.Entities | Where-Object EntityType -eq "Circle").Count -ne 0) { throw "A revision circle remains in the DWG." }
    $texts = @($verification.Drawing.Entities | Where-Object EntityType -eq "DBText")
    if ($texts.Count -ne 3) {
        $types = @($verification.Drawing.Entities | ForEach-Object { $_.EntityType }) -join ", "
        throw "Expected three generated texts; found $($texts.Count). Types: $types"
    }
    if (@($texts | Where-Object { [Math]::Abs([double]$_.TextHeight - 1.0) -gt 0.00000001 }).Count -ne 0) {
        throw "Configured text height 1.00 m was not applied to every DBText."
    }

    Write-Output "Civil 3D 2026 presentation settings smoke passed: 1:500 => 1.00 m; revision circles disabled; diagnostics preserved."
}
finally {
    Remove-Item Env:GEOAUTOENGINE_STAGE4_INPUTS -ErrorAction SilentlyContinue
    Remove-Item Env:GEOAUTOENGINE_STAGE4_SMOKE_MARKER -ErrorAction SilentlyContinue
    if ($settingsExisted) {
        [System.IO.File]::WriteAllBytes($settingsPath, $settingsBackup)
    }
    elseif (Test-Path -LiteralPath $settingsPath) {
        Remove-Item -LiteralPath $settingsPath -Force
    }
    if (Test-Path -LiteralPath $temporaryDirectory) {
        $resolved = (Resolve-Path -LiteralPath $temporaryDirectory).Path
        $expected = [System.IO.Path]::GetFullPath($temporaryDirectory)
        if (-not [string]::Equals($resolved, $expected, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing cleanup because temporary path changed: $resolved"
        }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
