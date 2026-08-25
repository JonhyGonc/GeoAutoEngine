using GeoAutoEngine.Domain;

namespace GeoAutoEngine.Tests;

public sealed class DomainContractTests
{
    [Fact]
    [Trait("Category", "FAST")]
    public void RecordId_IsStableForTheSameSourceAndIndex()
    {
        var source = SourceFileId.New();

        Assert.Equal(SurveyRecordId.From(source, 7), SurveyRecordId.From(source, 7));
        Assert.NotEqual(SurveyRecordId.From(source, 7), SurveyRecordId.From(source, 8));
        Assert.NotEqual(SurveyRecordId.From(source, 7), SurveyRecordId.From(SourceFileId.New(), 7));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void RecordOrigin_KeepsPhysicalLineSeparateFromRecordIndex()
    {
        var source = TestData.Source();

        var origin = new RecordOrigin(source.Id, source.DisplayName, 2, 19, 4);

        Assert.Equal(2, origin.SourceRecordIndex);
        Assert.Equal(19, origin.SourceLineNumber);
        Assert.Equal(4, origin.BatchOrdinal);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void SourceFile_RejectsInvalidContentFingerprint()
    {
        var columns = new ResolvedColumns(0, 1, 2, null, new Dictionary<string, int>());

        Assert.Throws<ArgumentException>(() => new SourceFile(
            SourceFileId.New(), 0, "a.csv", "C:\\a.csv", "invalid", "utf-8", ";", columns, 0));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void RecordDiagnostic_RequiresAnOrigin()
    {
        Assert.Throws<ArgumentException>(() => new Diagnostic(
            "UNKNOWN_CODE",
            "Unknown code",
            DiagnosticSeverity.Warning,
            DiagnosticScope.Record));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void DuplicateCandidate_PreservesBothOrigins()
    {
        var firstSource = TestData.Source(name: "day-1.csv");
        var secondSource = TestData.Source(selectionOrdinal: 1, name: "day-2.csv");
        var first = TestData.Record(firstSource);
        var second = TestData.Record(secondSource);

        var candidate = new DuplicateCandidate(
            DuplicateKind.SpatialDuplicateCandidate,
            first.Id,
            first.Origin,
            second.Id,
            second.Origin,
            "Same normalized code within tolerance.",
            0.02);

        Assert.Equal("day-1.csv", candidate.FirstOrigin.SourceFileName);
        Assert.Equal("day-2.csv", candidate.SecondOrigin.SourceFileName);
        Assert.Equal(0.02, candidate.Distance);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void ImportBatch_OrdersSourcesAndRecordsDeterministically()
    {
        var firstSource = TestData.Source(selectionOrdinal: 0, name: "day-1.csv");
        var secondSource = TestData.Source(selectionOrdinal: 1, name: "day-2.csv");
        var first = TestData.Record(firstSource, batch: 0);
        var second = TestData.Record(secondSource, batch: 1);

        var batch = ImportBatch.Create(
            BatchId.New(),
            [secondSource, firstSource],
            [second, first],
            [],
            [],
            []);

        Assert.Equal(["day-1.csv", "day-2.csv"], batch.SourceFiles.Select(source => source.DisplayName));
        Assert.Equal([0, 1], batch.ImportedRecords.Select(record => record.Origin.BatchOrdinal));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void ImportBatch_RejectsOriginsOutsideItsSources()
    {
        var batchSource = TestData.Source();
        var externalSource = TestData.Source(name: "external.csv");

        Assert.Throws<ArgumentException>(() => ImportBatch.Create(
            BatchId.New(),
            [batchSource],
            [TestData.Record(externalSource)],
            [],
            [],
            []));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void ImportBatch_DoesNotExposeMutableCollections()
    {
        var source = TestData.Source();
        var mutableRecords = new List<SurveyRecord> { TestData.Record(source) };

        var batch = ImportBatch.Create(BatchId.New(), [source], mutableRecords, [], [], []);
        mutableRecords.Clear();

        Assert.Single(batch.ImportedRecords);
        Assert.IsAssignableFrom<IReadOnlyList<SurveyRecord>>(batch.ImportedRecords);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void ImportBatch_RequiresContiguousBatchOrdinals()
    {
        var source = TestData.Source();

        Assert.Throws<ArgumentException>(() => ImportBatch.Create(
            BatchId.New(),
            [source],
            [TestData.Record(source, batch: 4)],
            [],
            [],
            []));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [Trait("Category", "FAST")]
    public void SurveyPoint_RejectsNonFiniteCoordinates(double invalidCoordinate)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SurveyPoint(invalidCoordinate, 1, 2));
    }
}
