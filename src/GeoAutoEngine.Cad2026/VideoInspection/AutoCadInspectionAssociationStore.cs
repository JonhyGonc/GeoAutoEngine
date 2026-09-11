using Autodesk.AutoCAD.DatabaseServices;
using GeoAutoEngine.Application.VideoInspection;

namespace GeoAutoEngine.Cad2026.VideoInspection;

public sealed class AutoCadInspectionAssociationStore
{
    private const string DictionaryName = "GEOAUTOENGINE_VIDEO_INSPECTION_ASSOCIATIONS";

    public void Save(Database database, CadSectionAssociation association)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(association);
        using var transaction = database.TransactionManager.StartTransaction();
        var dictionary = EnsureDictionary(database, transaction);
        var key = EntryKey(association.ReportId, association.SectionId);
        Xrecord record;
        if (dictionary.Contains(key))
        {
            record = (Xrecord)transaction.GetObject(dictionary.GetAt(key), OpenMode.ForWrite);
        }
        else
        {
            record = new Xrecord();
            dictionary.SetAt(key, record);
            transaction.AddNewlyCreatedDBObject(record, true);
        }

        record.Data = new ResultBuffer(
            Text($"STORAGE_VERSION={association.StorageVersion}"),
            Text($"REPORT_ID={association.ReportId:D}"),
            Text($"SECTION_ID={association.SectionId:D}"),
            Text($"GEOMETRY_HANDLE={association.GeometryHandle}"),
            Text($"DIRECTION={association.Direction}"),
            Text(FormattableString.Invariant(
                $"LAST_KNOWN_GEOMETRY_LENGTH={association.LastKnownGeometryLength:R}")));
        transaction.Commit();
    }

    public CadSectionAssociation? Load(Database database, Guid reportId, Guid sectionId)
    {
        ArgumentNullException.ThrowIfNull(database);
        using var transaction = database.TransactionManager.StartOpenCloseTransaction();
        var namedObjects = (DBDictionary)transaction.GetObject(
            database.NamedObjectsDictionaryId,
            OpenMode.ForRead);
        if (!namedObjects.Contains(DictionaryName))
        {
            return null;
        }

        var dictionary = (DBDictionary)transaction.GetObject(namedObjects.GetAt(DictionaryName), OpenMode.ForRead);
        var key = EntryKey(reportId, sectionId);
        if (!dictionary.Contains(key))
        {
            return null;
        }

        var record = (Xrecord)transaction.GetObject(dictionary.GetAt(key), OpenMode.ForRead);
        var values = ReadValues(record.Data);
        if (!values.TryGetValue("REPORT_ID", out var storedReport) ||
            !Guid.TryParse(storedReport, out var parsedReport) || parsedReport != reportId ||
            !values.TryGetValue("SECTION_ID", out var storedSection) ||
            !Guid.TryParse(storedSection, out var parsedSection) || parsedSection != sectionId ||
            !values.TryGetValue("GEOMETRY_HANDLE", out var handle) || string.IsNullOrWhiteSpace(handle) ||
            !values.TryGetValue("DIRECTION", out var directionValue) ||
            !Enum.TryParse(directionValue, true, out InspectionDirection direction) ||
            !values.TryGetValue("LAST_KNOWN_GEOMETRY_LENGTH", out var lengthValue) ||
            !double.TryParse(
                lengthValue,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var length))
        {
            return null;
        }

        var storageVersion = values.TryGetValue("STORAGE_VERSION", out var versionValue) &&
                             int.TryParse(versionValue, out var version)
            ? version
            : 1;
        return new CadSectionAssociation(reportId, sectionId, handle, direction, length, storageVersion);
    }

    public bool Remove(Database database, Guid reportId, Guid sectionId)
    {
        ArgumentNullException.ThrowIfNull(database);
        using var transaction = database.TransactionManager.StartTransaction();
        var namedObjects = (DBDictionary)transaction.GetObject(
            database.NamedObjectsDictionaryId,
            OpenMode.ForRead);
        if (!namedObjects.Contains(DictionaryName))
        {
            transaction.Commit();
            return false;
        }

        var dictionary = (DBDictionary)transaction.GetObject(
            namedObjects.GetAt(DictionaryName),
            OpenMode.ForWrite);
        var key = EntryKey(reportId, sectionId);
        if (!dictionary.Contains(key))
        {
            transaction.Commit();
            return false;
        }

        var recordId = dictionary.GetAt(key);
        dictionary.Remove(key);
        if (!recordId.IsNull && transaction.GetObject(recordId, OpenMode.ForWrite, openErased: false) is DBObject record)
        {
            record.Erase();
        }

        transaction.Commit();
        return true;
    }

    public static bool TryResolvePolyline(
        Database database,
        Transaction transaction,
        string geometryHandle,
        OpenMode openMode,
        out Polyline? polyline)
    {
        polyline = null;
        try
        {
            var handle = new Handle(Convert.ToInt64(geometryHandle, 16));
            var objectId = database.GetObjectId(false, handle, 0);
            if (objectId.IsNull || !objectId.IsValid || objectId.IsErased)
            {
                return false;
            }

            polyline = transaction.GetObject(objectId, openMode, openErased: false) as Polyline;
            return polyline is not null;
        }
        catch (Exception exception) when (exception is Autodesk.AutoCAD.Runtime.Exception or
                                          FormatException or OverflowException)
        {
            return false;
        }
    }

    private static DBDictionary EnsureDictionary(Database database, Transaction transaction)
    {
        var namedObjects = (DBDictionary)transaction.GetObject(
            database.NamedObjectsDictionaryId,
            OpenMode.ForRead);
        if (namedObjects.Contains(DictionaryName))
        {
            return (DBDictionary)transaction.GetObject(namedObjects.GetAt(DictionaryName), OpenMode.ForWrite);
        }

        namedObjects.UpgradeOpen();
        var dictionary = new DBDictionary();
        namedObjects.SetAt(DictionaryName, dictionary);
        transaction.AddNewlyCreatedDBObject(dictionary, true);
        return dictionary;
    }

    private static string EntryKey(Guid reportId, Guid sectionId) =>
        $"R_{reportId:N}_S_{sectionId:N}";

    private static TypedValue Text(string value) => new((int)DxfCode.Text, value);

    private static IReadOnlyDictionary<string, string> ReadValues(ResultBuffer? data)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (data is null)
        {
            return result;
        }

        foreach (var value in data.AsArray())
        {
            var text = Convert.ToString(value.Value, System.Globalization.CultureInfo.InvariantCulture);
            var separator = text?.IndexOf('=') ?? -1;
            if (separator > 0)
            {
                result[text!.Substring(0, separator)] = text.Substring(separator + 1);
            }
        }

        return result;
    }
}
