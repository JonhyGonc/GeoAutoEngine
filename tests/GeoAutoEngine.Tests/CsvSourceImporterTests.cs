using System.Text;
using System.Text.Json;
using System.Diagnostics;
using GeoAutoEngine.Application;
using GeoAutoEngine.Csv;
using GeoAutoEngine.Domain;

namespace GeoAutoEngine.Tests;

public sealed class CsvSourceImporterTests
{
    [Fact]
    [Trait("Category", "FAST")]
    public async Task HeterogeneousMultiCsv_ResolvesEachSourceAndConsolidatesOneTraceableBatch()
    {
        var sources = CsvImportSources.FromFiles(
        [
            ReferenceFile("legacy_csv", "multi_csv", "levantamento_01.csv"),
            ReferenceFile("legacy_csv", "multi_csv", "levantamento_02.csv"),
        ]);
        var service = new ImportBatchService(new CsvSourceImporter(), new SpatialDuplicateDetector());

        var batch = await service.ImportAsync(sources);

        Assert.Equal(2, batch.SourceFiles.Count);
        Assert.Equal([";", "|"], batch.SourceFiles.Select(source => source.Delimiter));
        Assert.Equal([3, 3], batch.SourceFiles.Select(source => source.RecordCount));
        Assert.Equal(6, batch.ImportedRecords.Count);
        Assert.Empty(batch.RejectedRecords);
        Assert.Equal(6, batch.TotalRecordCount);
        Assert.Equal(2, batch.Summary.SourceCount);
        Assert.Equal(6, batch.Summary.TotalRecordCount);
        Assert.Equal([3, 3], batch.Summary.Sources.Select(source => source.TotalRecordCount));
        Assert.Equal(Enumerable.Range(0, 6), batch.ImportedRecords.Select(record => record.Origin.BatchOrdinal));
        Assert.All(batch.ImportedRecords.Take(3), record => Assert.Equal("levantamento_01.csv", record.Origin.SourceFileName));
        Assert.All(batch.ImportedRecords.Skip(3), record => Assert.Equal("levantamento_02.csv", record.Origin.SourceFileName));
        Assert.Contains(batch.DuplicateCandidates, candidate =>
            candidate.FirstOrigin.SourceFileName != candidate.SecondOrigin.SourceFileName && candidate.Kind == DuplicateKind.ExactDuplicate);
        Assert.Contains(batch.Diagnostics, diagnostic => diagnostic.Code == "CSV_DUPLICATE_CANDIDATE");
    }

    [Fact]
    [Trait("Category", "FAST")]
    public async Task Cp1252AndHeaderAliases_AreResolvedPerSource()
    {
        var path = TempCsv(Encoding.Latin1.GetBytes(
            "Ponto|Código|Leste (E)|Norte (N)|Cota\r\n1|AF0,95 Ø250|500,00|1000,00|10,50\r\n"));
        try
        {
            var result = await new CsvSourceImporter().ImportAsync(new ImportSourceRequest(path, 0));

            Assert.Equal("cp1252", result.SourceFile.EncodingName);
            Assert.Equal("|", result.SourceFile.Delimiter);
            var record = Assert.Single(result.ImportedRecords);
            Assert.Equal("AF0.95D250", record.Code);
            Assert.Equal(500d, record.Point.Easting);
            Assert.Equal(1000d, record.Point.Northing);
            Assert.Equal(10.5d, record.Point.Elevation);
            Assert.Equal(2, record.Origin.SourceLineNumber);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    [Trait("Category", "FAST")]
    public async Task HeaderlessCsv_UsesValidatedLegacyPositions()
    {
        var path = TempCsv(Encoding.UTF8.GetBytes("1;PVAF;1000.0;500.0\n2;AF0.95D250;1001.0;501.0\n"));
        try
        {
            var result = await new CsvSourceImporter().ImportAsync(new ImportSourceRequest(path, 0));

            Assert.Equal(2, result.ImportedRecords.Count);
            Assert.Equal(1, result.ImportedRecords[0].Origin.SourceLineNumber);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CSV_HEADERLESS");
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CSV_POSITIONAL_COLUMNS");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    [Trait("Category", "FAST")]
    public async Task InvalidRecord_IsRejectedWithoutLosingItsOrigin()
    {
        var path = TempCsv(Encoding.UTF8.GetBytes("Norte;Leste;Código\n1000;inválida;PVAF\n"));
        try
        {
            var result = await new CsvSourceImporter().ImportAsync(new ImportSourceRequest(path, 0));

            Assert.Empty(result.ImportedRecords);
            var rejection = Assert.Single(result.RejectedRecords);
            Assert.Equal(2, rejection.Origin.SourceLineNumber);
            Assert.Contains("Coordenadas inválidas ou ausentes.", rejection.Reasons);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CSV_INVALID_COORDINATES");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    [Trait("Category", "FAST")]
    public async Task StructurallyInvalidSource_DoesNotDiscardOtherFilesInTheBatch()
    {
        var valid = ReferenceFile("legacy_csv", "multi_csv", "levantamento_01.csv");
        var missing = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.csv");
        var service = new ImportBatchService(new CsvSourceImporter(), new SpatialDuplicateDetector());

        var batch = await service.ImportAsync(CsvImportSources.FromFiles([valid, missing]));

        Assert.Equal(2, batch.SourceFiles.Count);
        Assert.Equal(SourceImportStatus.Imported, batch.SourceFiles[0].Status);
        Assert.Equal(SourceImportStatus.Rejected, batch.SourceFiles[1].Status);
        Assert.Equal(1, batch.Summary.RejectedSourceCount);
        Assert.Equal(0, batch.Summary.Sources[1].TotalRecordCount);
        Assert.Equal(3, batch.ImportedRecords.Count);
        Assert.True(batch.HasErrors);
        Assert.Contains(batch.Diagnostics, diagnostic => diagnostic.Code == "CSV_SOURCE_NOT_FOUND");
    }

    [Fact]
    [Trait("Category", "REGRESSION")]
    public async Task QuotedMultilineField_PreservesPhysicalStartLineOfFollowingRecord()
    {
        var path = TempCsv(Encoding.UTF8.GetBytes(
            "Norte;Leste;Código;Observação\n1000;500;PVAF;\"linha 1\nlinha 2\"\n1001;501;RG;ok\n"));
        try
        {
            var result = await new CsvSourceImporter().ImportAsync(new ImportSourceRequest(path, 0));

            Assert.Equal(2, result.ImportedRecords.Count);
            Assert.Equal(2, result.ImportedRecords[0].Origin.SourceLineNumber);
            Assert.Equal(4, result.ImportedRecords[1].Origin.SourceLineNumber);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [Trait("Category", "REGRESSION")]
    [InlineData("Rua S#U00e3o Francisco 1407.csv", 206, 198, 8, "Bloco=40;Revisar=8;Texto=158", "Esgoto sanitário=29;Não identificada=81;Telefone=3;Águas pluviais=85")]
    [InlineData("RUA BOEHMERWALD 1407(1).csv", 152, 132, 19, "Bloco=15;Revisar=19;Texto=117", "Esgoto sanitário=28;Não identificada=76;Água potável=4;Águas pluviais=24")]
    public async Task RealFixture_MatchesImportAndCxInterpretationCounts(
        string fileName,
        int total,
        int recognized,
        int unknown,
        string expectedTypes,
        string expectedNetworks)
    {
        var path = ReferenceFile("legacy_csv", "real", fileName);
        var result = await new CsvSourceImporter().ImportAsync(new ImportSourceRequest(path, 0));

        Assert.Equal(total, result.SourceFile.RecordCount);
        Assert.Equal(total, result.ImportedRecords.Count + result.RejectedRecords.Count);
        Assert.Empty(result.RejectedRecords);
        var processable = result.ImportedRecords.Where(record => record.Interpretation.RecordType != "Ignorar").ToArray();
        Assert.Equal(recognized, processable.Count(record => record.Interpretation.Recognized));
        Assert.Equal(unknown, processable.Count(record => !record.Interpretation.Recognized));
        Assert.Equal(unknown, result.Diagnostics.Count(diagnostic => diagnostic.Code == "CSV_UNKNOWN_CODE"));
        Assert.Equal(ParseCounts(expectedTypes), CountBy(processable, record => record.Interpretation.RecordType));
        Assert.Equal(
            ParseCounts(expectedNetworks),
            CountBy(processable.Where(record => record.Interpretation.Recognized), record => record.Interpretation.Network!));
    }

    [Fact]
    [Trait("Category", "REGRESSION")]
    public async Task EveryUnchangedInterpretedField_MatchesFrozenStage2LegacyOracle()
    {
        using var oracle = JsonDocument.Parse(File.ReadAllText(
            ReferenceFile("regression_data", "stage2_interpretation_oracle.json"),
            Encoding.UTF8));
        foreach (var fixture in oracle.RootElement.GetProperty("fixtures").EnumerateArray())
        {
            var fileName = fixture.GetProperty("file_name").GetString()!;
            var result = await new CsvSourceImporter().ImportAsync(new ImportSourceRequest(
                ReferenceFile("legacy_csv", "real", fileName), 0));
            var expectedRows = fixture.GetProperty("rows").EnumerateArray().ToArray();
            Assert.Equal(expectedRows.Length, result.ImportedRecords.Count);
            Assert.Empty(result.RejectedRecords);

            foreach (var expected in expectedRows)
            {
                var expectedOriginalCode = JsonString(expected, "codigo_original") ?? string.Empty;
                if (expectedOriginalCode is "CXAP LAC" or "CXTL")
                {
                    continue;
                }

                var record = result.ImportedRecords[expected.GetProperty("source_index").GetInt32()];
                var actual = record.Interpretation;
                Assert.Equal(expectedOriginalCode, actual.OriginalCode);
                Assert.Equal(JsonString(expected, "codigo_normalizado") ?? string.Empty, actual.NormalizedCode);
                Assert.Equal(JsonString(expected, "natureza") ?? string.Empty, actual.Nature);
                Assert.Equal(JsonString(expected, "tipo") ?? string.Empty, actual.RecordType);
                Assert.Equal(JsonString(expected, "rede"), actual.Network);
                Assert.Equal(JsonDouble(expected, "profundidade"), actual.Depth);
                Assert.Equal(JsonDouble(expected, "diametro"), actual.Diameter);
                Assert.Equal(JsonString(expected, "material"), actual.Material);
                Assert.Equal(JsonString(expected, "complemento"), actual.Complement);
                Assert.Equal(expected.GetProperty("reconhecido").GetBoolean(), actual.Recognized);
                Assert.Equal(JsonString(expected, "confianca") ?? string.Empty, actual.Confidence);
                Assert.Equal(JsonString(expected, "regra") ?? string.Empty, actual.Rule);
                Assert.Equal(JsonString(expected, "observacao") ?? string.Empty, actual.Observation);
            }
        }
    }

    [Fact]
    [Trait("Category", "REGRESSION")]
    public void CompleteRuleCatalog_MatchesFrozenStage2LegacyOracle()
    {
        using var oracle = JsonDocument.Parse(File.ReadAllText(
            ReferenceFile("regression_data", "stage2_interpretation_oracle.json"),
            Encoding.UTF8));
        var normalizer = new SurveyCodeNormalizer();
        var interpreter = new SurveyCodeInterpreter();

        foreach (var expected in oracle.RootElement.GetProperty("catalog_cases").EnumerateArray())
        {
            var original = JsonString(expected, "codigo_original") ?? string.Empty;
            var actual = interpreter.Interpret(original, normalizer.Normalize(original));
            AssertInterpretation(expected, actual);
        }
    }

    [Fact]
    [Trait("Category", "REGRESSION")]
    public async Task TenThousandRecords_ImportAndDuplicateScanStayWithinCheckpointBudget()
    {
        const int count = 10_000;
        var content = new StringBuilder("Norte;Leste;Cota;Código\n");
        for (var index = 0; index < count; index++)
        {
            content.Append(7_000_000 + index).Append(';')
                .Append(500_000 + index).Append(";10;AF1.00D100PVC\n");
        }

        var path = TempCsv(Encoding.UTF8.GetBytes(content.ToString()));
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var batch = await new ImportBatchService(new CsvSourceImporter(), new SpatialDuplicateDetector())
                .ImportAsync(CsvImportSources.FromSingleFile(path));
            stopwatch.Stop();

            Assert.Equal(count, batch.ImportedRecords.Count);
            Assert.Empty(batch.RejectedRecords);
            Assert.Empty(batch.DuplicateCandidates);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"Importação levou {stopwatch.Elapsed}.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string TempCsv(byte[] content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"geoauto-{Guid.NewGuid():N}.csv");
        File.WriteAllBytes(path, content);
        return path;
    }

    private static Dictionary<string, int> CountBy(
        IEnumerable<SurveyRecord> records,
        Func<SurveyRecord, string> selector) =>
        records.GroupBy(selector, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

    private static Dictionary<string, int> ParseCounts(string value) =>
        value.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Split('=', 2))
            .ToDictionary(parts => parts[0], parts => int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture), StringComparer.Ordinal);

    private static string? JsonString(JsonElement element, string propertyName)
    {
        var property = element.GetProperty(propertyName);
        return property.ValueKind == JsonValueKind.Null ? null : property.GetString();
    }

    private static double? JsonDouble(JsonElement element, string propertyName)
    {
        var property = element.GetProperty(propertyName);
        return property.ValueKind == JsonValueKind.Null ? null : property.GetDouble();
    }

    private static void AssertInterpretation(JsonElement expected, SurveyInterpretation actual)
    {
        Assert.Equal(JsonString(expected, "codigo_original") ?? string.Empty, actual.OriginalCode);
        Assert.Equal(JsonString(expected, "codigo_normalizado") ?? string.Empty, actual.NormalizedCode);
        Assert.Equal(JsonString(expected, "natureza") ?? string.Empty, actual.Nature);
        Assert.Equal(JsonString(expected, "tipo") ?? string.Empty, actual.RecordType);
        Assert.Equal(JsonString(expected, "rede"), actual.Network);
        Assert.Equal(JsonDouble(expected, "profundidade"), actual.Depth);
        Assert.Equal(JsonDouble(expected, "diametro"), actual.Diameter);
        Assert.Equal(JsonString(expected, "material"), actual.Material);
        Assert.Equal(JsonString(expected, "complemento"), actual.Complement);
        Assert.Equal(expected.GetProperty("reconhecido").GetBoolean(), actual.Recognized);
        Assert.Equal(JsonString(expected, "confianca") ?? string.Empty, actual.Confidence);
        Assert.Equal(JsonString(expected, "regra") ?? string.Empty, actual.Rule);
        Assert.Equal(JsonString(expected, "observacao") ?? string.Empty, actual.Observation);
    }

    private static string ReferenceFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "reference")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine([directory!.FullName, "reference", .. parts]);
    }
}
