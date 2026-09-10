using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using GeoAutoEngine.Application.VideoInspection;

namespace GeoAutoEngine.Plugin.VideoInspection.Transport;

internal sealed record ReceivedInspection(
    InspectionExchangeDocument Document,
    int RequestId,
    DateTime ReceivedAtUtc,
    string Source);

internal sealed class NamedPipeInspectionServer : IDisposable
{
    private readonly object _sync = new();
    private readonly InspectionExchangeSerializer _serializer;
    private readonly Action<ReceivedInspection> _accept;
    private readonly string _applicationVersion;
    private readonly string _cadProduct;
    private readonly string _cadVersion;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly ConcurrentDictionary<int, TaskCompletionSource<InspectionCorrectionAcknowledgement>>
        _pendingCorrectionAcks = new();
    private CancellationTokenSource? _cancellation;
    private Task? _listenerTask;
    private NamedPipeServerStream? _activePipe;
    private bool _connectionReady;
    private int _nextServerRequestId = 100000;

    internal static NamedPipeInspectionServer? Current { get; private set; }

    public NamedPipeInspectionServer(
        InspectionExchangeSerializer serializer,
        Action<ReceivedInspection> accept,
        string applicationVersion,
        string cadProduct,
        string cadVersion)
    {
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _accept = accept ?? throw new ArgumentNullException(nameof(accept));
        _applicationVersion = applicationVersion;
        _cadProduct = cadProduct;
        _cadVersion = cadVersion;
    }

    public bool IsCorrectionClientConnected
    {
        get
        {
            lock (_sync)
            {
                return _connectionReady && _activePipe?.IsConnected == true;
            }
        }
    }

    public void Start()
    {
        lock (_sync)
        {
            if (_listenerTask is not null)
            {
                return;
            }

            _cancellation = new CancellationTokenSource();
            _listenerTask = ListenAsync(_cancellation.Token);
            Current = this;
        }

        PluginLog.Information($"Named Pipe {InspectionPipeProtocol.DefaultPipeName} iniciado.");
    }

    public void Stop()
    {
        Task? listener;
        lock (_sync)
        {
            if (_listenerTask is null)
            {
                if (ReferenceEquals(Current, this))
                {
                    Current = null;
                }

                return;
            }

            _cancellation!.Cancel();
            _activePipe?.Dispose();
            _connectionReady = false;
            listener = _listenerTask;
            _listenerTask = null;
            if (ReferenceEquals(Current, this))
            {
                Current = null;
            }
        }

        FailPendingCorrections(new OperationCanceledException("O Named Pipe foi encerrado."));
        try
        {
            listener.GetAwaiter().GetResult();
        }
        catch (Exception exception) when (exception is OperationCanceledException or IOException or ObjectDisposedException)
        {
            // Cancellation and disposal are the expected listener shutdown path.
        }
        catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
        {
            PluginLog.Error("Encerramento do listener de inspeções", exception);
        }
        finally
        {
            lock (_sync)
            {
                _activePipe = null;
                _connectionReady = false;
                _cancellation?.Dispose();
                _cancellation = null;
            }
        }

        PluginLog.Information($"Named Pipe {InspectionPipeProtocol.DefaultPipeName} encerrado.");
    }

    public async Task<InspectionCorrectionAcknowledgement> SendCorrectionsAsync(
        InspectionCorrectionPush corrections,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(corrections);
        if (corrections.Corrections.Count == 0)
        {
            throw new InvalidOperationException("Não existem correções para enviar ao GeoAutoDocs.");
        }

        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        NamedPipeServerStream pipe;
        lock (_sync)
        {
            if (!_connectionReady || _activePipe?.IsConnected != true)
            {
                throw new InvalidOperationException(
                    "O GeoAutoDocs não está conectado para receber correções. " +
                    "Abra o relatório no GeoAutoDocs e mantenha a conexão com o GeoAutoEngine ativa.");
            }

            pipe = _activePipe;
        }

        var requestId = Interlocked.Increment(ref _nextServerRequestId);
        var completion = new TaskCompletionSource<InspectionCorrectionAcknowledgement>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pendingCorrectionAcks.TryAdd(requestId, completion))
        {
            throw new InvalidOperationException("Não foi possível reservar a solicitação de correção.");
        }

        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellation.CancelAfter(timeout);
        using var registration = timeoutCancellation.Token.Register(() =>
            completion.TrySetCanceled(timeoutCancellation.Token));

        try
        {
            await WriteFrameAsync(
                pipe,
                InspectionPipeMessageType.CorrectionPush,
                requestId,
                InspectionPipeProtocol.SerializePayload(corrections),
                cancellationToken).ConfigureAwait(false);
            PluginLog.Information(
                $"CorrectionPush enviado requestId={requestId}; ReportId={corrections.ReportId:D}; " +
                $"correções={corrections.Corrections.Count}.");

            try
            {
                return await completion.Task.ConfigureAwait(false);
            }
            catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(
                    "O GeoAutoDocs não respondeu ao envio das correções dentro do tempo esperado.",
                    exception);
            }
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
            throw new InvalidOperationException(
                "A conexão com o GeoAutoDocs foi encerrada durante o envio das correções.",
                exception);
        }
        finally
        {
            _pendingCorrectionAcks.TryRemove(requestId, out _);
        }
    }

    public void Dispose()
    {
        Stop();
        _writeGate.Dispose();
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            NamedPipeServerStream? pipe = null;
            try
            {
                pipe = new NamedPipeServerStream(
                    InspectionPipeProtocol.DefaultPipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);
                lock (_sync)
                {
                    _activePipe = pipe;
                    _connectionReady = false;
                }

                await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                await ProcessConnectionAsync(pipe, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (IOException exception)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    PluginLog.Error("Falha de I/O no listener de inspeções", exception);
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
            {
                PluginLog.Error("Falha no listener de inspeções", exception);
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                lock (_sync)
                {
                    if (ReferenceEquals(_activePipe, pipe))
                    {
                        _activePipe = null;
                        _connectionReady = false;
                    }
                }

                FailPendingCorrections(new IOException("A conexão com o GeoAutoDocs foi encerrada."));
                pipe?.Dispose();
            }
        }
    }

    private async Task ProcessConnectionAsync(
        NamedPipeServerStream pipe,
        CancellationToken cancellationToken)
    {
        InspectionPipeFrame helloFrame;
        try
        {
            helloFrame = await InspectionPipeProtocol.ReadFrameAsync(pipe, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is InvalidDataException or EndOfStreamException)
        {
            PluginLog.Error("Handshake GADX inválido", exception);
            return;
        }

        if (helloFrame.ProtocolVersion != InspectionPipeProtocol.CurrentVersion)
        {
            await WriteErrorAsync(
                pipe,
                helloFrame.RequestId,
                "IncompatibleVersion",
                $"Protocolo GADX {helloFrame.ProtocolVersion} não é suportado.",
                cancellationToken).ConfigureAwait(false);
            return;
        }

        if (helloFrame.MessageType != InspectionPipeMessageType.Hello)
        {
            await WriteErrorAsync(
                pipe,
                helloFrame.RequestId,
                "CommunicationError",
                $"Mensagem inicial inesperada: {helloFrame.MessageType}.",
                cancellationToken).ConfigureAwait(false);
            return;
        }

        InspectionHello hello;
        try
        {
            hello = InspectionPipeProtocol.DeserializePayload<InspectionHello>(helloFrame.Payload);
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException)
        {
            await WriteErrorAsync(
                pipe,
                helloFrame.RequestId,
                "InvalidPayload",
                "Handshake GADX inválido.",
                cancellationToken).ConfigureAwait(false);
            PluginLog.Error($"Handshake inválido requestId={helloFrame.RequestId}", exception);
            return;
        }

        if (hello.ProtocolVersion != InspectionPipeProtocol.CurrentVersion ||
            hello.InspectionSchemaVersion != InspectionExchangeSchema.CurrentVersion)
        {
            await WriteErrorAsync(
                pipe,
                helloFrame.RequestId,
                "IncompatibleVersion",
                $"Compatibilidade requerida: protocolo {InspectionPipeProtocol.CurrentVersion}, " +
                $"schema {InspectionExchangeSchema.CurrentVersion}.",
                cancellationToken).ConfigureAwait(false);
            return;
        }

        var helloAck = new InspectionHelloAck
        {
            Application = "GeoAutoEngine",
            ApplicationVersion = _applicationVersion,
            ProtocolVersion = InspectionPipeProtocol.CurrentVersion,
            InspectionSchemaVersion = InspectionExchangeSchema.CurrentVersion,
            CadProduct = _cadProduct,
            CadVersion = _cadVersion,
        };
        await WriteFrameAsync(
            pipe,
            InspectionPipeMessageType.HelloAck,
            helloFrame.RequestId,
            InspectionPipeProtocol.SerializePayload(helloAck),
            cancellationToken).ConfigureAwait(false);
        lock (_sync)
        {
            if (ReferenceEquals(_activePipe, pipe) && pipe.IsConnected)
            {
                _connectionReady = true;
            }
        }

        PluginLog.Information(
            $"Handshake GADX aceito requestId={helloFrame.RequestId}; cliente={hello.Application}; " +
            $"versão={hello.ApplicationVersion}.");

        while (!cancellationToken.IsCancellationRequested && pipe.IsConnected)
        {
            InspectionPipeFrame frame;
            try
            {
                frame = await InspectionPipeProtocol.ReadFrameAsync(pipe, cancellationToken).ConfigureAwait(false);
            }
            catch (EndOfStreamException)
            {
                PluginLog.Information(
                    $"Conexão GADX encerrada pelo cliente após requestId={helloFrame.RequestId}.");
                return;
            }
            catch (InvalidDataException exception)
            {
                PluginLog.Error("Frame GADX inválido", exception);
                return;
            }

            if (frame.ProtocolVersion != InspectionPipeProtocol.CurrentVersion)
            {
                await WriteErrorAsync(
                    pipe,
                    frame.RequestId,
                    "IncompatibleVersion",
                    $"Protocolo GADX {frame.ProtocolVersion} não é suportado.",
                    cancellationToken).ConfigureAwait(false);
                continue;
            }

            switch (frame.MessageType)
            {
                case InspectionPipeMessageType.InspectionPush:
                    await HandleInspectionPushAsync(pipe, frame, cancellationToken).ConfigureAwait(false);
                    break;
                case InspectionPipeMessageType.CorrectionAck:
                    HandleCorrectionAck(frame);
                    break;
                default:
                    await WriteErrorAsync(
                        pipe,
                        frame.RequestId,
                        "CommunicationError",
                        $"Mensagem inesperada após o handshake: {frame.MessageType}.",
                        cancellationToken).ConfigureAwait(false);
                    break;
            }
        }
    }

    private async Task HandleInspectionPushAsync(
        NamedPipeServerStream pipe,
        InspectionPipeFrame pushFrame,
        CancellationToken cancellationToken)
    {
        try
        {
            var document = _serializer.Deserialize(pushFrame.Payload);
            _accept(new ReceivedInspection(document, pushFrame.RequestId, DateTime.UtcNow, "Named Pipe"));
            await WriteFrameAsync(
                pipe,
                InspectionPipeMessageType.InspectionAck,
                pushFrame.RequestId,
                InspectionPipeProtocol.SerializePayload(new InspectionAcknowledgement
                {
                    Accepted = true,
                    Status = "Success",
                    Message = "Inspeção recebida e validada pelo GeoAutoEngine.",
                }),
                cancellationToken).ConfigureAwait(false);
            PluginLog.Information(
                $"InspectionAck Accepted=true requestId={pushFrame.RequestId}; " +
                $"ReportId={document.Report.ReportId:D}; trechos={document.Sections.Count}; " +
                $"ocorrências={document.Sections.Sum(section => section.Occurrences.Count)}.");
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException or JsonException)
        {
            await WriteFrameAsync(
                pipe,
                InspectionPipeMessageType.InspectionAck,
                pushFrame.RequestId,
                InspectionPipeProtocol.SerializePayload(new InspectionAcknowledgement
                {
                    Accepted = false,
                    Status = exception is NotSupportedException ? "IncompatibleVersion" : "InvalidPayload",
                    Message = exception.Message,
                }),
                cancellationToken).ConfigureAwait(false);
            PluginLog.Error($"InspectionAck Accepted=false requestId={pushFrame.RequestId}", exception);
        }
    }

    private void HandleCorrectionAck(InspectionPipeFrame frame)
    {
        InspectionCorrectionAcknowledgement acknowledgement;
        try
        {
            acknowledgement = InspectionPipeProtocol.DeserializePayload<InspectionCorrectionAcknowledgement>(
                frame.Payload);
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException)
        {
            if (_pendingCorrectionAcks.TryRemove(frame.RequestId, out var invalidCompletion))
            {
                invalidCompletion.TrySetException(new InvalidDataException(
                    "O GeoAutoDocs respondeu com um CorrectionAck inválido.",
                    exception));
            }

            PluginLog.Error($"CorrectionAck inválido requestId={frame.RequestId}", exception);
            return;
        }

        if (_pendingCorrectionAcks.TryRemove(frame.RequestId, out var completion))
        {
            completion.TrySetResult(acknowledgement);
            PluginLog.Information(
                $"CorrectionAck recebido requestId={frame.RequestId}; Accepted={acknowledgement.Accepted}; " +
                $"Status={acknowledgement.Status}; Applied={acknowledgement.AppliedCount}; " +
                $"Rejected={acknowledgement.RejectedCount}.");
        }
        else
        {
            PluginLog.Information($"CorrectionAck sem solicitação pendente requestId={frame.RequestId}.");
        }
    }

    private async Task WriteErrorAsync(
        Stream pipe,
        int requestId,
        string status,
        string message,
        CancellationToken cancellationToken) => await WriteFrameAsync(
            pipe,
            InspectionPipeMessageType.Error,
            requestId,
            InspectionPipeProtocol.SerializePayload(new InspectionPipeError
            {
                Status = status,
                Message = message,
            }),
            cancellationToken).ConfigureAwait(false);

    private async Task WriteFrameAsync(
        Stream pipe,
        InspectionPipeMessageType messageType,
        int requestId,
        byte[] payload,
        CancellationToken cancellationToken)
    {
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await InspectionPipeProtocol.WriteFrameAsync(
                pipe,
                messageType,
                requestId,
                payload,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private void FailPendingCorrections(Exception exception)
    {
        foreach (var pending in _pendingCorrectionAcks.ToArray())
        {
            if (_pendingCorrectionAcks.TryRemove(pending.Key, out var completion))
            {
                completion.TrySetException(exception);
            }
        }
    }
}
