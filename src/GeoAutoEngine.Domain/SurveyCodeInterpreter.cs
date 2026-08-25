using System.Text.RegularExpressions;
using System.Collections.Concurrent;

namespace GeoAutoEngine.Domain;

public sealed class SurveyCodeInterpreter
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);
    private static readonly ConcurrentDictionary<string, Regex> RegexCache = new(StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<string, string> Networks =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["AF"] = "Água potável", ["AFT"] = "Água potável", ["AP"] = "Águas pluviais",
            ["E"] = "Esgoto sanitário", ["ES"] = "Esgoto sanitário", ["EI"] = "Esgoto industrial",
            ["EL"] = "Rede elétrica", ["NI"] = "Não identificada", ["TEL"] = "Telefone",
            ["TL"] = "Telefone", ["FB"] = "Fibra óptica", ["TBQ"] = "Rede de incêndio",
            ["GAP"] = "Outra",
        };

    private static readonly string[] Materials = ["PVC", "PEAD", "FOFO", "ACO"];

    private static readonly HashSet<string> IgnoredCodes = new(StringComparer.Ordinal)
    {
        "GUIA", "GUI", "G", "RUA", "ESTRADA", "ALCR", "ALAMBRADO", "GRADIL", "MURETA",
        "PORTAO", "CERCA", "CERCADO", "MR", "CN", "RAMPA", "ROTATORIA", "CANALETA", "VALA",
        "AREA ALAGADA", "AREA ALAGADA COM IRREGULARIDADES", "AREA COM IRREGULARIDADES",
        "AREA COM TUBULACOES", "AREA COM TUBULACOES APARENTES", "AREA COM VAZAMENTO DE AGUA",
        "AREA DE ARMAZENAMENTO DE MATERIAIS", "AREA DE CACAMBA", "AREA DE DEPOSITO DE MATERIAIS",
        "AREA DE IRREGULARIDADES", "AREA DE MATERIAIS", "AREA ISOLADA", "AREA VERDE",
        "CAMINHO SEGURO", "ATERRO", "OBSTRUCAO", "ADIAMENTO", "ALIAMENTO", "DESCARTE DE MATERIAL",
        "DEPOSITO DE MATERIAIS", "MATERIAIS", "JARDIM", "POSSIVEL ENVELOPE", "TUBULACAO APARENTE",
        "REDE CAPEADA", "MINA", "CONTAINER", "GALPAO", "CACAMBA", "TANQUE", "ESTEIRA", "USINAGEM",
        "ESCADA", "LAVA BOTA", "PLATAFORMA DE TUBULACOES", "PLATAFORMA ELEVATORIA",
        "GAIOLA DE MATERIAIS", "GAIOLA MATERIAIS", "ESTRUTURA DE CONCRETO", "CONTENCAO",
        "CANTEIRO SONDAGEM", "PILAR", "TAF", "SAIDA",
    };

    private static readonly HashSet<string> FieldObservations = new(StringComparer.Ordinal)
    {
        "CAMINHO SEGURO", "ATERRO", "OBSTRUCAO", "ADIAMENTO", "ALIAMENTO", "DESCARTE DE MATERIAL",
        "DEPOSITO DE MATERIAIS", "MATERIAIS", "JARDIM", "POSSIVEL ENVELOPE", "TUBULACAO APARENTE",
        "REDE CAPEADA", "MINA", "SAIDA",
    };

    private static readonly (string Pattern, string Network, string Rule)[] ObjectsWithInformation =
    [
        (@"^AFCAP(?<p>\d+(?:\.\d+)?)$", "Água potável", "AFCAP_PROF"),
        (@"^CXAP(?<p>\d+(?:\.\d+)?)$", "Águas pluviais", "CXAP_PROF"),
        (@"^CXEL(?<p>\d+(?:\.\d+)?)$", "Rede elétrica", "CXEL_PROF"),
        (@"^CX(?<p>\d+(?:\.\d+)?)$", "Não identificada", "CX_PROF"),
        (@"^CAP(?<p>\d+(?:\.\d+)?)$", "Não identificada", "CAP_PROF"),
        (@"^PVENCOBERTO-(?<p>\d+(?:\.\d+)?)$", "Esgoto sanitário", "PV_ENCOBERTO_PROF"),
    ];

    private static readonly (string Pattern, string Network, string Rule)[] ExactObjects =
    [
        (@"^APL$", "Águas pluviais", "APL_LEGADO"), (@"^ESI$", "Esgoto industrial", "ESI_LEGADO"),
        (@"^ESA$", "Esgoto sanitário", "ESA_LEGADO"), (@"^ELE$", "Rede elétrica", "ELE_LEGADO"),
        (@"^TEL$", "Telefone", "TEL_LEGADO"), (@"^PVTL$", "Telefone", "PV_TEL_LEGADO"),
        (@"^PV-AF-ENCOBERTO$", "Água potável", "PV_AF_ENCOBERTO"), (@"^PVAFG?$", "Água potável", "PV_AF"),
        (@"^PVAP\d*$", "Águas pluviais", "PV_AP"), (@"^PVES$", "Esgoto sanitário", "PV_ES"),
        (@"^PVE(?:\d+(?:-(?:AFG|LAC))?)?$", "Esgoto sanitário", "PVE"),
        (@"^PV-ENCOBERTO$", "Não identificada", "PV_ENCOBERTO"), (@"^PV$", "Não identificada", "PV_GENERICO"),
        (@"^CXAP(?:-EL|-LAC|\d+)?$", "Águas pluviais", "CX_AP"),
        (@"^CXEL(?:-LAC| ALTA TENSAO)?$", "Rede elétrica", "CX_EL"),
        (@"^CX(?:TL|TEL)(?:-LAC)?$", "Telefone", "CX_TEL"),
        (@"^(?:CXEI(?:-AFG)?|CXEIAFG)$", "Esgoto industrial", "CX_EI"),
        (@"^CXES(?:AFG)?$", "Esgoto sanitário", "CX_ES"),
        (@"^(?:CXAF|CXAFG|CX CAVALETE)$", "Água potável", "CX_AF"),
        (@"^CXNI$", "Não identificada", "CX_NI"),
        (@"^CX (?:VALVULA|VALVULA DE INCENDIO)$", "Rede de incêndio", "CX_VALVULA_INC"),
        (@"^CX GORDURA$", "Esgoto sanitário", "CX_GORDURA"),
        (@"^CXE(?:-AFG| REDONDA|\d+(?: ASS| AFG)?)?$", "Esgoto sanitário", "CX_E"),
        (@"^CXLAC\d*$", "Outra", "CX_LAC"),
        (@"^CX(?:$| ASS| COM LIXO| DE CONTENCAO| MACRO)$", "Não identificada", "CX_GENERICA"),
        (@"^CXCEGA$", "Não identificada", "CX_CEGA"), (@"^RG$", "Água potável", "REGISTRO_AF"),
        (@"^CAP-AF$", "Água potável", "CAP_AF"), (@"^CAP-NI$", "Não identificada", "CAP_NI"),
        (@"^CURVA-AF$", "Água potável", "CURVA_AF"), (@"^CRUZETA-AF$", "Água potável", "CRUZETA_AF"),
        (@"^CVLT-AF$", "Água potável", "CVLT_AF"),
        (@"^(?:GR|GRELHA)(?:-(?:AFG|ASS))?$", "Águas pluviais", "GRELHA"),
        (@"^BL(?:-(?:AFG|ASS))?$", "Águas pluviais", "BOCA_LOBO"),
        (@"^PI-EI$", "Esgoto industrial", "PI_EI"), (@"^PI\d+(?:\.\d+)?$", "Não identificada", "PI"),
        (@"^PTIL$", "Rede elétrica", "PTIL"), (@"^PT$", "Rede elétrica", "PT"),
        (@"^PONTO (?:DE )?(?:HIDRANTE|INCENDIO)$", "Rede de incêndio", "HIDRANTE"),
        (@"^RALO$", "Águas pluviais", "RALO"), (@"^SUBESTACAO$", "Rede elétrica", "SUBESTACAO"),
        (@"^TRANSFORMADOR$", "Rede elétrica", "TRANSFORMADOR"), (@"^PAINEL ELETRONICO$", "Rede elétrica", "PAINEL"),
        (@"^BOMBA$", "Outra", "BOMBA"), (@"^COMPORTA(?: DE EFLUENTES)?$", "Esgoto industrial", "COMPORTA"),
        (@"^CASA DE BOMBAS$", "Outra", "CASA_BOMBAS"),
        (@"^TANQUE DE EFLUENTES INDUSTRIAL$", "Esgoto industrial", "TANQUE_EI"),
        (@"^ESTACAO DE TRATAMENTO$", "Esgoto industrial", "ETE"), (@"^GAS$", "Gás", "GAS"),
        (@"^(?:CASA GAS|GAIOLA DE GAS|GAIOLA DE COMBUSTIVEIS)$", "Gás", "ESTRUTURA_GAS"),
        (@"^PN-EL$", "Rede elétrica", "PN_EL"), (@"^POCO$", "Não identificada", "POCO"),
    ];

    private static readonly (string Pattern, string Network, string Rule)[] FlexibleBlocks =
    [
        (@"^PVENCOBERTO(?:-?AFG)?(?:$|[^A-Z])", "Esgoto sanitário", "PV_ENCOBERTO_PROF"),
        (@"^PVES(?:-?AFG)?(?:$|[^A-Z])", "Esgoto sanitário", "PV_ES"),
        (@"^PVE(?:\d+)?(?:-?(?:AFG|LAC))?(?:$|[^A-Z])", "Esgoto sanitário", "PVE"),
        (@"^PVAP\d*(?:-?AFG)?(?:$|[^A-Z])", "Águas pluviais", "PV_AP"),
        (@"^PVTL(?:-?AFG)?(?:$|[^A-Z])", "Telefone", "PV_TEL_LEGADO"),
        (@"^POSTE(?:$|[^A-Z])", "Rede elétrica", "POSTE"),
        (@"^PTIL(?:$|[^A-Z])", "Rede elétrica", "PTIL"), (@"^PT(?:$|[^A-Z])", "Rede elétrica", "PT"),
        (@"^RG(?:$|[^A-Z])", "Água potável", "REGISTRO_AF"),
        (@"^RE(?:$|[^A-Z])", "Água potável", "REGISTRO_AF"),
        (@"^(?:PONTO (?:DE )?)?HIDRANTE(?:$|[^A-Z])", "Rede de incêndio", "HIDRANTE"),
        (@"^PONTO (?:DE )?INCENDIO(?:$|[^A-Z])", "Rede de incêndio", "HIDRANTE"),
        (@"^PV(?:$|[^A-Z])", "Não identificada", "PV_GENERICO"),
    ];

    private static readonly IReadOnlyDictionary<string, (string Network, string Rule)> TextsWithoutDepth =
        new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["AF"] = ("Água potável", "AF_SEM_PROF"), ["AF MACRO"] = ("Água potável", "AF_DESCRICAO"),
            ["AF-DESCARGA"] = ("Água potável", "AF_DESCRICAO"), ["AF SUBIDA D50"] = ("Água potável", "AF_DESCRICAO"),
            ["AP-SAIDA-SP"] = ("Águas pluviais", "AP_DESCRICAO"), ["EL-SP"] = ("Rede elétrica", "EL_DESCRICAO"),
            ["INICIO-ESGOTO"] = ("Esgoto sanitário", "INICIO_ES"), ["INICIO-AF"] = ("Água potável", "INICIO_AF"),
            ["QUEDA-EL"] = ("Rede elétrica", "QUEDA_EL"), ["TBQ-INC"] = ("Rede de incêndio", "TBQ_INC"),
        };

    public SurveyInterpretation Interpret(string originalCode, string normalizedCode)
    {
        ArgumentNullException.ThrowIfNull(originalCode);
        ArgumentNullException.ThrowIfNull(normalizedCode);
        if (normalizedCode.Length == 0)
        {
            return Result(originalCode, normalizedCode, "Vazio", "Ignorar", "Outra", false, "baixa", "VAZIO", "Código vazio ou ausente.");
        }

        var result = ClassifyBoundary(originalCode, normalizedCode)
            ?? ClassifyFlexibleBlock(originalCode, normalizedCode)
            ?? ClassifyObjectWithInformation(originalCode, normalizedCode);
        if (result is not null)
        {
            return result;
        }

        var possibleBox = Match(normalizedCode, @"^POSS-CX(?<p>\d+(?:\.\d+)?)$");
        if (possibleBox.Success)
        {
            return Result(originalCode, normalizedCode, "Informação/indício de campo", "Texto", "Não identificada",
                confidence: "média", rule: "POSS_CX", observation: "Possível caixa; exige revisão antes de inserir bloco definitivo.",
                depth: ParseDouble(possibleBox.Groups["p"].Value));
        }

        result = ClassifyExactObject(originalCode, normalizedCode);
        if (result is not null)
        {
            return result;
        }

        if (normalizedCode.StartsWith("CX", StringComparison.Ordinal))
        {
            return Result(
                originalCode,
                normalizedCode,
                "Objeto físico",
                "Bloco",
                "Não identificada",
                confidence: "média",
                rule: "CX_GENERICA",
                observation: "Caixa sem rede específica; reconstruir a geometria no layer de rede não identificada.");
        }

        result = ClassifyNetwork(originalCode, normalizedCode);
        if (result is not null)
        {
            return result;
        }

        if (IsMatch(normalizedCode, @"^(?:BASE|XDRONE|SW\d+F?|MARCO(?:-\w+|\d+)?|ERG\d+|A\d+)$"))
        {
            return Result(originalCode, normalizedCode, "Ponto de controle/levantamento", "Ignorar", "Outra", rule: "CONTROLE_TOPOGRAFICO");
        }

        if (IsMatch(normalizedCode, @"^ESTACAO E\d+$"))
        {
            return Result(originalCode, normalizedCode, "Ponto de controle/levantamento", "Ignorar", "Outra", rule: "ESTACAO_CONTROLE");
        }

        if (IgnoredCodes.Contains(normalizedCode) || normalizedCode.StartsWith("AREA ", StringComparison.Ordinal))
        {
            var nature = normalizedCode.StartsWith("AREA ", StringComparison.Ordinal) || FieldObservations.Contains(normalizedCode)
                ? "Observação de campo"
                : "Elemento topográfico fora do escopo";
            return Result(originalCode, normalizedCode, nature, "Ignorar", "Outra", rule: "FORA_ESCOPO");
        }

        if (IsMatch(normalizedCode, @"^MATACAO\d+(?:\.\d+)?$"))
        {
            return Result(originalCode, normalizedCode, "Objeto físico fora do escopo", "Ignorar", "Outra",
                rule: "MATACAO", depth: FirstDecimal(normalizedCode));
        }

        return Result(originalCode, normalizedCode, "A confirmar", "Revisar", "Não identificada", false, "baixa",
            "DESCONHECIDO", "Código não coberto pelas regras atuais; enviar para a fila de revisão.");
    }

    private static SurveyInterpretation? ClassifyBoundary(string original, string code)
    {
        var match = Match(code, @"^(?:DIVISA|DV)(?:\s+(.+))?$");
        if (!match.Success || !match.Groups[1].Success)
        {
            return null;
        }

        var complement = Replace(match.Groups[1].Value, @"\s+", " ").Trim();
        var compact = Replace(complement, @"[\s/.-]+", string.Empty);
        var text = compact is "SN" or "SEMNUMERO" ? "SN" : char.IsDigit(complement[0]) ? $"Nº{complement}" : complement;
        return Result(original, code, "Anotação de divisa", "Texto", "Outra", rule: "ANOTACAO_DIVISA",
            observation: "Texto de divisa preparado para o layer -Texto, sem interpretação como profundidade.", complement: text);
    }

    private static SurveyInterpretation? ClassifyFlexibleBlock(string original, string code)
    {
        if (IsMatch(code, @"^POSSIVEL(?:\s+DE)?\s+PV(?:E)?(?:$|[^A-Z])"))
        {
            return Result(original, code, "Informação/indício de campo", "Bloco", "Não identificada",
                confidence: "média", rule: "POSSIVEL_PV",
                observation: "Possível PV inserido como bloco genérico e marcado como ponto de atenção.");
        }

        foreach (var entry in FlexibleBlocks)
        {
            if (IsMatch(code, entry.Pattern))
            {
                return Result(original, code, "Objeto físico", "Bloco", entry.Network, rule: entry.Rule,
                    observation: "Bloco identificado pelo código inicial; informações complementares foram ignoradas para geração.");
            }
        }

        return null;
    }

    private static SurveyInterpretation? ClassifyObjectWithInformation(string original, string code)
    {
        foreach (var entry in ObjectsWithInformation)
        {
            var match = Match(code, entry.Pattern);
            if (match.Success)
            {
                return Result(original, code, "Objeto físico com informação", "Texto + Bloco", entry.Network,
                    rule: entry.Rule, depth: ParseDouble(match.Groups["p"].Value));
            }
        }

        return null;
    }

    private static SurveyInterpretation? ClassifyExactObject(string original, string code)
    {
        foreach (var entry in ExactObjects)
        {
            if (IsMatch(code, entry.Pattern))
            {
                return Result(original, code, "Objeto físico", "Bloco", entry.Network, rule: entry.Rule);
            }
        }

        return null;
    }

    private static SurveyInterpretation? ClassifyNetwork(string original, string code)
    {
        if (TextsWithoutDepth.TryGetValue(code, out var text))
        {
            return Result(original, code, "Informação da rede", "Texto", text.Network, rule: text.Rule,
                diameter: Diameter(code), complement: TextComplement(code));
        }

        var match = Match(code, @"^AFVISIVELD(?<d>\d+)(?<mat>PVC|PEAD|FOFO|ACO)?$");
        if (match.Success)
        {
            return Result(original, code, "Informação da rede", "Texto", "Água potável", rule: "AF_VISIVEL",
                diameter: int.Parse(match.Groups["d"].Value, System.Globalization.CultureInfo.InvariantCulture),
                material: match.Groups["mat"].Success ? match.Groups["mat"].Value : null);
        }

        match = Match(code, @"^APD(?<d>\d+)APARENTE$");
        if (match.Success)
        {
            return Result(original, code, "Informação da rede", "Texto", "Águas pluviais", rule: "AP_APARENTE",
                diameter: ParseInt(match, "d"), complement: "APARENTE");
        }

        match = Match(code, @"^APGSD(?<d>\d+)$");
        if (match.Success)
        {
            return Result(original, code, "Informação da rede", "Texto", "Águas pluviais", confidence: "média",
                rule: "AP_GSD", diameter: ParseInt(match, "d"), complement: "GSD");
        }

        match = Match(code, @"^TBQD(?<d>\d+)$");
        if (match.Success)
        {
            return Result(original, code, "Informação da rede", "Texto", "Rede de incêndio", confidence: "média",
                rule: "TBQ_DIAM", diameter: ParseInt(match, "d"));
        }

        match = Match(code, @"^TEL(?<p>\d+(?:\.\d+)?)(?:-ENV-(?<sec>\d+X\d+)|-(?<sub>SUB|SUBIDA))?$");
        if (match.Success)
        {
            var complement = match.Groups["sec"].Success ? $"ENV-{match.Groups["sec"].Value}"
                : match.Groups["sub"].Success ? match.Groups["sub"].Value : null;
            return Result(original, code, "Informação da rede", "Texto", "Telefone", rule: "TEL",
                depth: ParseDouble(match.Groups["p"].Value), complement: complement);
        }

        match = Match(code, @"^(?<prefix>AFT|AF|AP|EI|ES|E|EL|NI|TL|FB|GAP)(?<p>\d+(?:\.\d+)?)(?:D(?<d>\d+))?(?<rest>[A-Z0-9-]*)$");
        if (!match.Success)
        {
            return null;
        }

        var prefix = match.Groups["prefix"].Value;
        var rest = match.Groups["rest"].Value;
        string? material = null;
        var complementValue = rest.Trim('-');
        foreach (var candidate in Materials)
        {
            if (!rest.Contains(candidate, StringComparison.Ordinal))
            {
                continue;
            }

            material = candidate;
            complementValue = rest.Replace(candidate, string.Empty, StringComparison.Ordinal).Trim('-');
            break;
        }

        return Result(original, code, "Informação da rede", "Texto", Networks[prefix],
            confidence: prefix is "AFT" or "GAP" ? "média" : "alta", rule: $"REDE_{prefix}",
            depth: ParseDouble(match.Groups["p"].Value),
            diameter: match.Groups["d"].Success ? ParseInt(match, "d") : null,
            material: material, complement: complementValue.Length == 0 ? null : complementValue);
    }

    private static SurveyInterpretation Result(
        string original,
        string normalized,
        string nature,
        string recordType,
        string network,
        bool recognized = true,
        string confidence = "alta",
        string rule = "",
        string observation = "",
        double? depth = null,
        double? diameter = null,
        string? material = null,
        string? complement = null) =>
        new(network, rule, material, diameter, depth, nature, recordType, recognized, confidence, rule,
            observation, complement, original, normalized);

    private static double? FirstDecimal(string value)
    {
        var match = Match(value, @"\d+(?:\.\d+)?");
        return match.Success ? ParseDouble(match.Value) : null;
    }

    private static double ParseDouble(string value) =>
        double.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

    private static int ParseInt(Match match, string group) =>
        int.Parse(match.Groups[group].Value, System.Globalization.CultureInfo.InvariantCulture);

    private static double? Diameter(string value)
    {
        var match = Match(value, @"D(\d+)");
        return match.Success ? ParseDouble(match.Groups[1].Value) : null;
    }

    private static string? TextComplement(string value)
    {
        var separator = value.IndexOfAny(['-', ' ']);
        return separator >= 0 ? value.Substring(separator + 1) : null;
    }

    private static bool IsMatch(string input, string pattern) => Match(input, pattern).Success;

    private static Match Match(string input, string pattern) => GetRegex(pattern).Match(input);

    private static string Replace(string input, string pattern, string replacement) =>
        GetRegex(pattern).Replace(input, replacement);

    private static Regex GetRegex(string pattern) => RegexCache.GetOrAdd(
        pattern,
        static value => new Regex(value, RegexOptions.CultureInvariant | RegexOptions.Compiled, RegexTimeout));
}
