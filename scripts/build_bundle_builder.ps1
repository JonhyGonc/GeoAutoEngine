param(
    [ValidateSet("Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$dotnetSource = Join-Path $root ".dotnet"
$builderSource = Join-Path $root "tools\GeoAutoEngineBundleBuilder.cs"
$outputRoot = Join-Path $root "artifacts\bundle-builder"
$stagingRoot = Join-Path $outputRoot "staging"
$payloadRoot = Join-Path $stagingRoot "payload"
$archivePath = Join-Path $stagingRoot "GeoAutoEngine.builder.payload.zip"
$builderPath = Join-Path $outputRoot "GeoAutoEngine_BundleBuilder.exe"
$compiler = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"

foreach ($required in @(
    (Join-Path $dotnetSource "dotnet.exe"),
    $builderSource,
    $compiler,
    (Join-Path $root "Directory.Build.props"),
    (Join-Path $root "Directory.Build.targets"),
    (Join-Path $root "Directory.Packages.props")
)) {
    if (-not (Test-Path -LiteralPath $required)) {
        throw "Pré-requisito não encontrado: $required"
    }
}

if (Test-Path -LiteralPath $outputRoot) {
    Remove-Item -LiteralPath $outputRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $payloadRoot -Force | Out-Null

function Copy-TreeWithoutBuildOutput {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Source,
        [Parameter(Mandatory = $true)]
        [string]$Destination
    )

    New-Item -ItemType Directory -Path $Destination -Force | Out-Null

    Get-ChildItem -LiteralPath $Source -Force | ForEach-Object {
        if ($_.PSIsContainer) {
            if ($_.Name -notin @("bin", "obj", ".git", ".vs")) {
                Copy-TreeWithoutBuildOutput -Source $_.FullName -Destination (Join-Path $Destination $_.Name)
            }
        }
        else {
            Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $Destination $_.Name) -Force
        }
    }
}

Write-Host "Preparando .NET portátil..."
Copy-TreeWithoutBuildOutput -Source $dotnetSource -Destination (Join-Path $payloadRoot ".dotnet")

foreach ($directory in @("src", "assets", "build")) {
    Write-Host "Incluindo $directory..."
    Copy-TreeWithoutBuildOutput -Source (Join-Path $root $directory) -Destination (Join-Path $payloadRoot $directory)
}

foreach ($file in @("Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props")) {
    Copy-Item -LiteralPath (Join-Path $root $file) -Destination (Join-Path $payloadRoot $file) -Force
}

New-Item -ItemType Directory -Path $stagingRoot -Force | Out-Null

$payloadItems = @(
    (Join-Path $payloadRoot ".dotnet"),
    (Join-Path $payloadRoot "src"),
    (Join-Path $payloadRoot "assets"),
    (Join-Path $payloadRoot "build"),
    (Join-Path $payloadRoot "Directory.Build.props"),
    (Join-Path $payloadRoot "Directory.Build.targets"),
    (Join-Path $payloadRoot "Directory.Packages.props")
)

Write-Host "Compactando ambiente de compilação..."
Compress-Archive -LiteralPath $payloadItems -DestinationPath $archivePath -CompressionLevel Optimal

$compilerArguments = @(
    "/nologo",
    "/target:exe",
    "/platform:x64",
    "/optimize+",
    "/out:$builderPath",
    "/resource:$archivePath,GeoAutoEngine.builder.payload.zip",
    "/reference:System.IO.Compression.dll",
    "/reference:System.IO.Compression.FileSystem.dll",
    "/reference:System.Windows.Forms.dll",
    $builderSource
)

Write-Host "Gerando executável..."
& $compiler $compilerArguments
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $builderPath -PathType Leaf)) {
    throw "Falha ao gerar GeoAutoEngine_BundleBuilder.exe: $LASTEXITCODE"
}

$hash = (Get-FileHash -LiteralPath $builderPath -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText("$builderPath.sha256.txt", "$hash  GeoAutoEngine_BundleBuilder.exe`r`n", [Text.Encoding]::ASCII)

Remove-Item -LiteralPath $stagingRoot -Recurse -Force

Write-Host ""
Write-Host "Gerador criado com sucesso:" -ForegroundColor Green
Write-Host $builderPath
Write-Host ""
Write-Host "Leve somente GeoAutoEngine_BundleBuilder.exe para o computador que possui Civil 3D."
Write-Host "Ao executar, ele detectará a versão instalada e criará GeoAutoEngine.bundle na Área de Trabalho."