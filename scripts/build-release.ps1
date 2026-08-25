param(
    [ValidateSet("Release")]
    [string]$Configuration = "Release",

    [ValidateSet("2024", "2025", "2026")]
    [string[]]$Versions = @("2024", "2025", "2026"),

    [string]$AutoCAD2024Directory = "$env:ProgramFiles\Autodesk\AutoCAD 2024",
    [string]$AutoCAD2025Directory = "$env:ProgramFiles\Autodesk\AutoCAD 2025",
    [string]$AutoCAD2026Directory = "$env:ProgramFiles\Autodesk\AutoCAD 2026"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$dotnet = Join-Path $root ".dotnet\dotnet.exe"
$solution = Join-Path $root "GeoAutoEngine.sln"
$pluginProject = Join-Path $root "src\GeoAutoEngine.Plugin\GeoAutoEngine.Plugin.csproj"
$testProject = Join-Path $root "tests\GeoAutoEngine.Tests\GeoAutoEngine.Tests.csproj"
$installerSource = Join-Path $root "tools\GeoAutoEngineInstaller.cs"
$releaseRoot = Join-Path $root "artifacts\release"
$bundleRoot = Join-Path $releaseRoot "bundle\GeoAutoEngine.bundle"
$installerDirectory = Join-Path $releaseRoot "installer"
$installerPath = Join-Path $installerDirectory "GeoAutoEngine3_Setup.exe"
$stagingDirectory = Join-Path $releaseRoot "staging"
$archivePath = Join-Path $stagingDirectory "GeoAutoEngine.bundle.zip"
$compiler = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$defaultObjectArx2024 = Join-Path $env:SystemDrive "Autodesk\ObjectARX_for_AutoCAD_2024_Win_64bit_dlm\inc"
if (-not (Test-Path -LiteralPath (Join-Path $AutoCAD2024Directory "AcCoreMgd.dll")) -and
    (Test-Path -LiteralPath (Join-Path $defaultObjectArx2024 "AcCoreMgd.dll"))) {
    $AutoCAD2024Directory = $defaultObjectArx2024
}
$cadDirectories = @{
    "2024" = $AutoCAD2024Directory
    "2025" = $AutoCAD2025Directory
    "2026" = $AutoCAD2026Directory
}

$env:DOTNET_CLI_HOME = Join-Path $root ".dotnet_cli_home"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_NOLOGO = "1"

foreach ($required in @($dotnet, $solution, $pluginProject, $testProject, $installerSource, $compiler)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Pré-requisito não encontrado: $required"
    }
}

foreach ($version in $Versions) {
    foreach ($assembly in @("AcCoreMgd.dll", "AcDbMgd.dll", "AcMgd.dll", "AcWindows.dll", "AdWindows.dll")) {
        $reference = Join-Path $cadDirectories[$version] $assembly
        if (-not (Test-Path -LiteralPath $reference -PathType Leaf)) {
            throw "SDK/assemblies oficiais do AutoCAD $version ausentes: $reference"
        }
    }
}

$resolvedRoot = [IO.Path]::GetFullPath($root)
$resolvedRelease = [IO.Path]::GetFullPath($releaseRoot)
if (-not $resolvedRelease.StartsWith($resolvedRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "A pasta de Release precisa permanecer dentro do projeto."
}

if (Test-Path -LiteralPath $releaseRoot) {
    Remove-Item -LiteralPath $releaseRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $bundleRoot -Force | Out-Null

& $dotnet restore $solution --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet restore falhou: $LASTEXITCODE" }
& $dotnet test $testProject --configuration $Configuration --no-restore --nologo --logger "console;verbosity=minimal"
if ($LASTEXITCODE -ne 0) { throw "Testes C# falharam: $LASTEXITCODE" }

foreach ($version in $Versions) {
    $properties = @(
        "-p:AutodeskAutoCADVersion=$version",
        "-p:AutodeskAutoCAD2024Directory=$AutoCAD2024Directory",
        "-p:AutodeskAutoCAD2025Directory=$AutoCAD2025Directory",
        "-p:AutodeskAutoCAD2026Directory=$AutoCAD2026Directory",
        "-p:BundleRoot=$bundleRoot"
    )
    & $dotnet restore $pluginProject --nologo @properties
    if ($LASTEXITCODE -ne 0) { throw "Restore do host $version falhou: $LASTEXITCODE" }
    & $dotnet build $pluginProject --configuration $Configuration --no-restore --nologo --no-incremental @properties
    if ($LASTEXITCODE -ne 0) { throw "Build do host $version falhou: $LASTEXITCODE" }
}

if ($Versions.Count -ne 3) {
    Write-Output "Build parcial concluído: $($Versions -join ', '). O instalador exige os três hosts."
    exit 0
}

foreach ($version in @("2024", "2025", "2026")) {
    $hostPath = Join-Path $bundleRoot "Contents\$version\GeoAutoEngine.Plugin.dll"
    if (-not (Test-Path -LiteralPath $hostPath -PathType Leaf)) {
        throw "Host $version ausente no bundle: $hostPath"
    }
}

$forbiddenNames = @("AcCoreMgd.dll", "AcDbMgd.dll", "AcMgd.dll", "AcWindows.dll", "AdWindows.dll", "AeccDbMgd.dll", "AecBaseMgd.dll")
$forbidden = Get-ChildItem -LiteralPath $bundleRoot -Recurse -File |
    Where-Object { $_.Name -in $forbiddenNames }
if ($forbidden) {
    throw "Assemblies Autodesk não podem ser distribuídas: $($forbidden.FullName -join ', ')"
}

[xml]$manifest = Get-Content -LiteralPath (Join-Path $bundleRoot "PackageContents.xml") -Raw
$series = @($manifest.ApplicationPackage.Components.RuntimeRequirements | ForEach-Object { $_.SeriesMin })
if (($series -join ',') -ne "R24.3,R25.0,R25.1") {
    throw "RuntimeRequirements inválido: $($series -join ',')"
}

New-Item -ItemType Directory -Path $installerDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $stagingDirectory -Force | Out-Null
Compress-Archive -LiteralPath $bundleRoot -DestinationPath $archivePath -CompressionLevel Optimal

$compilerArguments = @(
    "/nologo",
    "/target:winexe",
    "/platform:x64",
    "/optimize+",
    "/out:$installerPath",
    "/resource:$archivePath,GeoAutoEngine.bundle.zip",
    "/reference:System.Windows.Forms.dll",
    "/reference:System.IO.Compression.dll",
    "/reference:System.IO.Compression.FileSystem.dll",
    $installerSource
)
& $compiler $compilerArguments
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $installerPath -PathType Leaf)) {
    throw "Compilação do instalador falhou: $LASTEXITCODE"
}

$hash = (Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText("$installerPath.sha256.txt", "$hash  GeoAutoEngine3_Setup.exe`r`n", [Text.Encoding]::ASCII)
Remove-Item -LiteralPath $stagingDirectory -Recurse -Force

Write-Output "Bundle: $bundleRoot"
Write-Output "Instalador: $installerPath"
Write-Output "SHA256: $hash"
