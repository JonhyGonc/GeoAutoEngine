param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$dotnet = Join-Path $repositoryRoot ".dotnet\dotnet.exe"
$pluginProject = Join-Path $repositoryRoot "src\GeoAutoEngine.Plugin\GeoAutoEngine.Plugin.csproj"
$bundleSource = Join-Path $repositoryRoot "artifacts\bundle\GeoAutoEngine.bundle"
$applicationPlugins = Join-Path $env:APPDATA "Autodesk\ApplicationPlugins"
$bundleInstall = Join-Path $applicationPlugins "GeoAutoEngine.bundle"
$cadExecutable = Join-Path $env:ProgramFiles "Autodesk\AutoCAD 2026\acad.exe"
$sourceDrawing = Join-Path $env:ProgramFiles "Autodesk\AutoCAD 2026\C3D\PushPins.dwg"
$evidenceDirectory = Join-Path $repositoryRoot "docs\evidence\stage1\cad"

$env:DOTNET_CLI_HOME = Join-Path $repositoryRoot ".dotnet_cli_home"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_NOLOGO = "1"
$preexistingCadProcessIds = @(Get-Process acad -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id)

function Read-ConsoleLog {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return ""
    }

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $probeLength = [Math]::Min(100, $bytes.Length)
    if ($probeLength -gt 0 -and $bytes[0..($probeLength - 1)] -contains 0) {
        return [System.Text.Encoding]::Unicode.GetString($bytes)
    }

    return [System.Text.Encoding]::UTF8.GetString($bytes)
}

foreach ($required in @($dotnet, $pluginProject, $cadExecutable, $sourceDrawing)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Required file not found: $required"
    }
}

& $dotnet build $pluginProject --configuration $Configuration --no-restore --nologo
if ($LASTEXITCODE -ne 0) {
    throw "Plugin build failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path -LiteralPath $bundleSource -PathType Container)) {
    throw "Bundle output not found: $bundleSource"
}

$forbiddenAssemblies = Get-ChildItem -LiteralPath (Join-Path $bundleSource "Contents\Windows") -File |
    Where-Object { $_.Name -in @("AcCoreMgd.dll", "AcDbMgd.dll", "AcMgd.dll", "AcWindows.dll", "AdWindows.dll", "AeccDbMgd.dll") }
if ($forbiddenAssemblies) {
    throw "Autodesk assemblies must not be copied into the bundle: $($forbiddenAssemblies.Name -join ', ')"
}

if (Test-Path -LiteralPath $bundleInstall) {
    throw "Refusing to overwrite an existing installed bundle: $bundleInstall"
}

New-Item -ItemType Directory -Path $applicationPlugins -Force | Out-Null
New-Item -ItemType Directory -Path $evidenceDirectory -Force | Out-Null
Copy-Item -LiteralPath $bundleSource -Destination $bundleInstall -Recurse

$results = @()
try {
    foreach ($product in @("ACAD", "C3D")) {
        $temporaryDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ("geoautoengine-stage1-" + $product.ToLowerInvariant() + "-" + [guid]::NewGuid().ToString("N"))
        New-Item -ItemType Directory -Path $temporaryDirectory | Out-Null
        try {
            $drawing = Join-Path $temporaryDirectory "plugin-smoke.dwg"
            $scriptBase = Join-Path $temporaryDirectory "plugin-smoke"
            $script = "$scriptBase.scr"
            $marker = Join-Path $temporaryDirectory "plugin-smoke.json"
            $stdoutPath = Join-Path $temporaryDirectory "stdout.log"
            $stderrPath = Join-Path $temporaryDirectory "stderr.log"
            Copy-Item -LiteralPath $sourceDrawing -Destination $drawing
            @(
                "FILEDIA"
                "0"
                "CMDECHO"
                "1"
                "GEOAUTO3PING"
                "_.QUIT"
                "_N"
            ) | Set-Content -LiteralPath $script -Encoding ascii

            $drawingHashBefore = (Get-FileHash -LiteralPath $drawing -Algorithm SHA256).Hash.ToLowerInvariant()
            $env:GEOAUTOENGINE_STAGE1_SMOKE_MARKER = $marker
            $arguments = @(
                $drawing,
                "/b", $script,
                "/product", $product,
                "/language", "en-US",
                "/nologo"
            )
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
                throw "AutoCAD desktop host timed out for $product."
            }
            $process.WaitForExit()
            $process.Refresh()
            $durationSeconds = ((Get-Date) - $started).TotalSeconds
            $exitCode = [int]$process.ExitCode
            Remove-Item Env:GEOAUTOENGINE_STAGE1_SMOKE_MARKER -ErrorAction SilentlyContinue

            $logPath = Join-Path $evidenceDirectory ("plugin_smoke_2026_" + $product.ToLowerInvariant() + ".log")
            $combinedLog = (Read-ConsoleLog $stdoutPath) + (Read-ConsoleLog $stderrPath)
            Set-Content -LiteralPath $logPath -Value $combinedLog -Encoding utf8

            if ($exitCode -ne 0) {
                throw "AutoCAD desktop host returned $exitCode for $product; inspect $logPath."
            }
            if (-not (Test-Path -LiteralPath $marker -PathType Leaf)) {
                throw "Plugin marker was not created for $product; bundle command was not loaded. Inspect $logPath."
            }

            $probe = Get-Content -LiteralPath $marker -Raw | ConvertFrom-Json
            $rawMarkerEvidence = Join-Path $evidenceDirectory ("plugin_probe_2026_" + $product.ToLowerInvariant() + ".json")
            Get-Content -LiteralPath $marker -Raw | Set-Content -LiteralPath $rawMarkerEvidence -Encoding utf8
            $drawingHashAfter = (Get-FileHash -LiteralPath $drawing -Algorithm SHA256).Hash.ToLowerInvariant()
            $preexistingSessionsPreserved = @($preexistingCadProcessIds | Where-Object {
                Get-Process -Id $_ -ErrorAction SilentlyContinue
            }).Count -eq $preexistingCadProcessIds.Count
            $verification = [ordered]@{
                command_executed = $probe.command -eq "GEOAUTO3PING"
                acadver_is_25_1 = [string]$probe.AcadVersion -like "25.1*"
                dbmod_unchanged_during_plugin_load = [bool]$probe.DatabaseModificationStateUnchanged
                autodesk_assemblies_absent_from_bundle = $true
                preexisting_sessions_preserved = $preexistingSessionsPreserved
            }
            $failedChecks = @($verification.GetEnumerator() | Where-Object { -not [bool]$_.Value } | ForEach-Object { $_.Key })
            $failureEvidence = Join-Path $evidenceDirectory ("plugin_smoke_failure_2026_" + $product.ToLowerInvariant() + ".json")
            if ($failedChecks.Count -gt 0) {
                [ordered]@{
                    verification = $verification
                    drawing_sha256_before = $drawingHashBefore
                    drawing_sha256_after = $drawingHashAfter
                } | ConvertTo-Json -Depth 4 | Set-Content `
                    -LiteralPath $failureEvidence `
                    -Encoding utf8
                throw "Plugin smoke verification failed for $product ($($failedChecks -join ', ')); inspect $rawMarkerEvidence and $logPath."
            }
            if (Test-Path -LiteralPath $failureEvidence) {
                Remove-Item -LiteralPath $failureEvidence -Force
            }

            $markerEvidence = Join-Path $evidenceDirectory ("plugin_smoke_2026_" + $product.ToLowerInvariant() + ".json")
            $report = [ordered]@{
                schema_version = 1
                captured_at_utc = [DateTime]::UtcNow.ToString("o")
                host = [ordered]@{
                    version = "2026"
                    product = $product
                    executable = $cadExecutable
                }
                plugin = [ordered]@{
                    bundle = "GeoAutoEngine.bundle"
                    command = "GEOAUTO3PING"
                    acadver = [string]$probe.AcadVersion
                    dbmod_at_initialize = $probe.DatabaseModificationStateAtInitialize
                    dbmod_at_command = $probe.DatabaseModificationStateAtCommand
                }
                verification = $verification
                drawing_sha256_before = $drawingHashBefore
                drawing_sha256_after = $drawingHashAfter
                host_session_rewrote_dwg_file = $drawingHashBefore -ne $drawingHashAfter
                preexisting_session_ids = $preexistingCadProcessIds
                timing_seconds = [Math]::Round($durationSeconds, 6)
                log_sha256 = (Get-FileHash -LiteralPath $logPath -Algorithm SHA256).Hash.ToLowerInvariant()
            }
            $report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $markerEvidence -Encoding utf8
            $results += $report
        }
        finally {
            Remove-Item Env:GEOAUTOENGINE_STAGE1_SMOKE_MARKER -ErrorAction SilentlyContinue
            if (Test-Path -LiteralPath $temporaryDirectory) {
                Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force
            }
        }
    }

    $summaryPath = Join-Path $evidenceDirectory "PLUGIN_SMOKE_SUMMARY.json"
    [ordered]@{
        schema_version = 1
        target = "AutoCAD 2026 / Civil 3D 2026"
        results = $results
    } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $summaryPath -Encoding utf8
    Write-Output "CAD plugin smoke passed for AutoCAD 2026 and Civil 3D 2026."
}
finally {
    Remove-Item Env:GEOAUTOENGINE_STAGE1_SMOKE_MARKER -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $bundleInstall) {
        $resolvedInstall = (Resolve-Path -LiteralPath $bundleInstall).Path
        $expectedInstall = [System.IO.Path]::GetFullPath($bundleInstall)
        if (-not [string]::Equals($resolvedInstall, $expectedInstall, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing cleanup because installed bundle path did not resolve exactly: $resolvedInstall"
        }
        Remove-Item -LiteralPath $resolvedInstall -Recurse -Force
    }
}
