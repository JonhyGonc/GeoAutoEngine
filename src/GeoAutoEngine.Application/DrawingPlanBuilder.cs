using System.Globalization;
using GeoAutoEngine.Domain;

namespace GeoAutoEngine.Application;

public sealed record DrawingPlanBuildOptions
{
    public DrawingPlanBuildOptions(
        double textHeight = 0.5,
        double minimumDistance = 1.0,
        double textOffsetDistance = 0.10,
        double attentionRadius = 0.75,
        bool showRevisionMarks = true)
    {
        if (!NumberCompatibility.IsFinite(textHeight) || textHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(textHeight));
        }

        if (!NumberCompatibility.IsFinite(minimumDistance) || minimumDistance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumDistance));
        }

        if (!NumberCompatibility.IsFinite(textOffsetDistance) || textOffsetDistance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(textOffsetDistance));
        }

        if (!NumberCompatibility.IsFinite(attentionRadius) || attentionRadius <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(attentionRadius));
        }

        TextHeight = textHeight;
        MinimumDistance = minimumDistance;
        TextOffsetDistance = textOffsetDistance;
        AttentionRadius = attentionRadius;
        ShowRevisionMarks = showRevisionMarks;
    }

    public double TextHeight { get; }

    public double MinimumDistance { get; }

    public double TextOffsetDistance { get; }

    public double AttentionRadius { get; }

    public bool ShowRevisionMarks { get; }

    internal string StableKey
    {
        get
        {
            var key = FormattableString.Invariant(
                $"height={TextHeight:R};distance={MinimumDistance:R};offset={TextOffsetDistance:R};attention={AttentionRadius:R}");
            return ShowRevisionMarks ? key : key + ";revision=False";
        }
    }
}

public sealed class DrawingPlanBuilder
{
    private const string MainTextStyle = "GEO_ARIAL";
    private const string BoundaryTextStyle = "GEO_ARIAL_BOLD";
    private const string BoundaryLayer = "-Texto";
    private const string DiagnosticLayer = "GF-Diagnostico";
    private const string XDataApplication = "GEOAUTOENGINE";
    private readonly DrawingPlanBuildOptions _options;

    public DrawingPlanBuilder(DrawingPlanBuildOptions? options = null)
    {
        _options = options ?? new DrawingPlanBuildOptions();
    }

    public DrawingPlan Build(ImportBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        var records = batch.ImportedRecords.OrderBy(record => record.Origin.BatchOrdinal).ToArray();
        var texts = new List<TextPlanItem>();
        var blocks = new List<BlockPlanItem>();
        var rainStructures = new List<RainStructurePlanItem>();
        var boxStructures = new List<BoxStructurePlanItem>();
        var alerts = new List<CircleAlertPlanItem>();
        var xdata = new List<XDataPlanItem>();
        var suppressed = new List<SuppressedPlanItem>();
        var sourceNames = batch.SourceFiles.ToDictionary(source => source.Id, source => source.DisplayName);
        var diagnostics = batch.Diagnostics.Select(diagnostic => new PlanDiagnostic(
            diagnostic.Code,
            diagnostic.Message,
            diagnostic.Severity,
            diagnostic.Origins,
            sourceFileId: diagnostic.SourceFileId,
            sourceFileName: diagnostic.SourceFileId.HasValue ? sourceNames[diagnostic.SourceFileId.Value] : null)).ToList();
        var textCandidates = new List<SurveyRecord>();
        var pvPoints = new List<Point2>();
        var blockExactKeys = new Dictionary<BlockExactKey, PlanItemId>();
        var blockGrids = new Dictionary<string, SpatialItemGrid>(StringComparer.Ordinal);
        var structureGroup = new List<SurveyRecord>();
        (string Rule, string Code, string Network, bool IsBox)? structureKey = null;

        void FinishStructureGroup()
        {
            if (structureGroup.Count == 0 || structureKey is null)
            {
                structureGroup.Clear();
                structureKey = null;
                return;
            }

            var key = structureKey.Value;
            if (key.IsBox)
            {
                PrepareBoxGroup(structureGroup, key.Rule, key.Code, key.Network, boxStructures, alerts, xdata, diagnostics);
            }
            else
            {
                PrepareRainGroup(structureGroup, key.Rule, key.Code, rainStructures, alerts, xdata, diagnostics);
            }

            structureGroup.Clear();
            structureKey = null;
        }

        foreach (var record in records)
        {
            var interpretation = record.Interpretation;
            var rule = interpretation.Rule.Trim().ToUpperInvariant();
            var type = interpretation.RecordType.Trim();

            if (interpretation.Recognized && IsPvRule(rule))
            {
                pvPoints.Add(new Point2(record.Point.Easting, record.Point.Northing));
            }

            if (interpretation.Recognized && IsTextType(type) && interpretation.Depth.HasValue)
            {
                textCandidates.Add(record);
            }

            if (interpretation.Recognized && rule == "ANOTACAO_DIVISA" &&
                !string.IsNullOrWhiteSpace(interpretation.Complement))
            {
                var item = CreateBoundaryAnnotation(record);
                texts.Add(item);
                xdata.Add(CreateXData(item.Context, "ANOTACAO_DIVISA", record.Code, item.Anchor));
            }

            if (!string.Equals(type, "Ignorar", StringComparison.Ordinal) &&
                (!interpretation.Recognized || rule == "DESCONHECIDO"))
            {
                var alert = CreateUnknownCodeAlert(record);
                alerts.Add(alert);
                xdata.Add(CreateXData(alert.Context, "ATENCAO", record.Code, alert.Center));
            }

            if (interpretation.Recognized && IsOutlineStructureRule(rule))
            {
                // CXTL com profundidade pode ter uma profundidade diferente em cada
                // ponto levantado da mesma caixa. A profundidade continua pertencendo
                // ao texto de cada registro, mas não deve fragmentar a reconstrução
                // geométrica da caixa em grupos de um único ponto.
                var structureCode = rule == "CXTL_PROF"
                    ? "CXTL"
                    : interpretation.NormalizedCode.Trim().ToUpperInvariant();
                var key = (
                    Rule: rule,
                    Code: structureCode,
                    Network: (interpretation.Network ?? "Não identificada").Trim(),
                    IsBox: IsBoxRule(rule));
                if (structureKey.HasValue && structureKey.Value != key)
                {
                    FinishStructureGroup();
                }

                structureKey ??= key;
                structureGroup.Add(record);
            }
            else
            {
                FinishStructureGroup();
            }

            if (interpretation.Recognized && IsBlockType(type) && !IsOutlineStructureRule(rule))
            {
                PrepareBlock(record, rule, blocks, xdata, suppressed, diagnostics, blockExactKeys, blockGrids);
            }
        }

        FinishStructureGroup();
        PrepareTexts(textCandidates, pvPoints, texts, xdata, suppressed, diagnostics);

        if (!_options.ShowRevisionMarks)
        {
            var hiddenAlertIds = new HashSet<PlanItemId>(alerts.Select(alert => alert.Context.Id));
            alerts.Clear();
            xdata.RemoveAll(item => hiddenAlertIds.Contains(item.TargetItemId));
        }

        var layers = texts.Select(item => new LayerPlanDefinition(item.Context.Layer, item.ColorIndex))
            .Concat(blocks.Select(item => new LayerPlanDefinition(item.Context.Layer)))
            .Concat(rainStructures.Select(item => new LayerPlanDefinition(item.Context.Layer)))
            .Concat(boxStructures.Select(item => new LayerPlanDefinition(item.Context.Layer)))
            .Concat(alerts.Select(item => new LayerPlanDefinition(item.Context.Layer, item.ColorIndex)))
            .GroupBy(item => item.Name, StringComparer.Ordinal)
            .Select(group => new LayerPlanDefinition(group.Key, group.Select(item => item.ColorIndex).FirstOrDefault(value => value.HasValue)))
            .ToArray();
        var styles = texts.Select(item => item.StyleName)
            .Distinct(StringComparer.Ordinal)
            .Select(name => name == BoundaryTextStyle
                ? new TextStylePlanDefinition(name, "Arial", "arialbd.ttf", bold: true, italic: false)
                : new TextStylePlanDefinition(name, "Arial", "arial.ttf", bold: false, italic: false))
            .ToArray();
        var sourceKey = string.Join(";", batch.SourceFiles.Select(source => $"{source.SelectionOrdinal}:{source.Id}"));
        var planId = DrawingPlanId.FromStableKey($"drawing-plan|{sourceKey}|{_options.StableKey}");

        return new DrawingPlan(
            planId,
            batch.Id,
            layers,
            styles,
            texts,
            blocks,
            rainStructures,
            boxStructures,
            alerts,
            xdata,
            suppressed,
            diagnostics,
            batch.RejectedRecords);
    }

    private void PrepareTexts(
        IReadOnlyList<SurveyRecord> candidates,
        IReadOnlyList<Point2> pvPoints,
        ICollection<TextPlanItem> texts,
        ICollection<XDataPlanItem> xdata,
        ICollection<SuppressedPlanItem> suppressed,
        ICollection<PlanDiagnostic> diagnostics)
    {
        var pvIndex = new NearestPointIndex(pvPoints);
        var keptRecords = new HashSet<SurveyRecordId>();
        var retainedBySuppressed = new Dictionary<SurveyRecordId, SurveyRecord>();

        foreach (var group in candidates.GroupBy(record => record.Interpretation.NormalizedCode, StringComparer.Ordinal))
        {
            var ranked = pvPoints.Count == 0
                ? group.OrderBy(record => record.Origin.BatchOrdinal).ToArray()
                : group.OrderByDescending(record => pvIndex.DistanceToNearest(record.Point.Easting, record.Point.Northing))
                    .ThenBy(record => record.Origin.BatchOrdinal)
                    .ToArray();
            var grid = new SpatialRecordGrid(_options.MinimumDistance);

            foreach (var record in ranked)
            {
                var retained = grid.FindWithin(record.Point.Easting, record.Point.Northing, _options.MinimumDistance);
                if (retained is not null)
                {
                    retainedBySuppressed.Add(record.Id, retained);
                    continue;
                }

                keptRecords.Add(record.Id);
                grid.Add(record);
            }
        }

        foreach (var record in candidates.OrderBy(record => record.Origin.BatchOrdinal))
        {
            if (!keptRecords.Contains(record.Id))
            {
                var retained = retainedBySuppressed[record.Id];
                SuppressText(record, TextPlanKind.Depth, retained, suppressed);
                if (record.Interpretation.Diameter.HasValue)
                {
                    SuppressText(record, TextPlanKind.Diameter, retained, suppressed);
                }

                diagnostics.Add(new PlanDiagnostic(
                    "PLAN_TEXT_PROXIMITY_DUPLICATE",
                    "Texto suprimido por proximidade de outro registro com o mesmo código normalizado.",
                    DiagnosticSeverity.Information,
                    [record.Origin, retained.Origin]));
                continue;
            }

            var depth = CreateNetworkText(record, TextPlanKind.Depth);
            texts.Add(depth);
            xdata.Add(CreateXData(depth.Context, "TEXTO_PROFUNDIDADE", record.Code, depth.Anchor));
            if (record.Interpretation.Diameter.HasValue)
            {
                var diameter = CreateNetworkText(record, TextPlanKind.Diameter);
                texts.Add(diameter);
                xdata.Add(CreateXData(diameter.Context, "TEXTO_DIAMETRO", record.Code, diameter.Anchor));
            }
        }
    }

    private TextPlanItem CreateNetworkText(SurveyRecord record, TextPlanKind kind)
    {
        var interpretation = record.Interpretation;
        var network = interpretation.Network?.Trim() ?? string.Empty;
        var mapping = DrawingPlanCatalog.Network(network);
        var discriminator = kind == TextPlanKind.Depth ? "depth" : "diameter";
        var context = Context(record, DrawingItemKind.Text, discriminator, mapping.TextLayer);
        var content = kind == TextPlanKind.Depth
            ? $"P.{interpretation.Depth!.Value.ToString("F2", CultureInfo.InvariantCulture)}"
            : $"∅{FormatCompact(interpretation.Diameter!.Value)}";
        var offset = kind == TextPlanKind.Depth
            ? _options.TextOffsetDistance
            : -(_options.TextHeight + _options.TextOffsetDistance);
        return new TextPlanItem(
            context,
            kind,
            Point(record),
            PlanRotationMode.FollowNetwork,
            0,
            _options.TextHeight,
            offset,
            content,
            MainTextStyle,
            null,
            mapping.CurveLayers);
    }

    private TextPlanItem CreateBoundaryAnnotation(SurveyRecord record)
    {
        var context = Context(record, DrawingItemKind.Text, "boundary-annotation", BoundaryLayer);
        return new TextPlanItem(
            context,
            TextPlanKind.BoundaryAnnotation,
            Point(record),
            PlanRotationMode.Fixed,
            0,
            _options.TextHeight,
            0,
            record.Interpretation.Complement!.Trim(),
            BoundaryTextStyle,
            7,
            []);
    }

    private CircleAlertPlanItem CreateUnknownCodeAlert(SurveyRecord record)
    {
        var context = Context(record, DrawingItemKind.CircleAlert, "unknown-code", DiagnosticLayer);
        return new CircleAlertPlanItem(
            context,
            PlanAlertKind.UnknownCode,
            PlanAttentionTarget.Text,
            Point(record),
            _options.AttentionRadius,
            1,
            record.Interpretation.OriginalCode.Trim().Length > 0 ? record.Interpretation.OriginalCode.Trim() : record.Code,
            1);
    }

    private void PrepareBlock(
        SurveyRecord record,
        string rule,
        ICollection<BlockPlanItem> blocks,
        ICollection<XDataPlanItem> xdata,
        ICollection<SuppressedPlanItem> suppressed,
        ICollection<PlanDiagnostic> diagnostics,
        IDictionary<BlockExactKey, PlanItemId> exactKeys,
        IDictionary<string, SpatialItemGrid> familyGrids)
    {
        if (!DrawingPlanCatalog.TryBlock(rule, out var definition))
        {
            var itemId = ItemId(record, DrawingItemKind.Block, "unmapped");
            suppressed.Add(new SuppressedPlanItem(
                itemId,
                DrawingItemKind.Block,
                rule.Length > 0 ? rule : "REGRA_VAZIA",
                "Regra reconhecida como bloco, mas sem associação automática segura no catálogo legado.",
                [record.Origin]));
            diagnostics.Add(new PlanDiagnostic(
                "PLAN_BLOCK_RULE_UNMAPPED",
                $"Regra de bloco sem mapeamento seguro: {rule}.",
                DiagnosticSeverity.Information,
                [record.Origin],
                itemId));
            return;
        }

        var itemContext = Context(record, DrawingItemKind.Block, definition.Key, definition.Layer ?? DrawingPlanCatalog.Network(record.Interpretation.Network).TextLayer);
        var exactKey = new BlockExactKey(rule, definition.Key, record.Point.Easting, record.Point.Northing, 0);
        if (exactKeys.TryGetValue(exactKey, out var exactRetained))
        {
            suppressed.Add(new SuppressedPlanItem(
                itemContext.Id,
                DrawingItemKind.Block,
                rule,
                "Bloco exato duplicado para a mesma regra, definição e coordenadas.",
                [record.Origin],
                exactRetained));
            diagnostics.Add(new PlanDiagnostic(
                "PLAN_BLOCK_EXACT_DUPLICATE",
                $"Bloco duplicado exato suprimido: {definition.BlockName}.",
                DiagnosticSeverity.Information,
                [record.Origin],
                itemContext.Id));
            return;
        }

        exactKeys.Add(exactKey, itemContext.Id);
        var grid = familyGrids.TryGetValue(definition.Family, out var existingGrid)
            ? existingGrid
            : familyGrids[definition.Family] = new SpatialItemGrid(_options.MinimumDistance);
        var nearby = grid.FindWithin(record.Point.Easting, record.Point.Northing, _options.MinimumDistance);
        if (nearby.HasValue)
        {
            suppressed.Add(new SuppressedPlanItem(
                itemContext.Id,
                DrawingItemKind.Block,
                rule,
                "Bloco próximo de outro bloco mantido da mesma família.",
                [record.Origin],
                nearby.Value));
            diagnostics.Add(new PlanDiagnostic(
                "PLAN_BLOCK_PROXIMITY_DUPLICATE",
                $"Bloco próximo da família {definition.Family} suprimido.",
                DiagnosticSeverity.Information,
                [record.Origin],
                itemContext.Id));
            return;
        }

        grid.Add(record.Point.Easting, record.Point.Northing, itemContext.Id);
        var mapping = DrawingPlanCatalog.Network(record.Interpretation.Network);
        var item = new BlockPlanItem(
            itemContext,
            Point(record),
            new PlanScale3(definition.ScaleX, definition.ScaleY, definition.ScaleZ),
            definition.RotationMode,
            definition.FixedRotation,
            definition.Family,
            record.Code,
            record.Interpretation.Network ?? "Não identificada",
            definition.Key,
            definition.BlockName,
            definition.AssetFileName,
            definition.AlignmentMode,
            mapping.CurveLayers);
        blocks.Add(item);
        xdata.Add(CreateXData(item.Context, "BLOCO", record.Code, item.Position));
    }

    private void PrepareRainGroup(
        IReadOnlyList<SurveyRecord> group,
        string rule,
        string code,
        ICollection<RainStructurePlanItem> structures,
        ICollection<CircleAlertPlanItem> alerts,
        ICollection<XDataPlanItem> xdata,
        ICollection<PlanDiagnostic> diagnostics)
    {
        var layer = rule == "GRELHA" ? "GF-Grelha" : "GF-Boca de Lobo";
        var reconstruction = StructureOutlineReconstructor.Reconstruct(group, rule == "BOCA_LOBO");
        foreach (var match in reconstruction.Matches)
        {
            var origins = match.Records.Select(record => record.Origin).ToArray();
            var stableKey = StableKey(match.Records[0], DrawingItemKind.RainStructure, $"{rule}:{string.Join(",", origins.Select(origin => origin.BatchOrdinal))}");
            var context = new DrawingItemContext(PlanItemId.FromStableKey(stableKey), stableKey, layer, rule, origins);
            var planVertices = match.Vertices.Select(point => new PlanPoint3(point.X, point.Y, 0)).ToArray();
            var center = new PlanPoint3(planVertices.Average(point => point.X), planVertices.Average(point => point.Y), 0);
            var item = new RainStructurePlanItem(
                context,
                rule == "GRELHA" ? RainStructureKind.Grate : RainStructureKind.CatchBasin,
                planVertices,
                center,
                code);
            structures.Add(item);
            xdata.Add(CreateXData(context, "ESTRUTURA_PLUVIAL", code, center));
        }

        foreach (var record in reconstruction.UnmatchedRecords)
        {
            AddStructureAlert(
                reconstruction.UnmatchedRecords,
                record,
                rule,
                code,
                layer,
                PlanAlertKind.RainStructureReview,
                "rain-review",
                alerts,
                xdata);
        }

        if (reconstruction.Matches.Count > 1)
        {
            diagnostics.Add(new PlanDiagnostic(
                "PLAN_RAIN_STRUCTURE_MULTIPLE_RECONSTRUCTED",
                $"{reconstruction.Matches.Count} estruturas pluviais {code} foram reconstruídas do mesmo grupo consecutivo.",
                DiagnosticSeverity.Information,
                group.Select(record => record.Origin)));
        }

        if (reconstruction.UnmatchedRecords.Count > 0)
        {
            var diagnosticCode = reconstruction.Matches.Count > 0
                ? "PLAN_RAIN_STRUCTURE_UNMATCHED_POINTS"
                : "PLAN_RAIN_STRUCTURE_INCOMPLETE";
            var message = reconstruction.Matches.Count > 0
                ? $"Estrutura pluvial {code} deixou {reconstruction.UnmatchedRecords.Count} ponto(s) sem associação geométrica; revisão necessária."
                : $"Estrutura pluvial {code} não formou um retângulo válido com três pontos.";
            diagnostics.Add(new PlanDiagnostic(
                diagnosticCode,
                message,
                DiagnosticSeverity.Warning,
                reconstruction.UnmatchedRecords.Select(record => record.Origin)));
        }
    }

    private void PrepareBoxGroup(
        IReadOnlyList<SurveyRecord> group,
        string rule,
        string code,
        string network,
        ICollection<BoxStructurePlanItem> structures,
        ICollection<CircleAlertPlanItem> alerts,
        ICollection<XDataPlanItem> xdata,
        ICollection<PlanDiagnostic> diagnostics)
    {
        var layer = DrawingPlanCatalog.Network(network).TextLayer;
        var reconstruction = StructureOutlineReconstructor.Reconstruct(group, orthogonalize: true);
        foreach (var match in reconstruction.Matches)
        {
            var origins = match.Records.Select(record => record.Origin).ToArray();
            var stableKey = StableKey(match.Records[0], DrawingItemKind.BoxStructure, $"{rule}:{string.Join(",", origins.Select(origin => origin.BatchOrdinal))}");
            var context = new DrawingItemContext(PlanItemId.FromStableKey(stableKey), stableKey, layer, rule, origins);
            var vertices = match.Vertices.Select(point => new PlanPoint3(point.X, point.Y, 0)).ToArray();
            var center = new PlanPoint3(vertices.Average(point => point.X), vertices.Average(point => point.Y), 0);
            var item = new BoxStructurePlanItem(context, vertices, center, code, network);
            structures.Add(item);
            xdata.Add(CreateXData(context, "CAIXA", code, center));
        }

        foreach (var record in reconstruction.UnmatchedRecords)
        {
            AddStructureAlert(
                reconstruction.UnmatchedRecords,
                record,
                rule,
                code,
                DiagnosticLayer,
                PlanAlertKind.BoxStructureReview,
                "box-review",
                alerts,
                xdata);
        }

        if (reconstruction.Matches.Count > 1)
        {
            diagnostics.Add(new PlanDiagnostic(
                "PLAN_BOX_STRUCTURE_MULTIPLE_RECONSTRUCTED",
                $"{reconstruction.Matches.Count} caixas {code} foram reconstruídas do mesmo grupo consecutivo.",
                DiagnosticSeverity.Information,
                group.Select(record => record.Origin)));
        }

        if (reconstruction.UnmatchedRecords.Count > 0)
        {
            var diagnosticCode = reconstruction.Matches.Count > 0
                ? "PLAN_BOX_STRUCTURE_UNMATCHED_POINTS"
                : "PLAN_BOX_STRUCTURE_INCOMPLETE";
            var message = reconstruction.Matches.Count > 0
                ? $"Caixa {code} deixou {reconstruction.UnmatchedRecords.Count} ponto(s) sem associação geométrica; revisão necessária."
                : $"Caixa {code} não formou um retângulo válido com três pontos.";
            diagnostics.Add(new PlanDiagnostic(
                diagnosticCode,
                message,
                DiagnosticSeverity.Warning,
                reconstruction.UnmatchedRecords.Select(record => record.Origin)));
        }
    }

    private void AddStructureAlert(
        IReadOnlyList<SurveyRecord> group,
        SurveyRecord centerRecord,
        string rule,
        string code,
        string layer,
        PlanAlertKind kind,
        string stableKeyDiscriminator,
        ICollection<CircleAlertPlanItem> alerts,
        ICollection<XDataPlanItem> xdata)
    {
        var stableKey = StableKey(centerRecord, DrawingItemKind.CircleAlert, $"{stableKeyDiscriminator}:{rule}");
        var context = new DrawingItemContext(
            PlanItemId.FromStableKey(stableKey),
            stableKey,
            layer,
            rule,
            group.Select(record => record.Origin));
        var alert = new CircleAlertPlanItem(
            context,
            kind,
            PlanAttentionTarget.Block,
            Point(centerRecord),
            _options.AttentionRadius,
            1,
            code,
            group.Count);
        alerts.Add(alert);
        xdata.Add(CreateXData(context, "ATENCAO", code, alert.Center));
    }

    private static void SuppressText(
        SurveyRecord record,
        TextPlanKind kind,
        SurveyRecord retained,
        ICollection<SuppressedPlanItem> suppressed)
    {
        var discriminator = kind == TextPlanKind.Depth ? "depth" : "diameter";
        suppressed.Add(new SuppressedPlanItem(
            ItemId(record, DrawingItemKind.Text, discriminator),
            DrawingItemKind.Text,
            record.Interpretation.Rule,
            "Texto próximo com o mesmo código normalizado.",
            [record.Origin],
            ItemId(retained, DrawingItemKind.Text, discriminator)));
    }

    private static DrawingItemContext Context(SurveyRecord record, DrawingItemKind kind, string discriminator, string layer)
    {
        var stableKey = StableKey(record, kind, discriminator);
        return new DrawingItemContext(
            PlanItemId.FromStableKey(stableKey),
            stableKey,
            layer,
            record.Interpretation.Rule.Length > 0 ? record.Interpretation.Rule : "SEM_REGRA",
            [record.Origin]);
    }

    private static PlanItemId ItemId(SurveyRecord record, DrawingItemKind kind, string discriminator) =>
        PlanItemId.FromStableKey(StableKey(record, kind, discriminator));

    private static string StableKey(SurveyRecord record, DrawingItemKind kind, string discriminator) =>
        $"{record.Origin.SourceFileId}:{record.Origin.SourceRecordIndex}:{kind}:{record.Interpretation.Rule}:{discriminator}";

    private static XDataPlanItem CreateXData(DrawingItemContext context, string type, string code, PlanPoint3 origin) =>
        new(
            context.Id,
            XDataApplication,
            new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["CODIGO"] = code,
                ["ORIGEM"] = FormattableString.Invariant($"{origin.X:F8},{origin.Y:F8},{origin.Z:F8}"),
                ["REGISTRO"] = context.StableKey,
                ["TIPO"] = type,
                ["SOURCE_FILE"] = context.Origins[0].SourceFileName,
                ["SOURCE_LINE"] = context.Origins[0].SourceLineNumber.ToString(CultureInfo.InvariantCulture),
            });

    private static PlanPoint3 Point(SurveyRecord record) => new(record.Point.Easting, record.Point.Northing, 0);

    private static bool IsTextType(string type) => type is "Texto" or "Texto + Bloco";

    private static bool IsBlockType(string type) => type is "Bloco" or "Texto + Bloco";

    private static bool IsPvRule(string rule) => rule.StartsWith("PV", StringComparison.Ordinal) || rule == "POSSIVEL_PV";

    private static bool IsRainRule(string rule) => rule is "GRELHA" or "BOCA_LOBO";

    private static bool IsBoxRule(string rule) => rule.StartsWith("CX", StringComparison.Ordinal);

    private static bool IsOutlineStructureRule(string rule) => IsRainRule(rule) || IsBoxRule(rule);

    private static string FormatCompact(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);

    private static double SquaredDistance(Point2 first, Point2 second)
    {
        var x = first.X - second.X;
        var y = first.Y - second.Y;
        return (x * x) + (y * y);
    }

    private readonly record struct BlockExactKey(string Rule, string Definition, double X, double Y, double Z);

    private readonly record struct Point2(double X, double Y);

    private sealed class SpatialRecordGrid
    {
        private readonly double _cellSize;
        private readonly Dictionary<(long X, long Y), List<SurveyRecord>> _cells = [];

        public SpatialRecordGrid(double distance) => _cellSize = distance > 0 ? distance : 1;

        public void Add(SurveyRecord record)
        {
            var key = Cell(record.Point.Easting, record.Point.Northing);
            if (!_cells.TryGetValue(key, out var values))
            {
                values = [];
                _cells.Add(key, values);
            }

            values.Add(record);
        }

        public SurveyRecord? FindWithin(double x, double y, double distance)
        {
            if (distance <= 0)
            {
                return null;
            }

            var cell = Cell(x, y);
            for (var offsetX = -1; offsetX <= 1; offsetX++)
            {
                for (var offsetY = -1; offsetY <= 1; offsetY++)
                {
                    if (!_cells.TryGetValue((cell.X + offsetX, cell.Y + offsetY), out var records))
                    {
                        continue;
                    }

                    foreach (var record in records)
                    {
                        var deltaX = x - record.Point.Easting;
                        var deltaY = y - record.Point.Northing;
                        if (Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY)) < distance)
                        {
                            return record;
                        }
                    }
                }
            }

            return null;
        }

        private (long X, long Y) Cell(double x, double y) => ((long)Math.Floor(x / _cellSize), (long)Math.Floor(y / _cellSize));
    }

    private sealed class SpatialItemGrid
    {
        private readonly double _cellSize;
        private readonly Dictionary<(long X, long Y), List<(double X, double Y, PlanItemId Id)>> _cells = [];

        public SpatialItemGrid(double distance) => _cellSize = distance > 0 ? distance : 1;

        public void Add(double x, double y, PlanItemId id)
        {
            var key = Cell(x, y);
            if (!_cells.TryGetValue(key, out var values))
            {
                values = [];
                _cells.Add(key, values);
            }

            values.Add((x, y, id));
        }

        public PlanItemId? FindWithin(double x, double y, double distance)
        {
            if (distance <= 0)
            {
                return null;
            }

            var cell = Cell(x, y);
            for (var offsetX = -1; offsetX <= 1; offsetX++)
            {
                for (var offsetY = -1; offsetY <= 1; offsetY++)
                {
                    if (!_cells.TryGetValue((cell.X + offsetX, cell.Y + offsetY), out var values))
                    {
                        continue;
                    }

                    foreach (var value in values)
                    {
                        var deltaX = x - value.X;
                        var deltaY = y - value.Y;
                        if (Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY)) < distance)
                        {
                            return value.Id;
                        }
                    }
                }
            }

            return null;
        }

        private (long X, long Y) Cell(double x, double y) => ((long)Math.Floor(x / _cellSize), (long)Math.Floor(y / _cellSize));
    }

    private sealed class NearestPointIndex
    {
        private readonly Node? _root;

        public NearestPointIndex(IReadOnlyList<Point2> points) => _root = Build(points.ToArray(), 0);

        public double DistanceToNearest(double x, double y)
        {
            if (_root is null)
            {
                return double.PositiveInfinity;
            }

            var nearestSquared = double.PositiveInfinity;
            Search(_root, new Point2(x, y), ref nearestSquared);
            return Math.Sqrt(nearestSquared);
        }

        private static Node? Build(Point2[] points, int depth)
        {
            if (points.Length == 0)
            {
                return null;
            }

            var splitOnX = depth % 2 == 0;
            Array.Sort(points, (left, right) => (splitOnX ? left.X : left.Y).CompareTo(splitOnX ? right.X : right.Y));
            var middle = points.Length / 2;
            return new Node(
                points[middle],
                splitOnX,
                Build(points.Take(middle).ToArray(), depth + 1),
                Build(points.Skip(middle + 1).ToArray(), depth + 1));
        }

        private static void Search(Node node, Point2 target, ref double nearestSquared)
        {
            nearestSquared = Math.Min(nearestSquared, SquaredDistance(node.Point, target));
            var delta = node.SplitOnX ? target.X - node.Point.X : target.Y - node.Point.Y;
            var near = delta <= 0 ? node.Left : node.Right;
            var far = delta <= 0 ? node.Right : node.Left;
            if (near is not null)
            {
                Search(near, target, ref nearestSquared);
            }

            if (far is not null && (delta * delta) < nearestSquared)
            {
                Search(far, target, ref nearestSquared);
            }
        }

        private sealed record Node(Point2 Point, bool SplitOnX, Node? Left, Node? Right);
    }
}

internal static class DrawingPlanCatalog
{
    private static readonly IReadOnlyDictionary<string, BlockDefinition> Blocks = BuildBlocks();

    public static NetworkDefinition Network(string? network)
    {
        var definition = NetworkCadCatalog.Resolve(network);
        return new NetworkDefinition(definition.TextLayer, definition.CurveLayers);
    }

    public static bool TryBlock(string rule, out BlockDefinition definition) => Blocks.TryGetValue(rule, out definition!);

    private static IReadOnlyDictionary<string, BlockDefinition> BuildBlocks()
    {
        var definitions = new[]
        {
            new BlockDefinition("PV_ESGOTO_SANITARIO", ["PV_ES", "PVE", "PV_ENCOBERTO_PROF"], "pv_esgoto_sanitario", "pv_esgoto_sanitario.dwg", "PV", PlanRotationMode.Fixed),
            new BlockDefinition("PV_PLUVIAL", ["PV_AP"], "pv_pluvial", "pv_pluvial.dwg", "PV", PlanRotationMode.Fixed),
            new BlockDefinition("PV_TELEFONE", ["PV_TEL_LEGADO"], "pv_telefone", "pv_telefone.dwg", "PV", PlanRotationMode.Fixed),
            new BlockDefinition("PV_GENERICO", ["PV_AF", "PV_AF_ENCOBERTO", "PV_GENERICO", "PV_ENCOBERTO", "POSSIVEL_PV"], "pv", "pv.dwg", "PV", PlanRotationMode.Fixed),
            new BlockDefinition("REGISTRO_AGUA_POTAVEL", ["REGISTRO_AF"], "rg_agua_potavel", "rg_agua_potavel.dwg", "RG_AGUA_POTAVEL", PlanRotationMode.FollowNetwork),
            new BlockDefinition("HIDRANTE", ["HIDRANTE"], "hidrante", "hidrante.dwg", "HIDRANTE", PlanRotationMode.Fixed),
            // AutoCAD symbol names are case-insensitive. Legacy drawings commonly
            // contain POSTE, so use an app-owned name to guarantee poste.dwg is
            // loaded from the GeoAutoEngine bundle rather than reusing that block.
            new BlockDefinition("POSTE", ["PT", "PTIL", "POSTE"], "GEOAUTO_POSTE", "poste.dwg", "POSTE", PlanRotationMode.Fixed),
        };
        return definitions.SelectMany(definition => definition.Rules.Select(rule => (rule, definition)))
            .ToDictionary(item => item.rule, item => item.definition, StringComparer.Ordinal);
    }

    internal sealed record NetworkDefinition(string TextLayer, IReadOnlyList<string> CurveLayers);

    internal sealed record BlockDefinition(
        string Key,
        IReadOnlyList<string> Rules,
        string BlockName,
        string AssetFileName,
        string Family,
        PlanRotationMode RotationMode,
        string? Layer = null,
        double ScaleX = 1,
        double ScaleY = 1,
        double ScaleZ = 1,
        double FixedRotation = 0,
        BlockAlignmentMode AlignmentMode = BlockAlignmentMode.InsertionPoint);
}
