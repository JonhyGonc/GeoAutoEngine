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
/// Cobertura simples por segmento físico.
/// Cada associação trabalha somente com a Polyline física selecionada. Depois de um
/// BREAK, o segmento restante é uma nova Polyline física e pode ser associado a outra filmagem.
/// </summary>
public sealed class AutoCadInspectionCoverageController
{
    public const string InspectedLayerName = "GF-VideoInspecao-Inspecionado";
    public const string InterruptedLayerName = "Video-interrompido";
    public const string CoverageRegisteredApplicationName = "GEOAUTOENGINE_VI_COVERAGE_V3";

    private const string DictionaryName = "GEOAUTOENGINE_VIDEO_INSPECTION_COVERAGE_V3";
    private const string PreviousRegisteredApplicationName = "GEOAUTOENGINE_VI_COVERAGE_V2";
    private const string InspectedComponent = "COVERAGE_INSPECTED";
    private const string InterruptedComponent = "COVERAGE_INTERRUPTED";
    private const string LegacySharedInspectedComponent = "COVERAGE_INSPECTED_SHARED";
    private const string LegacySharedInterruptedComponent = "COVERAGE_NOT_INSPECTED_SHARED";
    private const short InspectedLayerColorIndex = 3;
    private const short InterruptedLayerColorIndex = 1;
    private const short ByLayerColorIndex = 256;
    private const double GeometryTolerance = 1e-6;
    private const double StateLengthTolerance = 1e-3;

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

        // Limpa somente overlays visuais antigos. Segmentos físicos criados por BREAKs
        // anteriores nunca são apagados por esta rotina.
        EraseLegacyVisualCoverageForSection(
            modelSpace,
            transaction,
            session.ReportId,
            section.SectionId);

        var state = LoadState(document.Database, transaction, session.ReportId, section.SectionId);
        if (state is not null &&
            !string.Equals(state.SourceHandle, association.GeometryHandle, StringComparison.OrdinalIgnoreCase))
        {
            EraseStateOverlays(document.Database, transaction, state);
            DeleteState(document.Database, transaction, session.ReportId, section.SectionId);
            state = null;
        }

        if (state?.BreakApplied == true && !IsBreakStateValid(document.Database, transaction, source, state))
        {
            EraseStateOverlays(document.Database, transaction, state);
            DeleteState(document.Database, transaction, session.ReportId, section.SectionId);
            state = null;
        }

        // Se este segmento era a parte vermelha de outra filmagem, ele agora passa a ser
        // a geometria física desta filmagem. Remove somente o overlay vermelho anterior.
        EraseInterruptedOverlaysForSource(
            modelSpace,
            transaction,
            association.GeometryHandle,
            state?.InterruptedOverlayHandle);

        if (state?.BreakApplied == true)
        {
            var result = ReapplyExistingBreak(
                document,
                modelSpace,
                transaction,
                session,
                section,
                source,
                state,
                options);
            transaction.Commit();
            document.Editor.Regen();
            document.Editor.UpdateScreen();
            document.Editor.WriteMessage(
                $"\nGeoAutoEngine VI cobertura V3: BREAK existente validado; " +
                $"inspecionado={result.InspectedLength:0.###} m; restante={result.RemainingLength:0.###} m; " +
                $"linhas={options.AddCoverageLines}.\n");
            return result;
        }

        var plan = InspectionCoveragePlanner.Create(
            section.InvestigatedMeters,
            source.Length,
            association.Direction,
            positionedEvents);

        document.Editor.WriteMessage(
            $"\nGeoAutoEngine VI cobertura V3: handle={association.GeometryHandle}; " +
            $"geometria={plan.GeometryLength:0.###} m; filmado={plan.InspectedLength:0.###} m; " +
            $"restante={plan.RemainingLength:0.###} m; direção={association.Direction}; " +
            $"RequiresBreak={plan.RequiresBreak}; linhas={options.AddCoverageLines}; " +
            $"BREAK={options.ApplyBreak}.\n");

        var inspectedOverlay = ResolvePolyline(
            document.Database,
            transaction,
            state?.InspectedOverlayHandle,
            OpenMode.ForWrite);
        var interruptedOverlay = ResolvePolyline(
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

            Polyline inspectedGeometry;
            Polyline remainingGeometry;
            if (options.ApplyBreak)
            {
                physicalRemainder = (Polyline)source.Clone();
                CopyGeometry(remainingPiece, physicalRemainder);
                CopyGeometry(inspectedPiece, source);
                modelSpace.AppendEntity(physicalRemainder);
                transaction.AddNewlyCreatedDBObject(physicalRemainder, true);
                inspectedGeometry = source;
                remainingGeometry = physicalRemainder;
                breakApplied = true;
            }
            else
            {
                inspectedGeometry = inspectedPiece;
                remainingGeometry = remainingPiece;
            }

            if (options.AddCoverageLines)
            {
                var inspectedLayer = EnsureLayer(
                    document.Database,
                    transaction,
                    InspectedLayerName,
                    InspectedLayerColorIndex);
                var interruptedLayer = EnsureLayer(
                    document.Database,
                    transaction,
                    InterruptedLayerName,
                    InterruptedLayerColorIndex);

                inspectedOverlay = UpsertOverlay(
                    document.Database,
                    modelSpace,
                    transaction,
                    inspectedOverlay,
                    inspectedGeometry,
                    inspectedLayer,
                    session.ReportId,
                    section.SectionId,
                    InspectedComponent,
                    source.Handle.ToString());
                interruptedOverlay = UpsertOverlay(
                    document.Database,
                    modelSpace,
                    transaction,
                    interruptedOverlay,
                    remainingGeometry,
                    interruptedLayer,
                    session.ReportId,
                    section.SectionId,
                    InterruptedComponent,
                    breakApplied
                        ? physicalRemainder!.Handle.ToString()
                        : source.Handle.ToString());
                BringToFront(modelSpace, transaction, inspectedOverlay, interruptedOverlay);
            }
            else
            {
                Erase(inspectedOverlay);
                Erase(interruptedOverlay);
                inspectedOverlay = null;
                interruptedOverlay = null;
            }
        }
        else if (plan.IsFullyInspected)
        {
            if (options.AddCoverageLines)
            {
                var inspectedLayer = EnsureLayer(
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
                    inspectedLayer,
                    session.ReportId,
                    section.SectionId,
                    InspectedComponent,
                    source.Handle.ToString());
                Erase(interruptedOverlay);
                interruptedOverlay = null;
                BringToFront(modelSpace, transaction, inspectedOverlay);
            }
            else
            {
                Erase(inspectedOverlay);
                Erase(interruptedOverlay);
                inspectedOverlay = null;
                interruptedOverlay = null;
            }
        }
        else
        {
            Erase(inspectedOverlay);
            inspectedOverlay = null;
            if (options.AddCoverageLines)
            {
                var interruptedLayer = EnsureLayer(
                    document.Database,
                    transaction,
                    InterruptedLayerName,
                    InterruptedLayerColorIndex);
                interruptedOverlay = UpsertOverlay(
                    document.Database,
                    modelSpace,
                    transaction,
                    interruptedOverlay,
                    source,
                    interruptedLayer,
                    session.ReportId,
                    section.SectionId,
                    InterruptedComponent,
                    source.Handle.ToString());
                BringToFront(modelSpace, transaction, interruptedOverlay);
            }
            else
            {
                Erase(interruptedOverlay);
                interruptedOverlay = null;
            }
        }

        SaveState(
            document.Database,
            transaction,
            session.ReportId,
            section.SectionId,
            new CoverageState(
                association.GeometryHandle,
                HandleOf(physicalRemainder),
                HandleOf(inspectedOverlay),
                HandleOf(interruptedOverlay),
                plan.GeometryLength,
                plan.InspectedLength,
                breakApplied));

        transaction.Commit();
        document.Editor.Regen();
        document.Editor.UpdateScreen();
        document.Editor.WriteMessage(
            $"\nGeoAutoEngine VI cobertura V3 concluída: verde={(inspectedOverlay is null ? "não" : "sim")}; " +
            $"BREAK={breakApplied}; vermelho={(interruptedOverlay is null ? "não" : "sim")}.\n");

        return new AutoCadInspectionCoverageResult(
            plan.InspectedLength,
            plan.RemainingLength,
            breakApplied,
            InspectedLayerName,
            InterruptedLayerName);
    }

    public int ClearManagedCoverage(Document document, Guid reportId, Guid sectionId)
    {
        ArgumentNullException.ThrowIfNull(document);
        using var documentLock = document.LockDocument();
        using var transaction = document.Database.TransactionManager.StartTransaction();
        var erased = 0;
        var state = LoadState(document.Database, transaction, reportId, sectionId);
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
        EraseLegacyVisualCoverageForSection(modelSpace, transaction, reportId, sectionId);
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
        if (!IsVisualCoverageComponent(component))
        {
            return false;
        }

        sourceHandle = ReadXData(data, "SOURCE_HANDLE") ?? string.Empty;
        return !string.IsNullOrWhiteSpace(sourceHandle);
    }

    private static AutoCadInspectionCoverageResult ReapplyExistingBreak(
        Document document,
        BlockTableRecord modelSpace,
        Transaction transaction,
        VideoInspectionSession session,
        InspectionSectionSession section,
        Polyline source,
        CoverageState state,
        AutoCadInspectionCoverageOptions options)
    {
        var remainder = ResolvePolyline(
            document.Database,
            transaction,
            state.RemainderHandle,
            OpenMode.ForRead)!;
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
            var inspectedLayer = EnsureLayer(
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
                inspectedLayer,
                session.ReportId,
                section.SectionId,
                InspectedComponent,
                source.Handle.ToString());

            if (!HasInspectedOverlayForSource(modelSpace, transaction, remainder.Handle.ToString()))
            {
                var interruptedLayer = EnsureLayer(
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
                    interruptedLayer,
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

        return new AutoCadInspectionCoverageResult(
            source.Length,
            remainder.Length,
            true,
            InspectedLayerName,
            InterruptedLayerName);
    }

    private static bool IsBreakStateValid(
        Database database,
        Transaction transaction,
        Polyline source,
        CoverageState state)
    {
        if (!state.BreakApplied || string.IsNullOrWhiteSpace(state.RemainderHandle))
        {
            return false;
        }

        var remainder = ResolvePolyline(database, transaction, state.RemainderHandle, OpenMode.ForRead);
        if (remainder is null)
        {
            return false;
        }

        if (Math.Abs(source.Length - state.InspectedLength) > StateLengthTolerance)
        {
            return false;
        }

        return Math.Abs(source.Length + remainder.Length - state.OriginalLength) <= StateLengthTolerance;
    }

    private static SplitPair Split(Polyline source, double cadDistance)
    {
        if (cadDistance <= GeometryTolerance || cadDistance >= source.Length - GeometryTolerance)
        {
            throw new InvalidOperationException(
                $"O ponto de BREAK ({cadDistance:0.###} m) não está no interior da Polyline ({source.Length:0.###} m).");
        }

        var point = source.GetPointAtDist(cadDistance);
        var objects = source.GetSplitCurves(new Point3dCollection { point });
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
            XDataValue("REPORT_ID", reportId.ToString("D")),
            XDataValue("SECTION_ID", sectionId.ToString("D")),
            XDataValue("COMPONENT", component),
            XDataValue("SOURCE_HANDLE", sourceHandle));
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

    private static void EraseLegacyVisualCoverageForSection(
        BlockTableRecord modelSpace,
        Transaction transaction,
        Guid reportId,
        Guid sectionId)
    {
        var expectedReport = reportId.ToString("D");
        var expectedSection = sectionId.ToString("D");
        foreach (ObjectId id in modelSpace)
        {
            if (transaction.GetObject(id, OpenMode.ForRead, openErased: false) is not Entity entity)
            {
                continue;
            }

            // V2: somente overlays visuais. Nunca apagar Polylines físicas.
            using (var v2 = entity.GetXDataForApplication(PreviousRegisteredApplicationName))
            {
                var component = ReadXData(v2, "COMPONENT");
                if (IsVisualCoverageComponent(component) &&
                    string.Equals(ReadXData(v2, "REPORT_ID"), expectedReport, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(ReadXData(v2, "SECTION_ID"), expectedSection, StringComparison.OrdinalIgnoreCase))
                {
                    entity.UpgradeOpen();
                    entity.Erase();
                    continue;
                }
            }

            // Implementação shared antiga: explicitamente NÃO apagar
            // COVERAGE_PHYSICAL_SEGMENT.
            using var legacy = entity.GetXDataForApplication(AutoCadInspectionRenderer.RegisteredApplicationName);
            var legacyComponent = ReadXData(legacy, "COMPONENT");
            if (!IsLegacyVisualCoverageComponent(legacyComponent) ||
                !string.Equals(ReadXData(legacy, "REPORT_ID"), expectedReport, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var storedSection = ReadXData(legacy, "SECTION_ID");
            if (!string.IsNullOrWhiteSpace(storedSection) &&
                !string.Equals(storedSection, expectedSection, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            entity.UpgradeOpen();
            entity.Erase();
        }
    }

    private static bool IsVisualCoverageComponent(string? component) =>
        string.Equals(component, InspectedComponent, StringComparison.Ordinal) ||
        string.Equals(component, InterruptedComponent, StringComparison.Ordinal);

    private static bool IsLegacyVisualCoverageComponent(string? component) =>
        string.Equals(component, LegacySharedInspectedComponent, StringComparison.Ordinal) ||
        string.Equals(component, LegacySharedInterruptedComponent, StringComparison.Ordinal);

    private static int EraseCoverageEntitiesForSection(
        BlockTableRecord modelSpace,
        Transaction transaction,
        Guid reportId,
        Guid sectionId)
    {
        var count = 0;
        var expectedReport = reportId.ToString("D");
        var expectedSection = sectionId.ToString("D");
        foreach (ObjectId id in modelSpace)
        {
            if (transaction.GetObject(id, OpenMode.ForRead, openErased: false) is not Entity entity)
            {
                continue;
            }

            using var data = entity.GetXDataForApplication(CoverageRegisteredApplicationName);
            if (!string.Equals(ReadXData(data, "REPORT_ID"), expectedReport, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(ReadXData(data, "SECTION_ID"), expectedSection, StringComparison.OrdinalIgnoreCase) ||
                !IsVisualCoverageComponent(ReadXData(data, "COMPONENT")))
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

        return new CoverageState(
            sourceHandle!,
            Value(values, "REMAINDER_HANDLE"),
            Value(values, "INSPECTED_OVERLAY_HANDLE"),
            Value(values, "INTERRUPTED_OVERLAY_HANDLE"),
            ParseDouble(Value(values, "ORIGINAL_LENGTH")),
            ParseDouble(Value(values, "INSPECTED_LENGTH")),
            bool.TryParse(Value(values, "BREAK_APPLIED"), out var breakApplied) && breakApplied);
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
            dictionary = (DBDictionary)transaction.GetObject(namedObjects.GetAt(DictionaryName), OpenMode.ForWrite);
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
            new TypedValue((int)DxfCode.Text, "STORAGE_VERSION=3"),
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

    private static void EraseStateOverlays(Database database, Transaction transaction, CoverageState state)
    {
        EraseByHandle(database, transaction, state.InspectedOverlayHandle);
        EraseByHandle(database, transaction, state.InterruptedOverlayHandle);
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

    private static bool EraseByHandle(Database database, Transaction transaction, string? handle)
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

    private static TypedValue XDataValue(string key, string value) =>
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
