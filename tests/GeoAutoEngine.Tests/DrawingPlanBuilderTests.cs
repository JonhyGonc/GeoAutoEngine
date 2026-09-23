using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using GeoAutoEngine.Application;
using GeoAutoEngine.Csv;
using GeoAutoEngine.Domain;
using Xunit.Abstractions;

namespace GeoAutoEngine.Tests;

public sealed class DrawingPlanBuilderTests
{
    private readonly ITestOutputHelper _output;

    public DrawingPlanBuilderTests(ITestOutputHelper output) => _output = output;

    [Fact]
    [Trait("Category", "FAST")]
    public void NetworkText_ContainsLegacyContentPlacementAndCadIntent()
    {
        var batch = Batch(
            RecordSpec.Text("E1.40D150", 10, 20, "Esgoto sanitário", "REDE_E", 1.4, 150));

        var plan = new DrawingPlanBuilder().Build(batch);

        Assert.Equal(2, plan.Texts.Count);
        var depth = Assert.Single(plan.Texts, item => item.Kind == TextPlanKind.Depth);
        var diameter = Assert.Single(plan.Texts, item => item.Kind == TextPlanKind.Diameter);
        Assert.Equal("P.1.40", depth.Content);
        Assert.Equal("Ø150", diameter.Content);
        Assert.Equal("GF-Rede Esgoto", depth.Context.Layer);
        Assert.Equal(new PlanPoint3(10, 20, 0), depth.Anchor);
        Assert.Equal(0.5, depth.Height);
        Assert.Equal(0.1, depth.PerpendicularOffset);
        Assert.Equal(-0.6, diameter.PerpendicularOffset, precision: 10);
        Assert.Equal(PlanRotationMode.FollowNetwork, depth.RotationMode);
        Assert.Contains("REDE_ESGOTO", depth.ReferenceCurveLayers);
        Assert.All(plan.Texts, item => Assert.Single(item.Context.Origins));
        Assert.Equal(2, plan.XData.Count);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void TextDeduplication_PreservesCandidateFarthestFromPvLikeLegacy()
    {
        var batch = Batch(
            RecordSpec.Block("PVE", 0, 0, "Esgoto sanitário", "PVE"),
            RecordSpec.Text("E1.40D150", 0.4, 0, "Esgoto sanitário", "REDE_E", 1.4, 150),
            RecordSpec.Text("E1.40D150", 1.1, 0, "Esgoto sanitário", "REDE_E", 1.4, 150));

        var plan = new DrawingPlanBuilder().Build(batch);

        Assert.Equal(2, plan.Texts.Count);
        Assert.All(plan.Texts, item => Assert.Equal(1.1, item.Anchor.X));
        Assert.Equal(2, plan.SuppressedItems.Count(item => item.Kind == DrawingItemKind.Text));
        Assert.All(plan.SuppressedItems.Where(item => item.Kind == DrawingItemKind.Text), item => Assert.NotNull(item.RetainedItemId));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void Blocks_UseCatalogFamiliesRotationAndDataOnlyDeduplication()
    {
        var batch = Batch(
            RecordSpec.Block("RG", 0, 0, "Água potável", "REGISTRO_AF"),
            RecordSpec.Block("PVES", 10, 0, "Esgoto sanitário", "PV_ES"),
            RecordSpec.Block("PVES", 10, 0, "Esgoto sanitário", "PV_ES"),
            RecordSpec.Block("PVAP", 10.5, 0, "Águas pluviais", "PV_AP"));

        var plan = new DrawingPlanBuilder().Build(batch);

        Assert.Equal(2, plan.Blocks.Count);
        var valve = Assert.Single(plan.Blocks, item => item.BlockKey == "REGISTRO_AGUA_POTAVEL");
        Assert.Equal(PlanRotationMode.FollowNetwork, valve.RotationMode);
        Assert.Equal("rg_agua_potavel.dwg", valve.AssetFileName);
        Assert.Contains(plan.SuppressedItems, item => item.Reason.Contains("duplicado", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(plan.SuppressedItems, item => item.Reason.Contains("mesma família", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void Poste_UsesTheAppOwnedDefinitionAndBundledAsset()
    {
        var batch = Batch(RecordSpec.Block("POSTE", 10, 20, "Rede elétrica", "POSTE"));

        var poste = Assert.Single(new DrawingPlanBuilder().Build(batch).Blocks);

        Assert.Equal("POSTE", poste.BlockKey);
        Assert.Equal("GEOAUTO_POSTE", poste.BlockName);
        Assert.Equal("poste.dwg", poste.AssetFileName);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void GenericWaterPv_UsesTheBundledGenericPvAsset()
    {
        var batch = Batch(RecordSpec.Block("PVAFG", 10, 20, "Água potável", "PV_AF"));

        var pv = Assert.Single(new DrawingPlanBuilder().Build(batch).Blocks);

        Assert.Equal("PV_GENERICO", pv.BlockKey);
        Assert.Equal("pv.dwg", pv.AssetFileName);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void RejectedInputReasons_ArePreservedInDrawingPlan()
    {
        var source = TestData.Source(recordCount: 1);
        var rejection = new RejectedRecord(
            TestData.Origin(source),
            new Dictionary<string, string?> { ["Leste"] = "inválida" },
            ["Coordenadas inválidas ou ausentes."]);
        var batch = ImportBatch.Create(BatchId.New(), [source], [], [rejection], [], []);

        var plan = new DrawingPlanBuilder().Build(batch);

        var preserved = Assert.Single(plan.RejectedInputs);
        Assert.Equal(rejection.Origin, preserved.Origin);
        Assert.Equal(rejection.Reasons, preserved.Reasons);
        Assert.Equal(1, plan.Summary.RejectedInputCount);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void RainStructures_BoundaryAnnotationsAndUnknownAlerts_AreTypedAndTraceable()
    {
        var batch = Batch(
            RecordSpec.Block("BL", 0, 0, "Águas pluviais", "BOCA_LOBO"),
            RecordSpec.Block("BL", 4, 0, "Águas pluviais", "BOCA_LOBO"),
            RecordSpec.Block("BL", 0.4, 2, "Águas pluviais", "BOCA_LOBO"),
            RecordSpec.Boundary("DIVISA 113", 10, 10, "Nº113"),
            RecordSpec.Unknown("XP54", 20, 20));

        var plan = new DrawingPlanBuilder().Build(batch);

        var structure = Assert.Single(plan.RainStructures);
        Assert.Equal(RainStructureKind.CatchBasin, structure.Kind);
        Assert.Equal(
            [new PlanPoint3(0, 0, 0), new PlanPoint3(4, 0, 0), new PlanPoint3(4, 2, 0), new PlanPoint3(0, 2, 0)],
            structure.Vertices);
        var annotation = Assert.Single(plan.Texts);
        Assert.Equal(TextPlanKind.BoundaryAnnotation, annotation.Kind);
        Assert.Equal("Nº113", annotation.Content);
        Assert.Equal("GEO_ARIAL_BOLD", annotation.StyleName);
        var alert = Assert.Single(plan.CircleAlerts);
        Assert.Equal(PlanAlertKind.UnknownCode, alert.Kind);
        Assert.Equal(PlanAttentionTarget.Text, alert.Target);
        Assert.Equal("GF-Diagnostico", alert.Context.Layer);
        Assert.Equal(0.75, alert.Radius);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void RainStructures_MultipleConsecutiveTriplets_ReconstructEveryStructure()
    {
        var batch = Batch(
            RecordSpec.Block("BL", 0, 0, "Águas pluviais", "BOCA_LOBO"),
            RecordSpec.Block("BL", 2, 0, "Águas pluviais", "BOCA_LOBO"),
            RecordSpec.Block("BL", 2, 1, "Águas pluviais", "BOCA_LOBO"),
            RecordSpec.Block("BL", 10, 10, "Águas pluviais", "BOCA_LOBO"),
            RecordSpec.Block("BL", 12, 10, "Águas pluviais", "BOCA_LOBO"),
            RecordSpec.Block("BL", 12, 11, "Águas pluviais", "BOCA_LOBO"));

        var plan = new DrawingPlanBuilder().Build(batch);

        Assert.Equal(2, plan.RainStructures.Count);
        Assert.DoesNotContain(plan.CircleAlerts, item => item.Kind == PlanAlertKind.RainStructureReview);
        Assert.Contains(plan.Diagnostics, item => item.Code == "PLAN_RAIN_STRUCTURE_MULTIPLE_RECONSTRUCTED");
        Assert.All(plan.RainStructures, item => Assert.Equal(3, item.Context.Origins.Count));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void RainStructures_OutlierDoesNotPreventValidGeometryReconstruction()
    {
        var batch = Batch(
            RecordSpec.Block("GR", 50, 50, "Águas pluviais", "GRELHA"),
            RecordSpec.Block("GR", 0, 0, "Águas pluviais", "GRELHA"),
            RecordSpec.Block("GR", 2, 0, "Águas pluviais", "GRELHA"),
            RecordSpec.Block("GR", 2, 1, "Águas pluviais", "GRELHA"));

        var plan = new DrawingPlanBuilder().Build(batch);

        var structure = Assert.Single(plan.RainStructures);
        Assert.Equal(
            [new PlanPoint3(2, 0, 0), new PlanPoint3(0, 0, 0), new PlanPoint3(0, 1, 0), new PlanPoint3(2, 1, 0)],
            structure.Vertices);
        var alert = Assert.Single(plan.CircleAlerts, item => item.Kind == PlanAlertKind.RainStructureReview);
        Assert.Equal(new PlanPoint3(50, 50, 0), alert.Center);
        Assert.Contains(plan.Diagnostics, item => item.Code == "PLAN_RAIN_STRUCTURE_UNMATCHED_POINTS");
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void BoxStructures_AllCxRulesUseTheSameGeometricReconstruction()
    {
        var definitions = new[]
        {
            (Code: "CX1.20", Rule: "CX_PROF", Network: "Não identificada"),
            (Code: "CXAP1.20", Rule: "CXAP_PROF", Network: "Águas pluviais"),
            (Code: "CXEL1.20", Rule: "CXEL_PROF", Network: "Rede elétrica"),
            (Code: "CXAP", Rule: "CX_AP", Network: "Águas pluviais"),
            (Code: "CXEL", Rule: "CX_EL", Network: "Rede elétrica"),
            (Code: "CXTL", Rule: "CX_TEL", Network: "Telefone"),
            (Code: "CXEI", Rule: "CX_EI", Network: "Esgoto industrial"),
            (Code: "CXES", Rule: "CX_ES", Network: "Esgoto sanitário"),
            (Code: "CXAF", Rule: "CX_AF", Network: "Água potável"),
            (Code: "CXNI", Rule: "CX_NI", Network: "Não identificada"),
            (Code: "CX VALVULA", Rule: "CX_VALVULA_INC", Network: "Rede de incêndio"),
            (Code: "CX GORDURA", Rule: "CX_GORDURA", Network: "Esgoto sanitário"),
            (Code: "CXE", Rule: "CX_E", Network: "Esgoto sanitário"),
            (Code: "CXLAC", Rule: "CX_LAC", Network: "Outra"),
            (Code: "CX", Rule: "CX_GENERICA", Network: "Não identificada"),
            (Code: "CXCEGA", Rule: "CX_CEGA", Network: "Não identificada"),
        };
        var specs = definitions.SelectMany((definition, index) =>
        {
            var x = index * 10d;
            return new[]
            {
                RecordSpec.Block(definition.Code, x, 0, definition.Network, definition.Rule),
                RecordSpec.Block(definition.Code, x + 2, 0, definition.Network, definition.Rule),
                RecordSpec.Block(definition.Code, x + 2.1, 1, definition.Network, definition.Rule),
            };
        }).ToArray();

        var plan = new DrawingPlanBuilder().Build(Batch(specs));

        Assert.Equal(definitions.Length, plan.BoxStructures.Count);
        Assert.All(plan.BoxStructures, item => Assert.Equal(3, item.Context.Origins.Count));
        Assert.Contains(plan.BoxStructures, item => item.Context.Rule == "CX_AP" && item.Context.Layer == "GF-Rede Pluvial");
        Assert.Contains(plan.BoxStructures, item => item.Context.Rule == "CX_EL" && item.Context.Layer == "GF-Rede Eletrica");
        Assert.Contains(plan.BoxStructures, item => item.Context.Rule == "CX_AF" && item.Context.Layer == "GF-Rede Água");
        Assert.DoesNotContain(plan.CircleAlerts, item => item.Kind == PlanAlertKind.BoxStructureReview);
        Assert.DoesNotContain(plan.Diagnostics, item => item.Code == "PLAN_BLOCK_RULE_UNMAPPED");
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void TelephoneBox_DepthAtEachSurveyPoint_ReconstructsSingleTelephoneBox()
    {
        var plan = new DrawingPlanBuilder().Build(Batch(
            RecordSpec.TextAndBlock("CXTL0.80", 0, 0, "Telefone", "CXTL_PROF", 0.80),
            RecordSpec.TextAndBlock("CXTL0.82", 2, 0, "Telefone", "CXTL_PROF", 0.82),
            RecordSpec.TextAndBlock("CXTL0.79", 2.1, 1, "Telefone", "CXTL_PROF", 0.79)));

        var box = Assert.Single(plan.BoxStructures);
        Assert.Equal("CXTL_PROF", box.Context.Rule);
        Assert.Equal("GF-Rede Telefone", box.Context.Layer);
        Assert.Equal(3, box.Context.Origins.Count);
        Assert.Equal("CXTL", box.Code);
        Assert.Equal(3, plan.Texts.Count(item => item.Kind == TextPlanKind.Depth));
        Assert.Contains(plan.Texts, item => item.Content == "P.0.80");
        Assert.Contains(plan.Texts, item => item.Content == "P.0.82");
        Assert.Contains(plan.Texts, item => item.Content == "P.0.79");
        Assert.DoesNotContain(plan.CircleAlerts, item => item.Kind == PlanAlertKind.BoxStructureReview);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void BoxStructures_IncompleteCxRemainsVisibleForReview()
    {
        var plan = new DrawingPlanBuilder().Build(Batch(
            RecordSpec.Block("CXAF", 5, 7, "Água potável", "CX_AF")));

        Assert.Empty(plan.BoxStructures);
        var alert = Assert.Single(plan.CircleAlerts, item => item.Kind == PlanAlertKind.BoxStructureReview);
        Assert.Equal(new PlanPoint3(5, 7, 0), alert.Center);
        Assert.Equal("GF-Diagnostico", alert.Context.Layer);
        Assert.Contains(plan.Diagnostics, item => item.Code == "PLAN_BOX_STRUCTURE_INCOMPLETE");
    }

    [Fact]
    [Trait("Category", "FAST")]
    public async Task MultiCsv_BuildsOnePlanAndPreservesEverySourceOrigin()
    {
        var files = new[]
        {
            ReferenceFile("legacy_csv", "multi_csv", "levantamento_01.csv"),
            ReferenceFile("legacy_csv", "multi_csv", "levantamento_02.csv"),
        };
        var batch = await new ImportBatchService(new CsvSourceImporter(), new SpatialDuplicateDetector())
            .ImportAsync(CsvImportSources.FromFiles(files));

        var plan = new DrawingPlanBuilder().Build(batch);

        Assert.Equal(batch.Id, plan.SourceBatchId);
        Assert.NotEmpty(plan.CircleAlerts);
        var originNames = plan.Texts.SelectMany(item => item.Context.Origins)
            .Concat(plan.Blocks.SelectMany(item => item.Context.Origins))
            .Concat(plan.CircleAlerts.SelectMany(item => item.Context.Origins))
            .Concat(plan.SuppressedItems.SelectMany(item => item.Origins))
            .Concat(plan.Diagnostics.SelectMany(item => item.Origins))
            .Select(origin => origin.SourceFileName)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Equal(["levantamento_01.csv", "levantamento_02.csv"], originNames.Order(StringComparer.Ordinal));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public async Task HeterogeneousMultiCsv_DeduplicatesPreparedTextsAcrossSourceBoundary()
    {
        var first = Path.Combine(Path.GetTempPath(), $"stage3-a-{Guid.NewGuid():N}.csv");
        var second = Path.Combine(Path.GetTempPath(), $"stage3-b-{Guid.NewGuid():N}.csv");
        try
        {
            await File.WriteAllTextAsync(first, "Norte;Leste;Cota;Código\n1000;500;10;E1.20D150\n");
            await File.WriteAllTextAsync(second, "Coord Y|Coord X|Elevação|Descrição\n1000.2|500.2|10|E1.20D150\n");
            var batch = await new ImportBatchService(new CsvSourceImporter(), new SpatialDuplicateDetector())
                .ImportAsync(CsvImportSources.FromFiles([first, second]));

            var plan = new DrawingPlanBuilder().Build(batch);

            Assert.Equal(2, plan.Texts.Count);
            Assert.Equal(2, plan.SuppressedItems.Count(item => item.Kind == DrawingItemKind.Text));
            Assert.All(plan.SuppressedItems, item => Assert.Contains(item.Origins, origin => origin.SourceFileName == Path.GetFileName(second)));
            Assert.All(plan.SuppressedItems, item => Assert.NotNull(item.RetainedItemId));
        }
        finally
        {
            File.Delete(first);
            File.Delete(second);
        }
    }

    [Fact]
    [Trait("Category", "FAST")]
    public async Task Stage4CadSmokeFixture_ProducesFourFixedAndThreeGeometryPendingItems()
    {
        var batch = await new ImportBatchService(new CsvSourceImporter(), new SpatialDuplicateDetector())
            .ImportAsync(CsvImportSources.FromFiles([
                ReferenceFile("..", "GeoAutoEngine3", "samples", "stage4_cad_smoke_day1.csv"),
                ReferenceFile("..", "GeoAutoEngine3", "samples", "stage4_cad_smoke_day2.csv"),
            ]));

        var plan = new DrawingPlanBuilder().Build(batch);

        Assert.Equal(3, plan.Texts.Count);
        Assert.Equal(2, plan.Blocks.Count);
        Assert.Single(plan.RainStructures);
        Assert.Single(plan.CircleAlerts);
        Assert.Equal(2, batch.SourceFiles.Count);
        Assert.Equal(7, plan.XData.Count);
        Assert.Equal(3, plan.Texts.Count(item => item.RotationMode == PlanRotationMode.FollowNetwork) +
            plan.Blocks.Count(item => item.RotationMode == PlanRotationMode.FollowNetwork));
        Assert.Equal(4, plan.Texts.Count(item => item.RotationMode == PlanRotationMode.Fixed) +
            plan.Blocks.Count(item => item.RotationMode == PlanRotationMode.Fixed) +
            plan.RainStructures.Count + plan.CircleAlerts.Count);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public async Task ReimportedSources_ProduceIdenticalPlanIdentityItemsAndCanonicalJson()
    {
        var source = CsvImportSources.FromSingleFile(ReferenceFile("legacy_csv", "real", "RUA BOEHMERWALD 1407(1).csv"));
        var service = new ImportBatchService(new CsvSourceImporter(), new SpatialDuplicateDetector());
        var firstBatch = await service.ImportAsync(source);
        var secondBatch = await service.ImportAsync(source);
        var builder = new DrawingPlanBuilder();

        var first = builder.Build(firstBatch);
        var second = builder.Build(secondBatch);

        Assert.NotEqual(first.SourceBatchId, second.SourceBatchId);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.ToCanonicalJson(), second.ToCanonicalJson());
        Assert.Equal(first.XData.Select(item => item.TargetItemId), second.XData.Select(item => item.TargetItemId));
    }

    [Theory]
    [Trait("Category", "REGRESSION")]
    [InlineData("RUA BOEHMERWALD 1407(1).csv", 72, 6, 3, 0, 19, 0)]
    [InlineData("Rua S#U00e3o Francisco 1407.csv", 93, 4, 3, 9, 8, 0)]
    public async Task RealFixture_PreservesPreparationAndReconstructsBoxes(
        string fileName,
        int texts,
        int blocks,
        int rainStructures,
        int boxStructures,
        int unknownAlerts,
        int unmappedBlocks)
    {
        var batch = await new ImportBatchService(new CsvSourceImporter(), new SpatialDuplicateDetector())
            .ImportAsync(CsvImportSources.FromSingleFile(ReferenceFile("legacy_csv", "real", fileName)));

        var plan = new DrawingPlanBuilder().Build(batch);

        Assert.Equal(texts, plan.Texts.Count);
        Assert.Equal(blocks, plan.Blocks.Count);
        Assert.Equal(rainStructures, plan.RainStructures.Count);
        Assert.Equal(boxStructures, plan.BoxStructures.Count);
        Assert.Equal(unknownAlerts, plan.CircleAlerts.Count(item => item.Kind == PlanAlertKind.UnknownCode));
        Assert.Equal(unmappedBlocks, plan.Diagnostics.Count(item => item.Code == "PLAN_BLOCK_RULE_UNMAPPED"));
        Assert.Equal(
            plan.Texts.Count + plan.Blocks.Count + plan.RainStructures.Count + plan.BoxStructures.Count + plan.CircleAlerts.Count,
            plan.XData.Count);
    }

    [Fact]
    [Trait("Category", "REGRESSION")]
    public async Task RealFixtures_PreserveUnaffectedCadIndependentLegacyFields()
    {
        using var oracle = JsonDocument.Parse(File.ReadAllText(
            ReferenceFile("regression_data", "stage3_drawing_oracle.json")));
        foreach (var fixture in oracle.RootElement.GetProperty("fixtures").EnumerateArray())
        {
            var fileName = fixture.GetProperty("file_name").GetString()!;
            var batch = await new ImportBatchService(new CsvSourceImporter(), new SpatialDuplicateDetector())
                .ImportAsync(CsvImportSources.FromSingleFile(ReferenceFile("legacy_csv", "real", fileName)));
            var plan = new DrawingPlanBuilder().Build(batch);
            var actual = JsonSerializer.SerializeToElement(new
            {
                texts = plan.Texts.Select(item => new
                {
                    kind = item.Kind.ToString(),
                    content = item.Content,
                    layer = item.Context.Layer,
                    anchor = new[] { item.Anchor.X, item.Anchor.Y, item.Anchor.Z },
                    height = item.Height,
                    style = item.StyleName,
                    rotation_mode = item.RotationMode.ToString(),
                    perpendicular_offset = item.PerpendicularOffset,
                    curve_layers = item.ReferenceCurveLayers,
                }),
                blocks = plan.Blocks.Select(item => new
                {
                    rule = item.Context.Rule,
                    code = item.Code,
                    network = item.Network,
                    key = item.BlockKey,
                    name = item.BlockName,
                    asset = item.AssetFileName,
                    layer = item.Context.Layer,
                    position = new[] { item.Position.X, item.Position.Y, item.Position.Z },
                    scale = new[] { item.Scale.X, item.Scale.Y, item.Scale.Z },
                    rotation_mode = item.RotationMode.ToString(),
                    fixed_rotation = item.FixedRotationRadians,
                    curve_layers = item.ReferenceCurveLayers,
                    alignment = item.AlignmentMode.ToString(),
                }),
                rain_structures = plan.RainStructures.Select(item => new
                {
                    kind = item.Kind.ToString(),
                    code = item.Code,
                    layer = item.Context.Layer,
                    vertices = item.Vertices.Select(point => new[] { point.X, point.Y, point.Z }),
                    center = new[] { item.Center.X, item.Center.Y, item.Center.Z },
                }),
                alerts = plan.CircleAlerts.Select(item => new
                {
                    kind = item.Kind.ToString(),
                    code = item.Code,
                    layer = item.Context.Layer,
                    center = new[] { item.Center.X, item.Center.Y, item.Center.Z },
                    radius = item.Radius,
                    color = item.ColorIndex,
                    related_records = item.RelatedRecordCount,
                }),
            });

            AssertCanonicalItems(fixture, actual, "texts");
            AssertCanonicalItems(fixture, actual, "blocks");
            AssertCanonicalItems(fixture, actual, "rain_structures");
            AssertCanonicalItems(fixture, actual, "alerts", new HashSet<string>(["CXAP LAC", "CXTL"], StringComparer.Ordinal));
            var expectedSuppressions = fixture.GetProperty("block_suppressions");
            Assert.Equal(
                expectedSuppressions.GetProperty("unmapped_rules").EnumerateObject()
                    .Where(property => !property.Name.StartsWith("CX", StringComparison.Ordinal))
                    .Sum(property => property.Value.GetInt32()),
                plan.Diagnostics.Count(item => item.Code == "PLAN_BLOCK_RULE_UNMAPPED"));
            Assert.Equal(
                SumObjectValues(expectedSuppressions.GetProperty("exact_duplicates")),
                plan.Diagnostics.Count(item => item.Code == "PLAN_BLOCK_EXACT_DUPLICATE"));
            Assert.Equal(
                SumObjectValues(expectedSuppressions.GetProperty("proximity_duplicates")),
                plan.Diagnostics.Count(item => item.Code == "PLAN_BLOCK_PROXIMITY_DUPLICATE"));
        }
    }

    [Fact]
    [Trait("Category", "REGRESSION")]
    public void TenThousandRecords_BuildDrawingPlanStaysWithinCheckpointBudget()
    {
        const int count = 10_000;
        var specs = Enumerable.Range(0, count)
            .Select(index => RecordSpec.Text($"AF{index + 1}.00D100", index * 2, 0, "Água potável", "REDE_AF", index + 1, 100))
            .ToArray();
        var batch = Batch(specs);
        var stopwatch = Stopwatch.StartNew();

        var plan = new DrawingPlanBuilder().Build(batch);
        stopwatch.Stop();

        Assert.Equal(count * 2, plan.Texts.Count);
        _output.WriteLine("BuildDrawingPlan 10k: {0:F3} ms", stopwatch.Elapsed.TotalMilliseconds);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"BuildDrawingPlan levou {stopwatch.Elapsed}.");
    }

    private static ImportBatch Batch(params RecordSpec[] specs)
    {
        var source = TestData.Source(recordCount: specs.Length, name: "stage3.csv");
        var records = specs.Select((spec, index) => new SurveyRecord(
            SurveyRecordId.From(source.Id, index),
            TestData.Origin(source, index, batch: index),
            new Dictionary<string, string?> { ["Código"] = spec.Code },
            new SurveyPoint(spec.X, spec.Y, 10),
            spec.Code,
            new SurveyInterpretation(
                spec.Network,
                spec.Rule,
                null,
                spec.Diameter,
                spec.Depth,
                "Teste",
                spec.Type,
                spec.Recognized,
                "alta",
                spec.Rule,
                complement: spec.Complement,
                originalCode: spec.Code,
                normalizedCode: spec.Code))).ToArray();
        return ImportBatch.Create(BatchId.New(), [source], records, [], [], []);
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

    private static void AssertCanonicalItems(
        JsonElement expectedRoot,
        JsonElement actualRoot,
        string propertyName,
        IReadOnlySet<string>? excludedCodes = null)
    {
        static bool Included(JsonElement item, IReadOnlySet<string>? exclusions) =>
            exclusions is null || !item.TryGetProperty("code", out var code) || !exclusions.Contains(code.GetString() ?? string.Empty);

        var expected = expectedRoot.GetProperty(propertyName).EnumerateArray()
            .Where(item => Included(item, excludedCodes))
            .Select(Canonical)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var actual = actualRoot.GetProperty(propertyName).EnumerateArray()
            .Where(item => Included(item, excludedCodes))
            .Select(Canonical)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expected.Length, actual.Length);
        for (var index = 0; index < expected.Length; index++)
        {
            Assert.True(
                string.Equals(expected[index], actual[index], StringComparison.Ordinal),
                $"Divergência em {propertyName}[{index}].{Environment.NewLine}Esperado: {expected[index]}{Environment.NewLine}Atual: {actual[index]}");
        }
    }

    private static string Canonical(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => "{" + string.Join(",", element.EnumerateObject()
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .Select(property => $"{JsonSerializer.Serialize(property.Name)}:{Canonical(property.Value)}")) + "}",
        JsonValueKind.Array => "[" + string.Join(",", element.EnumerateArray().Select(Canonical)) + "]",
        JsonValueKind.String => JsonSerializer.Serialize(element.GetString()),
        JsonValueKind.Number => Math.Round(element.GetDouble(), 8).ToString("R", CultureInfo.InvariantCulture),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null => "null",
        _ => throw new InvalidOperationException($"Unsupported JSON kind: {element.ValueKind}."),
    };

    private static int SumObjectValues(JsonElement element) =>
        element.EnumerateObject().Sum(property => property.Value.GetInt32());

    private sealed record RecordSpec(
        string Code,
        double X,
        double Y,
        string? Network,
        string Rule,
        string Type,
        bool Recognized,
        double? Depth = null,
        double? Diameter = null,
        string? Complement = null)
    {
        public static RecordSpec Text(string code, double x, double y, string network, string rule, double depth, double? diameter = null) =>
            new(code, x, y, network, rule, "Texto", true, depth, diameter);

        public static RecordSpec TextAndBlock(string code, double x, double y, string network, string rule, double depth, double? diameter = null) =>
            new(code, x, y, network, rule, "Texto + Bloco", true, depth, diameter);

        public static RecordSpec Block(string code, double x, double y, string network, string rule) =>
            new(code, x, y, network, rule, "Bloco", true);

        public static RecordSpec Boundary(string code, double x, double y, string complement) =>
            new(code, x, y, "Outra", "ANOTACAO_DIVISA", "Texto", true, Complement: complement);

        public static RecordSpec Unknown(string code, double x, double y) =>
            new(code, x, y, "Não identificada", "DESCONHECIDO", "Revisar", false);
    }
}
