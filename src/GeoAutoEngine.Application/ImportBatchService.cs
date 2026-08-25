using GeoAutoEngine.Domain;

namespace GeoAutoEngine.Application;

public sealed class ImportBatchService
{
    private readonly ISourceImporter _sourceImporter;
    private readonly IDuplicateDetector _duplicateDetector;

    public ImportBatchService(ISourceImporter sourceImporter, IDuplicateDetector duplicateDetector)
    {
        ArgumentNullException.ThrowIfNull(sourceImporter);
        ArgumentNullException.ThrowIfNull(duplicateDetector);
        _sourceImporter = sourceImporter;
        _duplicateDetector = duplicateDetector;
    }

    public async ValueTask<ImportBatch> ImportAsync(
        IEnumerable<ImportSourceRequest> requests,
        IProgress<ImportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requests);
        var orderedRequests = requests.OrderBy(request => request.SelectionOrdinal).ToArray();
        if (orderedRequests.Length == 0)
        {
            throw new ArgumentException("At least one CSV source is required.", nameof(requests));
        }

        if (orderedRequests.Select(request => request.SelectionOrdinal).Distinct().Count() != orderedRequests.Length)
        {
            throw new ArgumentException("Selection ordinals must be unique.", nameof(requests));
        }

        var sourceResults = new List<SourceImportResult>(orderedRequests.Length);
        for (var index = 0; index < orderedRequests.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var request = orderedRequests[index];
            var result = await _sourceImporter.ImportAsync(request, cancellationToken).ConfigureAwait(false);
            if (result.SourceFile.SelectionOrdinal != request.SelectionOrdinal)
            {
                throw new InvalidOperationException("The source importer changed the selection ordinal.");
            }

            sourceResults.Add(result);
            progress?.Report(new ImportProgress(index + 1, orderedRequests.Length, result.SourceFile.DisplayName));
        }

        return Consolidate(BatchId.New(), sourceResults);
    }

    public ImportBatch Consolidate(BatchId batchId, IEnumerable<SourceImportResult> sourceResults)
    {
        ArgumentNullException.ThrowIfNull(sourceResults);
        var sources = sourceResults.OrderBy(result => result.SourceFile.SelectionOrdinal).ToArray();
        if (sources.Length == 0)
        {
            throw new ArgumentException("At least one imported source is required.", nameof(sourceResults));
        }

        var importedRecords = new List<SurveyRecord>();
        var rejectedRecords = new List<RejectedRecord>();
        var diagnostics = new List<Diagnostic>();
        var batchOrdinal = 0;

        foreach (var sourceResult in sources)
        {
            var recordsByIndex = sourceResult.ImportedRecords
                .Select(record => (
                    record.Origin.SourceRecordIndex,
                    Imported: (SurveyRecord?)record,
                    Rejected: (RejectedRecord?)null))
                .Concat(sourceResult.RejectedRecords.Select(record =>
                    (record.Origin.SourceRecordIndex, Imported: (SurveyRecord?)null, Rejected: (RejectedRecord?)record)))
                .OrderBy(record => record.SourceRecordIndex)
                .ToArray();

            var rebasedOrigins = new Dictionary<int, RecordOrigin>();
            foreach (var sourceRecord in recordsByIndex)
            {
                var originalOrigin = sourceRecord.Imported?.Origin ?? sourceRecord.Rejected!.Origin;
                var origin = originalOrigin.AtBatchOrdinal(batchOrdinal++);
                rebasedOrigins.Add(origin.SourceRecordIndex, origin);

                if (sourceRecord.Imported is not null)
                {
                    var imported = sourceRecord.Imported;
                    importedRecords.Add(new SurveyRecord(
                        SurveyRecordId.From(origin.SourceFileId, origin.SourceRecordIndex),
                        origin,
                        imported.RawValues,
                        imported.Point,
                        imported.Code,
                        imported.Interpretation));
                }
                else
                {
                    rejectedRecords.Add(sourceRecord.Rejected!.AtOrigin(origin));
                }
            }

            foreach (var diagnostic in sourceResult.Diagnostics)
            {
                if (diagnostic.Origins.Count == 0)
                {
                    diagnostics.Add(diagnostic);
                    continue;
                }

                diagnostics.Add(diagnostic.AtOrigins(diagnostic.Origins.Select(origin =>
                    rebasedOrigins.TryGetValue(origin.SourceRecordIndex, out var rebased)
                        ? rebased
                        : throw new InvalidOperationException("A diagnostic refers to an unknown source record."))));
            }
        }

        var duplicateCandidates = _duplicateDetector.Detect(importedRecords);
        diagnostics.AddRange(duplicateCandidates.Select(candidate => new Diagnostic(
            "CSV_DUPLICATE_CANDIDATE",
            candidate.Reason,
            DiagnosticSeverity.Warning,
            DiagnosticScope.Record,
            origins: [candidate.FirstOrigin, candidate.SecondOrigin])));
        return ImportBatch.Create(
            batchId,
            sources.Select(result => result.SourceFile),
            importedRecords,
            rejectedRecords,
            diagnostics,
            duplicateCandidates);
    }
}

public sealed class NoDuplicateDetector : IDuplicateDetector
{
    public IReadOnlyList<DuplicateCandidate> Detect(IReadOnlyList<SurveyRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        return Array.Empty<DuplicateCandidate>();
    }
}

public sealed class SpatialDuplicateDetector : IDuplicateDetector
{
    private readonly double _maximumDistance;

    public SpatialDuplicateDetector(double maximumDistance = 0.05)
    {
        if (!NumberCompatibility.IsFinite(maximumDistance) || maximumDistance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumDistance));
        }

        _maximumDistance = maximumDistance;
    }

    public IReadOnlyList<DuplicateCandidate> Detect(IReadOnlyList<SurveyRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        if (records.Count < 2)
        {
            return Array.Empty<DuplicateCandidate>();
        }

        var cellSize = _maximumDistance > 0 ? _maximumDistance : 1d;
        var cells = new Dictionary<GridKey, List<SurveyRecord>>();
        var candidates = new List<DuplicateCandidate>();

        foreach (var record in records.OrderBy(record => record.Origin.BatchOrdinal))
        {
            var cellX = Cell(record.Point.Easting, cellSize);
            var cellY = Cell(record.Point.Northing, cellSize);
            for (var offsetX = -1; offsetX <= 1; offsetX++)
            {
                for (var offsetY = -1; offsetY <= 1; offsetY++)
                {
                    var key = new GridKey(record.Code, cellX + offsetX, cellY + offsetY);
                    if (!cells.TryGetValue(key, out var nearby))
                    {
                        continue;
                    }

                    foreach (var previous in nearby)
                    {
                        var deltaX = record.Point.Easting - previous.Point.Easting;
                        var deltaY = record.Point.Northing - previous.Point.Northing;
                        var distance = Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
                        if (distance > _maximumDistance)
                        {
                            continue;
                        }

                        var exact = distance == 0 && record.Point.Elevation == previous.Point.Elevation;
                        candidates.Add(new DuplicateCandidate(
                            exact ? DuplicateKind.ExactDuplicate : DuplicateKind.SpatialDuplicateCandidate,
                            previous.Id,
                            previous.Origin,
                            record.Id,
                            record.Origin,
                            exact
                                ? $"Registros possivelmente duplicados: mesmo código e mesmas coordenadas ({previous.Origin.SourceFileName}:{previous.Origin.SourceLineNumber} e {record.Origin.SourceFileName}:{record.Origin.SourceLineNumber})."
                                : $"Registros próximos com o mesmo código, distância {distance:0.###} ({previous.Origin.SourceFileName}:{previous.Origin.SourceLineNumber} e {record.Origin.SourceFileName}:{record.Origin.SourceLineNumber}).",
                            distance));
                    }
                }
            }

            var ownKey = new GridKey(record.Code, cellX, cellY);
            if (!cells.TryGetValue(ownKey, out var ownCell))
            {
                ownCell = [];
                cells.Add(ownKey, ownCell);
            }

            ownCell.Add(record);
        }

        return candidates.AsReadOnly();
    }

    private static long Cell(double coordinate, double cellSize) => (long)Math.Floor(coordinate / cellSize);

    private readonly record struct GridKey(string Code, long X, long Y);
}
