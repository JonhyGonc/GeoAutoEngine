namespace GeoAutoEngine.Domain;

public sealed record ResolvedColumns
{
    public ResolvedColumns(
        int northing,
        int easting,
        int code,
        int? elevation,
        IEnumerable<KeyValuePair<string, int>> columns)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(northing);
        ArgumentOutOfRangeException.ThrowIfNegative(easting);
        ArgumentOutOfRangeException.ThrowIfNegative(code);
        if (elevation is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(elevation));
        }

        Northing = northing;
        Easting = easting;
        Code = code;
        Elevation = elevation;
        Columns = CollectionSnapshot.Dictionary(columns, nameof(columns));
    }

    public int Northing { get; }

    public int Easting { get; }

    public int Code { get; }

    public int? Elevation { get; }

    public IReadOnlyDictionary<string, int> Columns { get; }
}

public enum SourceImportStatus
{
    Imported,
    Rejected,
}

public sealed record SourceFile
{
    public SourceFile(
        SourceFileId id,
        int selectionOrdinal,
        string displayName,
        string canonicalPath,
        string contentSha256,
        string encodingName,
        string delimiter,
        ResolvedColumns? resolvedColumns,
        int recordCount,
        SourceImportStatus status = SourceImportStatus.Imported)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id.Value, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfNegative(selectionOrdinal);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalPath);
        ArgumentOutOfRangeException.ThrowIfNegative(recordCount);
        if (contentSha256.Length > 0 && (contentSha256.Length != 64 || contentSha256.Any(character => !Uri.IsHexDigit(character))))
        {
            throw new ArgumentException("Content SHA-256 must contain exactly 64 hexadecimal characters.", nameof(contentSha256));
        }

        if (status == SourceImportStatus.Imported &&
            (string.IsNullOrWhiteSpace(contentSha256) || string.IsNullOrWhiteSpace(encodingName) ||
             string.IsNullOrWhiteSpace(delimiter) || resolvedColumns is null))
        {
            throw new ArgumentException("An imported source requires hash, encoding, delimiter and resolved columns.", nameof(status));
        }

        if (status == SourceImportStatus.Rejected && recordCount != 0)
        {
            throw new ArgumentException("A structurally rejected source cannot declare imported record identities.", nameof(recordCount));
        }

        Id = id;
        SelectionOrdinal = selectionOrdinal;
        DisplayName = displayName;
        CanonicalPath = canonicalPath;
        ContentSha256 = contentSha256.ToLowerInvariant();
        EncodingName = encodingName;
        Delimiter = delimiter;
        ResolvedColumns = resolvedColumns;
        RecordCount = recordCount;
        Status = status;
    }

    public SourceFileId Id { get; }

    public int SelectionOrdinal { get; }

    public string DisplayName { get; }

    public string CanonicalPath { get; }

    public string ContentSha256 { get; }

    public string EncodingName { get; }

    public string Delimiter { get; }

    public ResolvedColumns? ResolvedColumns { get; }

    public int RecordCount { get; }

    public SourceImportStatus Status { get; }
}

public sealed record RecordOrigin
{
    public RecordOrigin(
        SourceFileId sourceFileId,
        string sourceFileName,
        int sourceRecordIndex,
        int sourceLineNumber,
        int batchOrdinal)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(sourceFileId.Value, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFileName);
        ArgumentOutOfRangeException.ThrowIfNegative(sourceRecordIndex);
        ArgumentOutOfRangeException.ThrowIfLessThan(sourceLineNumber, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(batchOrdinal);

        SourceFileId = sourceFileId;
        SourceFileName = sourceFileName;
        SourceRecordIndex = sourceRecordIndex;
        SourceLineNumber = sourceLineNumber;
        BatchOrdinal = batchOrdinal;
    }

    public SourceFileId SourceFileId { get; }

    public string SourceFileName { get; }

    public int SourceRecordIndex { get; }

    public int SourceLineNumber { get; }

    public int BatchOrdinal { get; }

    public RecordOrigin AtBatchOrdinal(int batchOrdinal) =>
        new(SourceFileId, SourceFileName, SourceRecordIndex, SourceLineNumber, batchOrdinal);
}

public readonly record struct SurveyPoint
{
    public SurveyPoint(double easting, double northing, double elevation)
    {
        if (!NumberCompatibility.IsFinite(easting) || !NumberCompatibility.IsFinite(northing) || !NumberCompatibility.IsFinite(elevation))
        {
            throw new ArgumentOutOfRangeException(nameof(easting), "Survey coordinates must be finite.");
        }

        Easting = easting;
        Northing = northing;
        Elevation = elevation;
    }

    public double Easting { get; }

    public double Northing { get; }

    public double Elevation { get; }
}

public sealed record SurveyInterpretation
{
    public SurveyInterpretation(
        string? network,
        string? element,
        string? material,
        double? diameter,
        double? depth,
        string nature = "",
        string recordType = "",
        bool recognized = true,
        string confidence = "alta",
        string rule = "",
        string observation = "",
        string? complement = null,
        string originalCode = "",
        string normalizedCode = "")
    {
        if (diameter.HasValue && (!NumberCompatibility.IsFinite(diameter.Value) || diameter.Value < 0))
        {
            throw new ArgumentOutOfRangeException(nameof(diameter));
        }

        if (depth.HasValue && (!NumberCompatibility.IsFinite(depth.Value) || depth.Value < 0))
        {
            throw new ArgumentOutOfRangeException(nameof(depth));
        }

        Network = network;
        Element = element;
        Material = material;
        Diameter = diameter;
        Depth = depth;
        Nature = nature;
        RecordType = recordType;
        Recognized = recognized;
        Confidence = confidence;
        Rule = rule;
        Observation = observation;
        Complement = complement;
        OriginalCode = originalCode;
        NormalizedCode = normalizedCode;
    }

    public string? Network { get; }

    public string? Element { get; }

    public string? Material { get; }

    public double? Diameter { get; }

    public double? Depth { get; }

    public string Nature { get; }

    public string RecordType { get; }

    public bool Recognized { get; }

    public string Confidence { get; }

    public string Rule { get; }

    public string Observation { get; }

    public string? Complement { get; }

    public string OriginalCode { get; }

    public string NormalizedCode { get; }
}

public sealed record SurveyRecord
{
    public SurveyRecord(
        SurveyRecordId id,
        RecordOrigin origin,
        IEnumerable<KeyValuePair<string, string?>> rawValues,
        SurveyPoint point,
        string code,
        SurveyInterpretation interpretation)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id.Value, Guid.Empty);
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentNullException.ThrowIfNull(interpretation);

        Id = id;
        Origin = origin;
        RawValues = CollectionSnapshot.Dictionary(rawValues, nameof(rawValues));
        Point = point;
        Code = code;
        Interpretation = interpretation;
    }

    public SurveyRecordId Id { get; }

    public RecordOrigin Origin { get; }

    public IReadOnlyDictionary<string, string?> RawValues { get; }

    public SurveyPoint Point { get; }

    public string Code { get; }

    public SurveyInterpretation Interpretation { get; }

    public SurveyRecord AtOrigin(RecordOrigin origin) =>
        new(SurveyRecordId.From(origin.SourceFileId, origin.SourceRecordIndex), origin, RawValues, Point, Code, Interpretation);
}

public sealed record RejectedRecord
{
    public RejectedRecord(
        RecordOrigin origin,
        IEnumerable<KeyValuePair<string, string?>> rawValues,
        IEnumerable<string> reasons)
    {
        ArgumentNullException.ThrowIfNull(origin);
        var reasonSnapshot = CollectionSnapshot.List(reasons, nameof(reasons));
        if (reasonSnapshot.Count == 0 || reasonSnapshot.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("At least one non-empty rejection reason is required.", nameof(reasons));
        }

        Origin = origin;
        RawValues = CollectionSnapshot.Dictionary(rawValues, nameof(rawValues));
        Reasons = reasonSnapshot;
    }

    public RecordOrigin Origin { get; }

    public IReadOnlyDictionary<string, string?> RawValues { get; }

    public IReadOnlyList<string> Reasons { get; }

    public RejectedRecord AtOrigin(RecordOrigin origin) => new(origin, RawValues, Reasons);
}

public enum DiagnosticSeverity
{
    Information,
    Warning,
    Error,
}

public enum DiagnosticScope
{
    Batch,
    Source,
    Record,
}

public sealed record Diagnostic
{
    public Diagnostic(
        string code,
        string message,
        DiagnosticSeverity severity,
        DiagnosticScope scope,
        SourceFileId? sourceFileId = null,
        IEnumerable<RecordOrigin>? origins = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        var originSnapshot = CollectionSnapshot.List(origins ?? [], nameof(origins));
        if (scope == DiagnosticScope.Source && sourceFileId is null)
        {
            throw new ArgumentException("A source diagnostic must identify its source.", nameof(sourceFileId));
        }

        if (scope == DiagnosticScope.Record && originSnapshot.Count == 0)
        {
            throw new ArgumentException("A record diagnostic must identify at least one origin.", nameof(origins));
        }

        Code = code;
        Message = message;
        Severity = severity;
        Scope = scope;
        SourceFileId = sourceFileId;
        Origins = originSnapshot;
    }

    public string Code { get; }

    public string Message { get; }

    public DiagnosticSeverity Severity { get; }

    public DiagnosticScope Scope { get; }

    public SourceFileId? SourceFileId { get; }

    public IReadOnlyList<RecordOrigin> Origins { get; }

    public Diagnostic AtOrigins(IEnumerable<RecordOrigin> origins) =>
        new(Code, Message, Severity, Scope, SourceFileId, origins);
}

public enum DuplicateKind
{
    ExactDuplicate,
    SpatialDuplicateCandidate,
}

public sealed record DuplicateCandidate
{
    public DuplicateCandidate(
        DuplicateKind kind,
        SurveyRecordId firstRecordId,
        RecordOrigin firstOrigin,
        SurveyRecordId secondRecordId,
        RecordOrigin secondOrigin,
        string reason,
        double? distance)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(firstRecordId.Value, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(secondRecordId.Value, Guid.Empty);
        ArgumentNullException.ThrowIfNull(firstOrigin);
        ArgumentNullException.ThrowIfNull(secondOrigin);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (firstRecordId == secondRecordId)
        {
            throw new ArgumentException("A duplicate candidate must refer to two different records.", nameof(secondRecordId));
        }

        if (SurveyRecordId.From(firstOrigin.SourceFileId, firstOrigin.SourceRecordIndex) != firstRecordId ||
            SurveyRecordId.From(secondOrigin.SourceFileId, secondOrigin.SourceRecordIndex) != secondRecordId)
        {
            throw new ArgumentException("Duplicate candidate identifiers must match their record origins.");
        }

        if (distance is < 0 || (distance.HasValue && !NumberCompatibility.IsFinite(distance.Value)))
        {
            throw new ArgumentOutOfRangeException(nameof(distance));
        }

        Kind = kind;
        FirstRecordId = firstRecordId;
        FirstOrigin = firstOrigin;
        SecondRecordId = secondRecordId;
        SecondOrigin = secondOrigin;
        Reason = reason;
        Distance = distance;
    }

    public DuplicateKind Kind { get; }

    public SurveyRecordId FirstRecordId { get; }

    public RecordOrigin FirstOrigin { get; }

    public SurveyRecordId SecondRecordId { get; }

    public RecordOrigin SecondOrigin { get; }

    public string Reason { get; }

    public double? Distance { get; }
}
