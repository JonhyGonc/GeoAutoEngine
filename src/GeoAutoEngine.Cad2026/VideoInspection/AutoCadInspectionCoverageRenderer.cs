using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using GeoAutoEngine.Application.VideoInspection;

namespace GeoAutoEngine.Cad2026.VideoInspection;

public sealed record AutoCadInspectionCoverageResult(
    double InspectedLength,
    double RemainingLength,
    bool BreakApplied,
    string InspectedLayer,
    string NotInspectedLayer);

public sealed class AutoCadInspectionCoverageRenderer
{
    public const string InspectedLayerName = "GF-VideoInspecao-Inspecionado";
    public const string NotInspectedLayerName = "GF-VideoInspecao-NaoInspecionado";

    private const string DictionaryName = "GEOAUTOENGINE_VIDEO_INSPECTION_COVERAGE";
    private const short InspectedLayerColorIndex = 3;
    private const short NotInspectedLayerColorIndex = 1;
    private const short ByLayerColorIndex = 256;
    private const double GeometryTolerance = 1e-6;

    public AutoCadInspectionCoverageResult Apply(
        Document document,
        VideoInspectionSession session,
        InspectionSectionSession section,
        IReadOnlyList<PositionedInspectionEvent> positionedEvents)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(positionedEvents);

        var association = section.Association
            ?? throw new InvalidOperationException("O trecho ainda não possui geometria associada.");

        using var documentLock = document.LockDocument();
        using var transaction = document.Database.TransactionManager.StartTransaction();
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
                out var original))
        {
            throw new InvalidOperationException("A Polyline original associada ao trecho não existe mais.");
        }

        var state = LoadState(
            document.Database,
            transaction,
            session.ReportId,
            section.SectionId);
        if (state is not null &&
            !string.Equals(
                state.OriginalHandle,
                association.GeometryHandle,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "O trecho foi reassociado depois da criação da cobertura. " +
                "A rede anterior foi preservada e não será alterada automaticamente.");
        }

        var remainder = state is null
            ? null
            : ResolvePolyline(
                document.Database,
                transaction,
                state.RemainderHandle,
                OpenMode.ForWrite);

        Polyline? merged = null;
        Polyline fullGeometry;
        if (state is not null && !string.IsNullOrWhiteSpace(state.RemainderHandle))
        {
            if (remainder is null)
            {
                throw new InvalidOperationException(
                    "O segundo segmento da Polyline original não foi encontrado. " +
                    "Nenhuma nova alteração foi feita na rede.");
            }

            merged = association.Direction == InspectionDirection.Forward
                ? Merge(original!, remainder)
                : Merge(remainder, original!);
            fullGeometry = merged;
        }
        else
        {
            fullGeometry = original!;
        }

        try
        {
            var plan = InspectionCoveragePlanner.Create(
                section.InvestigatedMeters,
                fullGeometry.Length,
                association.Direction,
                positionedEvents);
            var inspectedLayerId = EnsureLayer(
                document.Database,
                transaction,
                InspectedLayerName,
                InspectedLayerColorIndex);
            var notInspectedLayerId = EnsureLayer(
                document.Database,
                transaction,
                NotInspectedLayerName,
                NotInspectedLayerColorIndex);

            if (plan.RequiresBreak)
            {
                using var split = Split(fullGeometry, plan.SplitCadDistance);
                var geometricFirst = split.First;
                var geometricSecond = split.Second;
                var inspectedPiece = association.Direction == InspectionDirection.Forward
                    ? geometricFirst
                    : geometricSecond;
                var remainingPiece = association.Direction == InspectionDirection.Forward
                    ? geometricSecond
                    : geometricFirst;

                CopyGeometry(inspectedPiece, original!);
                if (remainder is null)
                {
                    remainder = (Polyline)original!.Clone();
                    CopyGeometry(remainingPiece, remainder);
                    modelSpace.AppendEntity(remainder);
                    transaction.AddNewlyCreatedDBObject(remainder, true);
                }
                else
                {
                    CopyGeometry(remainingPiece, remainder);
                }

                var inspectedOverlay = UpsertOverlay(
                    document.Database,
                    modelSpace,
                    transaction,
                    ResolvePolyline(
                        document.Database,
                        transaction,
                        state?.InspectedOverlayHandle,
                        OpenMode.ForWrite),
                    inspectedPiece,
                    inspectedLayerId);
                var notInspectedOverlay = UpsertOverlay(
                    document.Database,
                    modelSpace,
                    transaction,
                    ResolvePolyline(
                        document.Database,
                        transaction,
                        state?.NotInspectedOverlayHandle,
                        OpenMode.ForWrite),
                    remainingPiece,
                    notInspectedLayerId);

                SaveState(
                    document.Database,
                    transaction,
                    session.ReportId,
                    section.SectionId,
                    new CoverageState(
                        association.GeometryHandle,
                        remainder.Handle.ToString(),
                        inspectedOverlay.Handle.ToString(),
                        notInspectedOverlay.Handle.ToString(),
                        plan.GeometryLength));

                transaction.Commit();
                document.Editor.Regen();
                document.Editor.UpdateScreen();
                return new AutoCadInspectionCoverageResult(
                    plan.InspectedLength,
                    plan.RemainingLength,
                    true,
                    InspectedLayerName,
                    NotInspectedLayerName);
            }

            if (plan.IsFullyInspected)
            {
                if (remainder is not null)
                {
                    CopyGeometry(fullGeometry, original!);
                    remainder.Erase();
                    remainder = null;
                }

                var inspectedOverlay = UpsertOverlay(
                    document.Database,
                    modelSpace,
                    transaction,
                    ResolvePolyline(
                        document.Database,
                        transaction,
                        state?.InspectedOverlayHandle,
                        OpenMode.ForWrite),
                    original!,
                    inspectedLayerId);
                EraseManagedOverlay(
                    ResolvePolyline(
                        document.Database,
                        transaction,
                        state?.NotInspectedOverlayHandle,
                        OpenMode.ForWrite));

                SaveState(
                    document.Database,
                    transaction,
                    session.ReportId,
                    section.SectionId,
                    new CoverageState(
                        association.GeometryHandle,
                        null,
                        inspectedOverlay.Handle.ToString(),
                        null,
                        plan.GeometryLength));

                transaction.Commit();
                document.Editor.Regen();
                document.Editor.UpdateScreen();
                return new AutoCadInspectionCoverageResult(
                    plan.InspectedLength,
                    plan.RemainingLength,
                    false,
                    InspectedLayerName,
                    NotInspectedLayerName);
            }

            var redOverlay = UpsertOverlay(
                document.Database,
                modelSpace,
                transaction,
                ResolvePolyline(
                    document.Database,
                    transaction,
                    state?.NotInspectedOverlayHandle,
                    OpenMode.ForWrite),
                fullGeometry,
                notInspectedLayerId);
            EraseManagedOverlay(
                ResolvePolyline(
                    document.Database,
                    transaction,
                    state?.InspectedOverlayHandle,
                    OpenMode.ForWrite));
            SaveState(
                document.Database,
                transaction,
                session.ReportId,
                section.SectionId,
                new CoverageState(
                    association.GeometryHandle,
                    state?.RemainderHandle,
                    null,
                    redOverlay.Handle.ToString(),
                    plan.GeometryLength));

            transaction.Commit();
            document.Editor.Regen();
            document.Editor.UpdateScreen();
            return new AutoCadInspectionCoverageResult(
                plan.InspectedLength,
                plan.RemainingLength,
                false,
                InspectedLayerName,
                NotInspectedLayerName);
        }
        finally
        {
            merged?.Dispose();
        }
    }

    private static SplitPair Split(Polyline source, double cadDistance)
    {
        var point = source.GetPointAtDist(cadDistance);
        var splitPoints = new Point3dCollection();
        splitPoints.Add(point);
        var objects = source.GetSplitCurves(splitPoints);
        var pieces = objects.Cast<DBObject>().OfType<Polyline>().ToArray();
        foreach (var item in objects.Cast<DBObject>().Where(item => item is not Polyline))
        {
            item.Dispose();
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

    private static Polyline Merge(Polyline first, Polyline second)
    {
        if (first.EndPoint.DistanceTo(second.StartPoint) > GeometryTolerance)
        {
            throw new InvalidOperationException(
                "Os dois segmentos da rede original não são mais contíguos. " +
                "A cobertura não será recalculada automaticamente.");
        }

        var merged = new Polyline();
        merged.Normal = first.Normal;
        merged.Elevation = first.Elevation;
        merged.Thickness = first.Thickness;
        for (var index = 0; index < first.NumberOfVertices; index++)
        {
            AddVertex(merged, first, index);
        }

        if (second.NumberOfVertices > 0)
        {
            var junction = merged.NumberOfVertices - 1;
            merged.SetBulgeAt(junction, second.GetBulgeAt(0));
            merged.SetStartWidthAt(junction, second.GetStartWidthAt(0));
            merged.SetEndWidthAt(junction, second.GetEndWidthAt(0));
            for (var index = 1; index < second.NumberOfVertices; index++)
            {
                AddVertex(merged, second, index);
            }
        }

        merged.Closed = false;
        return merged;
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
            AddVertex(target, source, index);
        }

        target.Closed = source.Closed;
    }

    private static void AddVertex(Polyline target, Polyline source, int index) =>
        target.AddVertexAt(
            target.NumberOfVertices,
            source.GetPoint2dAt(index),
            source.GetBulgeAt(index),
            source.GetStartWidthAt(index),
            source.GetEndWidthAt(index));

    private static Polyline UpsertOverlay(
        Database database,
        BlockTableRecord modelSpace,
        Transaction transaction,
        Polyline? overlay,
        Polyline source,
        ObjectId layerId)
    {
        if (overlay is null)
        {
            overlay = new Polyline();
            overlay.SetDatabaseDefaults(database);
            modelSpace.AppendEntity(overlay);
            transaction.AddNewlyCreatedDBObject(overlay, true);
        }

        CopyGeometry(source, overlay);
        overlay.LayerId = layerId;
        overlay.ColorIndex = ByLayerColorIndex;
        return overlay;
    }

    private static void EraseManagedOverlay(Polyline? overlay)
    {
        if (overlay is not null && !overlay.IsErased)
        {
            overlay.Erase();
        }
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
        var linetypes = (LinetypeTable)transaction.GetObject(database.LinetypeTableId, OpenMode.ForRead);
        if (linetypes.Has("Continuous"))
        {
            record.LinetypeObjectId = linetypes["Continuous"];
        }

        return record.ObjectId;
    }

    private static Polyline? ResolvePolyline(
        Database database,
        Transaction transaction,
        string? handleValue,
        OpenMode openMode)
    {
        if (string.IsNullOrWhiteSpace(handleValue))
        {
            return null;
        }

        try
        {
            var handle = new Handle(Convert.ToInt64(handleValue, 16));
            var objectId = database.GetObjectId(false, handle, 0);
            if (objectId.IsNull || !objectId.IsValid || objectId.IsErased)
            {
                return null;
            }

            return transaction.GetObject(objectId, openMode, openErased: false) as Polyline;
        }
        catch (System.Exception exception) when (
            exception is Autodesk.AutoCAD.Runtime.Exception or FormatException or OverflowException)
        {
            return null;
        }
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
        if (!values.TryGetValue("ORIGINAL_HANDLE", out var originalHandle) ||
            string.IsNullOrWhiteSpace(originalHandle))
        {
            return null;
        }

        values.TryGetValue("REMAINDER_HANDLE", out var remainderHandle);
        values.TryGetValue("INSPECTED_OVERLAY_HANDLE", out var inspectedOverlayHandle);
        values.TryGetValue("NOT_INSPECTED_OVERLAY_HANDLE", out var notInspectedOverlayHandle);
        var originalLength = values.TryGetValue("ORIGINAL_LENGTH", out var lengthValue) &&
            double.TryParse(
                lengthValue,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var parsedLength)
            ? parsedLength
            : 0.0;

        return new CoverageState(
            originalHandle,
            EmptyToNull(remainderHandle),
            EmptyToNull(inspectedOverlayHandle),
            EmptyToNull(notInspectedOverlayHandle),
            originalLength);
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
            Text("STORAGE_VERSION=1"),
            Text($"REPORT_ID={reportId:D}"),
            Text($"SECTION_ID={sectionId:D}"),
            Text($"ORIGINAL_HANDLE={state.OriginalHandle}"),
            Text($"REMAINDER_HANDLE={state.RemainderHandle ?? string.Empty}"),
            Text($"INSPECTED_OVERLAY_HANDLE={state.InspectedOverlayHandle ?? string.Empty}"),
            Text($"NOT_INSPECTED_OVERLAY_HANDLE={state.NotInspectedOverlayHandle ?? string.Empty}"),
            Text(FormattableString.Invariant($"ORIGINAL_LENGTH={state.OriginalLength:R}")));
    }

    private static IReadOnlyDictionary<string, string> ReadValues(ResultBuffer? data)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (data is null)
        {
            return result;
        }

        foreach (var value in data.AsArray())
        {
            var text = Convert.ToString(value.Value, System.Globalization.CultureInfo.InvariantCulture);
            var separator = text?.IndexOf('=') ?? -1;
            if (separator > 0)
            {
                result[text!.Substring(0, separator)] = text.Substring(separator + 1);
            }
        }

        return result;
    }

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static string EntryKey(Guid reportId, Guid sectionId) =>
        $"R_{reportId:N}_S_{sectionId:N}";

    private static TypedValue Text(string value) => new((int)DxfCode.Text, value);

    private sealed record CoverageState(
        string OriginalHandle,
        string? RemainderHandle,
        string? InspectedOverlayHandle,
        string? NotInspectedOverlayHandle,
        double OriginalLength);

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
