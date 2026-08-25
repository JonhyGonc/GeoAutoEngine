param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("2024", "2025", "2026")]
    [string]$Version,

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [switch]$SkipBuild,

    [string]$AutoCAD2024Directory = "$env:ProgramFiles\Autodesk\AutoCAD 2024",
    [string]$AutoCAD2025Directory = "$env:ProgramFiles\Autodesk\AutoCAD 2025",
    [string]$AutoCAD2026Directory = "$env:ProgramFiles\Autodesk\AutoCAD 2026"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$dotnet = Join-Path $repositoryRoot ".dotnet\dotnet.exe"
$pluginProject = Join-Path $repositoryRoot "src\GeoAutoEngine.Plugin\GeoAutoEngine.Plugin.csproj"
$bundleSource = Join-Path $repositoryRoot "artifacts\bundle\GeoAutoEngine.bundle"
$sourceHost = Join-Path $bundleSource "Contents\$Version"
$pluginsDirectory = Join-Path $env:APPDATA "Autodesk\ApplicationPlugins"
$bundleInstall = Join-Path $pluginsDirectory "GeoAutoEngine.bundle"
$installedContents = Join-Path $bundleInstall "Contents"
$installedHost = Join-Path $installedContents $Version
$objectArx2024Directory = Join-Path $env:SystemDrive "Autodesk\ObjectARX_for_AutoCAD_2024_Win_64bit_dlm\inc"

if (-not (Test-Path -LiteralPath (Join-Path $AutoCAD2024Directory "AcCoreMgd.dll")) -and
    (Test-Path -LiteralPath (Join-Path $objectArx2024Directory "AcCoreMgd.dll"))) {
    $AutoCAD2024Directory = $objectArx2024Directory
}

if (Get-Process acad -ErrorAction SilentlyContinue) {
    throw "Feche o AutoCAD e o Civil 3D antes de executar o deploy."
}

foreach ($requiredFile in @($dotnet, $pluginProject)) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "Arquivo necessário não encontrado: $requiredFile"
    }
}

$env:DOTNET_CLI_HOME = Join-Path $repositoryRoot ".dotnet_cli_home"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_NOLOGO = "1"

if (-not $SkipBuild) {
    $properties = @(
        "-p:AutodeskAutoCADVersion=$Version",
        "-p:AutodeskAutoCAD2024Directory=$AutoCAD2024Directory",
        "-p:AutodeskAutoCAD2025Directory=$AutoCAD2025Directory",
        "-p:AutodeskAutoCAD2026Directory=$AutoCAD2026Directory"
    )
    & $dotnet restore $pluginProject --nologo @properties
    if ($LASTEXITCODE -ne 0) { throw "Restore do host $Version falhou: $LASTEXITCODE" }

    & $dotnet build $pluginProject --configuration $Configuration --no-restore --nologo --no-incremental @properties
    if ($LASTEXITCODE -ne 0) { throw "Build do host $Version falhou: $LASTEXITCODE" }
}

foreach ($requiredPath in @(
    (Join-Path $bundleSource "PackageContents.xml"),
    (Join-Path $sourceHost "GeoAutoEngine.Plugin.dll")
)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Artefato do host $Version não encontrado: $requiredPath"
    }
}

New-Item -ItemType Directory -Path $pluginsDirectory -Force | Out-Null
if (-not (Test-Path -LiteralPath $bundleInstall)) {
    # Bootstrap only: install common metadata/assets and the requested host. Do
    # not introduce artifacts for the other CAD releases during a one-host deploy.
    New-Item -ItemType Directory -Path $bundleInstall -Force | Out-Null
    New-Item -ItemType Directory -Path $installedContents -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $bundleSource "PackageContents.xml") -Destination $bundleInstall
    $sourceAssets = Join-Path $bundleSource "Contents\Assets"
    if (Test-Path -LiteralPath $sourceAssets) {
        Copy-Item -LiteralPath $sourceAssets -Destination $installedContents -Recurse
    }
}

# Common bundle metadata must follow every host deploy. Otherwise an existing
# installation can keep removed commands or stale runtime requirements even
# when the managed assemblies were updated successfully.
Copy-Item -LiteralPath (Join-Path $bundleSource "PackageContents.xml") `
    -Destination $bundleInstall `
    -Force

New-Item -ItemType Directory -Path $installedContents -Force | Out-Null
if (Test-Path -LiteralPath $installedHost) {
    $backupHost = "$installedHost.backup-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
    Move-Item -LiteralPath $installedHost -Destination $backupHost
}

try {
    Copy-Item -LiteralPath $sourceHost -Destination $installedHost -Recurse
}
catch {
    if ((-not (Test-Path -LiteralPath $installedHost)) -and $backupHost) {
        Move-Item -LiteralPath $backupHost -Destination $installedHost
    }
    throw
}

Write-Host "Deploy $Version concluído: $installedHost" -ForegroundColor Green
if ($backupHost) {
    Write-Host "Backup do host ${Version}: $backupHost"
}
