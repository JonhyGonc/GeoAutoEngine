using System.Text;

namespace GeoAutoEngine.Csv;

internal sealed record CsvRow(IReadOnlyList<string> Values, int PhysicalLineNumber);

internal sealed record DecodedCsv(string Text, string EncodingName);

internal static class CsvParser
{
    private static readonly char[] DelimiterCandidates = [';', ',', '\t', '|'];

    static CsvParser()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static DecodedCsv Decode(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf)
        {
            return new DecodedCsv(new UTF8Encoding(true, true).GetString(bytes, 3, bytes.Length - 3), "utf-8-sig");
        }

        try
        {
            return new DecodedCsv(new UTF8Encoding(false, true).GetString(bytes), "utf-8");
        }
        catch (DecoderFallbackException)
        {
            try
            {
                return new DecodedCsv(Encoding.GetEncoding(1252, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback).GetString(bytes), "cp1252");
            }
            catch (DecoderFallbackException)
            {
                return new DecodedCsv(Encoding.GetEncoding(28591).GetString(bytes), "latin1");
            }
        }
    }

    public static char DetectDelimiter(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var candidates = DelimiterCandidates
            .Select(delimiter => (Delimiter: delimiter, Score: ScoreDelimiter(text, delimiter)))
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => Array.IndexOf(DelimiterCandidates, candidate.Delimiter))
            .ToArray();
        return candidates[0].Score > 0 ? candidates[0].Delimiter : ';';
    }

    public static IReadOnlyList<CsvRow> Parse(string text, char delimiter)
    {
        ArgumentNullException.ThrowIfNull(text);
        var rows = new List<CsvRow>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var line = 1;
        var rowLine = 1;

        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (character == '"')
            {
                if (inQuotes && index + 1 < text.Length && text[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (!inQuotes && character == delimiter)
            {
                fields.Add(field.ToString());
                field.Clear();
                continue;
            }

            if (character is '\r' or '\n')
            {
                if (character == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                {
                    index++;
                }

                if (inQuotes)
                {
                    field.Append('\n');
                    line++;
                    continue;
                }

                fields.Add(field.ToString());
                field.Clear();
                AddRow(rows, fields, rowLine);
                fields.Clear();
                line++;
                rowLine = line;
                continue;
            }

            field.Append(character);
        }

        if (inQuotes)
        {
            throw new FormatException($"Campo CSV com aspas não encerradas, iniciado na linha {rowLine}.");
        }

        if (field.Length > 0 || fields.Count > 0)
        {
            fields.Add(field.ToString());
            AddRow(rows, fields, rowLine);
        }

        return rows.AsReadOnly();
    }

    private static int ScoreDelimiter(string text, char delimiter)
    {
        IReadOnlyList<CsvRow> rows;
        try
        {
            rows = Parse(text, delimiter).Take(25).ToArray();
        }
        catch (FormatException)
        {
            return 0;
        }

        if (rows.Count == 0)
        {
            return 0;
        }

        var mode = rows.GroupBy(row => row.Values.Count)
            .OrderByDescending(group => group.Count())
            .ThenByDescending(group => group.Key)
            .First();
        return mode.Key <= 1 ? 0 : (mode.Count() * 1000) + (mode.Key * 10) - (rows.Count - mode.Count());
    }

    private static void AddRow(ICollection<CsvRow> rows, IReadOnlyList<string> fields, int line)
    {
        if (fields.Any(value => !string.IsNullOrWhiteSpace(value)))
        {
            rows.Add(new CsvRow(Array.AsReadOnly(fields.ToArray()), line));
        }
    }
}
