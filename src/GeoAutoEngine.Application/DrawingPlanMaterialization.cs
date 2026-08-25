using GeoAutoEngine.Domain;

namespace GeoAutoEngine.Application;

public enum DrawingPlanProcessingMode
{
    Complete,
    BlocksOnly,
    TextsOnly,
}

public sealed class DrawingPlanMaterializationSelection
{
    private DrawingPlanMaterializationSelection(
        IReadOnlyList<TextPlanItem> texts,
        IReadOnlyList<BlockPlanItem> blocks,
        IReadOnlyList<RainStructurePlanItem> rainStructures,
        IReadOnlyList<BoxStructurePlanItem> boxStructures,
        IReadOnlyList<CircleAlertPlanItem> circleAlerts,
        IReadOnlyList<XDataPlanItem> xdata)
    {
        Texts = texts;
        Blocks = blocks;
        RainStructures = rainStructures;
        BoxStructures = boxStructures;
        CircleAlerts = circleAlerts;
        XData = xdata;
    }

    public IReadOnlyList<TextPlanItem> Texts { get; }

    public IReadOnlyList<BlockPlanItem> Blocks { get; }

    public IReadOnlyList<RainStructurePlanItem> RainStructures { get; }

    public IReadOnlyList<BoxStructurePlanItem> BoxStructures { get; }

    public IReadOnlyList<CircleAlertPlanItem> CircleAlerts { get; }

    public IReadOnlyList<XDataPlanItem> XData { get; }

    public static DrawingPlanMaterializationSelection Create(
        DrawingPlan plan,
        DrawingPlanProcessingMode mode)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!Enum.IsDefined(typeof(DrawingPlanProcessingMode), mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        var texts = mode is DrawingPlanProcessingMode.Complete or DrawingPlanProcessingMode.TextsOnly
            ? plan.Texts
            : [];
        var blocks = mode is DrawingPlanProcessingMode.Complete or DrawingPlanProcessingMode.BlocksOnly
            ? plan.Blocks
            : [];
        var structures = mode is DrawingPlanProcessingMode.Complete or DrawingPlanProcessingMode.BlocksOnly
            ? plan.RainStructures
            : [];
        var boxes = mode is DrawingPlanProcessingMode.Complete or DrawingPlanProcessingMode.BlocksOnly
            ? plan.BoxStructures
            : [];
        var alerts = mode switch
        {
            DrawingPlanProcessingMode.Complete => plan.CircleAlerts,
            DrawingPlanProcessingMode.BlocksOnly => plan.CircleAlerts
                .Where(item => item.Target == PlanAttentionTarget.Block)
                .ToArray(),
            DrawingPlanProcessingMode.TextsOnly => plan.CircleAlerts
                .Where(item => item.Target == PlanAttentionTarget.Text)
                .ToArray(),
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };
        var activeIds = new HashSet<PlanItemId>(
            texts.Select(item => item.Context.Id)
                .Concat(blocks.Select(item => item.Context.Id))
                .Concat(structures.Select(item => item.Context.Id))
                .Concat(boxes.Select(item => item.Context.Id))
                .Concat(alerts.Select(item => item.Context.Id)));
        var xdata = plan.XData.Where(item => activeIds.Contains(item.TargetItemId)).ToArray();

        return new DrawingPlanMaterializationSelection(texts, blocks, structures, boxes, alerts, xdata);
    }
}
