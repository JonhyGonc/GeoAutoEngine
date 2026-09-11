using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;

namespace GeoAutoEngine.Cad2026.VideoInspection;

public sealed class AutoCadInspectionAnnotationCleaner
{
    private static readonly HashSet<string> AnnotationComponents = new(StringComparer.Ordinal)
    {
        "MARKER",
        "INTERVAL_END_MARKER",
        "INTERVAL_CONNECTOR",
        "LEADER",
        "TEXT",
        "SECTION_NAME",
        "SECTION_DIAMETER",
    };

    public int Clear(Document document, Guid reportId, Guid sectionId)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (reportId == Guid.Empty)
        {
            throw new ArgumentException("Informe um ReportId válido.", nameof(reportId));
        }

        if (sectionId == Guid.Empty)
        {
            throw new ArgumentException("Informe um SectionId válido.", nameof(sectionId));
        }

        using var documentLock = document.LockDocument();
        using var transaction = document.Database.TransactionManager.StartTransaction();
        var blockTable = (BlockTable)transaction.GetObject(
            document.Database.BlockTableId,
            OpenMode.ForRead);
        var modelSpace = (BlockTableRecord)transaction.GetObject(
            blockTable[BlockTableRecord.ModelSpace],
            OpenMode.ForRead);
        var expectedReport = reportId.ToString("D");
        var expectedSection = sectionId.ToString("D");
        var erased = 0;

        foreach (ObjectId objectId in modelSpace)
        {
            if (transaction.GetObject(
                    objectId,
                    OpenMode.ForRead,
                    openErased: false) is not Entity entity)
            {
                continue;
            }

            using var data = entity.GetXDataForApplication(
                AutoCadInspectionRenderer.RegisteredApplicationName);
            if (!string.Equals(
                    ReadXData(data, "REPORT_ID"),
                    expectedReport,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    ReadXData(data, "SECTION_ID"),
                    expectedSection,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var component = ReadXData(data, "COMPONENT");
            if (component is null || !AnnotationComponents.Contains(component))
            {
                // Não apagar cobertura, segmento físico do BREAK ou qualquer entidade
                // de levantamento. Somente o estaqueamento gerenciado deste trecho.
                continue;
            }

            entity.UpgradeOpen();
            entity.Erase();
            erased++;
        }

        transaction.Commit();
        if (erased > 0)
        {
            document.Editor.Regen();
            document.Editor.UpdateScreen();
        }

        return erased;
    }

    private static string? ReadXData(ResultBuffer? data, string key)
    {
        if (data is null)
        {
            return null;
        }

        var prefix = key + "=";
        return data.AsArray()
            .Where(value => value.TypeCode == (int)DxfCode.ExtendedDataAsciiString)
            .Select(value => Convert.ToString(
                value.Value,
                System.Globalization.CultureInfo.InvariantCulture))
            .FirstOrDefault(value => value?.StartsWith(prefix, StringComparison.Ordinal) == true)
            ?.Substring(prefix.Length);
    }
}
