using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;

namespace GeoAutoEngine.Cad2026.VideoInspection;

public sealed class AutoCadInspectionManagedEntityProbe
{
    public bool HasManagedEntities(
        Document document,
        Guid reportId,
        Guid sectionId)
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
        using var transaction = document.Database.TransactionManager.StartOpenCloseTransaction();
        var blockTable = (BlockTable)transaction.GetObject(
            document.Database.BlockTableId,
            OpenMode.ForRead);
        var modelSpace = (BlockTableRecord)transaction.GetObject(
            blockTable[BlockTableRecord.ModelSpace],
            OpenMode.ForRead);
        var expectedReport = reportId.ToString("D");
        var expectedSection = sectionId.ToString("D");

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
            if (string.Equals(
                    ReadXData(data, "REPORT_ID"),
                    expectedReport,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    ReadXData(data, "SECTION_ID"),
                    expectedSection,
                    StringComparison.OrdinalIgnoreCase))
            {
                transaction.Commit();
                return true;
            }
        }

        transaction.Commit();
        return false;
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
