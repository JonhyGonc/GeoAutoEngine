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
        using var documentLock = document.LockDocument();
        using var transaction = document.Database.TransactionManager.StartTransaction();
        var blockTable = (BlockTable)transaction.GetObject(
            document.Database.BlockTableId,
            OpenMode.ForRead);
        var modelSpace = (BlockTableRecord)transaction.GetObject(
            blockTable[BlockTableRecord.ModelSpace],
            OpenMode.ForRead);
        var report = reportId.ToString("D");
        var section = sectionId.ToString("D");
        var erased = 0;

        foreach (ObjectId objectId in modelSpace)
        {
            if (transaction.GetObject(objectId, OpenMode.ForRead, openErased: false) is not Entity entity)
            {
                continue;
            }

            using var data = entity.GetXDataForApplication(AutoCadInspectionRenderer.RegisteredApplicationName);
            if (!string.Equals(Read(data, "REPORT_ID"), report, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Read(data, "SECTION_ID"), section, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var component = Read(data, "COMPONENT");
            if (component is null || !AnnotationComponents.Contains(component))
            {
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

    private static string? Read(ResultBuffer? data, string key)
    {
        if (data is null)
        {
            return null;
        }

        var prefix = key + "=";
        return data.AsArray()
            .Where(value => value.TypeCode == (int)DxfCode.ExtendedDataAsciiString)
            .Select(value => Convert.ToString(value.Value, System.Globalization.CultureInfo.InvariantCulture))
            .FirstOrDefault(value => value?.StartsWith(prefix, StringComparison.Ordinal) == true)
            ?.Substring(prefix.Length);
    }
}
