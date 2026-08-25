using System.Globalization;
using System.Text;
using GeoAutoEngine.Domain;

namespace GeoAutoEngine.Csv;

internal sealed record CsvColumnResolution(ResolvedColumns Columns, bool UsedCoordinateFallback);

internal static class CsvColumnResolver
{
    private static readonly string[] EastingAliases =
        ["Leste (E)", "Leste", "Este (E)", "Este", "Coord X", "Coordenada X", "Coord E", "Coordenada E", "X", "E", "Easting"];
    private static readonly string[] NorthingAliases =
        ["Norte (N)", "Norte", "Coord Y", "Coordenada Y", "Coord N", "Coordenada N", "Y", "N", "Northing"];
    private static readonly string[] CodeAliases =
        ["Código", "Codigo", "Code", "Cod", "Descrição", "Descricao", "norm_codigo_original", "norm_codigo_normalizado"];
    private static readonly string[] ElevationAliases =
        ["Cota", "Elevação", "Elevacao", "Coord Z", "Coordenada Z", "Z", "Altitude", "Elevation"];

    public static bool ContainsCodeAlias(IReadOnlyList<string> headers) => FindByAlias(headers, CodeAliases) is not null;

    public static CsvColumnResolution Resolve(IReadOnlyList<string> headers, IReadOnlyList<CsvRow> rows)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(rows);
        var code = FindByAlias(headers, CodeAliases) ?? (headers.Count > 1 ? 1 : 0);
        var northing = FindByAlias(headers, NorthingAliases);
        var easting = FindByAlias(headers, EastingAliases);
        var usedFallback = false;

        if (northing is null)
        {
            northing = ValidPositionalColumn(rows, 2) ? 2 : null;
            usedFallback |= northing.HasValue;
        }

        if (easting is null)
        {
            easting = ValidPositionalColumn(rows, 3) ? 3 : null;
            usedFallback |= easting.HasValue;
        }

        if (northing is null || easting is null || northing == easting)
        {
            throw new InvalidOperationException("Não foi possível localizar colunas válidas para Norte/Y e Leste/X.");
        }

        var elevation = FindByAlias(headers, ElevationAliases);
        var columns = headers.Select((name, index) => new KeyValuePair<string, int>(name, index));
        return new CsvColumnResolution(new ResolvedColumns(northing.Value, easting.Value, code, elevation, columns), usedFallback);
    }

    public static string NormalizeName(string value)
    {
        var text = value.Normalize(NormalizationForm.FormKD).ToLowerInvariant();
        var result = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(character))
            {
                result.Append(character);
            }
        }

        return result.ToString();
    }

    private static int? FindByAlias(IReadOnlyList<string> headers, IEnumerable<string> aliases)
    {
        var indexes = headers.Select((name, index) => (Name: NormalizeName(name), Index: index))
            .GroupBy(item => item.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Index, StringComparer.Ordinal);
        foreach (var alias in aliases)
        {
            if (indexes.TryGetValue(NormalizeName(alias), out var index))
            {
                return index;
            }
        }

        return null;
    }

    private static bool ValidPositionalColumn(IReadOnlyList<CsvRow> rows, int index)
    {
        var present = rows.Where(row => index < row.Values.Count)
            .Select(row => row.Values[index])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
        if (present.Length == 0)
        {
            return false;
        }

        var numbers = present.Select(SurveyNumberParser.TryParse).Where(value => value.HasValue).Select(value => value!.Value).ToArray();
        return (double)numbers.Length / present.Length >= 0.60
            && numbers.Length > 0
            && (double)numbers.Count(number => Math.Abs(number) <= 1_000_000_000d) / numbers.Length >= 0.80;
    }
}

internal static class SurveyNumberParser
{
    public static double? TryParse(string? value)
    {
        var text = value?.Trim().Replace("\u00a0", string.Empty, StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal)
            ?? string.Empty;
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        if (text.Contains(',', StringComparison.Ordinal) && text.Contains('.', StringComparison.Ordinal))
        {
            text = text.LastIndexOf(",", StringComparison.Ordinal) > text.LastIndexOf(".", StringComparison.Ordinal)
                ? text.Replace(".", string.Empty, StringComparison.Ordinal).Replace(',', '.')
                : text.Replace(",", string.Empty, StringComparison.Ordinal);
        }
        else if (text.Contains(',', StringComparison.Ordinal))
        {
            text = text.Replace(',', '.');
        }
        else if (text.Count(character => character == '.') > 1)
        {
            var parts = text.Split('.');
            if (parts.Skip(1).All(part => part.Length == 3))
            {
                text = string.Concat(parts);
            }
        }

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && NumberCompatibility.IsFinite(number)
            ? number
            : null;
    }
}
