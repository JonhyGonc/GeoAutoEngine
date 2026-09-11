using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using GeoAutoEngine.Application.VideoInspection;

namespace GeoAutoEngine.Cad2026.VideoInspection;

public sealed record AutoCadInspectionCoverageOptions(
    bool AddCoverageLines,
    bool ApplyBreak);

public sealed record AutoCadInspectionCoverageResult(
    double InspectedLength,
    double RemainingLength,
    bool BreakApplied,
    string InspectedLayer,
    string InterruptedLayer);

/// <summary>
/// Cobertura simples por segmento físico. Não recompõe redes e não combina trechos
/// em uma geometria lógica. Depois de um BREAK, o segmento restante é tratado como
/// uma Polyline normal e pode ser associado a outra filmagem.
/// </summary>
public sealed class AutoCadInspectionCoverageController
{
    public const string InspectedLayerName = "GF-VideoInspecao-Inspecionado";
    public const string InterruptedLayerName = "Video-interrompido";
    public const string CoverageRegisteredApplicationName = "GEOAUTOENGINE_VI_COVERAGE_V2";

    private const string DictionaryName = "GEOAUTOENGINE_VIDEO_INSPECTION_COVERAGE_V2";
    private const string InspectedComponent = "COVERAGE_INSPECTED";
    private const string InterruptedComponent = "COVERAGE_INTERRUPTED";
    private const short InspectedLayerColorIndex = 3;
    private const short InterruptedLayerColorIndex = 1;
    private const short ByLayerColorIndex = 256;
    private const double GeometryTolerance = 1e-6;

    public AutoCadInspectionCoverageResult Apply(
        Document document,
        VideoInspectionSession session,
        InspectionSectionSession section,
        IReadOnlyList<PositionedInspectionEvent> positionedEvents,
        AutoCadInspectionCoverageOptions options)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(positionedEvents);
        ArgumentNullException.ThrowIfNull(options);

        var association = section.Association
            ?? throw new InvalidOperationException("O trecho ainda não possui geometria associada.");

        using var documentLock = document.LockDocument();
        using var transaction = document.Database.TransactionManager.StartTransaction();
        EnsureRegisteredApplication(document.Database, transaction);

        var blockTable = (BlockTable)transaction.GetObject(
            document.Database.BlockTableId,
            OpenMode.ForRead);
        var modelSpace = (BlockTableRecord)transaction.GetObject(
            blockTable[BlockTableRecord.ModelSpace],
            OpenMode.ForWrite);

        if (!AutoCadInspectionAssociationStore.TryResolvePolyline(
                document.Database,
                transaction,
                association.GeometryHandle,
                OpenMode.ForWrite,
                out var source) || source is null)
        {
            throw new InvalidOperationException("A Polyline física associada ao trecho não existe mais.");
        }

        // Remove apenas resíduos visuais das implementações antigas para este trecho.
        // Nenhuma Polyline física é apagada aqui.
        EraseLegacyCoverageForSection(modelSpace, transaction, session.ReportId, section.SectionId);

        var state = LoadState(document.Database, transaction, session.ReportId, section.SectionId);
        if (state is not null && !string.Equals(
                state.SourceHandle,
                association.GeometryHandle,
                StringComparison.OrdinalIgnoreCase))
        {
            EraseByHandle(document.Database, transaction, state.InspectedOverlayHandle);
            EraseByHandle(document.Database, transaction, state.InterruptedOverlayHandle);
            DeleteState(document.Database, transaction, session.ReportId, section.SectionId);
            state = null;
        }

        // Quando uma segunda filmagem usa exatamente o segmento que antes estava marcado
        // como interrompido, retire a cobertura vermelha anterior. O segmento físico fica.
        EraseInterruptedOverlaysForSource(
            modelSpace,
            transaction,
            association.GeometryHandle,
            state?.InterruptedOverlayHandle);

        if (state?.BreakApplied == true)
        {
            var remainder = ResolvePolyline(
                document.Database,
                transaction,
                state.RemainderHandle,
                OpenMode.ForRead);
            var inspectedOverlay = ResolvePolyline(
                document.Database,
                transaction,
                state.InspectedOverlayHandle,
                OpenMode.ForWrite);
            var interruptedOverlay = ResolvePolyline(
                document.Database,
                transaction,
                state.InterruptedOverlayHandle,
                OpenMode.ForWrite);

            if (options.AddCoverageLines)
            {
                var inspectedLayerId = EnsureLayer(
                    document.Database,
                    transaction,
                    InspectedLayerName,
                    InspectedLayerColorIndex);
                inspectedOverlay = UpsertOverlay(
                    document.Database,
                    modelSpace,
                    transaction,
                    inspectedOverlay,
                    source,
                    inspectedLayerId,
                    session.ReportId,
                    section.SectionId,
                    InspectedComponent,
                    source.Handle.ToString());

                if (remainder is not null && !HasInspectedOverlayForSource(
                        modelSpace,
                        transaction,
                        remainder.Handle.ToString()))
                {
                    var interruptedLayerId = EnsureLayer(
                        document.Database,
                        transaction,
                        InterruptedLayerName,
                        InterruptedLayerColorIndex);
                    interruptedOverlay = UpsertOverlay(
                        document.Database,
                        modelSpace,
                        transaction,
                        interruptedOverlay,
                        remainder,
                        interruptedLayerId,
                        session.ReportId,
                        section.SectionId,
                        InterruptedComponent,
                        remainder.Handle.ToString());
                }
                else
                {
                    Erase(interruptedOverlay);
                    interruptedOverlay = null;
                }

                BringToFront(modelSpace, transaction, inspectedOverlay, interruptedOverlay);
            }
            else
            {
                Erase(inspectedOverlay);
                Erase(interruptedOverlay);
                inspectedOverlay = null;
                interruptedOverlay = null;
            }

            var inspectedLength = source.Length;
            var remainingLength = remainder?.Length ?? 0.0;
            SaveState(
                document.Database,
                transaction,
                session.ReportId,
                section.SectionId,
                state with
                {
                    InspectedOverlayHandle = HandleOf(inspectedOverlay),
                    InterruptedOverlayHandle = HandleOf(interruptedOverlay),
                });
            transaction.Commit();
            document.Editor.Regen();
            document.Editor.UpdateScreen();
            return new AutoCadInspectionCoverageResult(
                inspectedLength,
                remainingLength,
                true,
                InspectedLayerName,
                InterruptedLayerName);
        }

        var plan = InspectionCoveragePlanner.Create(
            section.InvestigatedMeters,
            source.Length,
            association.Direction,
            positionedEvents);

        Polyline? inspectedOverlayCurrent = ResolvePolyline(
            document.Database,
            transaction,
            state?.InspectedOverlayHandle,
            OpenMode.ForWrite);
        Polyline? interruptedOverlayCurrent = ResolvePolyline(
            document.Database,
            transaction,
            state?.InterruptedOverlayHandle,
            OpenMode.ForWrite);
        Polyline? physicalRemainder = null;
        var breakApplied = false;

        if (plan.RequiresBreak)
        {
            using var split = Split(source, plan.SplitCadDistance);
            var inspectedPiece = association.Direction == InspectionDirection.Forward
                ? split.First
                : split.Second;
            var remainingPiece = association.Direction == InspectionDirection.Forward
                ? split.Second
                : split.First;

            if (options.ApplyBreak)
            {
                // Clone antes de alterar a geometria para preservar propriedades/XData da
                // Polyline física. O ObjectId/Handle da original continua existindo.
                physicalRemainder = (Polyline)source.Clone();
                CopyGeometry(remainingPiece, physicalRemainder);
                CopyGeometry(inspectedPiece, source);
                modelSpace.AppendEntity(physicalRemainder);
                transaction.AddNewlyCreatedDBObject(physicalRemainder, true);
                breakApplied = true;
            }

            if (options.AddCoverageLines)
            {
                var inspectedLayerId = EnsureLayer(
                    document.Database,
                    transaction,
                    InspectedLayerName,
                    InspectedLayerColorIndex);
                var interruptedLayerId = EnsureLayer(
                    document.Database,
                    transaction,
                    InterruptedLayerName,
                    InterruptedLayerColorIndex);

                var inspectedSource = breakApplied ? source : inspectedPiece;
                var interruptedSource = breakApplied ? physicalRemainder! : remainingPiece;
                inspectedOverlayCurrent = UpsertOverlay(
                    document.Database,
                    modelSpace,
                    transaction,
                    inspectedOverlayCurrent,
                    inspectedSource,
                    inspectedLayerId,
                    session.ReportId,
                    section.SectionId,
                    InspectedComponent,
                    source.Handle.ToString());
                interruptedOverlayCurrent = UpsertOverlay(
                    document.Database,
                    modelSpace,
                    transaction,
                    interruptedOverlayCurrent,
                    interruptedSource,
                    interruptedLayerId,
                    session.ReportId,
                    section.SectionId,
                    InterruptedComponent,
                    breakApplied
                        ? physicalRemainder!.Handle.ToString()
                        : source.Handle.ToString());
                BringToFront(
                    modelSpace,
                    transaction,
                    inspectedOverlayCurrent,
                    interruptedOverlayCurrent);
            }
            else
            {
                Erase(inspectedOverlayCurrent);
                Erase(interruptedOverlayCurrent);
                inspectedOverlayCurrent = null;
                interruptedOverlayCurrent = null;
            }
        }
        else if (plan.IsFullyInspected)
        {
            if (options.AddCoverageLines)
            {
                var inspectedLayerId = EnsureLayer(
                    document.Database,
                    transaction,
                    InspectedLayerName,
                    InspectedLayerColorIndex);
                inspectedOverlayCurrent = UpsertOverlay(
                    document.Database,
                    modelSpace,
                    transaction,
                    inspectedOverlayCurrent,
                    source,
                    inspectedLayerId,
                    session.ReportId,
                    section.SectionId,
                    InspectedComponent,
                    source.Handle.ToString());
                Erase(interruptedOverlayCurrent);
                interruptedOverlayCurrent = null;
                BringToFront(modelSpace, transaction, inspectedOverlayCurrent);
            }
            else
            {
                Erase(inspectedOverlayCurrent);
                Erase(interruptedOverlayCurrent);
                inspectedOverlayCurrent = null;
                interruptedOverlayCurrent = null;
            }
        }
        else
        {
            // Sem metragem inspecionada: não existe linha verde nem BREAK; se as linhas
            // estiverem ativas, o segmento inteiro continua interrompido.
            Erase(inspectedOverlayCurrent);
            inspectedOverlayCurrent = null;
            if (options.AddCoverageLines)
            {
                var interruptedLayerId = EnsureLayer(
                    document.Database,
                    transaction,
                    InterruptedLayerName,
                    InterruptedLayerColorIndex);
                interruptedOverlayCurrent = UpsertOverlay(
                    document.Database,
                    modelSpace,
                    transaction,
                    interruptedOverlayCurrent,
                    source,
                    interruptedLayerId,
                    session.ReportId,
                    section.SectionId,
                    InterruptedComponent,
                    source.Handle.ToString());
                BringToFront(modelSpace, transaction, interruptedOverlayCurrent);
            }
            else
            {
                Erase(interruptedOverlayCurrent);
                interruptedOverlayCurrent = null;
            }
        }

        if (options.AddCoverageLines || breakApplied || state is not null)
        {
            SaveState(
                document.Database,
                transaction,
                session.ReportId,
                section.SectionId,
                new CoverageState(
                    association.GeometryHandle,
                    HandleOf(physicalRemainder),
                    HandleOf(inspectedOverlayCurrent),
                    HandleOf(interruptedOverlayCurrent),
                    plan.GeometryLength,
                    plan.InspectedLength,
                    breakApplied));
        }

        transaction.Commit();
        document.Editor.Regen();
        document.Editor.UpdateScreen();
        return new AutoCadInspectionCoverageResult(
            plan.InspectedLength,
            plan.RemainingLength,
            breakApplied,
            InspectedLayerName,
            InterruptedLayerName);
    }

    public int ClearManagedCoverage(
        Document document,
        Guid reportId,
        Guid sectionId)
    {
        ArgumentNullException.ThrowIfNull(document);
        using var documentLock = document.LockDocument();
        using var transaction = document.Database.TransactionManager.StartTransaction();
        var state = LoadState(document.Database, transaction, reportId, sectionId);
        var erased = 0;
        if (state is not null)
        {
            erased += EraseByHandle(document.Database, transaction, state.InspectedOverlayHandle) ? 1 : 0;
            erased += EraseByHandle(document.Database, transaction, state.InterruptedOverlayHandle) ? 1 : 0;
            DeleteState(document.Database, transaction, reportId, sectionId);
        }

        var blockTable = (BlockTable)transaction.GetObject(
            document.Database.BlockTableId,
            OpenMode.ForRead);
        var modelSpace = (BlockTableRecord)transaction.GetObject(
            blockTable[BlockTableRecord.ModelSpace],
            OpenMode.ForRead);
        erased += EraseCoverageEntitiesForSection(modelSpace, transaction, reportId, sectionId);
        transaction.Commit();
        if (erased > 0)
        {
            document.Editor.Regen();
            document.Editor.UpdateScreen();
        }

        return erased;
    }

    public static bool TryGetPhysicalSourceHandle(Polyline selected, out string sourceHandle)
    {
        sourceHandle = string.Empty;
        using var data = selected.GetXDataForApplication(CoverageRegisteredApplicationName);
        if (data is null)
        {
            return false;
        }

        var component = ReadXData(data, "COMPONENT");
        if (!string.Equals(component, InspectedComponent, StringComparison.Ordinal) &&
            !string.Equals(component, InterruptedComponent, StringComparison.Ordinal))
        {
            return false;
        }

        sourceHandle = ReadXData(data, "SOURCE_HANDLE") ?? string.Empty;
        return !string.IsNullOrWhiteSpace(sourceHandle);
    }

    private static SplitPair Split(Polyline source, double cadDistance)
    {
        var point = source.GetPointAtDist(cadDistance);
        var points = new Point3dCollection { point };
        var objects = source.GetSplitCurves(points);
        var pieces = objects.Cast<DBObject>().OfType<Polyline>().ToArray();
        foreach (var other in objects.Cast<DBObject>().Where(item => item is not Polyline))
        {
            other.Dispose();
        }

        if (pieces.Length != 2)
        {
            foreach (var piece in pieces)
            {
                piece.Dispose();
            }

            throw new InvalidOperationException(
                $"O BREAK retornou {pieces.Length} segmento(s); eram esperados exatamente 2.");
        }

        var firstIndex = pieces[0].StartPoint.DistanceTo(source.StartPoint) <= GeometryTolerance ? 0 : 1;
        return firstIndex == 0
            ? new SplitPair(pieces[0], pieces[1])
            : new SplitPair(pieces[1], pieces[0]);
    }

    private static Polyline UpsertOverlay(
        Database database,
        BlockTableRecord modelSpace,
        Transaction transaction,
        Polyline? overlay,
        Polyline source,
        ObjectId layerId,
        Guid reportId,
        Guid sectionId,
        string component,
        string sourceHandle)
    {
        if (overlay is null || overlay.IsErased)
        {
            overlay = new Polyline();
            overlay.SetDatabaseDefaults(database);
            modelSpace.AppendEntity(overlay);
            transaction.AddNewlyCreatedDBObject(overlay, true);
        }

        CopyGeometry(source, overlay);
        overlay.LayerId = layerId;
        overlay.ColorIndex = ByLayerColorIndex;
        SetCoverageMetadata(overlay, reportId, sectionId, component, sourceHandle);
        return overlay;
    }

    private static void CopyGeometry(Polyline source, Polyline target)
    {
        while (target.NumberOfVertices > 0)
        {
            target.RemoveVertexAt(target.NumberOfVertices - 1);
        }

        target.Normal = source.Normal;
        target.Elevation = source.Elevation;
        target.Thickness = source.Thickness;
        for (var index = 0; index < source.NumberOfVertices; index++)
        {
            target.AddVertexAt(
                target.NumberOfVertices,
                source.GetPoint2dAt(index),
                source.GetBulgeAt(index),
                source.GetStartWidthAt(index),
                source.GetEndWidthAt(index));
        }

        target.Closed = source.Closed;
    }

    private static ObjectId EnsureLayer(
        Database database,
        Transaction transaction,
        string name,
        short colorIndex)
    {
        var table = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
        LayerTableRecord record;
        if (table.Has(name))
        {
            record = (LayerTableRecord)transaction.GetObject(table[name], OpenMode.ForWrite);
        }
        else
        {
            table.UpgradeOpen();
            record = new LayerTableRecord { Name = name };
            table.Add(record);
            transaction.AddNewlyCreatedDBObject(record, true);
        }

        record.Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex);
        record.IsOff = false;
        record.IsFrozen = false;
        record.IsLocked = false;
        var linetypes = (LinetypeTable)transaction.GetObject(database.LinetypeTableId, OpenMode.ForRead);
        if (linetypes.Has("Continuous"))
        {
            record.LinetypeObjectId = linetypes["Continuous"];
        }

        return record.ObjectId;
    }

    private static void BringToFront(
        BlockTableRecord modelSpace,
        Transaction transaction,
        params Polyline?[] overlays)
    {
        if (modelSpace.DrawOrderTableId.IsNull)
        {
            return;
        }

        var ids = new ObjectIdCollection();
        foreach (var overlay in overlays)
        {
            if (overlay is not null && !overlay.IsErased && !overlay.ObjectId.IsNull)
            {
                ids.Add(overlay.ObjectId);
            }
        }

        if (ids.Count == 0)
        {
            return;
        }

        var drawOrder = (DrawOrderTable)transaction.GetObject(
            modelSpace.DrawOrderTableId,
            OpenMode.ForWrite);
        drawOrder.MoveToTop(ids);
    }

    private static void SetCoverageMetadata(
        Polyline entity,
        Guid reportId,
        Guid sectionId,
        string component,
        string sourceHandle)
    {
        using var data = new ResultBuffer(
            new TypedValue((int)DxfCode.ExtendedDataRegAppName, CoverageRegisteredApplicationName),
            TextValue("REPORT_ID", reportId.ToString("D")),
            TextValue("SECTION_ID", sectionId.ToString("D")),
            TextValue("COMPONENT", component),
            TextValue("SOURCE_HANDLE", sourceHandle));
        entity.XData = data;
    }

    private static bool HasInspectedOverlayForSource(
        BlockTableRecord modelSpace,
        Transaction transaction,
        string sourceHandle)
    {
        foreach (ObjectId id in modelSpace)
        {
            if (transaction.GetObject(id, OpenMode.ForRead, openErased: false) is not Polyline polyline)
            {
                continue;
            }

            using var data = polyline.GetXDataForApplication(CoverageRegisteredApplicationName);
            if (string.Equals(ReadXData(data, "COMPONENT"), InspectedComponent, StringComparison.Ordinal) &&
                string.Equals(ReadXData(data, "SOURCE_HANDLE"), sourceHandle, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void EraseInterruptedOverlaysForSource(
        BlockTableRecord modelSpace,
        Transaction transaction,
        string sourceHandle,
        string? keepHandle)
    {
        foreach (ObjectId id in modelSpace)
        {
            if (transaction.GetObject(id, OpenMode.ForRead, openErased: false) is not Polyline polyline)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(keepHandle) &&
                string.Equals(polyline.Handle.ToString(), keepHandle, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            using var data = polyline.GetXDataForApplication(CoverageRegisteredApplicationName);
            if (!string.Equals(ReadXData(data, "COMPONENT"), InterruptedComponent, StringComparison.Ordinal) ||
                !string.Equals(ReadXData(data, "SOURCE_HANDLE"), sourceHandle, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            polyline.UpgradeOpen();
            polyline.Erase();
        }
    }

    private static void EraseLegacyCoverageForSection(
        BlockTableRecord modelSpace,
        Transaction transaction,
        Guid reportId,
        Guid sectionId)
    {
        var report = reportId.ToString("D");
        var section = sectionId.ToString("D");
        foreach (ObjectId id in modelSpace)
        {
            if (transaction.GetObject(id, OpenMode.ForRead, openErased: false) is not Entity entity)
            {
                continue;
            }

            using var data = entity.GetXDataForApplication(AutoCadInspectionRenderer.RegisteredApplicationName);
            var component = ReadXData(data, "COMPONENT");
            if (component?.StartsWith("COVERAGE_", StringComparison.Ordinal) != true ||
                !string.Equals(ReadXData(data, "REPORT_ID"), report, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(ReadXData(data, "SECTION_ID"), section, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            entity.UpgradeOpen();
            entity.Erase();
        }
    }

    private static int EraseCoverageEntitiesForSection(
        BlockTableRecord modelSpace,
        Transaction transaction,
        Guid reportId,
        Guid sectionId)
    {
        var count = 0;
        var report = reportId.ToString("D");
        var section = sectionId.ToString("D");
        foreach (ObjectId id in modelSpace)
        {
            if (transaction.GetObject(id, OpenMode.ForRead, openErased: false) is not Entity entity)
            {
                continue;
            }

            using var data = entity.GetXDataForApplication(CoverageRegisteredApplicationName);
            if (!string.Equals(ReadXData(data, "REPORT_ID"), report, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(ReadXData(data, "SECTION_ID"), section, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            entity.UpgradeOpen();
            entity.Erase();
            count++;
        }

        return count;
    }

    private static void EnsureRegisteredApplication(Database database, Transaction transaction)
    {
        var table = (RegAppTable)transaction.GetObject(database.RegAppTableId, OpenMode.ForRead);
        if (table.Has(CoverageRegisteredApplicationName))
        {
            return;
        }

        table.UpgradeOpen();
        var record = new RegAppTableRecord { Name = CoverageRegisteredApplicationName };
        table.Add(record);
        transaction.AddNewlyCreatedDBObject(record, true);
    }

    private static CoverageState? LoadState(
        Database database,
        Transaction transaction,
        Guid reportId,
        Guid sectionId)
    {
        var namedObjects = (DBDictionary)transaction.GetObject(
            database.NamedObjectsDictionaryId,
            OpenMode.ForRead);
        if (!namedObjects.Contains(DictionaryName))
        {
            return null;
        }

        var dictionary = (DBDictionary)transaction.GetObject(
            namedObjects.GetAt(DictionaryName),
            OpenMode.ForRead);
        var key = EntryKey(reportId, sectionId);
        if (!dictionary.Contains(key))
        {
            return null;
        }

        var record = (Xrecord)transaction.GetObject(dictionary.GetAt(key), OpenMode.ForRead);
        var values = ReadValues(record.Data);
        var sourceHandle = Value(values, "SOURCE_HANDLE");
        if (string.IsNullOrWhiteSpace(sourceHandle))
        {
            return null;
        }

        var originalLength = ParseDouble(Value(values, "ORIGINAL_LENGTH"));
        var inspectedLength = ParseDouble(Value(values, "INSPECTED_LENGTH"));
        var breakApplied = bool.TryParse(Value(values, "BREAK_APPLIED"), out var parsedBreak) && parsedBreak;
        return new CoverageState(
            sourceHandle!,
            Value(values, "REMAINDER_HANDLE"),
            Value(values, "INSPECTED_OVERLAY_HANDLE"),
            Value(values, "INTERRUPTED_OVERLAY_HANDLE"),
            originalLength,
            inspectedLength,
            breakApplied);
    }

    private static void SaveState(
        Database database,
        Transaction transaction,
        Guid reportId,
        Guid sectionId,
        CoverageState state)
    {
        var namedObjects = (DBDictionary)transaction.GetObject(
            database.NamedObjectsDictionaryId,
            OpenMode.ForRead);
        DBDictionary dictionary;
        if (namedObjects.Contains(DictionaryName))
        {
            dictionary = (DBDictionary)transaction.GetObject(
                namedObjects.GetAt(DictionaryName),
                OpenMode.ForWrite);
        }
        else
        {
            namedObjects.UpgradeOpen();
            dictionary = new DBDictionary();
            namedObjects.SetAt(DictionaryName, dictionary);
            transaction.AddNewlyCreatedDBObject(dictionary, true);
        }

        var key = EntryKey(reportId, sectionId);
        Xrecord record;
        if (dictionary.Contains(key))
        {
            record = (Xrecord)transaction.GetObject(dictionary.GetAt(key), OpenMode.ForWrite);
        }
        else
        {
            record = new Xrecord();
            dictionary.SetAt(key, record);
            transaction.AddNewlyCreatedDBObject(record, true);
        }

        record.Data = new ResultBuffer(
            new TypedValue((int)DxfCode.Text, "STORAGE_VERSION=2"),
            new TypedValue((int)DxfCode.Text, $"SOURCE_HANDLE={state.SourceHandle}"),
            new TypedValue((int)DxfCode.Text, $"REMAINDER_HANDLE={state.RemainderHandle ?? string.Empty}"),
            new TypedValue((int)DxfCode.Text, $"INSPECTED_OVERLAY_HANDLE={state.InspectedOverlayHandle ?? string.Empty}"),
            new TypedValue((int)DxfCode.Text, $"INTERRUPTED_OVERLAY_HANDLE={state.InterruptedOverlayHandle ?? string.Empty}"),
            new TypedValue((int)DxfCode.Text, FormattableString.Invariant($"ORIGINAL_LENGTH={state.OriginalLength:R}")),
            new TypedValue((int)DxfCode.Text, FormattableString.Invariant($"INSPECTED_LENGTH={state.InspectedLength:R}")),
            new TypedValue((int)DxfCode.Text, $"BREAK_APPLIED={state.BreakApplied}"));
    }

    private static void DeleteState(
        Database database,
        Transaction transaction,
        Guid reportId,
        Guid sectionId)
    {
        var namedObjects = (DBDictionary)transaction.GetObject(
            database.NamedObjectsDictionaryId,
            OpenMode.ForRead);
        if (!namedObjects.Contains(DictionaryName))
        {
            return;
        }

        var dictionary = (DBDictionary)transaction.GetObject(
            namedObjects.GetAt(DictionaryName),
            OpenMode.ForWrite);
        var key = EntryKey(reportId, sectionId);
        if (!dictionary.Contains(key))
        {
            return;
        }

        var recordId = dictionary.GetAt(key);
        dictionary.Remove(key);
        if (!recordId.IsNull && transaction.GetObject(recordId, OpenMode.ForWrite, openErased: false) is DBObject record)
        {
            record.Erase();
        }
    }

    private static Polyline? ResolvePolyline(
        Database database,
        Transaction transaction,
        string? handle,
        OpenMode mode)
    {
        if (string.IsNullOrWhiteSpace(handle))
        {
            return null;
        }

        try
        {
            var objectId = database.GetObjectId(false, new Handle(Convert.ToInt64(handle, 16)), 0);
            if (objectId.IsNull || !objectId.IsValid || objectId.IsErased)
            {
                return null;
            }

            return transaction.GetObject(objectId, mode, openErased: false) as Polyline;
        }
        catch (System.Exception exception) when (
            exception is Autodesk.AutoCAD.Runtime.Exception or FormatException or OverflowException)
        {
            return null;
        }
    }

    private static bool EraseByHandle(
        Database database,
        Transaction transaction,
        string? handle)
    {
        var entity = ResolvePolyline(database, transaction, handle, OpenMode.ForWrite);
        if (entity is null || entity.IsErased)
        {
            return false;
        }

        entity.Erase();
        return true;
    }

    private static void Erase(Polyline? entity)
    {
        if (entity is not null && !entity.IsErased)
        {
            entity.Erase();
        }
    }

    private static string? HandleOf(Polyline? polyline) =>
        polyline is null || polyline.IsErased ? null : polyline.Handle.ToString();

    private static TypedValue TextValue(string key, string value) =>
        new((int)DxfCode.ExtendedDataAsciiString, $"{key}={value}");

    private static string? ReadXData(ResultBuffer? data, string key)
    {
        if (data is null)
        {
            return null;
        }

        var prefix = key + "=";
        return data.AsArray()
            .Where(item => item.TypeCode == (int)DxfCode.ExtendedDataAsciiString)
            .Select(item => Convert.ToString(item.Value, System.Globalization.CultureInfo.InvariantCulture))
            .FirstOrDefault(text => text?.StartsWith(prefix, StringComparison.Ordinal) == true)
            ?.Substring(prefix.Length);
    }

    private static IReadOnlyDictionary<string, string> ReadValues(ResultBuffer? data)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (data is null)
        {
            return result;
        }

        foreach (var item in data.AsArray())
        {
            var text = Convert.ToString(item.Value, System.Globalization.CultureInfo.InvariantCulture);
            var separator = text?.IndexOf('=') ?? -1;
            if (separator > 0)
            {
                result[text!.Substring(0, separator)] = text.Substring(separator + 1);
            }
        }

        return result;
    }

    private static string? Value(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    private static double ParseDouble(string? value) =>
        double.TryParse(
            value,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : 0.0;

    private static string EntryKey(Guid reportId, Guid sectionId) =>
        $"R_{reportId:N}_S_{sectionId:N}";

    private sealed record CoverageState(
        string SourceHandle,
        string? RemainderHandle,
        string? InspectedOverlayHandle,
        string? InterruptedOverlayHandle,
        double OriginalLength,
        double InspectedLength,
        bool BreakApplied);

    private sealed class SplitPair : IDisposable
    {
        internal SplitPair(Polyline first, Polyline second)
        {
            First = first;
            Second = second;
        }

        internal Polyline First { get; }
        internal Polyline Second { get; }

        public void Dispose()
        {
            First.Dispose();
            Second.Dispose();
        }
    }
}
