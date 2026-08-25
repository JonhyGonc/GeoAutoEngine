using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;

namespace GeoAutoEngine.Domain;

public sealed class SurveyCodeNormalizer
{
    private static readonly IReadOnlyDictionary<string, string> ExactCorrections =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ARRA ALAGADA"] = "AREA ALAGADA",
            ["PONTO HIDRATANTE"] = "PONTO HIDRANTE",
            ["PONTO DE HIDRATANTE"] = "PONTO DE HIDRANTE",
            ["AREA DE DEPOSITO SE NATERIAIS"] = "AREA DE DEPOSITO DE MATERIAIS",
            ["INICIO- ESGOTO"] = "INICIO-ESGOTO",
            ["ESTACAO DE TRATAMENTO"] = "ESTACAO DE TRATAMENTO",
        };

    private static readonly IReadOnlyDictionary<string, string> ExactAliases =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["CURVA AF"] = "CURVA-AF", ["CRUZETA AF"] = "CRUZETA-AF",
            ["CVLT AF"] = "CVLT-AF", ["CX AF"] = "CXAF", ["CX AFG"] = "CXAFG",
            ["CX AP"] = "CXAP", ["CX AP LAC"] = "CXAP-LAC", ["CXAP LAC"] = "CXAP-LAC", ["CX EL"] = "CXEL",
            ["CX EL LAC"] = "CXEL-LAC", ["CX NI"] = "CXNI", ["CX LAC"] = "CXLAC",
            ["CX TL"] = "CXTL", ["CX TEL"] = "CXTL",
            ["CX E AFG"] = "CXE-AFG", ["CXE AFG"] = "CXE-AFG", ["CXEI AFG"] = "CXEI-AFG",
            ["GR AFG"] = "GR-AFG", ["GR ASS"] = "GR-ASS", ["GRELHA AFG"] = "GRELHA-AFG",
            ["GRELHA ASS"] = "GRELHA-ASS", ["BL AFG"] = "BL-AFG", ["BL ASS"] = "BL-ASS",
            ["PI EI"] = "PI-EI", ["PN EL"] = "PN-EL", ["EL SP"] = "EL-SP",
            ["AP SAIDA SP"] = "AP-SAIDA-SP", ["TBQ INC"] = "TBQ-INC", ["INICIA-AF"] = "INICIO-AF",
        };

    private const string CompactPrefixes = "AF|AFT|AP|EI|ES|EL|NI|TEL|TL|FB|GAP";
    private const string ComplementPrefixes = "AFT|AF|AP|EI|ES|E|EL|NI|TEL|TL|FB|GAP";
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);
    private static readonly ConcurrentDictionary<string, Regex> RegexCache = new(StringComparer.Ordinal);

    public string Normalize(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return string.Empty;
        }

        var text = code!.Normalize(NormalizationForm.FormKC).Trim().ToUpperInvariant()
            .Replace('–', '-').Replace('—', '-').Replace('−', '-')
            .Replace('Ø', 'D').Replace('⌀', 'D');
        text = RemoveDiacritics(text);
        text = Replace(text, "(?<=\\d),(?=\\d)", ".");
        text = Replace(text, @"\bDN\s*(?=\d)", "D");
        text = Replace(text, @"\s*\.\s*", ".");
        text = Replace(text, @"\s*-\s*", "-");
        text = Replace(text, @"\s+", " ").Trim();
        text = Replace(text, $@"^({CompactPrefixes})\s+(?=\d)", "$1");
        text = Replace(text, @"\bD\s+(?=\d)", "D");
        text = Replace(text, @"(?<=\d)\s+(?=D\d)", string.Empty);
        text = NormalizeComplementAfterNumber(text);
        text = NormalizeTelephoneEnvelope(text);
        text = NormalizePve(text);

        if (ExactCorrections.TryGetValue(text, out var correction))
        {
            text = correction;
        }

        return ExactAliases.TryGetValue(text, out var alias) ? alias : text;
    }

    public static string RemoveDiacritics(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string NormalizeComplementAfterNumber(string text)
    {
        var match = Match(text, $@"^({ComplementPrefixes})(\d+(?:\.\d+)?)(D\d+)?\s+(.+)$");
        if (!match.Success)
        {
            return text;
        }

        var suffix = Replace(match.Groups[4].Value, @"\s+", "-");
        return $"{match.Groups[1].Value}{match.Groups[2].Value}{match.Groups[3].Value}-{suffix}";
    }

    private static string NormalizeTelephoneEnvelope(string text)
    {
        var match = Match(text, @"^TEL-ENV-(\d+(?:\.\d+)?)-(\d+X\d+)$");
        return match.Success ? $"TEL{match.Groups[1].Value}-ENV-{match.Groups[2].Value}" : text;
    }

    private static string NormalizePve(string text)
    {
        var match = Match(text, @"^PVE\s*(\d+)(?:\s+(AFG|LAC))?$");
        return match.Success
            ? $"PVE{match.Groups[1].Value}{(match.Groups[2].Success ? $"-{match.Groups[2].Value}" : string.Empty)}"
            : text;
    }

    private static Match Match(string input, string pattern) => GetRegex(pattern).Match(input);

    private static string Replace(string input, string pattern, string replacement) =>
        GetRegex(pattern).Replace(input, replacement);

    private static Regex GetRegex(string pattern) => RegexCache.GetOrAdd(
        pattern,
        static value => new Regex(value, RegexOptions.CultureInvariant | RegexOptions.Compiled, RegexTimeout));
}
