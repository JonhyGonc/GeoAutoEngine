using GeoAutoEngine.Application;
using GeoAutoEngine.Domain;

namespace GeoAutoEngine.Tests;

internal static class TestData
{
    public const string Sha256 = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    public static SourceFile Source(
        int selectionOrdinal = 0,
        int recordCount = 1,
        string name = "survey.csv",
        SourceFileId? id = null,
        ResolvedColumns? columns = null) =>
        new(
            id ?? SourceFileId.New(),
            selectionOrdinal,
            name,
            Path.GetFullPath(name),
            Sha256,
            "utf-8",
            ";",
            columns ?? new ResolvedColumns(
                northing: 0,
                easting: 1,
                code: 3,
                elevation: 2,
                columns: new Dictionary<string, int>
                {
                    ["northing"] = 0,
                    ["easting"] = 1,
                    ["elevation"] = 2,
                    ["code"] = 3,
                }),
            recordCount);

    public static RecordOrigin Origin(SourceFile source, int index = 0, int? line = null, int? batch = null) =>
        new(source.Id, source.DisplayName, index, line ?? index + 2, batch ?? index);

    public static SurveyRecord Record(SourceFile source, int index = 0, int? batch = null, string code = "PV")
    {
        var origin = Origin(source, index, batch: batch);
        return new SurveyRecord(
            SurveyRecordId.From(source.Id, index),
            origin,
            new Dictionary<string, string?>
            {
                ["Norte"] = (7000000 + index).ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Leste"] = (500000 + index).ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Código"] = code,
            },
            new SurveyPoint(500000 + index, 7000000 + index, 10),
            code,
            new SurveyInterpretation("ESGOTO", "PV", null, null, null));
    }

    public static SourceImportResult SourceResult(SourceFile source, params SurveyRecord[] records) =>
        new(source, records, [], []);
}
