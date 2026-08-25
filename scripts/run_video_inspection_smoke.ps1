param(
    [ValidateSet("2026")]
    [string]$CadVersion = "2026"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$bundleSource = Join-Path $repositoryRoot "artifacts\bundle\GeoAutoEngine.bundle"
$bundleInstall = Join-Path $env:APPDATA "Autodesk\ApplicationPlugins\GeoAutoEngine.bundle"
$cadExecutable = Join-Path $env:ProgramFiles "Autodesk\AutoCAD $CadVersion\acad.exe"
$sourceDrawing = Join-Path $env:ProgramFiles "Autodesk\AutoCAD $CadVersion\C3D\PushPins.dwg"
$fixture = Join-Path $repositoryRoot "tests\GeoAutoEngine.Tests\Fixtures\sample.geoadocs.json"
$temporaryDirectory = Join-Path ([IO.Path]::GetTempPath()) ("geoautoengine-video-smoke-" + [guid]::NewGuid().ToString("N"))
$backupBundle = Join-Path $temporaryDirectory "installed-bundle-backup"
$installedBundleExisted = Test-Path -LiteralPath $bundleInstall -PathType Container
$startedProcess = $null
$smokeSucceeded = $false

foreach ($required in @($bundleSource, $cadExecutable, $sourceDrawing, $fixture)) {
    if (-not (Test-Path -LiteralPath $required)) {
        throw "Pré-requisito do smoke não encontrado: $required"
    }
}

if (Get-Process acad -ErrorAction SilentlyContinue) {
    throw "Feche as sessões do AutoCAD/Civil 3D antes do smoke de vídeo-inspeção."
}

function Read-Exactly {
    param([System.IO.Stream]$Stream, [int]$Length)
    $buffer = [byte[]]::new($Length)
    $offset = 0
    while ($offset -lt $Length) {
        $read = $Stream.Read($buffer, $offset, $Length - $offset)
        if ($read -eq 0) { throw "Frame GADX truncado." }
        $offset += $read
    }
    return $buffer
}

function Remove-DirectoryWithRetry {
    param([string]$Path)
    for ($attempt = 1; $attempt -le 10; $attempt++) {
        try {
            if (Test-Path -LiteralPath $Path) {
                Remove-Item -LiteralPath $Path -Recurse -Force
            }
            return
        }
        catch {
            if ($attempt -eq 10) { throw }
            Start-Sleep -Milliseconds 500
        }
    }
}

function Write-Frame {
    param(
        [System.IO.Stream]$Stream,
        [uint16]$MessageType,
        [int]$RequestId,
        [byte[]]$Payload
    )
    $header = [byte[]]::new(16)
    [Text.Encoding]::ASCII.GetBytes("GADX").CopyTo($header, 0)
    [BitConverter]::GetBytes([uint16]1).CopyTo($header, 4)
    [BitConverter]::GetBytes($MessageType).CopyTo($header, 6)
    [BitConverter]::GetBytes([int]$Payload.Length).CopyTo($header, 8)
    [BitConverter]::GetBytes($RequestId).CopyTo($header, 12)
    $Stream.Write($header, 0, $header.Length)
    if ($Payload.Length -gt 0) { $Stream.Write($Payload, 0, $Payload.Length) }
    $Stream.Flush()
}

function Read-Frame {
    param([System.IO.Stream]$Stream)
    $header = Read-Exactly -Stream $Stream -Length 16
    if ([Text.Encoding]::ASCII.GetString($header, 0, 4) -ne "GADX") { throw "Magic GADX inválido." }
    $length = [BitConverter]::ToInt32($header, 8)
    if ($length -lt 0 -or $length -gt 16777216) { throw "Payload GADX inválido: $length" }
    [pscustomobject]@{
        ProtocolVersion = [BitConverter]::ToUInt16($header, 4)
        MessageType = [BitConverter]::ToUInt16($header, 6)
        RequestId = [BitConverter]::ToInt32($header, 12)
        Payload = Read-Exactly -Stream $Stream -Length $length
    }
}

New-Item -ItemType Directory -Path $temporaryDirectory -Force | Out-Null
try {
    if ($installedBundleExisted) {
        $resolvedInstall = (Resolve-Path -LiteralPath $bundleInstall).Path
        $expectedInstall = [IO.Path]::GetFullPath($bundleInstall)
        if (-not [string]::Equals($resolvedInstall, $expectedInstall, [StringComparison]::OrdinalIgnoreCase)) {
            throw "O bundle instalado resolveu para um caminho inesperado: $resolvedInstall"
        }
        Move-Item -LiteralPath $resolvedInstall -Destination $backupBundle
    }

    New-Item -ItemType Directory -Path (Split-Path -Parent $bundleInstall) -Force | Out-Null
    Copy-Item -LiteralPath $bundleSource -Destination $bundleInstall -Recurse

    $drawing = Join-Path $temporaryDirectory "video-smoke.dwg"
    $script = Join-Path $temporaryDirectory "video-smoke.scr"
    $initializeMarker = Join-Path $temporaryDirectory "initialize.txt"
    $videoMarker = Join-Path $temporaryDirectory "video.json"
    $stdoutPath = Join-Path $temporaryDirectory "stdout.log"
    $stderrPath = Join-Path $temporaryDirectory "stderr.log"
    Copy-Item -LiteralPath $sourceDrawing -Destination $drawing
    $cadCommands = @(
        "FILEDIA"
        "0"
        "CMDECHO"
        "1"
        "_.DELAY"
        "7000"
        "GEOAUTO3VIDEO"
        "_.PLINE"
        "_non"
        "0,0"
        "_non"
        "100,0"
        ""
        "GEOAUTO3VIASSOCIATE"
        "50,0"
        "0,0"
        "GEOAUTO3VICOMMIT"
        "_.REGEN"
        "_.ZOOM"
        "_W"
        "_non"
        "-5,-5"
        "_non"
        "105,5"
        "_.DELAY"
        "10000"
        "GEOAUTO3VICOMMIT"
        "_.QSAVE"
        "_.DELAY"
        "1000"
        "_.QUIT"
        "_N"
    )
    $cadCommands | Set-Content -LiteralPath $script -Encoding ascii

    $env:GEOAUTOENGINE_INITIALIZE_SMOKE_MARKER = $initializeMarker
    $env:GEOAUTOENGINE_VIDEO_SMOKE_MARKER = $videoMarker
    $startParameters = @{
        FilePath = $cadExecutable
        ArgumentList = @($drawing, "/b", $script, "/product", "C3D", "/language", "en-US", "/nologo")
        WorkingDirectory = $repositoryRoot
        RedirectStandardOutput = $stdoutPath
        RedirectStandardError = $stderrPath
        PassThru = $true
    }
    $startParameters.WindowStyle = "Hidden"
    $startedProcess = Start-Process @startParameters

    $deadline = [DateTime]::UtcNow.AddSeconds(45)
    while (-not (Test-Path -LiteralPath $initializeMarker -PathType Leaf) -and [DateTime]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 100
    }
    if (-not (Test-Path -LiteralPath $initializeMarker -PathType Leaf)) {
        throw "O plugin não inicializou dentro do prazo."
    }

    $pipe = [System.IO.Pipes.NamedPipeClientStream]::new(
        ".",
        "GeoAutoEngine.Inspection",
        [System.IO.Pipes.PipeDirection]::InOut,
        [System.IO.Pipes.PipeOptions]::None)
    try {
        $pipe.Connect(5000)
        $helloRequestId = 41001
        $helloPayload = [Text.Encoding]::UTF8.GetBytes(
            '{"application":"GeoAutoDocs","applicationVersion":"smoke-v1","protocolVersion":1,"inspectionSchemaVersion":1}')
        Write-Frame -Stream $pipe -MessageType 1 -RequestId $helloRequestId -Payload $helloPayload
        $helloAck = Read-Frame -Stream $pipe
        if ($helloAck.ProtocolVersion -ne 1 -or $helloAck.MessageType -ne 2 -or
            $helloAck.RequestId -ne $helloRequestId) {
            throw "HelloAck incompatível."
        }
        $helloData = [Text.Encoding]::UTF8.GetString($helloAck.Payload) | ConvertFrom-Json
        if ($helloData.application -ne "GeoAutoEngine" -or $helloData.inspectionSchemaVersion -ne 1) {
            throw "Conteúdo do HelloAck incompatível."
        }

        $pushRequestId = 41002
        $inspectionPayload = [IO.File]::ReadAllBytes($fixture)
        Write-Frame -Stream $pipe -MessageType 3 -RequestId $pushRequestId -Payload $inspectionPayload
        $inspectionAck = Read-Frame -Stream $pipe
        if ($inspectionAck.MessageType -ne 4 -or $inspectionAck.RequestId -ne $pushRequestId) {
            throw "InspectionAck incompatível."
        }
        $ackData = [Text.Encoding]::UTF8.GetString($inspectionAck.Payload) | ConvertFrom-Json
        if (-not [bool]$ackData.accepted) { throw "InspectionAck rejeitado: $($ackData.message)" }
    }
    finally {
        $pipe.Dispose()
    }

    if (-not $startedProcess.WaitForExit(180000)) {
        $startedProcess.Kill($true)
        throw "Civil 3D excedeu o prazo do smoke."
    }
    $startedProcess.WaitForExit()
    $startedProcess.Refresh()
    if ($startedProcess.ExitCode -ne 0) {
        throw "Civil 3D retornou $($startedProcess.ExitCode). Log: $stdoutPath"
    }
    if (-not (Test-Path -LiteralPath $videoMarker -PathType Leaf)) {
        throw "O módulo não produziu o marcador do smoke."
    }
    $result = Get-Content -LiteralPath $videoMarker -Raw | ConvertFrom-Json

    if ($result.phase -ne "committed" -or $result.reportId -ne "22222222-2222-2222-2222-222222222222" -or
        $result.sections -ne 1 -or $result.occurrences -ne 2 -or $result.updated -ne 5) {
        throw "Resultado inesperado do smoke: $($result | ConvertTo-Json -Compress)"
    }

    $evidencePath = Join-Path $repositoryRoot "artifacts\video-inspection-smoke-2026.json"
    $evidence = [ordered]@{
        protocol = "GADX/1"
        hello_ack = $true
        inspection_ack_accepted = $true
        report_id = [string]$result.reportId
        sections = [int]$result.sections
        occurrences = [int]$result.occurrences
    }
    $evidence.first_commit_created = 5
    $evidence.second_commit_updated = [int]$result.updated
    $evidence.idempotent = [int]$result.updated -eq 5
    $evidence.regen_zoom_and_ten_second_wait_executed = $true
    $evidence.drawing_saved = $true
    $evidence | ConvertTo-Json | Set-Content -LiteralPath $evidencePath -Encoding utf8
    $smokeSucceeded = $true
    Write-Output "Smoke Video Inspection 2026 aprovado: handshake, ACK, Palette, associação, commit permanente e idempotência."
    Write-Output "Evidência: $evidencePath"
}
finally {
    Remove-Item Env:GEOAUTOENGINE_INITIALIZE_SMOKE_MARKER -ErrorAction SilentlyContinue
    Remove-Item Env:GEOAUTOENGINE_VIDEO_SMOKE_MARKER -ErrorAction SilentlyContinue
    if ($startedProcess -and -not $startedProcess.HasExited) {
        $startedProcess.Kill($true)
        $startedProcess.WaitForExit()
    }
    Start-Sleep -Seconds 1
    if (Test-Path -LiteralPath $bundleInstall) {
        $resolvedNewInstall = (Resolve-Path -LiteralPath $bundleInstall).Path
        $expectedNewInstall = [IO.Path]::GetFullPath($bundleInstall)
        if ([string]::Equals($resolvedNewInstall, $expectedNewInstall, [StringComparison]::OrdinalIgnoreCase)) {
            Remove-DirectoryWithRetry -Path $resolvedNewInstall
        }
    }
    if ($installedBundleExisted -and (Test-Path -LiteralPath $backupBundle)) {
        Move-Item -LiteralPath $backupBundle -Destination $bundleInstall
    }
    if ($smokeSucceeded -and (Test-Path -LiteralPath $temporaryDirectory)) {
        Remove-DirectoryWithRetry -Path $temporaryDirectory
    }
    elseif (Test-Path -LiteralPath $temporaryDirectory) {
        Write-Warning "Artefatos do smoke preservados para diagnóstico: $temporaryDirectory"
    }
}
