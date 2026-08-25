param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [switch]$UseInstalledBundle,

    [switch]$StartupOnly,

    [ValidateSet("2025", "2026")]
    [string]$CadVersion = "2026"
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$dotnet = Join-Path $repositoryRoot ".dotnet\dotnet.exe"
$pluginProject = Join-Path $repositoryRoot "src\GeoAutoEngine.Plugin\GeoAutoEngine.Plugin.csproj"
$bundleSource = Join-Path $repositoryRoot "artifacts\bundle\GeoAutoEngine.bundle"
$bundleInstall = Join-Path $env:APPDATA "Autodesk\ApplicationPlugins\GeoAutoEngine.bundle"
$cadExecutable = Join-Path $env:ProgramFiles "Autodesk\AutoCAD $CadVersion\acad.exe"
$sourceDrawing = Join-Path $env:ProgramFiles "Autodesk\AutoCAD $CadVersion\C3D\PushPins.dwg"
$sourceCsvFiles = @(
    (Join-Path $repositoryRoot "samples\stage4_cad_smoke_day1.csv"),
    (Join-Path $repositoryRoot "samples\stage4_cad_smoke_day2.csv")
)

$env:DOTNET_CLI_HOME = Join-Path $repositoryRoot ".dotnet_cli_home"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_NOLOGO = "1"

foreach ($required in @($dotnet, $pluginProject, $cadExecutable, $sourceDrawing) + $sourceCsvFiles) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Required file not found: $required"
    }
}

if (-not $UseInstalledBundle) {
    & $dotnet build $pluginProject --configuration $Configuration --no-restore --nologo -p:AutodeskAutoCADVersion=$CadVersion
    if ($LASTEXITCODE -ne 0) { throw "Plugin build failed with exit code $LASTEXITCODE." }
}
if ((Test-Path -LiteralPath $bundleInstall) -and -not $UseInstalledBundle) {
    throw "Refusing to overwrite installed bundle: $bundleInstall"
}
if (-not (Test-Path -LiteralPath $bundleInstall) -and $UseInstalledBundle) {
    throw "Installed bundle was not found: $bundleInstall"
}

$temporaryDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ("geoautoengine-ui-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $temporaryDirectory | Out-Null
$installedBySmoke = $false
if (-not $UseInstalledBundle) {
    New-Item -ItemType Directory -Path (Split-Path -Parent $bundleInstall) -Force | Out-Null
    Copy-Item -LiteralPath $bundleSource -Destination $bundleInstall -Recurse
    $installedBySmoke = $true
}
try {
    $drawing = Join-Path $temporaryDirectory "ui-prototype.dwg"
    $script = Join-Path $temporaryDirectory "ui-prototype.scr"
    $marker = Join-Path $temporaryDirectory "ui-prototype.json"
    $initializeMarker = Join-Path $temporaryDirectory "initialize-ready.txt"
    $startupMarker = Join-Path $temporaryDirectory "startup-ready.txt"
    $stdoutPath = Join-Path $temporaryDirectory "stdout.log"
    $stderrPath = Join-Path $temporaryDirectory "stderr.log"
    Copy-Item -LiteralPath $sourceDrawing -Destination $drawing
    $commands = @("FILEDIA", "0", "CMDECHO", "1")
    if ($StartupOnly) {
        $commands += @("_.DELAY", "2000")
    }
    else {
        $commands += @("GEOAUTO3PALETTE", "_.DELAY", "2000")
    }
    $commands += @("_.QUIT", "_N")
    $commands | Set-Content -LiteralPath $script -Encoding ascii

    $env:GEOAUTOENGINE_INITIALIZE_SMOKE_MARKER = $initializeMarker
    $env:GEOAUTOENGINE_STARTUP_SMOKE_MARKER = $startupMarker
    if (-not $StartupOnly) {
        $env:GEOAUTOENGINE_UI_SMOKE_INPUTS = [string]::Join([System.IO.Path]::PathSeparator, $sourceCsvFiles)
        $env:GEOAUTOENGINE_UI_SMOKE_MARKER = $marker
    }
    $process = Start-Process `
        -FilePath $cadExecutable `
        -ArgumentList @($drawing, "/b", $script, "/product", "C3D", "/language", "en-US", "/nologo") `
        -WorkingDirectory $repositoryRoot `
        -WindowStyle Hidden `
        -RedirectStandardOutput $stdoutPath `
        -RedirectStandardError $stderrPath `
        -PassThru
    if (-not $process.WaitForExit(180000)) {
        $process.Kill($true)
        throw "Civil 3D UI prototype smoke timed out."
    }
    $process.WaitForExit()
    $process.Refresh()
    $exitCode = [int]$process.ExitCode
    if ($exitCode -ne 0) { throw "Civil 3D returned $exitCode." }
    if (-not (Test-Path -LiteralPath $initializeMarker -PathType Leaf)) {
        throw "Plugin assembly did not initialize during Civil 3D startup."
    }
    if (-not (Test-Path -LiteralPath $startupMarker -PathType Leaf)) {
        throw "Plugin did not load and create the Ribbon during Civil 3D startup."
    }
    if ($StartupOnly) {
        Write-Output "Civil 3D $CadVersion startup smoke passed without invoking a GeoAutoEngine command."
        return
    }
    if (-not (Test-Path -LiteralPath $marker -PathType Leaf)) { throw "UI prototype marker was not created." }

    $probe = Get-Content -LiteralPath $marker -Raw | ConvertFrom-Json
    if ($probe.command -ne "GEOAUTO3PALETTE") { throw "Palette command was not executed." }
    if (-not [bool]$probe.ribbonCreated) { throw "GeoAutoEngine Ribbon tab was not created." }
    if (-not [bool]$probe.paletteVisible) { throw "GeoAutoEngine Palette was not visible." }
    if ($probe.SelectedFileCount -ne 2) { throw "Multi-CSV selection did not retain both files." }
    if ([string]$probe.Status -notlike "*Processamento conclu*") {
        throw "Core processing did not complete in the Palette. Status='$($probe.Status)' Summary='$($probe.Summary)'"
    }

    Write-Output "Civil 3D $CadVersion UI prototype smoke passed: Ribbon, Palette, Multi-CSV and Core processing."
}
finally {
    Remove-Item Env:GEOAUTOENGINE_UI_SMOKE_INPUTS -ErrorAction SilentlyContinue
    Remove-Item Env:GEOAUTOENGINE_UI_SMOKE_MARKER -ErrorAction SilentlyContinue
    Remove-Item Env:GEOAUTOENGINE_INITIALIZE_SMOKE_MARKER -ErrorAction SilentlyContinue
    Remove-Item Env:GEOAUTOENGINE_STARTUP_SMOKE_MARKER -ErrorAction SilentlyContinue
    if ($installedBySmoke -and (Test-Path -LiteralPath $bundleInstall)) {
        Remove-Item -LiteralPath $bundleInstall -Recurse -Force
    }
    if (Test-Path -LiteralPath $temporaryDirectory) {
        Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force
    }
}
