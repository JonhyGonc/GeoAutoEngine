using GeoAutoEngine.Application.VideoInspection;

namespace GeoAutoEngine.Tests;

public sealed class SectionLabelSyncPlannerTests
{
    [Theory]
    [Trait("Category", "FAST")]
    [InlineData("TRECHO 01", "TRECHO 02", "TRECHO 02")]
    [InlineData("PV-12 A PV-13", "PV-12 A PV-14", "PV-12 A PV-14")]
    [InlineData("Ø150", "Ø200", "Ø200")]
    public void OnlyContentChanged_UpdatesTheWords(string current, string generated, string expected)
    {
        var decision = SectionLabelSyncPlanner.Decide(current, generated);

        Assert.Equal(SectionLabelSyncAction.UpdateContents, decision.Action);
        Assert.Equal(expected, decision.Contents);
    }

    [Theory]
    [Trait("Category", "FAST")]
    // Cor e altura inline aplicadas pelo desenhista.
    [InlineData("{\\C1;\\H0.5x;TRECHO 01}", "TRECHO 02", "{\\C1;\\H0.5x;TRECHO 02}")]
    // Negrito numa palavra e linha quebrada à mão.
    [InlineData("{\\fArial|b1;PV-12}\\PA PV-13", "PV-12 A PV-14", "{\\fArial|b1;PV-12}\\PA PV-14")]
    // Diâmetro salvo pelo AutoCAD com o código Unicode do Ø e sublinhado.
    [InlineData("\\L\\U+00D8150\\l", "Ø200", "\\LØ200\\l")]
    public void ManualFormattingAndContentChanged_KeepsTheFormatting(
        string current,
        string generated,
        string expected)
    {
        var decision = SectionLabelSyncPlanner.Decide(current, generated);

        Assert.Equal(SectionLabelSyncAction.UpdateContents, decision.Action);
        Assert.Equal(expected, decision.Contents);
    }

    [Theory]
    [Trait("Category", "FAST")]
    // Rótulo movido ou girado à mão: a posição não entra na decisão e o conteúdo é o mesmo.
    [InlineData("TRECHO 01", "TRECHO 01")]
    // Só formatação manual, nome já correto: não aparece como desatualizado.
    [InlineData("{\\C1;\\H0.5x;TRECHO 01}", "TRECHO 01")]
    [InlineData("TRECHO\\P01", "TRECHO 01")]
    [InlineData("\\U+00D8150", "Ø150")]
    // Diferença só de maiúsculas não conta como mudança (mesmo critério do VIUPDATETEXTS).
    [InlineData("Trecho 01", "TRECHO 01")]
    public void MovedOrFormattedLabelWithSameWords_IsLeftUntouched(string current, string generated)
    {
        var decision = SectionLabelSyncPlanner.Decide(current, generated);

        Assert.Equal(SectionLabelSyncAction.Unchanged, decision.Action);
        Assert.Equal(current, decision.Contents);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void LabelDeletedByHand_IsRecreatedWithTheGeneratedContents()
    {
        var decision = SectionLabelSyncPlanner.Decide(null, "TRECHO 02");

        Assert.Equal(SectionLabelSyncAction.Recreate, decision.Action);
        Assert.Equal("TRECHO 02", decision.Contents);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void LabelEmptiedByHand_GetsTheGeneratedContents()
    {
        var decision = SectionLabelSyncPlanner.Decide(string.Empty, "TRECHO 02");

        Assert.Equal(SectionLabelSyncAction.UpdateContents, decision.Action);
        Assert.Equal("TRECHO 02", decision.Contents);
    }
}
