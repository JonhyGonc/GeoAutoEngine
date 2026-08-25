namespace GeoAutoEngine.Domain.AnnotationLayout;

public enum LayoutRealityRole
{
    Ignored,
    ExistingText,
    Block,
    CivilSymbol,
}

public sealed record LayoutRealityClassification(LayoutRealityRole Role, string Reason)
{
    public bool IsObstacle => Role != LayoutRealityRole.Ignored;

    public LayoutObstacleKind? ObstacleKind => Role switch
    {
        LayoutRealityRole.ExistingText => LayoutObstacleKind.ExistingText,
        LayoutRealityRole.Block => LayoutObstacleKind.Block,
        LayoutRealityRole.CivilSymbol => LayoutObstacleKind.Other,
        _ => null,
    };
}

public static class LayoutRealityPolicy
{
    public static LayoutRealityClassification Classify(
        string entityType,
        bool isExternalReference = false,
        bool isAnnotatedBlock = false,
        bool isImportantInfrastructureBlock = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
        var type = entityType.ToUpperInvariant();
        if (type is "ACDBTEXT" or "DBTEXT" or "ACDBMTEXT" or "MTEXT" or
            "ACDBATTRIBUTE" or "ATTRIBUTEREFERENCE")
        {
            return new LayoutRealityClassification(LayoutRealityRole.ExistingText, "TextGeometricExtents");
        }

        if (type is "ACDBBLOCKREFERENCE" or "BLOCKREFERENCE")
        {
            if (isExternalReference)
            {
                return new LayoutRealityClassification(LayoutRealityRole.Ignored, "XrefNotCapturedAsSingleObstacle");
            }

            if (isImportantInfrastructureBlock)
            {
                return new LayoutRealityClassification(LayoutRealityRole.Block, "RecognizedInfrastructureBlock");
            }

            return isAnnotatedBlock
                ? new LayoutRealityClassification(LayoutRealityRole.Block, "AnnotatedBlockGeometricExtents")
                : new LayoutRealityClassification(LayoutRealityRole.Ignored, "BlockWithoutRecognizedVisualAnnotation");
        }

        if (type.StartsWith("AECC", StringComparison.Ordinal) || type.StartsWith("AEC", StringComparison.Ordinal))
        {
            if (type.Contains("LABEL", StringComparison.Ordinal) ||
                type.Contains("TEXT", StringComparison.Ordinal) ||
                type.Contains("ANNOTATION", StringComparison.Ordinal))
            {
                return new LayoutRealityClassification(LayoutRealityRole.ExistingText, "RecognizedCivilLabel");
            }

            if (type.Contains("COGOPOINT", StringComparison.Ordinal))
            {
                return new LayoutRealityClassification(LayoutRealityRole.CivilSymbol, "RecognizedCogoPoint");
            }

            return new LayoutRealityClassification(LayoutRealityRole.Ignored, "UnsupportedCivilEntityType");
        }

        return type.Contains("PROXY", StringComparison.Ordinal)
            ? new LayoutRealityClassification(LayoutRealityRole.Ignored, "UnsupportedProxyEntity")
            : new LayoutRealityClassification(LayoutRealityRole.Ignored, "EntityTypeNotUsedForVisualCollision");
    }
}
