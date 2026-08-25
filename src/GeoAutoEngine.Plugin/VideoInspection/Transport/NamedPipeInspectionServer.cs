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
    private CancellationTokenSource? _cancellation;
    private Task? _listenerTask;
    private NamedPipeServerStream? _activePipe;

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
                return;
            }

            _cancellation!.Cancel();
            _activePipe?.Dispose();
            listener = _listenerTask;
            _listenerTask = null;
        }

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
                _cancellation?.Dispose();
                _cancellation = null;
            }
        }

        PluginLog.Information($"Named Pipe {InspectionPipeProtocol.DefaultPipeName} encerrado.");
    }

    public void Dispose() => Stop();

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
                    }
                }

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
        await InspectionPipeProtocol.WriteFrameAsync(
            pipe,
            InspectionPipeMessageType.HelloAck,
            helloFrame.RequestId,
            InspectionPipeProtocol.SerializePayload(helloAck),
            cancellationToken).ConfigureAwait(false);
        PluginLog.Information(
            $"Handshake GADX aceito requestId={helloFrame.RequestId}; cliente={hello.Application}; " +
            $"versão={hello.ApplicationVersion}.");

        InspectionPipeFrame pushFrame;
        try
        {
            pushFrame = await InspectionPipeProtocol.ReadFrameAsync(pipe, cancellationToken).ConfigureAwait(false);
        }
        catch (EndOfStreamException)
        {
            PluginLog.Information(
                $"Sonda de disponibilidade GADX concluída requestId={helloFrame.RequestId}.");
            return;
        }
        catch (InvalidDataException exception)
        {
            PluginLog.Error("Frame InspectionPush inválido", exception);
            return;
        }

        if (pushFrame.ProtocolVersion != InspectionPipeProtocol.CurrentVersion ||
            pushFrame.MessageType != InspectionPipeMessageType.InspectionPush)
        {
            await WriteErrorAsync(
                pipe,
                pushFrame.RequestId,
                "CommunicationError",
                $"Mensagem de inspeção inesperada: {pushFrame.MessageType}.",
                cancellationToken).ConfigureAwait(false);
            return;
        }

        try
        {
            var document = _serializer.Deserialize(pushFrame.Payload);
            _accept(new ReceivedInspection(document, pushFrame.RequestId, DateTime.UtcNow, "Named Pipe"));
            await InspectionPipeProtocol.WriteFrameAsync(
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
            await InspectionPipeProtocol.WriteFrameAsync(
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

    private static Task WriteErrorAsync(
        Stream pipe,
        int requestId,
        string status,
        string message,
        CancellationToken cancellationToken) => InspectionPipeProtocol.WriteFrameAsync(
            pipe,
            InspectionPipeMessageType.Error,
            requestId,
            InspectionPipeProtocol.SerializePayload(new InspectionPipeError
            {
                Status = status,
                Message = message,
            }),
            cancellationToken);
}
