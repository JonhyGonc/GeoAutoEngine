using System.Security.Cryptography;
using GeoAutoEngine.Application;
using GeoAutoEngine.Domain;

namespace GeoAutoEngine.Csv;

public sealed class CsvSourceImporter : ISourceImporter
{
    private readonly SurveyCodeNormalizer _normalizer;
    private readonly SurveyCodeInterpreter _interpreter;

    public CsvSourceImporter(SurveyCodeNormalizer? normalizer = null, SurveyCodeInterpreter? interpreter = null)
    {
        _normalizer = normalizer ?? new SurveyCodeNormalizer();
        _interpreter = interpreter ?? new SurveyCodeInterpreter();
    }

    public async ValueTask<SourceImportResult> ImportAsync(
        ImportSourceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var path = Path.GetFullPath(request.CanonicalPath);
        if (!File.Exists(path))
        {
            return FailedSource(request, path, "CSV_SOURCE_NOT_FOUND", $"Arquivo CSV não encontrado: {path}");
        }

        byte[] bytes;
        try
        {
            bytes = await ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return FailedSource(request, path, "CSV_SOURCE_UNREADABLE", $"Não foi possível ler o CSV: {path}. {exception.Message}");
        }

        var hash = ComputeSha256(bytes);
        var decoded = CsvParser.Decode(bytes);
        var delimiter = CsvParser.DetectDelimiter(decoded.Text);
        IReadOnlyList<CsvRow> parsedRows;
        try
        {
            parsedRows = CsvParser.Parse(decoded.Text, delimiter);
        }
        catch (FormatException exception)
        {
            return FailedSource(request, path, "CSV_MALFORMED", exception.Message, hash);
        }

        if (parsedRows.Count == 0)
        {
            return FailedSource(request, path, "CSV_EMPTY_SOURCE", $"O CSV está vazio: {path}", hash);
        }

        var hasHeader = CsvColumnResolver.ContainsCodeAlias(parsedRows[0].Values);
        var dataRows = (hasHeader ? parsedRows.Skip(1) : parsedRows).ToArray();
        var width = Math.Max(parsedRows.Max(row => row.Values.Count), hasHeader ? parsedRows[0].Values.Count : 0);
        var headers = hasHeader ? MakeUniqueHeaders(parsedRows[0].Values, width) : MakeHeaderlessHeaders(width);

        CsvColumnResolution resolution;
        try
        {
            resolution = CsvColumnResolver.Resolve(headers, dataRows);
        }
        catch (InvalidOperationException exception)
        {
            return FailedSource(request, path, "CSV_COLUMNS_UNRESOLVED", exception.Message, hash);
        }

        var displayName = Path.GetFileName(path);
        var sourceId = SourceFileId.From(path, hash);
        var source = new SourceFile(
            sourceId,
            request.SelectionOrdinal,
            displayName,
            path,
            hash,
            decoded.EncodingName,
            delimiter.ToString(),
            resolution.Columns,
            dataRows.Length);

        var imported = new List<SurveyRecord>(dataRows.Length);
        var rejected = new List<RejectedRecord>();
        var diagnostics = new List<Diagnostic>();
        if (!hasHeader)
        {
            diagnostics.Add(SourceDiagnostic("CSV_HEADERLESS", "CSV sem cabeçalho reconhecido; aplicado contrato posicional legado.", source));
        }

        if (resolution.UsedCoordinateFallback)
        {
            diagnostics.Add(SourceDiagnostic("CSV_POSITIONAL_COLUMNS", "Colunas de coordenadas resolvidas pelo fallback posicional validado.", source));
        }

        for (var index = 0; index < dataRows.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = dataRows[index];
            var origin = new RecordOrigin(sourceId, displayName, index, row.PhysicalLineNumber, index);
            var raw = RawValues(headers, row.Values);
            var originalCode = ValueAt(row, resolution.Columns.Code);
            var normalizedCode = _normalizer.Normalize(originalCode);
            var east = SurveyNumberParser.TryParse(ValueAt(row, resolution.Columns.Easting));
            var north = SurveyNumberParser.TryParse(ValueAt(row, resolution.Columns.Northing));
            var elevation = resolution.Columns.Elevation.HasValue
                ? SurveyNumberParser.TryParse(ValueAt(row, resolution.Columns.Elevation.Value))
                : 0d;
            var reasons = new List<string>();

            if (normalizedCode.Length == 0)
            {
                reasons.Add("Código vazio ou ausente.");
                diagnostics.Add(RecordDiagnostic("CSV_EMPTY_CODE", "Código vazio ou ausente.", DiagnosticSeverity.Error, source, origin));
            }

            if (!east.HasValue || !north.HasValue || !elevation.HasValue)
            {
                reasons.Add("Coordenadas inválidas ou ausentes.");
                diagnostics.Add(RecordDiagnostic("CSV_INVALID_COORDINATES", "Coordenadas inválidas ou ausentes.", DiagnosticSeverity.Error, source, origin));
            }

            if (reasons.Count > 0)
            {
                rejected.Add(new RejectedRecord(origin, raw, reasons));
                continue;
            }

            var interpretation = _interpreter.Interpret(originalCode, normalizedCode);
            var record = new SurveyRecord(
                SurveyRecordId.From(sourceId, index),
                origin,
                raw,
                new SurveyPoint(east!.Value, north!.Value, elevation!.Value),
                normalizedCode,
                interpretation);
            imported.Add(record);
            if (!interpretation.Recognized)
            {
                diagnostics.Add(RecordDiagnostic(
                    "CSV_UNKNOWN_CODE",
                    $"Código desconhecido: {originalCode}{Environment.NewLine}Arquivo: {displayName}{Environment.NewLine}Linha: {row.PhysicalLineNumber}",
                    DiagnosticSeverity.Warning,
                    source,
                    origin));
            }
        }

        return new SourceImportResult(source, imported, rejected, diagnostics);
    }

    private static IReadOnlyList<string> MakeUniqueHeaders(IReadOnlyList<string> source, int width)
    {
        var headers = new List<string>(width);
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < width; index++)
        {
            var baseName = index < source.Count ? source[index].Trim() : string.Empty;
            if (baseName.Length == 0)
            {
                baseName = $"Coluna_{index}";
            }

            counts.TryGetValue(baseName, out var count);
            counts[baseName] = count + 1;
            headers.Add(count == 0 ? baseName : $"{baseName}_{count + 1}");
        }

        return headers.AsReadOnly();
    }

    private static IReadOnlyList<string> MakeHeaderlessHeaders(int width)
    {
        var headers = Enumerable.Range(0, width).Select(index => $"Coluna_{index}").ToArray();
        headers[Math.Min(1, width - 1)] = "Código";
        return Array.AsReadOnly(headers);
    }

    private static IReadOnlyDictionary<string, string?> RawValues(IReadOnlyList<string> headers, IReadOnlyList<string> values) =>
        headers.Select((header, index) => new KeyValuePair<string, string?>(header, index < values.Count ? values[index] : null))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

    private static string ValueAt(CsvRow row, int index) => index < row.Values.Count ? row.Values[index] : string.Empty;

    private static Diagnostic SourceDiagnostic(string code, string message, SourceFile source) =>
        new(code, message, DiagnosticSeverity.Information, DiagnosticScope.Source, source.Id);

    private static Diagnostic RecordDiagnostic(
        string code,
        string message,
        DiagnosticSeverity severity,
        SourceFile source,
        RecordOrigin origin) =>
        new(code, message, severity, DiagnosticScope.Record, source.Id, [origin]);

    private static SourceImportResult FailedSource(
        ImportSourceRequest request,
        string path,
        string diagnosticCode,
        string message,
        string contentSha256 = "")
    {
        var identityHash = contentSha256.Length == 64 ? contentSha256 : new string('0', 64);
        var source = new SourceFile(
            SourceFileId.From(path, identityHash),
            request.SelectionOrdinal,
            Path.GetFileName(path),
            path,
            contentSha256,
            string.Empty,
            string.Empty,
            null,
            0,
            SourceImportStatus.Rejected);
        var diagnostic = new Diagnostic(
            diagnosticCode,
            message,
            DiagnosticSeverity.Error,
            DiagnosticScope.Source,
            source.Id);
        return new SourceImportResult(source, [], [], [diagnostic]);
    }

    private static async Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        if (stream.Length > int.MaxValue)
        {
            throw new IOException("O arquivo CSV excede o tamanho suportado.");
        }

        var bytes = new byte[(int)stream.Length];
        var offset = 0;
        while (offset < bytes.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = await stream.ReadAsync(bytes, offset, bytes.Length - offset, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException("O CSV terminou antes do tamanho informado pelo sistema de arquivos.");
            }

            offset += read;
        }

        return bytes;
    }

    private static string ComputeSha256(byte[] bytes)
    {
        using var sha256 = SHA256.Create();
        return string.Concat(sha256.ComputeHash(bytes).Select(value => value.ToString("x2")));
    }
}
