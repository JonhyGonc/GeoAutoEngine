param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("fast", "regression", "legacy", "cad", "full")]
    [string]$Suite,

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$workspaceRoot = Split-Path -Parent $repositoryRoot
$dotnet = Join-Path $repositoryRoot ".dotnet\dotnet.exe"
$testProject = Join-Path $repositoryRoot "tests\GeoAutoEngine.Tests\GeoAutoEngine.Tests.csproj"
$solution = Join-Path $repositoryRoot "GeoAutoEngine.sln"

if (-not (Test-Path -LiteralPath $dotnet)) {
    throw "Project-local .NET SDK not found: $dotnet"
}

$env:DOTNET_CLI_HOME = Join-Path $repositoryRoot ".dotnet_cli_home"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_NOLOGO = "1"

function Invoke-Checked {
    param([scriptblock]$Command)
    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "Test command failed with exit code $LASTEXITCODE."
    }
}

function Invoke-DotNetFast {
    Invoke-Checked {
        & $dotnet test $testProject `
            --configuration $Configuration `
            --no-restore `
            --filter "Category=FAST" `
            --nologo `
            --logger "console;verbosity=minimal"
    }
}

function Invoke-DotNetRegression {
    Invoke-Checked { & $dotnet build $solution --configuration $Configuration --no-restore --nologo }
    Invoke-Checked {
        & $dotnet test $testProject `
            --configuration $Configuration `
            --no-build `
            --nologo `
            --logger "console;verbosity=minimal"
    }
}

function Invoke-Legacy {
    Invoke-Checked { & (Join-Path $workspaceRoot "GeoAutoEngine.Legacy\scripts\test_suites.ps1") -Suite legacy }
}

function Invoke-LegacyFull {
    Invoke-Checked { & (Join-Path $workspaceRoot "GeoAutoEngine.Legacy\scripts\test_suites.ps1") -Suite full }
}

function Invoke-Cad {
    Invoke-Checked {
        & (Join-Path $repositoryRoot "scripts\run_stage4_cad_smoke.ps1") -Configuration Release
    }
}

Push-Location $repositoryRoot
try {
    switch ($Suite) {
        "fast" { Invoke-DotNetFast }
        "regression" { Invoke-DotNetRegression }
        "legacy" { Invoke-Legacy }
        "cad" { Invoke-Cad }
        "full" {
            $Configuration = "Release"
            Invoke-Checked { & $dotnet restore $solution --nologo }
            Invoke-DotNetRegression
            Invoke-LegacyFull
            Invoke-Cad
        }
    }
}
finally {
    Pop-Location
}
