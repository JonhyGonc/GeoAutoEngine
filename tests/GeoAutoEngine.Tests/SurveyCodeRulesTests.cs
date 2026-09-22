using GeoAutoEngine.Domain;

namespace GeoAutoEngine.Tests;

public sealed class SurveyCodeRulesTests
{
    private readonly SurveyCodeNormalizer _normalizer = new();
    private readonly SurveyCodeInterpreter _interpreter = new();

    [Theory]
    [Trait("Category", "FAST")]
    [InlineData("AF0,95 Ø250", "AF0.95D250", "Água potável", "Texto", "REDE_AF", 0.95, 250.0)]
    [InlineData("CXAP0.60", "CXAP0.60", "Águas pluviais", "Texto + Bloco", "CXAP_PROF", 0.60, null)]
    [InlineData("TEL-ENV-2.00-60X40", "TEL2.00-ENV-60X40", "Telefone", "Texto", "TEL", 2.00, null)]
    public void NormalizeAndInterpret_PreservesCoreNetworkRules(
        string original,
        string normalized,
        string network,
        string recordType,
        string rule,
        double depth,
        double? diameter)
    {
        var actualCode = _normalizer.Normalize(original);
        var result = _interpreter.Interpret(original, actualCode);

        Assert.Equal(normalized, actualCode);
        Assert.Equal(network, result.Network);
        Assert.Equal(recordType, result.RecordType);
        Assert.Equal(rule, result.Rule);
        Assert.Equal(depth, result.Depth);
        Assert.Equal(diameter, result.Diameter);
        Assert.True(result.Recognized);
    }

    [Theory]
    [Trait("Category", "FAST")]
    [InlineData("CXTL0,80", "CXTL0.80", "Telefone", "Texto + Bloco", "CXTL_PROF", 0.80)]
    [InlineData("CX TL 0,80", "CXTL0.80", "Telefone", "Texto + Bloco", "CXTL_PROF", 0.80)]
    [InlineData("CX TEL 0.80 LAC", "CXTL0.80-LAC", "Telefone", "Texto + Bloco", "CXTL_PROF", 0.80)]
    [InlineData("GAS1,35", "GAS1.35", "Gás", "Texto", "REDE_GAS", 1.35)]
    [InlineData("GAS 1,35", "GAS1.35", "Gás", "Texto", "REDE_GAS", 1.35)]
    public void DepthCodes_ForTelephoneBoxesAndGas_ArePreserved(
        string original,
        string normalized,
        string network,
        string recordType,
        string rule,
        double depth)
    {
        var actualCode = _normalizer.Normalize(original);
        var result = _interpreter.Interpret(original, actualCode);

        Assert.Equal(normalized, actualCode);
        Assert.True(result.Recognized);
        Assert.Equal(network, result.Network);
        Assert.Equal(recordType, result.RecordType);
        Assert.Equal(rule, result.Rule);
        Assert.Equal(depth, result.Depth);
    }

    [Theory]
    [Trait("Category", "FAST")]
    [InlineData("PVAF", "PV_AF", "Água potável")]
    [InlineData("BL ASS", "BOCA_LOBO", "Águas pluviais")]
    [InlineData("GRELHA AFG", "GRELHA", "Águas pluviais")]
    [InlineData("CXAP LAC", "CX_AP", "Águas pluviais")]
    [InlineData("CXTL", "CX_TEL", "Telefone")]
    [InlineData("PONTO HIDRATANTE", "HIDRANTE", "Rede de incêndio")]
    [InlineData("PVES 1.20 D150 OBS", "PV_ES", "Esgoto sanitário")]
    [InlineData("PVESAFG", "PV_ES", "Esgoto sanitário")]
    [InlineData("PVEAFG", "PVE", "Esgoto sanitário")]
    [InlineData("PVAPAFG", "PV_AP", "Águas pluviais")]
    [InlineData("PVTLAFG", "PV_TEL_LEGADO", "Telefone")]
    [InlineData("PVENCOBERTOAFG", "PV_ENCOBERTO_PROF", "Esgoto sanitário")]
    [InlineData("RE", "REGISTRO_AF", "Água potável")]
    [InlineData("POSTE", "POSTE", "Rede elétrica")]
    [InlineData("POSTE DE CONCRETO", "POSTE", "Rede elétrica")]
    public void PhysicalObjects_AreClassifiedBySupportedRules(string original, string rule, string network)
    {
        var normalized = _normalizer.Normalize(original);
        var result = _interpreter.Interpret(original, normalized);

        Assert.True(result.Recognized);
        Assert.Equal("Bloco", result.RecordType);
        Assert.Equal(rule, result.Rule);
        Assert.Equal(network, result.Network);
        Assert.Null(result.Depth);
    }

    [Theory]
    [Trait("Category", "FAST")]
    [InlineData("DIVISA 113", "Nº113")]
    [InlineData("DV S/N", "SN")]
    [InlineData("DIVISA TERRENO BALDIO", "TERRENO BALDIO")]
    public void BoundaryAnnotations_PreserveTheirText(string original, string expected)
    {
        var normalized = _normalizer.Normalize(original);
        var result = _interpreter.Interpret(original, normalized);

        Assert.Equal("ANOTACAO_DIVISA", result.Rule);
        Assert.Equal(expected, result.Complement);
        Assert.Null(result.Depth);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void UnknownCode_RemainsTraceableForReview()
    {
        const string original = "CODIGO-NOVO-XYZ";
        var result = _interpreter.Interpret(original, _normalizer.Normalize(original));

        Assert.False(result.Recognized);
        Assert.Equal("Revisar", result.RecordType);
        Assert.Equal("DESCONHECIDO", result.Rule);
        Assert.Equal(original, result.OriginalCode);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void UncataloguedCx_RemainsReconstructableAsGenericBox()
    {
        const string original = "CX NOVA REDE";
        var result = _interpreter.Interpret(original, _normalizer.Normalize(original));

        Assert.True(result.Recognized);
        Assert.Equal("Bloco", result.RecordType);
        Assert.Equal("CX_GENERICA", result.Rule);
        Assert.Equal("Não identificada", result.Network);
        Assert.Equal("média", result.Confidence);
    }
}
