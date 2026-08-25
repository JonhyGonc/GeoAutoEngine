using GeoAutoEngine.Application;
using GeoAutoEngine.Csv;
using GeoAutoEngine.Domain;

namespace GeoAutoEngine.Tests;

public sealed class DrawingPlanMaterializationTests
{
    [Theory]
    [Trait("Category", "FAST")]
    [InlineData(DrawingPlanProcessingMode.Complete, 3, 2, 1, 1, 2)]
    [InlineData(DrawingPlanProcessingMode.BlocksOnly, 0, 2, 1, 1, 1)]
    [InlineData(DrawingPlanProcessingMode.TextsOnly, 3, 0, 0, 0, 1)]
    public async Task ModeSelectsOnlyMaterializationWithoutChangingTheCanonicalPlan(
        DrawingPlanProcessingMode mode,
        int texts,
        int blocks,
        int structures,
        int boxes,
        int alerts)
    {
        var plan = await BuildSmokePlan();
        var canonicalBefore = plan.ToCanonicalJson();

        var selection = DrawingPlanMaterializationSelection.Create(plan, mode);

        Assert.Equal(texts, selection.Texts.Count);
        Assert.Equal(blocks, selection.Blocks.Count);
        Assert.Equal(structures, selection.RainStructures.Count);
        Assert.Equal(boxes, selection.BoxStructures.Count);
        Assert.Equal(alerts, selection.CircleAlerts.Count);
        Assert.Equal(texts + blocks + structures + boxes + alerts, selection.XData.Count);
        Assert.Equal(canonicalBefore, plan.ToCanonicalJson());
    }

    [Fact]
    [Trait("Category", "FAST")]
    public async Task SelectiveItemsAreTheExactObjectsFromTheCompletePlan()
    {
        var plan = await BuildSmokePlan();
        var blocks = DrawingPlanMaterializationSelection.Create(plan, DrawingPlanProcessingMode.BlocksOnly);
        var texts = DrawingPlanMaterializationSelection.Create(plan, DrawingPlanProcessingMode.TextsOnly);

        Assert.Equal(plan.Blocks, blocks.Blocks);
        Assert.Equal(plan.RainStructures, blocks.RainStructures);
        Assert.Equal(plan.BoxStructures, blocks.BoxStructures);
        Assert.Equal(plan.Texts, texts.Texts);
        Assert.All(blocks.CircleAlerts, item => Assert.Equal(PlanAttentionTarget.Block, item.Target));
        Assert.All(texts.CircleAlerts, item => Assert.Equal(PlanAttentionTarget.Text, item.Target));
        Assert.Contains(blocks.CircleAlerts, item => item.Kind == PlanAlertKind.RainStructureReview);
        Assert.Contains(texts.CircleAlerts, item => item.Kind == PlanAlertKind.UnknownCode);
        Assert.All(texts.Texts, item => Assert.Equal(1.0, item.Height, precision: 10));
        Assert.Contains(texts.Texts, item => item.RotationMode == PlanRotationMode.FollowNetwork);
    }

    private static async Task<DrawingPlan> BuildSmokePlan()
    {
        var repository = FindRepositoryRoot();
        var attentionFile = Path.Combine(Path.GetTempPath(), $"geoauto-attention-{Guid.NewGuid():N}.csv");
        var files = new[]
        {
            Path.Combine(repository, "samples", "stage4_cad_smoke_day1.csv"),
            Path.Combine(repository, "samples", "stage4_cad_smoke_day2.csv"),
            attentionFile,
        };
        try
        {
            await File.WriteAllTextAsync(
                attentionFile,
                "Norte;Leste;Cota;Código\n" +
                "2000;1000;10;GR\n" +
                "2010;1010;10;CXAF\n" +
                "2010;1012;10;CXAF\n" +
                "2011;1012;10;CXAF\n");
            var batch = await new ImportBatchService(new CsvSourceImporter(), new SpatialDuplicateDetector())
                .ImportAsync(CsvImportSources.FromFiles(files));
            return new DrawingPlanBuilder(new DrawingPlanBuildOptions(textHeight: 1.0)).Build(batch);
        }
        finally
        {
            File.Delete(attentionFile);
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GeoAutoEngine.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate the GeoAutoEngine repository root.");
    }
}
