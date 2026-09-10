namespace GeoAutoEngine.Application.VideoInspection;

public sealed record InspectionOccurrenceEdit(
    Guid SectionId,
    Guid OccurrenceId,
    decimal? Meterage,
    decimal? EndMeterage,
    IReadOnlyList<decimal> MeteragePoints,
    string Description);

public sealed class InspectionCorrectionPush
{
    public Guid ReportId { get; set; }
    public DateTime GeneratedAtUtc { get; set; }
    public IReadOnlyList<InspectionOccurrenceCorrection> Corrections { get; set; } = [];
}

public sealed class InspectionOccurrenceCorrection
{
    public Guid SectionId { get; set; }
    public Guid OccurrenceId { get; set; }
    public string LocationMode { get; set; } = string.Empty;
    public decimal? Meterage { get; set; }
    public decimal? EndMeterage { get; set; }
    public IReadOnlyList<decimal> MeteragePoints { get; set; } = [];
    public string Description { get; set; } = string.Empty;
}

public sealed class InspectionCorrectionAcknowledgement
{
    public bool Accepted { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int AppliedCount { get; set; }
    public int RejectedCount { get; set; }
}
