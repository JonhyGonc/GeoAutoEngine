using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using GeoAutoEngine.Application;
using GeoAutoEngine.Domain;
using AutoCadApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace GeoAutoEngine.Cad2026;

public sealed record AutoCadDrawingPlanApplyOptions
{
    public AutoCadDrawingPlanApplyOptions(
        string blockAssetDirectory,
        int? injectFailureAfterCreatedItems = null,
        bool regenerateAfterCommit = true,
        DrawingPlanProcessingMode processingMode = DrawingPlanProcessingMode.Complete)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blockAssetDirectory);
        if (injectFailureAfterCreatedItems is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(injectFailureAfterCreatedItems));
        }

        BlockAssetDirectory = Path.GetFullPath(blockAssetDirectory);
        InjectFailureAfterCreatedItems = injectFailureAfterCreatedItems;
        RegenerateAfterCommit = regenerateAfterCommit;
        ProcessingMode = processingMode;
    }

    public string BlockAssetDirectory { get; }

    public int? InjectFailureAfterCreatedItems { get; }

    public bool RegenerateAfterCommit { get; }

    public DrawingPlanProcessingMode ProcessingMode { get; }

}

public sealed class AutoCadDrawingPlanApplier : IDrawingPlanApplier
{
    private readonly AutoCadDrawingPlanApplyOptions _options;

    public AutoCadDrawingPlanApplier(AutoCadDrawingPlanApplyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public ValueTask<DrawingPlanApplicationResult> ApplyAsync(
        DrawingPlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var result = Apply(plan, cancellationToken);
        return new ValueTask<DrawingPlanApplicationResult>(result);
    }

    private DrawingPlanApplicationResult Apply(DrawingPlan plan, CancellationToken cancellationToken)
    {
        var diagnostics = new List<Diagnostic>();
        var pendingCount = 0;

        var selection = DrawingPlanMaterializationSelection.Create(plan, _options.ProcessingMode);
        var xdataByTarget = selection.XData.ToDictionary(item => item.TargetItemId);
        var applicableItemIds = selection.Texts.Select(item => item.Context.Id)
            .Concat(selection.Blocks.Select(item => item.Context.Id))
            .Concat(selection.RainStructures.Select(item => item.Context.Id))
            .Concat(selection.BoxStructures.Select(item => item.Context.Id))
            .Concat(selection.CircleAlerts.Select(item => item.Context.Id))
            .ToArray();
        var missingMetadata = applicableItemIds.Where(id => !xdataByTarget.ContainsKey(id)).ToArray();
        if (missingMetadata.Length > 0)
        {
            diagnostics.Add(new Diagnostic(
                "CAD_XDATA_METADATA_MISSING",
                $"{missingMetadata.Length} item(ns) aplicável(is) não possuem metadados XData.",
                DiagnosticSeverity.Error,
                DiagnosticScope.Batch));
            return Failure(diagnostics, pendingCount, rolledBack: false);
        }

        var document = AutoCadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            diagnostics.Add(new Diagnostic(
                "CAD_ACTIVE_DOCUMENT_MISSING",
                "O AutoCAD não possui documento ativo.",
                DiagnosticSeverity.Error,
                DiagnosticScope.Batch));
            return Failure(diagnostics, pendingCount, rolledBack: false);
        }

        var created = 0;
        var skipped = 0;
        var transactionStarted = false;
        var stage = "inicialização";
        try
        {
            using var documentLock = document.LockDocument();
            using var transaction = document.Database.TransactionManager.StartTransaction();
            transactionStarted = true;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var database = document.Database;
                stage = "garantia dos layers do plano";
                var selectedLayerNames = new HashSet<string>(
                    selection.Texts.Select(item => item.Context.Layer)
                        .Concat(selection.Blocks.Select(item => item.Context.Layer))
                        .Concat(selection.RainStructures.Select(item => item.Context.Layer))
                        .Concat(selection.BoxStructures.Select(item => item.Context.Layer))
                        .Concat(selection.CircleAlerts.Select(item => item.Context.Layer)),
                    StringComparer.OrdinalIgnoreCase);
                var layerIds = EnsureLayers(
                    database,
                    transaction,
                    plan.Layers.Where(item => selectedLayerNames.Contains(item.Name)));
                stage = "garantia dos estilos de texto";
                var selectedTextStyles = new HashSet<string>(
                    selection.Texts.Select(item => item.StyleName),
                    StringComparer.Ordinal);
                var textStyleIds = EnsureTextStyles(
                    database,
                    transaction,
                    plan.TextStyles.Where(item => selectedTextStyles.Contains(item.Name)));
                var applicationNames = selection.XData.Select(item => item.ApplicationName)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                stage = "registro de XData";
                EnsureRegisteredApplications(database, transaction, applicationNames);
                stage = "leitura do espaço atual";
                var currentSpace = (BlockTableRecord)transaction.GetObject(database.CurrentSpaceId, OpenMode.ForWrite);
                var existingEntities = ReadExistingEntities(currentSpace, transaction, applicationNames);
                stage = "indexação da geometria existente";
                using var geometry = new AutoCadNetworkGeometryResolver(currentSpace, transaction);
                geometry.AddPlannedObstacles(
                    plan.Blocks,
                    plan.RainStructures.Cast<IPolygonStructurePlanItem>()
                        .Concat(plan.BoxStructures));
                stage = "resolução geométrica dos textos";
                var resolvedTexts = geometry.ResolveTexts(selection.Texts, diagnostics);
                pendingCount = geometry.LastLayoutStatistics?.UnresolvedCount ?? 0;
                stage = "resolução geométrica dos blocos";
                var resolvedBlocks = geometry.ResolveBlocks(selection.Blocks, diagnostics);
                stage = "garantia dos layers resolvidos";
                foreach (var pair in EnsureLayers(
                    database,
                    transaction,
                    resolvedTexts.Select(item => new LayerPlanDefinition(item.Layer))))
                {
                    layerIds[pair.Key] = pair.Value;
                }

                stage = "aplicação dos blocos";
                foreach (var placement in resolvedBlocks)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var item = placement.Item;
                    var blockDefinitionId = EnsureBlockDefinition(database, transaction, item);
                    if (TryUpdateBlock(
                            existingEntities,
                            item.Context.StableKey,
                            placement,
                            blockDefinitionId,
                            layerIds[item.Context.Layer],
                            xdataByTarget[item.Context.Id],
                            plan.Id,
                            transaction))
                    {
                        skipped++;
                        continue;
                    }

                    var entity = CreateBlock(placement, blockDefinitionId, layerIds[item.Context.Layer]);
                    Append(currentSpace, transaction, entity, xdataByTarget[item.Context.Id], plan.Id);
                    RegisterCreated(item.Context.StableKey, entity.ObjectId, existingEntities, ref created);
                }

                stage = "aplicação das estruturas";
                foreach (var item in selection.RainStructures.Cast<IPolygonStructurePlanItem>()
                    .Concat(selection.BoxStructures))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (TryUpdatePolygonStructure(
                            existingEntities,
                            item.Context.StableKey,
                            item,
                            layerIds[item.Context.Layer],
                            xdataByTarget[item.Context.Id],
                            plan.Id,
                            transaction))
                    {
                        skipped++;
                        continue;
                    }

                    var entity = CreatePolygonStructure(item, layerIds[item.Context.Layer]);
                    Append(currentSpace, transaction, entity, xdataByTarget[item.Context.Id], plan.Id);
                    RegisterCreated(item.Context.StableKey, entity.ObjectId, existingEntities, ref created);
                }

                stage = "aplicação dos textos";
                foreach (var placement in resolvedTexts)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var item = placement.Item;
                    if (TryUpdateText(
                            existingEntities,
                            item.Context.StableKey,
                            placement,
                            textStyleIds,
                            layerIds[placement.Layer],
                            xdataByTarget[item.Context.Id],
                            plan.Id,
                            transaction))
                    {
                        skipped++;
                        continue;
                    }

                    var entity = CreateText(placement, textStyleIds, layerIds[placement.Layer]);
                    Append(
                        currentSpace,
                        transaction,
                        entity,
                        xdataByTarget[item.Context.Id],
                        plan.Id,
                        LayoutMetadata(placement));
                    RegisterCreated(item.Context.StableKey, entity.ObjectId, existingEntities, ref created);
                }

                stage = "aplicação dos alertas";
                foreach (var item in selection.CircleAlerts)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (TryUpdateAlert(
                            existingEntities,
                            item.Context.StableKey,
                            item,
                            layerIds[item.Context.Layer],
                            xdataByTarget[item.Context.Id],
                            plan.Id,
                            transaction))
                    {
                        skipped++;
                        continue;
                    }

                    var entity = CreateAlert(item, layerIds[item.Context.Layer]);
                    Append(currentSpace, transaction, entity, xdataByTarget[item.Context.Id], plan.Id);
                    RegisterCreated(item.Context.StableKey, entity.ObjectId, existingEntities, ref created);
                }

                stage = "commit da transação";
                transaction.Commit();
            }
            catch
            {
                transaction.Abort();
                throw;
            }
        }
        catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
        {
            diagnostics.Add(new Diagnostic(
                "CAD_APPLY_ROLLED_BACK",
                $"A aplicação foi revertida integralmente na etapa '{stage}': {exception.Message}",
                DiagnosticSeverity.Error,
                DiagnosticScope.Batch));
            return Failure(diagnostics, pendingCount, rolledBack: transactionStarted);
        }

        var regenerated = false;
        if (_options.RegenerateAfterCommit && created > 0)
        {
            try
            {
                document.Editor.Regen();
                regenerated = true;
            }
            catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
            {
                diagnostics.Add(new Diagnostic(
                    "CAD_REGEN_FAILED",
                    $"As entidades foram confirmadas, mas a regeneração falhou: {exception.Message}",
                    DiagnosticSeverity.Warning,
                    DiagnosticScope.Batch));
            }
        }

        return new DrawingPlanApplicationResult(
            true,
            created,
            skipped,
            pendingCount,
            0,
            false,
            regenerated,
            diagnostics.AsReadOnly());
    }

    private void RegisterCreated(
        string stableKey,
        ObjectId objectId,
        IDictionary<string, ObjectId> existingEntities,
        ref int created)
    {
        existingEntities[stableKey] = objectId;
        created++;
        if (_options.InjectFailureAfterCreatedItems.HasValue && created >= _options.InjectFailureAfterCreatedItems.Value)
        {
            throw new InvalidOperationException($"Falha injetada após {created} item(ns) para validar rollback.");
        }
    }

    private static DrawingPlanApplicationResult Failure(
        IReadOnlyList<Diagnostic> diagnostics,
        int pendingCount,
        bool rolledBack) =>
        new(false, 0, 0, pendingCount, 1, rolledBack, false, diagnostics);

    private static Dictionary<string, ObjectId> EnsureLayers(
        Database database,
        Transaction transaction,
        IEnumerable<LayerPlanDefinition> definitions)
    {
        var result = new Dictionary<string, ObjectId>(StringComparer.OrdinalIgnoreCase);
        var table = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
        foreach (var definition in definitions)
        {
            try
            {
                if (table.Has(definition.Name))
                {
                    result[definition.Name] = table[definition.Name];
                    continue;
                }

                table.UpgradeOpen();
                var record = new LayerTableRecord { Name = definition.Name };
                if (definition.ColorIndex.HasValue)
                {
                    record.Color = Color.FromColorIndex(ColorMethod.ByAci, checked((short)definition.ColorIndex.Value));
                }

                table.Add(record);
                transaction.AddNewlyCreatedDBObject(record, true);
                result[definition.Name] = record.ObjectId;
            }
            catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
            {
                throw new InvalidOperationException(
                    $"Não foi possível garantir o layer '{definition.Name}': {exception.Message}",
                    exception);
            }
        }

        return result;
    }

    private static IReadOnlyDictionary<string, ObjectId> EnsureTextStyles(
        Database database,
        Transaction transaction,
        IEnumerable<TextStylePlanDefinition> definitions)
    {
        var result = new Dictionary<string, ObjectId>(StringComparer.Ordinal);
        var table = (TextStyleTable)transaction.GetObject(database.TextStyleTableId, OpenMode.ForRead);
        foreach (var definition in definitions)
        {
            TextStyleTableRecord record;
            if (table.Has(definition.Name))
            {
                record = (TextStyleTableRecord)transaction.GetObject(table[definition.Name], OpenMode.ForWrite);
            }
            else
            {
                table.UpgradeOpen();
                record = new TextStyleTableRecord { Name = definition.Name };
                table.Add(record);
                transaction.AddNewlyCreatedDBObject(record, true);
            }

            record.FileName = definition.FontFileName;
            record.TextSize = 0;
            result.Add(definition.Name, record.ObjectId);
        }

        return result;
    }

    private static void EnsureRegisteredApplications(
        Database database,
        Transaction transaction,
        IEnumerable<string> applicationNames)
    {
        var table = (RegAppTable)transaction.GetObject(database.RegAppTableId, OpenMode.ForRead);
        foreach (var applicationName in applicationNames)
        {
            if (table.Has(applicationName))
            {
                continue;
            }

            table.UpgradeOpen();
            var record = new RegAppTableRecord { Name = applicationName };
            table.Add(record);
            transaction.AddNewlyCreatedDBObject(record, true);
        }
    }

    private static Dictionary<string, ObjectId> ReadExistingEntities(
        BlockTableRecord currentSpace,
        Transaction transaction,
        IReadOnlyList<string> applicationNames)
    {
        var result = new Dictionary<string, ObjectId>(StringComparer.Ordinal);
        var duplicates = new List<ObjectId>();
        foreach (ObjectId objectId in currentSpace)
        {
            if (transaction.GetObject(objectId, OpenMode.ForRead, openErased: false) is not Entity entity)
            {
                continue;
            }

            foreach (var applicationName in applicationNames)
            {
                using var data = entity.GetXDataForApplication(applicationName);
                var stableKey = XDataValue(data, "REGISTRO");
                if (!string.IsNullOrWhiteSpace(stableKey))
                {
                    if (result.ContainsKey(stableKey!))
                    {
                        duplicates.Add(objectId);
                    }
                    else
                    {
                        result.Add(stableKey!, objectId);
                    }
                    break;
                }
            }
        }

        foreach (var objectId in duplicates)
        {
            if (transaction.GetObject(objectId, OpenMode.ForWrite, openErased: false) is Entity duplicate)
            {
                duplicate.Erase();
            }
        }

        return result;
    }

    private static bool TryUpdateText(
        IDictionary<string, ObjectId> existingEntities,
        string stableKey,
        ResolvedTextPlacement placement,
        IReadOnlyDictionary<string, ObjectId> textStyleIds,
        ObjectId layerId,
        XDataPlanItem metadata,
        DrawingPlanId planId,
        Transaction transaction)
    {
        if (!TryOpenExisting<DBText>(existingEntities, stableKey, transaction, out var text))
        {
            return false;
        }

        ApplyText(text!, placement, textStyleIds, layerId);
        SetMetadata(text!, metadata, planId, LayoutMetadata(placement));
        return true;
    }

    private static bool TryUpdateBlock(
        IDictionary<string, ObjectId> existingEntities,
        string stableKey,
        ResolvedBlockPlacement placement,
        ObjectId blockDefinitionId,
        ObjectId layerId,
        XDataPlanItem metadata,
        DrawingPlanId planId,
        Transaction transaction)
    {
        if (!TryOpenExisting<BlockReference>(existingEntities, stableKey, transaction, out var block))
        {
            return false;
        }

        if (block!.BlockTableRecord != blockDefinitionId)
        {
            block.Erase();
            existingEntities.Remove(stableKey);
            return false;
        }

        ApplyBlock(block, placement, layerId);
        SetMetadata(block, metadata, planId);
        return true;
    }

    private static bool TryUpdatePolygonStructure(
        IDictionary<string, ObjectId> existingEntities,
        string stableKey,
        IPolygonStructurePlanItem item,
        ObjectId layerId,
        XDataPlanItem metadata,
        DrawingPlanId planId,
        Transaction transaction)
    {
        if (!TryOpenExisting<Polyline>(existingEntities, stableKey, transaction, out var polyline))
        {
            return false;
        }

        ApplyPolygonStructure(polyline!, item, layerId);
        SetMetadata(polyline!, metadata, planId);
        return true;
    }

    private static bool TryUpdateAlert(
        IDictionary<string, ObjectId> existingEntities,
        string stableKey,
        CircleAlertPlanItem item,
        ObjectId layerId,
        XDataPlanItem metadata,
        DrawingPlanId planId,
        Transaction transaction)
    {
        if (!TryOpenExisting<Circle>(existingEntities, stableKey, transaction, out var circle))
        {
            return false;
        }

        ApplyAlert(circle!, item, layerId);
        SetMetadata(circle!, metadata, planId);
        return true;
    }

    private static bool TryOpenExisting<TEntity>(
        IDictionary<string, ObjectId> existingEntities,
        string stableKey,
        Transaction transaction,
        out TEntity? entity)
        where TEntity : Entity
    {
        entity = null;
        if (!existingEntities.TryGetValue(stableKey, out var objectId))
        {
            return false;
        }

        var existing = transaction.GetObject(objectId, OpenMode.ForWrite, openErased: false) as Entity;
        if (existing is TEntity typed)
        {
            entity = typed;
            return true;
        }

        existing?.Erase();
        existingEntities.Remove(stableKey);
        return false;
    }

    private static DBText CreateText(
        ResolvedTextPlacement placement,
        IReadOnlyDictionary<string, ObjectId> textStyleIds,
        ObjectId layerId)
    {
        var text = new DBText();
        ApplyText(text, placement, textStyleIds, layerId);
        return text;
    }

    private static void ApplyText(
        DBText text,
        ResolvedTextPlacement placement,
        IReadOnlyDictionary<string, ObjectId> textStyleIds,
        ObjectId layerId)
    {
        var item = placement.Item;
        text.LayerId = layerId;
        text.Position = placement.Position;
        text.TextString = item.Content;
        text.Height = item.Height;
        text.Rotation = placement.Rotation;
        text.TextStyleId = textStyleIds[item.StyleName];
        text.ColorIndex = item.ColorIndex ?? 256;
    }

    private ObjectId EnsureBlockDefinition(Database database, Transaction transaction, BlockPlanItem item)
    {
        var table = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
        if (table.Has(item.BlockName))
        {
            return table[item.BlockName];
        }

        var assetPath = Path.Combine(_options.BlockAssetDirectory, item.AssetFileName);
        if (!File.Exists(assetPath))
        {
            throw new FileNotFoundException($"Ativo de bloco não encontrado: {assetPath}", assetPath);
        }

        using var sourceDatabase = new Database(buildDefaultDrawing: false, noDocument: true);
        sourceDatabase.ReadDwgFile(assetPath, FileOpenMode.OpenForReadAndAllShare, allowCPConversion: true, password: string.Empty);
        return database.Insert(item.BlockName, sourceDatabase, preserveSourceDatabase: false);
    }

    private static BlockReference CreateBlock(
        ResolvedBlockPlacement placement,
        ObjectId blockDefinitionId,
        ObjectId layerId)
    {
        var block = new BlockReference(placement.Position, blockDefinitionId);
        ApplyBlock(block, placement, layerId);
        return block;
    }

    private static void ApplyBlock(BlockReference block, ResolvedBlockPlacement placement, ObjectId layerId)
    {
        block.LayerId = layerId;
        block.Position = placement.Position;
        block.Rotation = placement.Rotation;
        block.ScaleFactors = new Scale3d(placement.Item.Scale.X, placement.Item.Scale.Y, placement.Item.Scale.Z);
    }

    private static Polyline CreatePolygonStructure(IPolygonStructurePlanItem item, ObjectId layerId)
    {
        var polyline = new Polyline(item.Vertices.Count);
        ApplyPolygonStructure(polyline, item, layerId);
        return polyline;
    }

    private static void ApplyPolygonStructure(Polyline polyline, IPolygonStructurePlanItem item, ObjectId layerId)
    {
        while (polyline.NumberOfVertices > item.Vertices.Count)
        {
            polyline.RemoveVertexAt(polyline.NumberOfVertices - 1);
        }

        polyline.LayerId = layerId;
        polyline.Closed = true;
        polyline.Elevation = item.Vertices[0].Z;
        for (var index = 0; index < item.Vertices.Count; index++)
        {
            var vertex = item.Vertices[index];
            var point = new Point2d(vertex.X, vertex.Y);
            if (index < polyline.NumberOfVertices)
            {
                polyline.SetPointAt(index, point);
                polyline.SetBulgeAt(index, 0);
                polyline.SetStartWidthAt(index, 0);
                polyline.SetEndWidthAt(index, 0);
            }
            else
            {
                polyline.AddVertexAt(index, point, 0, 0, 0);
            }
        }
    }

    private static Circle CreateAlert(CircleAlertPlanItem item, ObjectId layerId)
    {
        var circle = new Circle();
        ApplyAlert(circle, item, layerId);
        return circle;
    }

    private static void ApplyAlert(Circle circle, CircleAlertPlanItem item, ObjectId layerId)
    {
        circle.LayerId = layerId;
        circle.Center = new Point3d(item.Center.X, item.Center.Y, item.Center.Z);
        circle.Normal = Vector3d.ZAxis;
        circle.Radius = item.Radius;
        circle.ColorIndex = item.ColorIndex;
    }

    private static void Append(
        BlockTableRecord currentSpace,
        Transaction transaction,
        Entity entity,
        XDataPlanItem metadata,
        DrawingPlanId planId,
        IEnumerable<KeyValuePair<string, string>>? additionalValues = null)
    {
        currentSpace.AppendEntity(entity);
        transaction.AddNewlyCreatedDBObject(entity, true);
        SetMetadata(entity, metadata, planId, additionalValues);
    }

    private static void SetMetadata(
        Entity entity,
        XDataPlanItem metadata,
        DrawingPlanId planId,
        IEnumerable<KeyValuePair<string, string>>? additionalValues = null)
    {
        var values = new List<TypedValue>
        {
            new((int)DxfCode.ExtendedDataRegAppName, metadata.ApplicationName),
        };
        values.AddRange(metadata.Values.Select(pair =>
            new TypedValue((int)DxfCode.ExtendedDataAsciiString, $"{pair.Key}={pair.Value}")));
        if (additionalValues is not null)
        {
            values.AddRange(additionalValues.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair =>
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, $"{pair.Key}={pair.Value}")));
        }

        values.Add(new TypedValue((int)DxfCode.ExtendedDataAsciiString, $"PLAN_ID={planId}"));
        using var buffer = new ResultBuffer(values.ToArray());
        entity.XData = buffer;
    }

    private static IReadOnlyDictionary<string, string> LayoutMetadata(ResolvedTextPlacement placement) =>
        new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["AUTO_POSITION"] = FormattableString.Invariant(
                $"{placement.Position.X:F8},{placement.Position.Y:F8},{placement.Rotation:R}"),
            ["LAYOUT_CANDIDATE"] = placement.CandidateId,
            ["LAYOUT_DECISION"] = placement.DecisionKind.ToString().ToUpperInvariant(),
            ["LAYOUT_MODE"] = "STANDARD",
            ["LAYOUT_STATUS"] = placement.LayoutStatus.ToString().ToUpperInvariant(),
            ["LAYOUT_VERSION"] = "2A1",
        };

    internal static string? XDataValue(ResultBuffer? data, string key)
    {
        if (data is null)
        {
            return null;
        }

        var prefix = $"{key}=";
        return data.AsArray()
            .Where(value => value.TypeCode == (int)DxfCode.ExtendedDataAsciiString)
            .Select(value => Convert.ToString(value.Value, System.Globalization.CultureInfo.InvariantCulture))
            .FirstOrDefault(value => value?.StartsWith(prefix, StringComparison.Ordinal) == true)?.Substring(prefix.Length);
    }
}

public sealed record AutoCadGeneratedEntitySnapshot(
    string EntityType,
    string Layer,
    string StableKey,
    string? Text,
    string? BlockName,
    double[]? Position,
    double[]? Scale,
    double? Rotation,
    double? Radius,
    int ColorIndex,
    int VertexCount,
    IReadOnlyDictionary<string, string> XDataValues,
    double? TextHeight = null);

public sealed record AutoCadGeneratedDrawingSnapshot(
    int GeneratedEntityCount,
    IReadOnlyDictionary<string, int> CountsByType,
    IReadOnlyList<AutoCadGeneratedEntitySnapshot> Entities,
    IReadOnlyList<string> Layers,
    IReadOnlyList<string> TextStyles,
    IReadOnlyList<string> RegisteredApplications,
    IReadOnlyList<string>? PaperLayouts = null,
    string? StandardSheetLayout = null,
    IReadOnlyList<string>? StandardSheetScaleTexts = null);

public static class AutoCadGeneratedDrawingInspector
{
    public static AutoCadGeneratedDrawingSnapshot Capture(string applicationName = "GEOAUTOENGINE")
    {
        var document = AutoCadApplication.DocumentManager.MdiActiveDocument
            ?? throw new InvalidOperationException("AutoCAD has no active document.");
        using var transaction = document.Database.TransactionManager.StartOpenCloseTransaction();
        var currentSpace = (BlockTableRecord)transaction.GetObject(document.Database.CurrentSpaceId, OpenMode.ForRead);
        var entities = new List<AutoCadGeneratedEntitySnapshot>();
        foreach (ObjectId objectId in currentSpace)
        {
            if (transaction.GetObject(objectId, OpenMode.ForRead, openErased: false) is not Entity entity)
            {
                continue;
            }

            using var data = entity.GetXDataForApplication(applicationName);
            var stableKey = AutoCadDrawingPlanApplier.XDataValue(data, "REGISTRO");
            if (string.IsNullOrWhiteSpace(stableKey))
            {
                continue;
            }

            entities.Add(Snapshot(entity, stableKey!, XDataValues(data), transaction));
        }

        var layers = Names<LayerTable, LayerTableRecord>(document.Database.LayerTableId, transaction);
        var textStyles = Names<TextStyleTable, TextStyleTableRecord>(document.Database.TextStyleTableId, transaction);
        var applications = Names<RegAppTable, RegAppTableRecord>(document.Database.RegAppTableId, transaction);
        var sheet = AutoCadStandardSheetService.Inspect(document.Database, transaction);
        transaction.Commit();
        var ordered = entities.OrderBy(item => item.StableKey, StringComparer.Ordinal).ToArray();
        var counts = ordered.GroupBy(item => item.EntityType, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        return new AutoCadGeneratedDrawingSnapshot(
            ordered.Length,
            counts,
            Array.AsReadOnly(ordered),
            layers,
            textStyles,
            applications,
            sheet.PaperLayouts,
            sheet.MarkedLayoutName,
            sheet.ScaleTexts);
    }

    private static AutoCadGeneratedEntitySnapshot Snapshot(
        Entity entity,
        string stableKey,
        IReadOnlyDictionary<string, string> xdata,
        Transaction transaction) => entity switch
        {
            DBText text => new(
                nameof(DBText), text.Layer, stableKey, text.TextString, null, Point(text.Position), null,
                text.Rotation, null, text.ColorIndex, 0, xdata, text.Height),
            BlockReference block => new(
                nameof(BlockReference), block.Layer, stableKey, null,
                ((BlockTableRecord)transaction.GetObject(block.BlockTableRecord, OpenMode.ForRead)).Name,
                Point(block.Position),
                [block.ScaleFactors.X, block.ScaleFactors.Y, block.ScaleFactors.Z],
                block.Rotation, null, block.ColorIndex, 0, xdata),
            Polyline polyline => new(
                nameof(Polyline), polyline.Layer, stableKey, null, null, null, null, null, null,
                polyline.ColorIndex, polyline.NumberOfVertices, xdata),
            Circle circle => new(
                nameof(Circle), circle.Layer, stableKey, null, null, Point(circle.Center), null, null,
                circle.Radius, circle.ColorIndex, 0, xdata),
            _ => new(
                entity.GetType().Name, entity.Layer, stableKey, null, null, null, null, null, null,
                entity.ColorIndex, 0, xdata),
        };

    private static double[] Point(Point3d point) => [point.X, point.Y, point.Z];

    private static IReadOnlyDictionary<string, string> XDataValues(ResultBuffer data)
    {
        var values = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in data.AsArray().Where(value => value.TypeCode == (int)DxfCode.ExtendedDataAsciiString))
        {
            var text = Convert.ToString(item.Value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
            var separator = text.IndexOf('=');
            if (separator > 0)
            {
                values[text.Substring(0, separator)] = text.Substring(separator + 1);
            }
        }

        return values;
    }

    private static IReadOnlyList<string> Names<TTable, TRecord>(ObjectId tableId, Transaction transaction)
        where TTable : SymbolTable
        where TRecord : SymbolTableRecord
    {
        var table = (TTable)transaction.GetObject(tableId, OpenMode.ForRead);
        return table.Cast<ObjectId>()
            .Select(id => ((TRecord)transaction.GetObject(id, OpenMode.ForRead)).Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
    }
}
