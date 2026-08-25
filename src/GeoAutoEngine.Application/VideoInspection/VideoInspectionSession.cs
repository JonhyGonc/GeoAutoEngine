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

    public InspectionSectionSession? FindSection(Guid sectionId) =>
        Sections.FirstOrDefault(section => section.SectionId == sectionId);
}

public sealed class InspectionSectionSession
{
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
        Occurrences = occurrences;
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
    public IReadOnlyList<InspectionOccurrenceSession> Occurrences { get; }
    public CadSectionAssociation? Association { get; private set; }
    public IReadOnlyList<PositionedInspectionEvent> PositionedEvents { get; private set; } = [];
    public InspectionSectionStatus Status { get; private set; } = InspectionSectionStatus.Pending;
    public string? StatusMessage { get; private set; }

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
