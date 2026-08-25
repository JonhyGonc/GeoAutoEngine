using GeoAutoEngine.Application;

namespace GeoAutoEngine.Csv;

public static class CsvImportSources
{
    public static IReadOnlyList<ImportSourceRequest> FromSingleFile(string path) => FromFiles([path]);

    public static IReadOnlyList<ImportSourceRequest> FromFiles(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var canonicalPaths = paths.Select(Path.GetFullPath).ToArray();
        if (canonicalPaths.Length == 0)
        {
            throw new ArgumentException("At least one CSV source is required.", nameof(paths));
        }

        if (canonicalPaths.Any(path => !string.Equals(Path.GetExtension(path), ".csv", StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("Every import source must have a .csv extension.", nameof(paths));
        }

        if (canonicalPaths.Distinct(StringComparer.OrdinalIgnoreCase).Count() != canonicalPaths.Length)
        {
            throw new ArgumentException("The same CSV source cannot be selected twice.", nameof(paths));
        }

        return Array.AsReadOnly(canonicalPaths
            .Select((path, ordinal) => new ImportSourceRequest(path, ordinal))
            .ToArray());
    }
}
