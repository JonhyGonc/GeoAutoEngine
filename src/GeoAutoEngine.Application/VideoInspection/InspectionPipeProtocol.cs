using System.Text;
using System.Text.Json;

namespace GeoAutoEngine.Application.VideoInspection;

public enum InspectionPipeMessageType : ushort
{
    Hello = 1,
    HelloAck = 2,
    InspectionPush = 3,
    InspectionAck = 4,
    Error = 5,
}

public sealed record InspectionPipeFrame(
    ushort ProtocolVersion,
    InspectionPipeMessageType MessageType,
    int RequestId,
    byte[] Payload);

public static class InspectionPipeProtocol
{
    public const string DefaultPipeName = "GeoAutoEngine.Inspection";
    public const string Magic = "GADX";
    public const ushort CurrentVersion = 1;
    public const int HeaderLength = 16;
    public const int MaxPayloadBytes = 16 * 1024 * 1024;
    private static readonly JsonSerializerOptions ProtocolJsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly byte[] MagicBytes = Encoding.ASCII.GetBytes(Magic);

    public static byte[] SerializePayload<T>(T value) =>
        JsonSerializer.SerializeToUtf8Bytes(value, ProtocolJsonOptions);

    public static T DeserializePayload<T>(byte[] payload) =>
        JsonSerializer.Deserialize<T>(payload, ProtocolJsonOptions)
        ?? throw new InvalidDataException("Payload do protocolo vazio ou inválido.");

    public static async Task WriteFrameAsync(
        Stream stream,
        InspectionPipeMessageType messageType,
        int requestId,
        byte[] payload,
        CancellationToken cancellationToken = default,
        ushort protocolVersion = CurrentVersion)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(payload);
        if (payload.Length > MaxPayloadBytes)
        {
            throw new InvalidDataException($"Payload excede o limite de {MaxPayloadBytes} bytes.");
        }

        var header = new byte[HeaderLength];
        Buffer.BlockCopy(MagicBytes, 0, header, 0, MagicBytes.Length);
        WriteUInt16LittleEndian(header, 4, protocolVersion);
        WriteUInt16LittleEndian(header, 6, (ushort)messageType);
        WriteInt32LittleEndian(header, 8, payload.Length);
        WriteInt32LittleEndian(header, 12, requestId);
        await stream.WriteAsync(header, 0, header.Length, cancellationToken).ConfigureAwait(false);
        if (payload.Length > 0)
        {
            await stream.WriteAsync(payload, 0, payload.Length, cancellationToken).ConfigureAwait(false);
        }

        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async Task<InspectionPipeFrame> ReadFrameAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var header = new byte[HeaderLength];
        await ReadExactlyAsync(stream, header, cancellationToken).ConfigureAwait(false);
        for (var index = 0; index < MagicBytes.Length; index++)
        {
            if (header[index] != MagicBytes[index])
            {
                throw new InvalidDataException("Cabeçalho do Named Pipe possui identificação inválida.");
            }
        }

        var protocolVersion = ReadUInt16LittleEndian(header, 4);
        var messageTypeValue = ReadUInt16LittleEndian(header, 6);
        if (!Enum.IsDefined(typeof(InspectionPipeMessageType), messageTypeValue))
        {
            throw new InvalidDataException($"Tipo de mensagem desconhecido: {messageTypeValue}.");
        }

        var payloadLength = ReadInt32LittleEndian(header, 8);
        if (payloadLength < 0 || payloadLength > MaxPayloadBytes)
        {
            throw new InvalidDataException("Tamanho de payload inválido no Named Pipe.");
        }

        var requestId = ReadInt32LittleEndian(header, 12);
        var payload = new byte[payloadLength];
        if (payloadLength > 0)
        {
            await ReadExactlyAsync(stream, payload, cancellationToken).ConfigureAwait(false);
        }

        return new InspectionPipeFrame(
            protocolVersion,
            (InspectionPipeMessageType)messageTypeValue,
            requestId,
            payload);
    }

    private static async Task ReadExactlyAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var count = await stream.ReadAsync(
                buffer,
                offset,
                buffer.Length - offset,
                cancellationToken).ConfigureAwait(false);
            if (count == 0)
            {
                throw new EndOfStreamException("O frame do Named Pipe terminou antes do tamanho declarado.");
            }

            offset += count;
        }
    }

    private static ushort ReadUInt16LittleEndian(byte[] buffer, int offset) =>
        (ushort)(buffer[offset] | buffer[offset + 1] << 8);

    private static int ReadInt32LittleEndian(byte[] buffer, int offset) =>
        buffer[offset] |
        buffer[offset + 1] << 8 |
        buffer[offset + 2] << 16 |
        buffer[offset + 3] << 24;

    private static void WriteUInt16LittleEndian(byte[] buffer, int offset, ushort value)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
    }

    private static void WriteInt32LittleEndian(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
        buffer[offset + 2] = (byte)(value >> 16);
        buffer[offset + 3] = (byte)(value >> 24);
    }
}

public sealed class InspectionHello
{
    public string Application { get; set; } = "GeoAutoDocs";
    public string ApplicationVersion { get; set; } = string.Empty;
    public int ProtocolVersion { get; set; } = InspectionPipeProtocol.CurrentVersion;
    public int InspectionSchemaVersion { get; set; }
}

public sealed class InspectionHelloAck
{
    public string Application { get; set; } = string.Empty;
    public string ApplicationVersion { get; set; } = string.Empty;
    public int ProtocolVersion { get; set; }
    public int InspectionSchemaVersion { get; set; }
    public string? CadProduct { get; set; }
    public string? CadVersion { get; set; }
}

public sealed class InspectionAcknowledgement
{
    public bool Accepted { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class InspectionPipeError
{
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
