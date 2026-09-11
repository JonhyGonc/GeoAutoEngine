namespace GeoAutoEngine.Application.VideoInspection;

public sealed record InspectionCalloutScaleProfile(
    int ScaleDenominator,
    double TextHeight,
    double BaseCalloutLength,
    double CalloutLevelStep,
    double MaxCalloutLength,
    double TextClearance,
    double IntervalJunctionOffset,
    double LateralSpacing,
    double TextGap,
    double MarkerRadius)
{
    private static readonly int[] SupportedDenominators = [125, 200, 250, 500, 750];

    public static InspectionCalloutScaleProfile Legacy { get; } = new(
        250,
        InspectionCalloutLayoutEngine.TextHeight,
        InspectionCalloutLayoutEngine.BaseCalloutLength,
        InspectionCalloutLayoutEngine.CalloutLevelStep,
        InspectionCalloutLayoutEngine.MaxCalloutLength,
        InspectionCalloutLayoutEngine.TextClearance,
        InspectionCalloutLayoutEngine.IntervalJunctionOffset,
        InspectionCalloutLayoutEngine.LateralSpacing,
        InspectionCalloutLayoutEngine.TextGap,
        InspectionCalloutLayoutEngine.MarkerRadius);

    public static InspectionCalloutScaleProfile Default { get; } = Create(250, 0.50, 2.00);

    public static IReadOnlyList<int> SupportedScaleDenominators { get; } =
        Array.AsReadOnly(SupportedDenominators);

    public static bool IsSupported(int denominator) =>
        Array.IndexOf(SupportedDenominators, denominator) >= 0;

    public static InspectionCalloutScaleProfile Resolve(int denominator) => denominator switch
    {
        125 => Create(125, 0.25, 1.00),
        200 => Create(200, 0.40, 1.60),
        250 => Default,
        500 => Create(500, 1.00, 4.00),
        750 => Create(750, 1.50, 6.00),
        _ => Default,
    };

    private static InspectionCalloutScaleProfile Create(
        int denominator,
        double textHeight,
        double baseCalloutLength)
    {
        var lineFactor = baseCalloutLength / InspectionCalloutLayoutEngine.BaseCalloutLength;
        var textFactor = textHeight / InspectionCalloutLayoutEngine.TextHeight;
        var proportionalMaximum = InspectionCalloutLayoutEngine.MaxCalloutLength * lineFactor;
        var markerRadius = Math.Min(
            InspectionCalloutLayoutEngine.MarkerRadius,
            InspectionCalloutLayoutEngine.MarkerRadius * denominator / 250.0);
        return new InspectionCalloutScaleProfile(
            denominator,
            textHeight,
            baseCalloutLength,
            InspectionCalloutLayoutEngine.CalloutLevelStep * lineFactor,
            Math.Min(InspectionCalloutLayoutEngine.MaxCalloutLength, proportionalMaximum),
            InspectionCalloutLayoutEngine.TextClearance * textFactor,
            InspectionCalloutLayoutEngine.IntervalJunctionOffset * lineFactor,
            InspectionCalloutLayoutEngine.LateralSpacing * lineFactor,
            InspectionCalloutLayoutEngine.TextGap * textFactor,
            markerRadius);
    }
}
