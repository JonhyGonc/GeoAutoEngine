using System.Text.Json;
using System.Text.Json.Serialization;

namespace GeoAutoEngine.Domain;

public readonly record struct PlanPoint3
{
    public PlanPoint3(double x, double y, double z)
    {
        if (!NumberCompatibility.IsFinite(x) || !NumberCompatibility.IsFinite(y) || !NumberCompatibility.IsFinite(z))
        {
            throw new ArgumentOutOfRangeException(nameof(x), "Plan coordinates must be finite.");
        }

        X = x;
        Y = y;
        Z = z;
    }

    public double X { get; }

    public double Y { get; }

    public double Z { get; }
}

public readonly record struct PlanScale3
{
    public PlanScale3(double x, double y, double z)
    {
        if (!NumberCompatibility.IsFinite(x) || !NumberCompatibility.IsFinite(y) || !NumberCompatibility.IsFinite(z) || x <= 0 || y <= 0 || z <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(x), "Plan scales must be finite and positive.");
        }

        X = x;
        Y = y;
        Z = z;
    }

    public double X { get; }

    public double Y { get; }

    public double Z { get; }
}

public enum PlanRotationMode
{
    Fixed,
    FollowNetwork,
}

public enum TextPlanKind
{
    Depth,
    Diameter,
    Complementary,
    BoundaryAnnotation,
}

public enum BlockAlignmentMode
{
    InsertionPoint,
    BoundsCenter,
}

public enum RainStructureKind
{
    Grate,
    CatchBasin,
}

public enum PlanAlertKind
{
    UnknownCode,
    RainStructureReview,
    BoxStructureReview,
}

public enum PlanAttentionTarget
{
    Text,
    Block,
}

public enum DrawingItemKind
{
    Text,
    Block,
    RainStructure,
    BoxStructure,
    CircleAlert,
}

public sealed record DrawingItemContext
{
    public DrawingItemContext(
        PlanItemId id,
        string stableKey,
        string layer,
        string rule,
        IEnumerable<RecordOrigin> origins)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id.Value, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(stableKey);
        if (PlanItemId.FromStableKey(stableKey) != id)
        {
            throw new ArgumentException("The drawing item identifier must be derived from its stable key.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(layer);
        ArgumentException.ThrowIfNullOrWhiteSpace(rule);
        var originSnapshot = CollectionSnapshot.List(origins, nameof(origins))
            .OrderBy(origin => origin.BatchOrdinal)
            .ToArray();
        if (originSnapshot.Length == 0)
        {
            throw new ArgumentException("A drawing item must preserve at least one origin.", nameof(origins));
        }

        Id = id;
        StableKey = stableKey;
        Layer = layer;
        Rule = rule;
        Origins = Array.AsReadOnly(originSnapshot);
    }

    public PlanItemId Id { get; }

    public string StableKey { get; }

    public string Layer { get; }

    public string Rule { get; }

    public IReadOnlyList<RecordOrigin> Origins { get; }
}

public sealed record TextPlanItem
{
    public TextPlanItem(
        DrawingItemContext context,
        TextPlanKind kind,
        PlanPoint3 anchor,
        PlanRotationMode rotationMode,
        double fixedRotationRadians,
        double height,
        double perpendicularOffset,
        string content,
        string styleName,
        int? colorIndex,
        IEnumerable<string> referenceCurveLayers)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(styleName);
        if (!NumberCompatibility.IsFinite(fixedRotationRadians) || !NumberCompatibility.IsFinite(perpendicularOffset))
        {
            throw new ArgumentOutOfRangeException(nameof(fixedRotationRadians));
        }

        if (!NumberCompatibility.IsFinite(height) || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        if (colorIndex is < 1 or > 255)
        {
            throw new ArgumentOutOfRangeException(nameof(colorIndex));
        }

        Context = context;
        Kind = kind;
        Anchor = anchor;
        RotationMode = rotationMode;
        FixedRotationRadians = fixedRotationRadians;
        Height = height;
        PerpendicularOffset = perpendicularOffset;
        Content = content;
        StyleName = styleName;
        ColorIndex = colorIndex;
        ReferenceCurveLayers = SnapshotStrings(referenceCurveLayers, nameof(referenceCurveLayers));
    }

    public DrawingItemContext Context { get; }

    public TextPlanKind Kind { get; }

    public PlanPoint3 Anchor { get; }

    public PlanRotationMode RotationMode { get; }

    public double FixedRotationRadians { get; }

    public double Height { get; }

    public double PerpendicularOffset { get; }

    public string Content { get; }

    public string StyleName { get; }

    public int? ColorIndex { get; }

    public IReadOnlyList<string> ReferenceCurveLayers { get; }

    private static IReadOnlyList<string> SnapshotStrings(IEnumerable<string> values, string parameterName)
    {
        var snapshot = CollectionSnapshot.List(values, parameterName);
        if (snapshot.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Reference layers cannot be empty.", parameterName);
        }

        return snapshot;
    }
}

public sealed record BlockPlanItem
{
    public BlockPlanItem(
        DrawingItemContext context,
        PlanPoint3 position,
        PlanScale3 scale,
        PlanRotationMode rotationMode,
        double fixedRotationRadians,
        string family,
        string code,
        string network,
        string blockKey,
        string blockName,
        string assetFileName,
        BlockAlignmentMode alignmentMode,
        IEnumerable<string> referenceCurveLayers)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(family);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(network);
        ArgumentException.ThrowIfNullOrWhiteSpace(blockKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(blockName);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetFileName);
        if (!NumberCompatibility.IsFinite(fixedRotationRadians))
        {
            throw new ArgumentOutOfRangeException(nameof(fixedRotationRadians));
        }

        var curveLayers = CollectionSnapshot.List(referenceCurveLayers, nameof(referenceCurveLayers));
        if (curveLayers.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Reference layers cannot be empty.", nameof(referenceCurveLayers));
        }

        Context = context;
        Position = position;
        Scale = scale;
        RotationMode = rotationMode;
        FixedRotationRadians = fixedRotationRadians;
        Family = family;
        Code = code;
        Network = network;
        BlockKey = blockKey;
        BlockName = blockName;
        AssetFileName = assetFileName;
        AlignmentMode = alignmentMode;
        ReferenceCurveLayers = curveLayers;
    }

    public DrawingItemContext Context { get; }

    public PlanPoint3 Position { get; }

    public PlanScale3 Scale { get; }

    public PlanRotationMode RotationMode { get; }

    public double FixedRotationRadians { get; }

    public string Family { get; }

    public string Code { get; }

    public string Network { get; }

    public string BlockKey { get; }

    public string BlockName { get; }

    public string AssetFileName { get; }

    public BlockAlignmentMode AlignmentMode { get; }

    public IReadOnlyList<string> ReferenceCurveLayers { get; }
}

public interface IPolygonStructurePlanItem
{
    DrawingItemContext Context { get; }

    IReadOnlyList<PlanPoint3> Vertices { get; }

    PlanPoint3 Center { get; }

    string Code { get; }
}

public sealed record RainStructurePlanItem : IPolygonStructurePlanItem
{
    public RainStructurePlanItem(
        DrawingItemContext context,
        RainStructureKind kind,
        IEnumerable<PlanPoint3> vertices,
        PlanPoint3 center,
        string code)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        var vertexSnapshot = CollectionSnapshot.List(vertices, nameof(vertices));
        if (vertexSnapshot.Count != 4)
        {
            throw new ArgumentException("A rain structure requires four vertices.", nameof(vertices));
        }

        Context = context;
        Kind = kind;
        Vertices = vertexSnapshot;
        Center = center;
        Code = code;
    }

    public DrawingItemContext Context { get; }

    public RainStructureKind Kind { get; }

    public IReadOnlyList<PlanPoint3> Vertices { get; }

    public PlanPoint3 Center { get; }

    public string Code { get; }
}

public sealed record BoxStructurePlanItem : IPolygonStructurePlanItem
{
    public BoxStructurePlanItem(
        DrawingItemContext context,
        IEnumerable<PlanPoint3> vertices,
        PlanPoint3 center,
        string code,
        string network)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(network);
        var vertexSnapshot = CollectionSnapshot.List(vertices, nameof(vertices));
        if (vertexSnapshot.Count != 4)
        {
            throw new ArgumentException("A box structure requires four vertices.", nameof(vertices));
        }

        Context = context;
        Vertices = vertexSnapshot;
        Center = center;
        Code = code;
        Network = network;
    }

    public DrawingItemContext Context { get; }

    public IReadOnlyList<PlanPoint3> Vertices { get; }

    public PlanPoint3 Center { get; }

    public string Code { get; }

    public string Network { get; }
}

public sealed record CircleAlertPlanItem
{
    public CircleAlertPlanItem(
        DrawingItemContext context,
        PlanAlertKind kind,
        PlanAttentionTarget target,
        PlanPoint3 center,
        double radius,
        int colorIndex,
        string code,
        int relatedRecordCount)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        if (!NumberCompatibility.IsFinite(radius) || radius <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius));
        }

        if (colorIndex is < 1 or > 255)
        {
            throw new ArgumentOutOfRangeException(nameof(colorIndex));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(relatedRecordCount, 1);
        Context = context;
        Kind = kind;
        Target = target;
        Center = center;
        Radius = radius;
        ColorIndex = colorIndex;
        Code = code;
        RelatedRecordCount = relatedRecordCount;
    }

    public DrawingItemContext Context { get; }

    public PlanAlertKind Kind { get; }

    public PlanAttentionTarget Target { get; }

    public PlanPoint3 Center { get; }

    public double Radius { get; }

    public int ColorIndex { get; }

    public string Code { get; }

    public int RelatedRecordCount { get; }
}

public sealed record LayerPlanDefinition
{
    public LayerPlanDefinition(string name, int? colorIndex = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (colorIndex is < 1 or > 255)
        {
            throw new ArgumentOutOfRangeException(nameof(colorIndex));
        }

        Name = name;
        ColorIndex = colorIndex;
    }

    public string Name { get; }

    public int? ColorIndex { get; }
}

public sealed record TextStylePlanDefinition
{
    public TextStylePlanDefinition(string name, string fontName, string fontFileName, bool bold, bool italic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(fontName);
        ArgumentException.ThrowIfNullOrWhiteSpace(fontFileName);
        Name = name;
        FontName = fontName;
        FontFileName = fontFileName;
        Bold = bold;
        Italic = italic;
    }

    public string Name { get; }

    public string FontName { get; }

    public string FontFileName { get; }

    public bool Bold { get; }

    public bool Italic { get; }
}

public sealed record XDataPlanItem
{
    public XDataPlanItem(PlanItemId targetItemId, string applicationName, IEnumerable<KeyValuePair<string, string>> values)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(targetItemId.Value, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);
        TargetItemId = targetItemId;
        ApplicationName = applicationName;
        Values = CollectionSnapshot.Dictionary(values, nameof(values));
    }

    public PlanItemId TargetItemId { get; }

    public string ApplicationName { get; }

    public IReadOnlyDictionary<string, string> Values { get; }
}

public sealed record SuppressedPlanItem
{
    public SuppressedPlanItem(
        PlanItemId itemId,
        DrawingItemKind kind,
        string rule,
        string reason,
        IEnumerable<RecordOrigin> origins,
        PlanItemId? retainedItemId = null)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(itemId.Value, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(rule);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        var originSnapshot = CollectionSnapshot.List(origins, nameof(origins));
        if (originSnapshot.Count == 0)
        {
            throw new ArgumentException("A suppressed item must preserve at least one origin.", nameof(origins));
        }

        ItemId = itemId;
        Kind = kind;
        Rule = rule;
        Reason = reason;
        Origins = originSnapshot;
        RetainedItemId = retainedItemId;
    }

    public PlanItemId ItemId { get; }

    public DrawingItemKind Kind { get; }

    public string Rule { get; }

    public string Reason { get; }

    public IReadOnlyList<RecordOrigin> Origins { get; }

    public PlanItemId? RetainedItemId { get; }
}

public sealed record PlanDiagnostic
{
    public PlanDiagnostic(
        string code,
        string message,
        DiagnosticSeverity severity,
        IEnumerable<RecordOrigin> origins,
        PlanItemId? itemId = null,
        SourceFileId? sourceFileId = null,
        string? sourceFileName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        if (sourceFileId.HasValue != !string.IsNullOrWhiteSpace(sourceFileName))
        {
            throw new ArgumentException("A source diagnostic must preserve both source identifier and source name.", nameof(sourceFileId));
        }

        Code = code;
        Message = message;
        Severity = severity;
        Origins = CollectionSnapshot.List(origins, nameof(origins));
        ItemId = itemId;
        SourceFileId = sourceFileId;
        SourceFileName = sourceFileName;
    }

    public string Code { get; }

    public string Message { get; }

    public DiagnosticSeverity Severity { get; }

    public IReadOnlyList<RecordOrigin> Origins { get; }

    public PlanItemId? ItemId { get; }

    public SourceFileId? SourceFileId { get; }

    public string? SourceFileName { get; }
}

public sealed record DrawingPlanSummary(
    int TextCount,
    int BlockCount,
    int RainStructureCount,
    int BoxStructureCount,
    int CircleAlertCount,
    int SuppressedCount,
    int WarningCount,
    int RejectedInputCount);

public sealed class DrawingPlan
{
    public const string CurrentSchemaVersion = "3.1";

    public DrawingPlan(
        DrawingPlanId id,
        BatchId sourceBatchId,
        IEnumerable<LayerPlanDefinition> layers,
        IEnumerable<TextStylePlanDefinition> textStyles,
        IEnumerable<TextPlanItem> texts,
        IEnumerable<BlockPlanItem> blocks,
        IEnumerable<RainStructurePlanItem> rainStructures,
        IEnumerable<BoxStructurePlanItem> boxStructures,
        IEnumerable<CircleAlertPlanItem> circleAlerts,
        IEnumerable<XDataPlanItem> xdata,
        IEnumerable<SuppressedPlanItem> suppressedItems,
        IEnumerable<PlanDiagnostic> diagnostics,
        IEnumerable<RejectedRecord> rejectedInputs)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id.Value, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(sourceBatchId.Value, Guid.Empty);
        Id = id;
        SourceBatchId = sourceBatchId;
        Layers = SortedByName(layers, item => item.Name, nameof(layers));
        TextStyles = SortedByName(textStyles, item => item.Name, nameof(textStyles));
        Texts = Sorted(texts, item => item.Context.Id, nameof(texts));
        Blocks = Sorted(blocks, item => item.Context.Id, nameof(blocks));
        RainStructures = Sorted(rainStructures, item => item.Context.Id, nameof(rainStructures));
        BoxStructures = Sorted(boxStructures, item => item.Context.Id, nameof(boxStructures));
        CircleAlerts = Sorted(circleAlerts, item => item.Context.Id, nameof(circleAlerts));
        XData = Sorted(xdata, item => item.TargetItemId, nameof(xdata));
        SuppressedItems = Sorted(suppressedItems, item => item.ItemId, nameof(suppressedItems));
        Diagnostics = Array.AsReadOnly(CollectionSnapshot.List(diagnostics, nameof(diagnostics))
            .OrderBy(item => item.Code, StringComparer.Ordinal)
            .ThenBy(item => item.Origins.FirstOrDefault()?.BatchOrdinal ?? int.MaxValue)
            .ToArray());
        RejectedInputs = Array.AsReadOnly(CollectionSnapshot.List(rejectedInputs, nameof(rejectedInputs))
            .OrderBy(item => item.Origin.BatchOrdinal)
            .ToArray());

        var activeIds = Texts.Select(item => item.Context.Id)
            .Concat(Blocks.Select(item => item.Context.Id))
            .Concat(RainStructures.Select(item => item.Context.Id))
            .Concat(BoxStructures.Select(item => item.Context.Id))
            .Concat(CircleAlerts.Select(item => item.Context.Id))
            .ToArray();
        EnsureUnique(activeIds, "drawing item identifier");
        EnsureUnique(SuppressedItems.Select(item => item.ItemId), "suppressed item identifier");
        if (SuppressedItems.Any(item => activeIds.Contains(item.ItemId)))
        {
            throw new ArgumentException("A drawing item cannot be both active and suppressed.", nameof(suppressedItems));
        }

        if (XData.Any(item => !activeIds.Contains(item.TargetItemId)))
        {
            throw new ArgumentException("XData must target an active drawing item.", nameof(xdata));
        }

        EnsureUnique(XData.Select(item => item.TargetItemId), "XData target identifier");

        EnsureUnique(Layers.Select(item => item.Name), "layer name");
        EnsureUnique(TextStyles.Select(item => item.Name), "text style name");
        Summary = new DrawingPlanSummary(
            Texts.Count,
            Blocks.Count,
            RainStructures.Count,
            BoxStructures.Count,
            CircleAlerts.Count,
            SuppressedItems.Count,
            Diagnostics.Count(item => item.Severity == DiagnosticSeverity.Warning),
            RejectedInputs.Count);
    }

    public string SchemaVersion => CurrentSchemaVersion;

    public DrawingPlanId Id { get; }

    public BatchId SourceBatchId { get; }

    public IReadOnlyList<LayerPlanDefinition> Layers { get; }

    public IReadOnlyList<TextStylePlanDefinition> TextStyles { get; }

    public IReadOnlyList<TextPlanItem> Texts { get; }

    public IReadOnlyList<BlockPlanItem> Blocks { get; }

    public IReadOnlyList<RainStructurePlanItem> RainStructures { get; }

    public IReadOnlyList<BoxStructurePlanItem> BoxStructures { get; }

    public IReadOnlyList<CircleAlertPlanItem> CircleAlerts { get; }

    public IReadOnlyList<XDataPlanItem> XData { get; }

    public IReadOnlyList<SuppressedPlanItem> SuppressedItems { get; }

    public IReadOnlyList<PlanDiagnostic> Diagnostics { get; }

    public IReadOnlyList<RejectedRecord> RejectedInputs { get; }

    public DrawingPlanSummary Summary { get; }

    public string ToCanonicalJson()
    {
        var canonical = new
        {
            schemaVersion = SchemaVersion,
            layers = Layers,
            textStyles = TextStyles,
            texts = Texts.Select(item => new
            {
                context = CanonicalContext.From(item.Context),
                item.Kind,
                item.Anchor,
                item.RotationMode,
                item.FixedRotationRadians,
                item.Height,
                item.PerpendicularOffset,
                item.Content,
                item.StyleName,
                item.ColorIndex,
                item.ReferenceCurveLayers,
            }),
            blocks = Blocks.Select(item => new
            {
                context = CanonicalContext.From(item.Context),
                item.Position,
                item.Scale,
                item.RotationMode,
                item.FixedRotationRadians,
                item.Family,
                item.Code,
                item.Network,
                item.BlockKey,
                item.BlockName,
                item.AssetFileName,
                item.AlignmentMode,
                item.ReferenceCurveLayers,
            }),
            rainStructures = RainStructures.Select(item => new
            {
                context = CanonicalContext.From(item.Context),
                item.Kind,
                item.Vertices,
                item.Center,
                item.Code,
            }),
            boxStructures = BoxStructures.Select(item => new
            {
                context = CanonicalContext.From(item.Context),
                item.Vertices,
                item.Center,
                item.Code,
                item.Network,
            }),
            circleAlerts = CircleAlerts.Select(item => new
            {
                context = CanonicalContext.From(item.Context),
                item.Kind,
                item.Target,
                item.Center,
                item.Radius,
                item.ColorIndex,
                item.Code,
                item.RelatedRecordCount,
            }),
            xdata = XData.Select(item => new
            {
                targetItemId = item.TargetItemId.ToString(),
                item.ApplicationName,
                item.Values,
            }),
            suppressedItems = SuppressedItems.Select(item => new
            {
                itemId = item.ItemId.ToString(),
                item.Kind,
                item.Rule,
                item.Reason,
                retainedItemId = item.RetainedItemId?.ToString(),
                origins = item.Origins.Select(CanonicalOrigin.From),
            }),
            diagnostics = Diagnostics.Select(item => new
            {
                item.Code,
                item.Message,
                item.Severity,
                itemId = item.ItemId?.ToString(),
                sourceFileId = item.SourceFileId?.ToString(),
                item.SourceFileName,
                origins = item.Origins.Select(CanonicalOrigin.From),
            }),
            rejectedInputs = RejectedInputs.Select(item => new
            {
                origin = CanonicalOrigin.From(item.Origin),
                item.RawValues,
                item.Reasons,
            }),
        };

        return JsonSerializer.Serialize(canonical, CanonicalJsonOptions);
    }

    private static JsonSerializerOptions CanonicalJsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    private static IReadOnlyList<T> Sorted<T>(IEnumerable<T> values, Func<T, PlanItemId> idSelector, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        return Array.AsReadOnly(values.OrderBy(item => idSelector(item).Value).ToArray());
    }

    private static IReadOnlyList<T> SortedByName<T>(IEnumerable<T> values, Func<T, string> nameSelector, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        return Array.AsReadOnly(values.OrderBy(nameSelector, StringComparer.Ordinal).ToArray());
    }

    private static void EnsureUnique<T>(IEnumerable<T> values, string description)
        where T : notnull
    {
        var seen = new HashSet<T>();
        if (values.Any(value => !seen.Add(value)))
        {
            throw new ArgumentException($"Each {description} must be unique.");
        }
    }

    private sealed record CanonicalOrigin(
        string SourceFileId,
        string SourceFileName,
        int SourceRecordIndex,
        int SourceLineNumber,
        int BatchOrdinal)
    {
        public static CanonicalOrigin From(RecordOrigin origin) =>
            new(origin.SourceFileId.ToString(), origin.SourceFileName, origin.SourceRecordIndex, origin.SourceLineNumber, origin.BatchOrdinal);
    }

    private sealed record CanonicalContext(
        string Id,
        string StableKey,
        string Layer,
        string Rule,
        IReadOnlyList<CanonicalOrigin> Origins)
    {
        public static CanonicalContext From(DrawingItemContext context) =>
            new(
                context.Id.ToString(),
                context.StableKey,
                context.Layer,
                context.Rule,
                Array.AsReadOnly(context.Origins.Select(CanonicalOrigin.From).ToArray()));
    }
}
