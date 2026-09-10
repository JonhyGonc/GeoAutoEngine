namespace GeoAutoEngine.Application.VideoInspection;

public enum InspectionLocationMode
{
    Point,
    Range,
    EntireSection,
    NotApplicable,
    MultiPoint,
    From,
    Unknown,
}

public enum InspectionDirection
{
    Forward,
    Reverse,
}

public enum InspectionSectionStatus
{
    Pending,
    Associated,
    Generated,
    Attention,
    Error,
    AssociationLost,
}

public sealed class VideoInspectionSession
{
    public VideoInspectionSession(
        Guid projectId,
        string projectName,
        Guid reportId,
        string reportCode,
        string reportName,
        string client,
        string area,
        string contract,
        DateTime? reportDate,
        DateTime generatedAtUtc,
        IReadOnlyList<InspectionSectionSession> sections)
    {
        ProjectId = projectId;
        ProjectName = projectName;
        ReportId = reportId;
        ReportCode = reportCode;
        ReportName = reportName;
        Client = client;
        Area = area;
        Contract = contract;
        ReportDate = reportDate;
        GeneratedAtUtc = generatedAtUtc;
        Sections = sections;
    }

    public Guid ProjectId { get; }
    public string ProjectName { get; }
    public Guid ReportId { get; }
    public string ReportCode { get; }
    public string ReportName { get; }
    public string Client { get; }
    public string Area { get; }
    public string Contract { get; }
    public DateTime? ReportDate { get; }
    public DateTime GeneratedAtUtc { get; }
    public IReadOnlyList<InspectionSectionSession> Sections { get; }
    public int OccurrenceCount => Sections.Sum(section => section.Occurrences.Count);
    public int EditedOccurrenceCount => Sections.Sum(section => section.EditedOccurrenceCount);

    public InspectionSectionSession? FindSection(Guid sectionId) =>
        Sections.FirstOrDefault(section => section.SectionId == sectionId);

    public InspectionCorrectionPush CreateCorrectionPush() => new()
    {
        ReportId = ReportId,
        GeneratedAtUtc = DateTime.UtcNow,
        Corrections = Array.AsReadOnly(Sections
            .SelectMany(section => section.GetCorrections())
            .ToArray()),
    };

    public int AcceptCurrentCorrectionsAsBaseline() =>
        Sections.Sum(section => section.AcceptCurrentCorrectionsAsBaseline());
}

public sealed class InspectionSectionSession
{
    private readonly Dictionary<Guid, InspectionOccurrenceSession> _originalOccurrences;

    public InspectionSectionSession(
        Guid sectionId,
        Guid locationId,
        string locationName,
        string name,
        string? description,
        decimal? diameter,
        string? startPoint,
        string? endPoint,
        string? reference,
        decimal? investigatedMeters,
        int order,
        IReadOnlyList<InspectionVideoReferenceSession> videoReferences,
        IReadOnlyList<InspectionOccurrenceSession> occurrences)
    {
        SectionId = sectionId;
        LocationId = locationId;
        LocationName = locationName;
        Name = name;
        Description = description;
        Diameter = diameter;
        StartPoint = startPoint;
        EndPoint = endPoint;
        Reference = reference;
        InvestigatedMeters = investigatedMeters;
        Order = order;
        VideoReferences = videoReferences;
        Occurrences = Array.AsReadOnly(occurrences.ToArray());
        _originalOccurrences = occurrences.ToDictionary(occurrence => occurrence.OccurrenceId);
    }

    public Guid SectionId { get; }
    public Guid LocationId { get; }
    public string LocationName { get; }
    public string Name { get; }
    public string? Description { get; }
    public decimal? Diameter { get; }
    public string? StartPoint { get; }
    public string? EndPoint { get; }
    public string? Reference { get; }
    public decimal? InvestigatedMeters { get; }
    public int Order { get; }
    public IReadOnlyList<InspectionVideoReferenceSession> VideoReferences { get; }
    public IReadOnlyList<InspectionOccurrenceSession> Occurrences { get; private set; }
    public CadSectionAssociation? Association { get; private set; }
    public IReadOnlyList<PositionedInspectionEvent> PositionedEvents { get; private set; } = [];
    public InspectionSectionStatus Status { get; private set; } = InspectionSectionStatus.Pending;
    public string? StatusMessage { get; private set; }
    public int EditedOccurrenceCount => Occurrences.Count(occurrence => IsOccurrenceEdited(occurrence.OccurrenceId));

    public InspectionOccurrenceSession? FindOccurrence(Guid occurrenceId) =>
        Occurrences.FirstOrDefault(occurrence => occurrence.OccurrenceId == occurrenceId);

    public InspectionOccurrenceSession? FindOriginalOccurrence(Guid occurrenceId) =>
        _originalOccurrences.TryGetValue(occurrenceId, out var occurrence) ? occurrence : null;

    public bool IsOccurrenceEdited(Guid occurrenceId)
    {
        var current = FindOccurrence(occurrenceId);
        return current is not null &&
            _originalOccurrences.TryGetValue(occurrenceId, out var original) &&
            !EditableValuesEqual(current, original);
    }

    public void ApplyOccurrenceEdit(InspectionOccurrenceEdit edit)
    {
        ArgumentNullException.ThrowIfNull(edit);
        if (edit.SectionId != SectionId)
        {
            throw new ArgumentException("A correção não pertence ao trecho informado.", nameof(edit));
        }

        var index = FindOccurrenceIndex(edit.OccurrenceId);
        if (index < 0 || !_originalOccurrences.TryGetValue(edit.OccurrenceId, out var original))
        {
            throw new InvalidOperationException("A ocorrência que será editada não existe neste trecho.");
        }

        if (string.IsNullOrWhiteSpace(edit.Description))
        {
            throw new ArgumentException("O texto da ocorrência não pode ficar vazio.", nameof(edit));
        }

        var current = Occurrences[index];
        var edited = ApplyEditableValues(current, original, edit);
        var replacement = EditableValuesEqual(edited, original) ? original : edited;
        ReplaceOccurrence(index, replacement);
        InvalidatePositioningAfterEdit();
    }

    public bool RestoreOccurrence(Guid occurrenceId)
    {
        var index = FindOccurrenceIndex(occurrenceId);
        if (index < 0 || !_originalOccurrences.TryGetValue(occurrenceId, out var original))
        {
            return false;
        }

        if (EditableValuesEqual(Occurrences[index], original))
        {
            return false;
        }

        ReplaceOccurrence(index, original);
        InvalidatePositioningAfterEdit();
        return true;
    }

    public IReadOnlyList<InspectionOccurrenceCorrection> GetCorrections() =>
        Array.AsReadOnly(Occurrences
            .Where(occurrence => IsOccurrenceEdited(occurrence.OccurrenceId))
            .Select(occurrence => new InspectionOccurrenceCorrection
            {
                SectionId = SectionId,
                OccurrenceId = occurrence.OccurrenceId,
                LocationMode = occurrence.WireLocationMode,
                Meterage = occurrence.Meterage,
                EndMeterage = occurrence.EndMeterage,
                MeteragePoints = Array.AsReadOnly(occurrence.MeteragePoints.ToArray()),
                Description = occurrence.EffectiveDescription,
            })
            .ToArray());

    public int AcceptCurrentCorrectionsAsBaseline()
    {
        var accepted = 0;
        foreach (var occurrence in Occurrences)
        {
            if (!IsOccurrenceEdited(occurrence.OccurrenceId))
            {
                continue;
            }

            _originalOccurrences[occurrence.OccurrenceId] = occurrence;
            accepted++;
        }

        return accepted;
    }

    public void Associate(CadSectionAssociation association)
    {
        ArgumentNullException.ThrowIfNull(association);
        if (association.SectionId != SectionId)
        {
            throw new ArgumentException("A associação não pertence ao trecho informado.", nameof(association));
        }

        Association = association;
        PositionedEvents = [];
        Status = InspectionSectionStatus.Associated;
        StatusMessage = null;
    }

    public void MarkAssociationLost(string message)
    {
        Association = null;
        PositionedEvents = [];
        Status = InspectionSectionStatus.AssociationLost;
        StatusMessage = message;
    }

    public void SetPositioning(IReadOnlyList<PositionedInspectionEvent> positionedEvents)
    {
        ArgumentNullException.ThrowIfNull(positionedEvents);
        PositionedEvents = positionedEvents;
        var invalid = positionedEvents.Count(item => !item.CanCommit);
        Status = invalid > 0 ? InspectionSectionStatus.Attention : InspectionSectionStatus.Associated;
        StatusMessage = invalid > 0
            ? $"{invalid} ocorrência(s) requer(em) atenção."
            : "Ocorrências posicionadas para revisão.";
    }

    public void MarkGenerated(string message)
    {
        Status = InspectionSectionStatus.Generated;
        StatusMessage = message;
    }

    public void MarkError(string message)
    {
        Status = InspectionSectionStatus.Error;
        StatusMessage = message;
    }

    private InspectionOccurrenceSession ApplyEditableValues(
        InspectionOccurrenceSession current,
        InspectionOccurrenceSession original,
        InspectionOccurrenceEdit edit)
    {
        var description = edit.Description;
        switch (original.LocationMode)
        {
            case InspectionLocationMode.Point:
                ValidateRequiredMeterage(edit.Meterage, "metragem");
                return current with
                {
                    Meterage = edit.Meterage,
                    EffectiveDescription = description,
                };
            case InspectionLocationMode.Range:
                ValidateRequiredMeterage(edit.Meterage, "metragem inicial");
                ValidateRequiredMeterage(edit.EndMeterage, "metragem final");
                if (edit.EndMeterage < edit.Meterage)
                {
                    throw new ArgumentException("A metragem final não pode ser menor que a metragem inicial.", nameof(edit));
                }

                return current with
                {
                    Meterage = edit.Meterage,
                    EndMeterage = edit.EndMeterage,
                    EffectiveDescription = description,
                };
            case InspectionLocationMode.MultiPoint:
                ArgumentNullException.ThrowIfNull(edit.MeteragePoints);
                if (edit.MeteragePoints.Count != original.MeteragePoints.Count)
                {
                    throw new ArgumentException(
                        "A edição multiponto deve manter a mesma quantidade de pontos da ocorrência original.",
                        nameof(edit));
                }

                foreach (var meterage in edit.MeteragePoints)
                {
                    ValidateRequiredMeterage(meterage, "metragem multiponto");
                }

                return current with
                {
                    MeteragePoints = Array.AsReadOnly(edit.MeteragePoints.ToArray()),
                    EffectiveDescription = description,
                };
            case InspectionLocationMode.From:
                ValidateRequiredMeterage(edit.Meterage, "metragem inicial");
                return current with
                {
                    Meterage = edit.Meterage,
                    EffectiveDescription = description,
                };
            case InspectionLocationMode.EntireSection:
            case InspectionLocationMode.NotApplicable:
            case InspectionLocationMode.Unknown:
            default:
                return current with { EffectiveDescription = description };
        }
    }

    private static void ValidateRequiredMeterage(decimal? value, string fieldName)
    {
        if (!value.HasValue)
        {
            throw new ArgumentException($"Informe a {fieldName} da ocorrência.");
        }

        ValidateRequiredMeterage(value.Value, fieldName);
    }

    private static void ValidateRequiredMeterage(decimal value, string fieldName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(fieldName, "A metragem não pode ser negativa.");
        }
    }

    private int FindOccurrenceIndex(Guid occurrenceId)
    {
        for (var index = 0; index < Occurrences.Count; index++)
        {
            if (Occurrences[index].OccurrenceId == occurrenceId)
            {
                return index;
            }
        }

        return -1;
    }

    private void ReplaceOccurrence(int index, InspectionOccurrenceSession occurrence)
    {
        var occurrences = Occurrences.ToArray();
        occurrences[index] = occurrence;
        Occurrences = Array.AsReadOnly(occurrences);
    }

    private void InvalidatePositioningAfterEdit()
    {
        PositionedEvents = [];
        if (Association is not null)
        {
            Status = InspectionSectionStatus.Associated;
            StatusMessage = "Ocorrência editada; posicionamento será recalculado.";
        }
        else if (Status != InspectionSectionStatus.AssociationLost)
        {
            Status = InspectionSectionStatus.Pending;
            StatusMessage = "Ocorrência editada; associe a geometria para reposicionar.";
        }
    }

    private static bool EditableValuesEqual(
        InspectionOccurrenceSession first,
        InspectionOccurrenceSession second) =>
        first.Meterage == second.Meterage &&
        first.EndMeterage == second.EndMeterage &&
        first.MeteragePoints.SequenceEqual(second.MeteragePoints) &&
        string.Equals(first.EffectiveDescription, second.EffectiveDescription, StringComparison.Ordinal);
}

public sealed record InspectionVideoReferenceSession(
    Guid VideoReferenceId,
    string DisplayName,
    string FileName,
    int Order,
    string? Description,
    DateTime? CreatedAt);

public sealed record InspectionOccurrenceSession(
    Guid OccurrenceId,
    int Order,
    InspectionLocationMode LocationMode,
    string WireLocationMode,
    decimal? Meterage,
    decimal? EndMeterage,
    IReadOnlyList<decimal> MeteragePoints,
    string? ReferencePoint,
    string? Position,
    string? Notes,
    string EffectiveDescription,
    bool InterruptedRecording,
    bool IsSevere,
    Guid? VideoReferenceId,
    TimeSpan? VideoTimestamp);

public sealed record CadSectionAssociation(
    Guid ReportId,
    Guid SectionId,
    string GeometryHandle,
    InspectionDirection Direction,
    double LastKnownGeometryLength,
    int StorageVersion = 1);

public static class VideoInspectionSessionMapper
{
    public static VideoInspectionSession Map(InspectionExchangeDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var locations = document.Locations.ToDictionary(location => location.LocationId);
        var sections = document.Sections
            .OrderBy(section => section.Order)
            .Select(section => new InspectionSectionSession(
                section.SectionId,
                section.LocationId,
                locations.TryGetValue(section.LocationId, out var location) ? location.Name : string.Empty,
                section.Name,
                section.Description,
                section.Diameter,
                section.StartPoint,
                section.EndPoint,
                section.Reference,
                section.InvestigatedMeters,
                section.Order,
                Array.AsReadOnly(section.VideoReferences
                    .OrderBy(video => video.Order)
                    .Select(video => new InspectionVideoReferenceSession(
                        video.VideoReferenceId,
                        video.DisplayName,
                        video.FileName,
                        video.Order,
                        video.Description,
                        video.CreatedAt))
                    .ToArray()),
                Array.AsReadOnly(section.Occurrences
                    .OrderBy(occurrence => occurrence.Order)
                    .Select(occurrence => new InspectionOccurrenceSession(
                        occurrence.OccurrenceId,
                        occurrence.Order,
                        ParseLocationMode(occurrence.LocationMode),
                        occurrence.LocationMode,
                        occurrence.Meterage,
                        occurrence.EndMeterage,
                        Array.AsReadOnly(occurrence.MeteragePoints.ToArray()),
                        occurrence.ReferencePoint,
                        occurrence.Position,
                        occurrence.Notes,
                        occurrence.EffectiveDescription,
                        occurrence.InterruptedRecording,
                        occurrence.IsSevere,
                        occurrence.VideoReferenceId,
                        occurrence.VideoTimestamp))
                    .ToArray())))
            .ToArray();

        return new VideoInspectionSession(
            document.Project.ProjectId,
            document.Project.Name,
            document.Report.ReportId,
            document.Report.ReportCode,
            document.Report.Name,
            document.Report.Client,
            document.Report.Area,
            document.Report.Contract,
            document.Report.ReportDate,
            document.GeneratedAtUtc,
            Array.AsReadOnly(sections));
    }

    private static InspectionLocationMode ParseLocationMode(string value) => value switch
    {
        "Point" => InspectionLocationMode.Point,
        "Range" => InspectionLocationMode.Range,
        "EntireSection" => InspectionLocationMode.EntireSection,
        "NotApplicable" => InspectionLocationMode.NotApplicable,
        "MultiPoint" => InspectionLocationMode.MultiPoint,
        "From" => InspectionLocationMode.From,
        _ => InspectionLocationMode.Unknown,
    };
}
