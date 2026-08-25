namespace GeoAutoEngine.Domain;

public sealed record ImportBatchSummary
{
    private ImportBatchSummary(
        int sourceCount,
        int totalRecordCount,
        int importedRecordCount,
        int rejectedRecordCount,
        int rejectedSourceCount,
        int processableRecordCount,
        int recognizedRecordCount,
        int unknownCodeCount,
        int duplicateCandidateCount,
        IReadOnlyList<SourceImportSummary> sources,
        IReadOnlyDictionary<string, int> recordsByNetwork,
        IReadOnlyDictionary<string, int> recordsByType,
        IReadOnlyDictionary<string, int> recordsByMaterial,
        IReadOnlyDictionary<string, int> unknownCodes,
        IReadOnlyDictionary<DiagnosticSeverity, int> diagnosticsBySeverity)
    {
        SourceCount = sourceCount;
        TotalRecordCount = totalRecordCount;
        ImportedRecordCount = importedRecordCount;
        RejectedRecordCount = rejectedRecordCount;
        RejectedSourceCount = rejectedSourceCount;
        ProcessableRecordCount = processableRecordCount;
        RecognizedRecordCount = recognizedRecordCount;
        UnknownCodeCount = unknownCodeCount;
        DuplicateCandidateCount = duplicateCandidateCount;
        Sources = sources;
        RecordsByNetwork = recordsByNetwork;
        RecordsByType = recordsByType;
        RecordsByMaterial = recordsByMaterial;
        UnknownCodes = unknownCodes;
        DiagnosticsBySeverity = diagnosticsBySeverity;
    }

    public int SourceCount { get; }
    public int TotalRecordCount { get; }
    public int ImportedRecordCount { get; }
    public int RejectedRecordCount { get; }
    public int RejectedSourceCount { get; }
    public int ProcessableRecordCount { get; }
    public int RecognizedRecordCount { get; }
    public int UnknownCodeCount { get; }
    public int DuplicateCandidateCount { get; }
    public IReadOnlyList<SourceImportSummary> Sources { get; }
    public IReadOnlyDictionary<string, int> RecordsByNetwork { get; }
    public IReadOnlyDictionary<string, int> RecordsByType { get; }
    public IReadOnlyDictionary<string, int> RecordsByMaterial { get; }
    public IReadOnlyDictionary<string, int> UnknownCodes { get; }
    public IReadOnlyDictionary<DiagnosticSeverity, int> DiagnosticsBySeverity { get; }

    internal static ImportBatchSummary Create(
        IReadOnlyList<SourceFile> sources,
        IReadOnlyList<SurveyRecord> imported,
        IReadOnlyList<RejectedRecord> rejected,
        IReadOnlyList<Diagnostic> diagnostics,
        IReadOnlyList<DuplicateCandidate> duplicates)
    {
        var processable = imported.Where(record => record.Interpretation.RecordType != "Ignorar").ToArray();
        var recognized = processable.Where(record => record.Interpretation.Recognized).ToArray();
        var unknown = processable.Where(record => !record.Interpretation.Recognized).ToArray();
        var sourceSummaries = sources.OrderBy(source => source.SelectionOrdinal)
            .Select(source => new SourceImportSummary(
                source.Id,
                source.DisplayName,
                source.SelectionOrdinal,
                source.Status,
                source.RecordCount,
                imported.Count(record => record.Origin.SourceFileId == source.Id),
                rejected.Count(record => record.Origin.SourceFileId == source.Id)))
            .ToArray();

        return new ImportBatchSummary(
            sources.Count,
            imported.Count + rejected.Count,
            imported.Count,
            rejected.Count,
            sources.Count(source => source.Status == SourceImportStatus.Rejected),
            processable.Length,
            recognized.Length,
            unknown.Length,
            duplicates.Count,
            Array.AsReadOnly(sourceSummaries),
            CountBy(recognized, record => record.Interpretation.Network),
            CountBy(processable, record => record.Interpretation.RecordType),
            CountBy(processable, record => record.Interpretation.Material),
            CountBy(unknown, record => record.Interpretation.OriginalCode),
            new System.Collections.ObjectModel.ReadOnlyDictionary<DiagnosticSeverity, int>(
                diagnostics.GroupBy(diagnostic => diagnostic.Severity)
                    .ToDictionary(group => group.Key, group => group.Count())));
    }

    private static IReadOnlyDictionary<string, int> CountBy(
        IEnumerable<SurveyRecord> records,
        Func<SurveyRecord, string?> selector)
    {
        var counts = records.Select(selector)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(value => value!, StringComparer.Ordinal)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        return CollectionSnapshot.Dictionary(counts, nameof(records));
    }
}

public sealed record SourceImportSummary(
    SourceFileId SourceFileId,
    string DisplayName,
    int SelectionOrdinal,
    SourceImportStatus Status,
    int TotalRecordCount,
    int ImportedRecordCount,
    int RejectedRecordCount);
