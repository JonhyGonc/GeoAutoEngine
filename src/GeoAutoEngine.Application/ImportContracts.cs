using GeoAutoEngine.Domain;

namespace GeoAutoEngine.Application;

public sealed record ImportSourceRequest
{
    public ImportSourceRequest(string canonicalPath, int selectionOrdinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalPath);
        ArgumentOutOfRangeException.ThrowIfNegative(selectionOrdinal);
        CanonicalPath = canonicalPath;
        SelectionOrdinal = selectionOrdinal;
    }

    public string CanonicalPath { get; }

    public int SelectionOrdinal { get; }
}

public sealed class SourceImportResult
{
    public SourceImportResult(
        SourceFile sourceFile,
        IEnumerable<SurveyRecord> importedRecords,
        IEnumerable<RejectedRecord> rejectedRecords,
        IEnumerable<Diagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(sourceFile);
        ArgumentNullException.ThrowIfNull(importedRecords);
        ArgumentNullException.ThrowIfNull(rejectedRecords);
        ArgumentNullException.ThrowIfNull(diagnostics);
        SourceFile = sourceFile;
        ImportedRecords = Array.AsReadOnly(importedRecords.ToArray());
        RejectedRecords = Array.AsReadOnly(rejectedRecords.ToArray());
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());

        var allOrigins = ImportedRecords.Select(record => record.Origin)
            .Concat(RejectedRecords.Select(record => record.Origin))
            .ToArray();
        if (allOrigins.Length != sourceFile.RecordCount)
        {
            throw new ArgumentException("The source record count must include imported and rejected records.");
        }

        if (sourceFile.Status == SourceImportStatus.Rejected &&
            !Diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error && diagnostic.Scope == DiagnosticScope.Source))
        {
            throw new ArgumentException("A rejected source requires a source-level error diagnostic.", nameof(diagnostics));
        }

        if (allOrigins.Any(origin => origin.SourceFileId != sourceFile.Id))
        {
            throw new ArgumentException("Every source result record must refer to its source file.");
        }


        if (Diagnostics.SelectMany(diagnostic => diagnostic.Origins)
            .Any(origin => origin.SourceFileId != sourceFile.Id) ||
            Diagnostics.Where(diagnostic => diagnostic.SourceFileId.HasValue)
                .Any(diagnostic => diagnostic.SourceFileId!.Value != sourceFile.Id))
        {
            throw new ArgumentException("Every source diagnostic must refer to its source file.");
        }

        var indexes = allOrigins.Select(origin => origin.SourceRecordIndex).ToArray();
        if (indexes.Distinct().Count() != indexes.Length)
        {
            throw new ArgumentException("Source record indexes must be unique within a source.");
        }
    }

    public SourceFile SourceFile { get; }

    public IReadOnlyList<SurveyRecord> ImportedRecords { get; }

    public IReadOnlyList<RejectedRecord> RejectedRecords { get; }

    public IReadOnlyList<Diagnostic> Diagnostics { get; }
}

public sealed record ImportProgress(int CompletedSources, int TotalSources, string CurrentSourceName);

public interface ISourceImporter
{
    ValueTask<SourceImportResult> ImportAsync(
        ImportSourceRequest request,
        CancellationToken cancellationToken = default);
}

public interface IDuplicateDetector
{
    IReadOnlyList<DuplicateCandidate> Detect(IReadOnlyList<SurveyRecord> records);
}

public interface IDrawingPlanApplier
{
    ValueTask<DrawingPlanApplicationResult> ApplyAsync(
        DrawingPlan plan,
        CancellationToken cancellationToken = default);
}

public sealed record DrawingPlanApplicationResult(
    bool Succeeded,
    int CreatedItemCount,
    int SkippedExistingItemCount,
    int PendingItemCount,
    int FailedItemCount,
    bool RolledBack,
    bool Regenerated,
    IReadOnlyList<Diagnostic> Diagnostics);
