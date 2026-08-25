namespace GeoAutoEngine.Domain;

public sealed class ImportBatch
{
    private ImportBatch(
        BatchId id,
        IReadOnlyList<SourceFile> sourceFiles,
        IReadOnlyList<SurveyRecord> importedRecords,
        IReadOnlyList<RejectedRecord> rejectedRecords,
        IReadOnlyList<Diagnostic> diagnostics,
        IReadOnlyList<DuplicateCandidate> duplicateCandidates)
    {
        Id = id;
        SourceFiles = sourceFiles;
        ImportedRecords = importedRecords;
        RejectedRecords = rejectedRecords;
        Diagnostics = diagnostics;
        DuplicateCandidates = duplicateCandidates;
        Summary = ImportBatchSummary.Create(sourceFiles, importedRecords, rejectedRecords, diagnostics, duplicateCandidates);
    }

    public BatchId Id { get; }

    public IReadOnlyList<SourceFile> SourceFiles { get; }

    public IReadOnlyList<SurveyRecord> ImportedRecords { get; }

    public IReadOnlyList<RejectedRecord> RejectedRecords { get; }

    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    public IReadOnlyList<DuplicateCandidate> DuplicateCandidates { get; }

    public ImportBatchSummary Summary { get; }

    public int TotalRecordCount => ImportedRecords.Count + RejectedRecords.Count;

    public bool HasErrors => Diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

    public static ImportBatch Create(
        BatchId id,
        IEnumerable<SourceFile> sourceFiles,
        IEnumerable<SurveyRecord> importedRecords,
        IEnumerable<RejectedRecord> rejectedRecords,
        IEnumerable<Diagnostic> diagnostics,
        IEnumerable<DuplicateCandidate> duplicateCandidates)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id.Value, Guid.Empty);
        var sources = CollectionSnapshot.List(sourceFiles, nameof(sourceFiles))
            .OrderBy(source => source.SelectionOrdinal)
            .ToArray();
        if (sources.Length == 0)
        {
            throw new ArgumentException("An import batch requires at least one source file.", nameof(sourceFiles));
        }

        EnsureUnique(sources.Select(source => source.Id), nameof(sourceFiles), "source identifier");
        EnsureUnique(sources.Select(source => source.SelectionOrdinal), nameof(sourceFiles), "selection ordinal");

        var records = CollectionSnapshot.List(importedRecords, nameof(importedRecords))
            .OrderBy(record => record.Origin.BatchOrdinal)
            .ToArray();
        var rejections = CollectionSnapshot.List(rejectedRecords, nameof(rejectedRecords))
            .OrderBy(record => record.Origin.BatchOrdinal)
            .ToArray();
        var diagnosticSnapshot = CollectionSnapshot.List(diagnostics, nameof(diagnostics));
        var duplicates = CollectionSnapshot.List(duplicateCandidates, nameof(duplicateCandidates));

        var sourceIds = new HashSet<SourceFileId>(sources.Select(source => source.Id));
        var allOrigins = records.Select(record => record.Origin)
            .Concat(rejections.Select(record => record.Origin))
            .ToArray();
        if (allOrigins.Any(origin => !sourceIds.Contains(origin.SourceFileId)))
        {
            throw new ArgumentException("Every record origin must refer to a source in the batch.");
        }


        var sourceNames = sources.ToDictionary(source => source.Id, source => source.DisplayName);
        if (allOrigins.Any(origin => !string.Equals(
                origin.SourceFileName,
                sourceNames[origin.SourceFileId],
                StringComparison.Ordinal)))
        {
            throw new ArgumentException("Every record origin must preserve the source display name.");
        }

        EnsureUnique(allOrigins.Select(origin => origin.BatchOrdinal), nameof(importedRecords), "batch ordinal");
        if (!allOrigins.Select(origin => origin.BatchOrdinal).OrderBy(value => value).SequenceEqual(Enumerable.Range(0, allOrigins.Length)))
        {
            throw new ArgumentException("Batch ordinals must be contiguous and start at zero.", nameof(importedRecords));
        }

        EnsureUnique(
            allOrigins.Select(origin => (origin.SourceFileId, origin.SourceRecordIndex)),
            nameof(importedRecords),
            "source record identity");
        EnsureUnique(records.Select(record => record.Id), nameof(importedRecords), "record identifier");

        foreach (var source in sources)
        {
            var sourceRecordCount = allOrigins.Count(origin => origin.SourceFileId == source.Id);
            if (sourceRecordCount != source.RecordCount)
            {
                throw new ArgumentException(
                    $"Source '{source.DisplayName}' declares {source.RecordCount} records but contains {sourceRecordCount}.",
                    nameof(sourceFiles));
            }
        }

        if (diagnosticSnapshot.SelectMany(diagnostic => diagnostic.Origins)
            .Any(origin => !sourceIds.Contains(origin.SourceFileId)))
        {
            throw new ArgumentException("Every diagnostic origin must refer to a source in the batch.", nameof(diagnostics));
        }

        if (diagnosticSnapshot.Where(diagnostic => diagnostic.SourceFileId.HasValue)
            .Any(diagnostic => !sourceIds.Contains(diagnostic.SourceFileId!.Value)))
        {
            throw new ArgumentException("Every source diagnostic must refer to a source in the batch.", nameof(diagnostics));
        }

        var recordIds = new HashSet<SurveyRecordId>(records.Select(record => record.Id));
        if (duplicates.Any(candidate =>
                !recordIds.Contains(candidate.FirstRecordId) || !recordIds.Contains(candidate.SecondRecordId)))
        {
            throw new ArgumentException("Every duplicate candidate must refer to imported records in the batch.", nameof(duplicateCandidates));
        }

        var originsByRecordId = records.ToDictionary(record => record.Id, record => record.Origin);
        if (duplicates.Any(candidate =>
                candidate.FirstOrigin != originsByRecordId[candidate.FirstRecordId] ||
                candidate.SecondOrigin != originsByRecordId[candidate.SecondRecordId]))
        {
            throw new ArgumentException("Duplicate candidate origins must match their imported records.", nameof(duplicateCandidates));
        }

        return new ImportBatch(
            id,
            Array.AsReadOnly(sources),
            Array.AsReadOnly(records),
            Array.AsReadOnly(rejections),
            diagnosticSnapshot,
            duplicates);
    }

    private static void EnsureUnique<T>(IEnumerable<T> values, string parameterName, string description)
        where T : notnull
    {
        var seen = new HashSet<T>();
        if (values.Any(value => !seen.Add(value)))
        {
            throw new ArgumentException($"Each {description} must be unique.", parameterName);
        }
    }
}
