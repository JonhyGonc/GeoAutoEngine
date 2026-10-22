namespace GeoAutoEngine.Application.VideoInspection;

/// <summary>
/// Texto de chamado já existente no desenho, lido do XData do GeoAutoEngine.
/// </summary>
public sealed record InspectionExistingCalloutText(
    string StableKey,
    string Contents,
    double? InspectionDistance,
    double? IntervalEndInspectionDistance = null);

public enum InspectionTextRefreshOutcome
{
    /// <summary>O conteúdo mudou e será reescrito sem mexer na posição.</summary>
    Updated,

    /// <summary>O texto do desenho já está igual ao da inspeção.</summary>
    Unchanged,

    /// <summary>A metragem mudou; o chamado precisa ser regerado.</summary>
    PositionChanged,

    /// <summary>A ocorrência ainda não existe no desenho; o chamado precisa ser gerado.</summary>
    MissingInDrawing,
}

public sealed record InspectionTextRefreshItem(
    InspectionAnnotationCall Call,
    string StableKey,
    InspectionTextRefreshOutcome Outcome,
    string DesiredContents);

public sealed record InspectionTextRefreshPlan(
    IReadOnlyList<InspectionTextRefreshItem> Items,
    IReadOnlyList<string> OrphanedKeys)
{
    public int UpdatedCount => Count(InspectionTextRefreshOutcome.Updated);
    public int UnchangedCount => Count(InspectionTextRefreshOutcome.Unchanged);
    public int PositionChangedCount => Count(InspectionTextRefreshOutcome.PositionChanged);
    public int MissingInDrawingCount => Count(InspectionTextRefreshOutcome.MissingInDrawing);
    public int OrphanedCount => OrphanedKeys.Count;

    private int Count(InspectionTextRefreshOutcome outcome) =>
        Items.Count(item => !item.Call.IsSyntheticStart && item.Outcome == outcome);
}

/// <summary>
/// Decide, para cada chamado da inspeção atual, se o texto existente no desenho
/// pode ser atualizado no lugar (somente o conteúdo) ou se exige regerar o trecho.
/// Nunca calcula posição, rotação ou layout.
/// </summary>
public static class InspectionTextRefreshPlanner
{
    /// <summary>Tolerância de 5 mm para considerar que a metragem não mudou.</summary>
    public const double DistanceTolerance = 0.005;

    public static InspectionTextRefreshPlan Plan(
        Guid reportId,
        Guid sectionId,
        IReadOnlyList<InspectionAnnotationCall> calls,
        IReadOnlyList<InspectionExistingCalloutText> existingTexts)
    {
        ArgumentNullException.ThrowIfNull(calls);
        ArgumentNullException.ThrowIfNull(existingTexts);

        var existingByKey = new Dictionary<string, InspectionExistingCalloutText>(StringComparer.Ordinal);
        foreach (var existing in existingTexts)
        {
            if (!existingByKey.ContainsKey(existing.StableKey))
            {
                existingByKey.Add(existing.StableKey, existing);
            }
        }

        var desiredKeys = new HashSet<string>(StringComparer.Ordinal);
        var items = new List<InspectionTextRefreshItem>(calls.Count);
        foreach (var call in calls)
        {
            var stableKey = StableKey(reportId, sectionId, call);
            desiredKeys.Add(stableKey);
            var desiredContents = MTextContents(call);
            InspectionTextRefreshOutcome outcome;
            if (!existingByKey.TryGetValue(stableKey, out var existing))
            {
                outcome = InspectionTextRefreshOutcome.MissingInDrawing;
            }
            else if (PositionChanged(call, existing))
            {
                outcome = InspectionTextRefreshOutcome.PositionChanged;
            }
            else
            {
                // Preserva a estrutura que o desenhista deixou no MText (linhas unidas,
                // formatação); só as palavras que mudaram na inspeção são trocadas.
                desiredContents = MTextContentMerger.Merge(existing.Contents, desiredContents);
                outcome = string.Equals(existing.Contents, desiredContents, StringComparison.Ordinal)
                    ? InspectionTextRefreshOutcome.Unchanged
                    : InspectionTextRefreshOutcome.Updated;
            }

            items.Add(new InspectionTextRefreshItem(call, stableKey, outcome, desiredContents));
        }

        var orphaned = existingByKey.Keys
            .Where(key => !desiredKeys.Contains(key))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
        return new InspectionTextRefreshPlan(
            Array.AsReadOnly(items.ToArray()),
            Array.AsReadOnly(orphaned));
    }

    public static string StableKey(
        Guid reportId,
        Guid sectionId,
        InspectionAnnotationCall call)
    {
        ArgumentNullException.ThrowIfNull(call);
        return call.IsSyntheticStart
            ? $"VI:{reportId:D}:{sectionId:D}:FILMING_START"
            : VideoInspectionStableKey.Create(
                reportId,
                sectionId,
                call.OccurrenceId,
                call.Anchor.LocationIndex,
                call.Anchor.Role);
    }

    public static string MTextContents(InspectionAnnotationCall call)
    {
        ArgumentNullException.ThrowIfNull(call);
        return string.Join(
            "\\P",
            InspectionCalloutLayoutEngine.FormatTextLines(call).Select(EscapeMText));
    }

    public static string EscapeMText(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value
            .Replace("\\", "\\\\")
            .Replace("{", "\\{")
            .Replace("}", "\\}");
    }

    private static bool PositionChanged(
        InspectionAnnotationCall call,
        InspectionExistingCalloutText existing)
    {
        if (existing.InspectionDistance is { } anchorDistance &&
            !SameDistance(anchorDistance, call.Anchor.InspectionDistance))
        {
            return true;
        }

        var desiredEnd = call.IntervalEnd?.InspectionDistance;
        var existingEnd = existing.IntervalEndInspectionDistance;
        if (desiredEnd is null && existingEnd is null)
        {
            return false;
        }

        if (desiredEnd is null || existingEnd is null)
        {
            return true;
        }

        return !SameDistance(existingEnd.Value, desiredEnd.Value);
    }

    private static bool SameDistance(double left, double right) =>
        Math.Abs(left - right) <= DistanceTolerance;
}
