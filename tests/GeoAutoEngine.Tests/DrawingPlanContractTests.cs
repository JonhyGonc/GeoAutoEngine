using GeoAutoEngine.Domain;

namespace GeoAutoEngine.Tests;

public sealed class DrawingPlanContractTests
{
    [Fact]
    [Trait("Category", "FAST")]
    public void CanonicalJson_IsIndependentOfInputItemAndEnvelopeOrder()
    {
        var source = TestData.Source(recordCount: 2);
        var first = Block(Context(source, 0, "first"), 1);
        var second = Block(Context(source, 1, "second"), 2);
        var firstPlan = Plan([second, first]);
        var secondPlan = Plan([first, second]);

        Assert.Equal(firstPlan.ToCanonicalJson(), secondPlan.ToCanonicalJson());
        Assert.Contains("\"schemaVersion\":\"3.1\"", firstPlan.ToCanonicalJson(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void DeterministicIdentifiers_AreStableAndDiscriminatorSensitive()
    {
        var first = PlanItemId.FromStableKey("source:0:text:depth");
        var same = PlanItemId.FromStableKey("source:0:text:depth");
        var different = PlanItemId.FromStableKey("source:0:text:diameter");

        Assert.Equal(first, same);
        Assert.NotEqual(first, different);
    }

    [Fact]
    [Trait("Category", "REGRESSION")]
    public void DrawingPlan_RejectsDuplicateActiveItemIdentifiers()
    {
        var source = TestData.Source();
        var context = Context(source, 0, "same");
        var text = new TextPlanItem(
            context, TextPlanKind.Depth, new PlanPoint3(1, 2, 0), PlanRotationMode.FollowNetwork,
            0, 0.5, 0.1, "P.1.00", "GEO_ARIAL", null, ["REDE"]);
        var block = Block(context, 1);

        Assert.Throws<ArgumentException>(() => new DrawingPlan(
            DrawingPlanId.New(), BatchId.New(), [], [], [text], [block], [], [], [], [], [], [], []));
    }

    private static DrawingItemContext Context(SourceFile source, int index, string discriminator)
    {
        var key = $"{source.Id}:{index}:{discriminator}";
        return new DrawingItemContext(
            PlanItemId.FromStableKey(key), key, "GEO", "PV_ES", [TestData.Origin(source, index, batch: index)]);
    }

    private static BlockPlanItem Block(DrawingItemContext context, double x) => new(
        context,
        new PlanPoint3(x, 2, 0),
        new PlanScale3(1, 1, 1),
        PlanRotationMode.Fixed,
        0,
        "PV",
        "PVES",
        "Esgoto sanitário",
        "PV_ESGOTO_SANITARIO",
        "pv_esgoto_sanitario",
        "pv_esgoto_sanitario.dwg",
        BlockAlignmentMode.InsertionPoint,
        ["REDE_ESGOTO"]);

    private static DrawingPlan Plan(IEnumerable<BlockPlanItem> blocks) => new(
        DrawingPlanId.New(),
        BatchId.New(),
        [new LayerPlanDefinition("GEO")],
        [],
        [],
        blocks,
        [],
        [],
        [],
        [],
        [],
        [],
        []);
}
