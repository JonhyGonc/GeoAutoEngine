using GeoAutoEngine.Application;
using GeoAutoEngine.Domain;

namespace GeoAutoEngine.Tests;

public sealed class DrawingPresentationSettingsTests
{
    [Theory]
    [Trait("Category", "FAST")]
    [InlineData(5, 0.01)]
    [InlineData(10, 0.02)]
    [InlineData(20, 0.04)]
    [InlineData(25, 0.05)]
    [InlineData(50, 0.10)]
    [InlineData(75, 0.15)]
    [InlineData(100, 0.20)]
    [InlineData(125, 0.25)]
    [InlineData(200, 0.40)]
    [InlineData(250, 0.50)]
    [InlineData(500, 1.00)]
    [InlineData(750, 1.50)]
    [InlineData(1000, 2.00)]
    public void PrintedHeight_IsConvertedMathematicallyToModelSpace(int denominator, double expectedMeters)
    {
        var settings = new DrawingPresentationSettings(denominator, 2.0);

        Assert.Equal(expectedMeters, settings.TextHeightMeters, precision: 10);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void RestoreDefaults_ReestablishesTheApprovedPresentation()
    {
        var changed = new DrawingPresentationSettings(500, 3.0, BlockScaleMode.Automatic, showRevisionMarks: false);

        var restored = changed.RestoreDefaults();

        Assert.Equal(250, restored.ScaleDenominator);
        Assert.Equal(2.0, restored.PrintedTextHeightMillimeters);
        Assert.Equal(0.50, restored.TextHeightMeters, precision: 10);
        Assert.Equal(BlockScaleMode.Automatic, restored.BlockScaleMode);
        Assert.True(restored.ShowRevisionMarks);
        Assert.True(restored.AddStandardSheet);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void LocalStore_PersistsAndReloadsPresentationSettings()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"geoauto-presentation-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "settings.json");
        try
        {
            var expected = new DrawingPresentationSettings(
                500,
                2.5,
                BlockScaleMode.Automatic,
                showRevisionMarks: false,
                addStandardSheet: false);
            var store = new DrawingPresentationSettingsStore(path);

            store.Save(expected);

            Assert.Equal(expected, store.Load());
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            if (Directory.Exists(directory))
            {
                Directory.Delete(directory);
            }
        }
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void ExistingSettingsWithoutSheetOption_AreMigratedToEnabled()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"geoauto-presentation-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "settings.json");
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, """
                {
                  "ScaleDenominator": 500,
                  "PrintedTextHeightMillimeters": 2.0,
                  "BlockScaleMode": 0,
                  "ShowRevisionMarks": true
                }
                """);

            var loaded = new DrawingPresentationSettingsStore(path).Load();

            Assert.Equal(500, loaded.ScaleDenominator);
            Assert.True(loaded.AddStandardSheet);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void DrawingPlan_UsesConfiguredTextHeightAndRevisionVisibility()
    {
        var batch = PresentationBatch();
        var visible = new DrawingPlanBuilder(new DrawingPlanBuildOptions(textHeight: 1.0, showRevisionMarks: true)).Build(batch);
        var hidden = new DrawingPlanBuilder(new DrawingPlanBuildOptions(textHeight: 1.0, showRevisionMarks: false)).Build(batch);

        Assert.All(visible.Texts, text => Assert.Equal(1.0, text.Height, precision: 10));
        Assert.Single(visible.CircleAlerts);
        Assert.Empty(hidden.CircleAlerts);
        Assert.Equal(
            visible.Diagnostics.Select(item => (item.Code, item.Message, item.Severity)),
            hidden.Diagnostics.Select(item => (item.Code, item.Message, item.Severity)));
        Assert.DoesNotContain(hidden.XData, item => item.Values.TryGetValue("TIPO", out var type) && type == "ATENCAO");
    }

    private static ImportBatch PresentationBatch()
    {
        var source = TestData.Source(recordCount: 2);
        var textOrigin = TestData.Origin(source, 0, batch: 0);
        var unknownOrigin = TestData.Origin(source, 1, batch: 1);
        var text = new SurveyRecord(
            SurveyRecordId.From(source.Id, 0),
            textOrigin,
            new Dictionary<string, string?>(),
            new SurveyPoint(500, 1000, 0),
            "E1.20",
            new SurveyInterpretation(
                "Esgoto sanitário",
                "Rede",
                null,
                null,
                1.2,
                recordType: "Texto",
                rule: "REDE_E",
                originalCode: "E1.20",
                normalizedCode: "E1.20"));
        var unknown = new SurveyRecord(
            SurveyRecordId.From(source.Id, 1),
            unknownOrigin,
            new Dictionary<string, string?>(),
            new SurveyPoint(510, 1010, 0),
            "XXXX",
            new SurveyInterpretation(
                "Não identificada",
                "Revisar",
                null,
                null,
                null,
                recordType: "Revisar",
                recognized: false,
                rule: "DESCONHECIDO",
                originalCode: "XXXX",
                normalizedCode: "XXXX"));
        var diagnostic = new Diagnostic(
            "CSV_UNKNOWN_CODE",
            "Código desconhecido: XXXX",
            DiagnosticSeverity.Warning,
            DiagnosticScope.Record,
            source.Id,
            [unknownOrigin]);
        return ImportBatch.Create(BatchId.New(), [source], [text, unknown], [], [diagnostic], []);
    }
}
