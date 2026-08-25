param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("fast", "regression", "legacy", "cad", "full")]
    [string]$Suite,

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

# Compatibilidade com os comandos e evidências documentados na Etapa 1.
& (Join-Path $PSScriptRoot "test.ps1") -Suite $Suite -Configuration $Configuration
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
