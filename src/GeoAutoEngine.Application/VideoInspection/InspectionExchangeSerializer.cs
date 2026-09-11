using System.Diagnostics;
using System.Text.Json;

namespace GeoAutoEngine.Application.VideoInspection;

public sealed class InspectionExchangeSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public InspectionExchangeDocument Deserialize(string json) =>
        Deserialize(json, out _);

    public InspectionExchangeDocument Deserialize(
        string json,
        out InspectionExchangeDeserializeMetrics metrics)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        InspectionExchangeDocument document;
        var deserialize = Stopwatch.StartNew();
        try
        {
            document = JsonSerializer.Deserialize<InspectionExchangeDocument>(json, JsonOptions)
                ?? throw new InvalidDataException("O documento de intercâmbio está vazio.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("O arquivo de inspeção é inválido.", exception);
        }
        finally
        {
            deserialize.Stop();
        }

        var validation = Stopwatch.StartNew();
        try
        {
            Validate(document);
            PreserveSectionExportOrder(document);
        }
        finally
        {
            validation.Stop();
        }

        metrics = new InspectionExchangeDeserializeMetrics(
            deserialize.Elapsed.TotalMilliseconds,
            validation.Elapsed.TotalMilliseconds);
        return document;
    }

    public InspectionExchangeDocument Deserialize(byte[] utf8Payload)
    {
        ArgumentNullException.ThrowIfNull(utf8Payload);
        return Deserialize(System.Text.Encoding.UTF8.GetString(utf8Payload));
    }

    public void Validate(InspectionExchangeDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!string.Equals(document.Schema, InspectionExchangeSchema.Name, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Schema de intercâmbio não suportado: '{document.Schema}'.");
        }

        if (document.SchemaVersion != InspectionExchangeSchema.CurrentVersion)
        {
            throw new NotSupportedException(
                $"Schema {InspectionExchangeSchema.Name} v{document.SchemaVersion} não é suportado por esta versão.");
        }

        if (document.Project is null || document.Report is null ||
            document.Locations is null || document.Sections is null)
        {
            throw new InvalidDataException("O documento de intercâmbio está incompleto.");
        }

        if (document.Project.ProjectId == Guid.Empty)
        {
            throw new InvalidDataException("O identificador do projeto é obrigatório.");
        }

        if (document.Report.ReportId == Guid.Empty)
        {
            throw new InvalidDataException("O identificador do relatório é obrigatório.");
        }

        var locationIds = new HashSet<Guid>(document.Locations.Select(location => location.LocationId));
        if (locationIds.Contains(Guid.Empty) || locationIds.Count != document.Locations.Count)
        {
            throw new InvalidDataException("O documento contém locais sem identidade estável ou duplicados.");
        }

        if (document.Sections.Any(section =>
                section.SectionId == Guid.Empty || !locationIds.Contains(section.LocationId)))
        {
            throw new InvalidDataException("O documento contém trecho sem local válido.");
        }

        if (document.Sections.Select(section => section.SectionId).Distinct().Count() != document.Sections.Count)
        {
            throw new InvalidDataException("O documento contém trechos duplicados.");
        }

        if (document.Sections.Any(section => section.Occurrences is null || section.VideoReferences is null))
        {
            throw new InvalidDataException("O documento contém trecho incompleto.");
        }

        var occurrences = document.Sections.SelectMany(section => section.Occurrences).ToArray();
        if (occurrences.Any(occurrence => occurrence.OccurrenceId == Guid.Empty) ||
            occurrences.Select(occurrence => occurrence.OccurrenceId).Distinct().Count() != occurrences.Length)
        {
            throw new InvalidDataException("O documento contém ocorrências sem identidade estável ou duplicadas.");
        }

        if (occurrences.Any(occurrence => occurrence.MeteragePoints is null ||
                                          occurrence.Anomalies is null || occurrence.Evidence is null))
        {
            throw new InvalidDataException("O documento contém ocorrência incompleta.");
        }
    }

    private static void PreserveSectionExportOrder(InspectionExchangeDocument document)
    {
        // The order of the sections in the exported JSON is authoritative. Older
        // Order values may contain gaps or stale values after sections were edited.
        for (var index = 0; index < document.Sections.Count; index++)
        {
            document.Sections[index].Order = index;
        }
    }
}

public readonly record struct InspectionExchangeDeserializeMetrics(
    double DeserializeMilliseconds,
    double ValidationMilliseconds);
