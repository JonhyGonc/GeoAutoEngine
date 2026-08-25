using GeoAutoEngine.Application;
using GeoAutoEngine.Domain;

namespace GeoAutoEngine.Tests;

public sealed class ApplicationContractTests
{
    [Fact]
    [Trait("Category", "FAST")]
    public async Task ImportService_ImportsEachSourceAndConsolidatesOneBatch()
    {
        var importer = new RecordingImporter();
        var service = new ImportBatchService(importer, new NoDuplicateDetector());
        var requests = new[]
        {
            new ImportSourceRequest(Path.GetFullPath("day-2.csv"), 1),
            new ImportSourceRequest(Path.GetFullPath("day-1.csv"), 0),
        };

        var batch = await service.ImportAsync(requests);

        Assert.Equal(["day-1.csv", "day-2.csv"], importer.ImportedFileNames);
        Assert.Equal(2, batch.SourceFiles.Count);
        Assert.Equal([0, 1], batch.ImportedRecords.Select(record => record.Origin.BatchOrdinal));
        Assert.Equal(["day-1.csv", "day-2.csv"], batch.ImportedRecords.Select(record => record.Origin.SourceFileName));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void Consolidate_RebasesDiagnosticOrigins()
    {
        var source = TestData.Source();
        var provisional = TestData.Record(source, batch: 99);
        var diagnostic = new Diagnostic(
            "UNKNOWN_CODE",
            "Unknown code: XXXX",
            DiagnosticSeverity.Warning,
            DiagnosticScope.Record,
            source.Id,
            [provisional.Origin]);
        var sourceResult = new SourceImportResult(source, [provisional], [], [diagnostic]);
        var service = new ImportBatchService(new RecordingImporter(), new NoDuplicateDetector());

        var batch = service.Consolidate(BatchId.New(), [sourceResult]);

        Assert.Equal(0, batch.Diagnostics[0].Origins[0].BatchOrdinal);
        Assert.Equal(source.DisplayName, batch.Diagnostics[0].Origins[0].SourceFileName);
    }

    [Fact]
    [Trait("Category", "REGRESSION")]
    public void Consolidate_DiagnosesDuplicatesWithoutRemovingRecords()
    {
        var firstSource = TestData.Source(selectionOrdinal: 0, name: "day-1.csv");
        var secondSource = TestData.Source(selectionOrdinal: 1, name: "day-2.csv");
        var service = new ImportBatchService(new RecordingImporter(), new FirstPairDuplicateDetector());

        var batch = service.Consolidate(
            BatchId.New(),
            [
                TestData.SourceResult(firstSource, TestData.Record(firstSource)),
                TestData.SourceResult(secondSource, TestData.Record(secondSource)),
            ]);

        Assert.Equal(2, batch.ImportedRecords.Count);
        var candidate = Assert.Single(batch.DuplicateCandidates);
        Assert.NotEqual(candidate.FirstOrigin.SourceFileId, candidate.SecondOrigin.SourceFileId);
    }

    private sealed class RecordingImporter : ISourceImporter
    {
        public List<string> ImportedFileNames { get; } = [];

        public ValueTask<SourceImportResult> ImportAsync(
            ImportSourceRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = Path.GetFileName(request.CanonicalPath);
            ImportedFileNames.Add(name);
            var source = TestData.Source(request.SelectionOrdinal, name: name);
            return ValueTask.FromResult(TestData.SourceResult(source, TestData.Record(source)));
        }
    }

    private sealed class FirstPairDuplicateDetector : IDuplicateDetector
    {
        public IReadOnlyList<DuplicateCandidate> Detect(IReadOnlyList<SurveyRecord> records) =>
            [new DuplicateCandidate(
                DuplicateKind.ExactDuplicate,
                records[0].Id,
                records[0].Origin,
                records[1].Id,
                records[1].Origin,
                "Equivalent normalized records.",
                0)];
    }
}
