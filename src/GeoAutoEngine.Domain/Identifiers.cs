using System.Security.Cryptography;

namespace GeoAutoEngine.Domain;

public readonly record struct BatchId
{
    public BatchId(Guid value)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty);
        Value = value;
    }

    public Guid Value { get; }

    public static BatchId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct SourceFileId
{
    public SourceFileId(Guid value)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty);
        Value = value;
    }

    public Guid Value { get; }

    public static SourceFileId New() => new(Guid.NewGuid());

    public static SourceFileId From(string canonicalPath, string contentSha256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentSha256);
        var input = System.Text.Encoding.UTF8.GetBytes($"{Path.GetFullPath(canonicalPath).ToUpperInvariant()}\n{contentSha256.ToLowerInvariant()}");
        return new SourceFileId(StableIdentifier.FromBytes(input));
    }

    public override string ToString() => Value.ToString("D");
}

public readonly record struct SurveyRecordId
{
    public SurveyRecordId(Guid value)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty);
        Value = value;
    }

    public Guid Value { get; }

    public static SurveyRecordId From(SourceFileId sourceFileId, int sourceRecordIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sourceRecordIndex);

        var input = new byte[20];
        Buffer.BlockCopy(sourceFileId.Value.ToByteArray(), 0, input, 0, 16);
        Buffer.BlockCopy(BitConverter.GetBytes(sourceRecordIndex), 0, input, 16, 4);
        return new SurveyRecordId(StableIdentifier.FromBytes(input));
    }

    public override string ToString() => Value.ToString("D");
}

public readonly record struct DrawingPlanId
{
    public DrawingPlanId(Guid value)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty);
        Value = value;
    }

    public Guid Value { get; }

    public static DrawingPlanId New() => new(Guid.NewGuid());

    public static DrawingPlanId FromStableKey(string stableKey) =>
        new(StableIdentifier.FromText(stableKey));

    public override string ToString() => Value.ToString("D");
}

public readonly record struct PlanItemId
{
    public PlanItemId(Guid value)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty);
        Value = value;
    }

    public Guid Value { get; }

    public static PlanItemId New() => new(Guid.NewGuid());

    public static PlanItemId FromStableKey(string stableKey) =>
        new(StableIdentifier.FromText(stableKey));

    public override string ToString() => Value.ToString("D");
}

internal static class StableIdentifier
{
    public static Guid FromText(string stableKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stableKey);
        return FromBytes(System.Text.Encoding.UTF8.GetBytes(stableKey.Normalize()));
    }

    internal static Guid FromBytes(byte[] input)
    {
        using var sha256 = SHA256.Create();
        var digest = sha256.ComputeHash(input);
        var guidBytes = new byte[16];
        Buffer.BlockCopy(digest, 0, guidBytes, 0, guidBytes.Length);
        return new Guid(guidBytes);
    }
}
