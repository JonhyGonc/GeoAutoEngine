namespace GeoAutoEngine.Domain.AnnotationLayout;

public static class AnnotationGroupInvariantValidator
{
    public static AnnotationGroupInvariantResult Validate(
        LayoutPoint2 anchor,
        IReadOnlyList<LayoutTextPlacement> legacy,
        IReadOnlyList<LayoutTextPlacement> candidate)
    {
        ArgumentNullException.ThrowIfNull(legacy);
        ArgumentNullException.ThrowIfNull(candidate);
        if (legacy.Count == 0)
        {
            throw new ArgumentException("The Legacy annotation group cannot be empty.", nameof(legacy));
        }

        var violations = AnnotationGroupInvariantViolation.None;
        var details = new List<string>();
        var legacyIds = legacy.Select(item => item.ItemId).ToArray();
        var candidateIds = candidate.Select(item => item.ItemId).ToArray();
        if (candidate.Count != legacy.Count ||
            legacyIds.Distinct(StringComparer.Ordinal).Count() != legacyIds.Length ||
            candidateIds.Distinct(StringComparer.Ordinal).Count() != candidateIds.Length ||
            !candidateIds.OrderBy(value => value, StringComparer.Ordinal)
                .SequenceEqual(legacyIds.OrderBy(value => value, StringComparer.Ordinal), StringComparer.Ordinal))
        {
            violations |= AnnotationGroupInvariantViolation.GroupIntegrity;
            details.Add("GroupIntegrityViolation: members or roles differ from Legacy");
            return new AnnotationGroupInvariantResult(violations, details);
        }

        var legacyById = legacy.ToDictionary(item => item.ItemId, StringComparer.Ordinal);
        if (candidate.Any(item => legacyById[item.ItemId].Role != item.Role))
        {
            violations |= AnnotationGroupInvariantViolation.GroupIntegrity;
            details.Add("GroupIntegrityViolation: members or roles differ from Legacy");
            return new AnnotationGroupInvariantResult(violations, details);
        }

        var baselineRotation = legacy[0].RotationRadians;
        if (legacy.Any(item => AngularDifference(item.RotationRadians, baselineRotation) > LayoutGeometryTolerance.Angle) ||
            candidate.Any(item => AngularDifference(item.RotationRadians, legacyById[item.ItemId].RotationRadians) >
                LayoutGeometryTolerance.Angle))
        {
            violations |= AnnotationGroupInvariantViolation.Rotation;
            details.Add("RotationInvariantViolation: group rotation differs from Legacy");
        }

        var tangent = new LayoutVector2(Math.Cos(baselineRotation), Math.Sin(baselineRotation));
        var normal = new LayoutVector2(-tangent.Y, tangent.X);
        var depth = candidate.SingleOrDefault(item => item.Role == AnnotationTextRole.Depth);
        var diameter = candidate.SingleOrDefault(item => item.Role == AnnotationTextRole.Diameter);
        if (depth is null || candidate.Count(item => item.Role == AnnotationTextRole.Depth) != 1 ||
            candidate.Count(item => item.Role == AnnotationTextRole.Diameter) > 1)
        {
            violations |= AnnotationGroupInvariantViolation.GroupIntegrity;
            details.Add("GroupIntegrityViolation: annotation must contain one depth and at most one diameter");
        }
        else if (diameter is not null &&
            LayoutVector2.Dot(depth.Position - diameter.Position, normal) <= LayoutGeometryTolerance.Distance)
        {
            violations |= AnnotationGroupInvariantViolation.Order;
            details.Add("OrderInvariantViolation: depth is not above diameter in the Legacy local frame");
        }

        LayoutVector2? commonTranslation = null;
        foreach (var item in candidate)
        {
            var baseline = legacyById[item.ItemId];
            var translation = item.Position - baseline.Position;
            commonTranslation ??= translation;
            var translationDelta = new LayoutVector2(
                translation.X - commonTranslation.Value.X,
                translation.Y - commonTranslation.Value.Y);
            if (translationDelta.Length > LayoutGeometryTolerance.Distance)
            {
                violations |= AnnotationGroupInvariantViolation.GroupIntegrity;
            }

            var legacyNormal = LayoutVector2.Dot(baseline.Position - anchor, normal);
            var candidateNormal = LayoutVector2.Dot(item.Position - anchor, normal);
            if (legacyNormal > LayoutGeometryTolerance.Distance &&
                    candidateNormal <= LayoutGeometryTolerance.Distance ||
                legacyNormal < -LayoutGeometryTolerance.Distance &&
                    candidateNormal >= -LayoutGeometryTolerance.Distance)
            {
                violations |= AnnotationGroupInvariantViolation.Side;
            }
        }

        if (violations.HasFlag(AnnotationGroupInvariantViolation.GroupIntegrity) &&
            !details.Any(detail => detail.StartsWith("GroupIntegrityViolation", StringComparison.Ordinal)))
        {
            details.Add("GroupIntegrityViolation: members did not receive one rigid translation");
        }

        if (violations.HasFlag(AnnotationGroupInvariantViolation.Side))
        {
            details.Add("SideInvariantViolation: a group member crossed the Legacy side of the anchor");
        }

        return new AnnotationGroupInvariantResult(violations, details);
    }

    private static double AngularDifference(double first, double second) =>
        Math.Abs(LayoutAngles.NormalizeSigned(first - second));
}
