param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$referenceRoot = Join-Path $repositoryRoot 'reference'
$manifestPath = Join-Path $referenceRoot 'SHA256SUMS.csv'
$dataDirectories = @('legacy_csv', 'legacy_outputs', 'legacy_dwgs', 'regression_data')

$rows = foreach ($directory in $dataDirectories) {
    $root = Join-Path $referenceRoot $directory
    if (-not (Test-Path -LiteralPath $root)) { continue }
    Get-ChildItem -LiteralPath $root -File -Recurse | Sort-Object FullName | ForEach-Object {
        [pscustomobject]@{
            Path = $_.FullName.Substring($referenceRoot.Length + 1).Replace('\', '/')
            Bytes = $_.Length
            Sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    }
}

$rows | Export-Csv -LiteralPath $manifestPath -NoTypeInformation -Encoding utf8
Write-Host "Reference manifest: $manifestPath ($($rows.Count) files)"
