using GeoAutoEngine.Application.VideoInspection;

namespace GeoAutoEngine.Tests;

public sealed class MTextContentMergerTests
{
    [Theory]
    [Trait("Category", "FAST")]
    // Gerado sem edição manual: troca a palavra e mantém as linhas.
    [InlineData("12,00 m\\PTRINCA", "12,00 m\\PFISSURA", "12,00 m\\PFISSURA")]
    // Linhas unidas à mão e nada mudou: não devolve o parágrafo.
    [InlineData("12,00 m TRINCA", "12,00 m\\PTRINCA", "12,00 m TRINCA")]
    // Linhas unidas à mão e a anomalia mudou: troca só a palavra.
    [InlineData("12,00 m TRINCA", "12,00 m\\PFISSURA", "12,00 m FISSURA")]
    [InlineData(
        "12,00 m TRINCA LONGITUDINAL\\PNA GERATRIZ",
        "12,00 m\\PFISSURA\\PNA GERATRIZ",
        "12,00 m FISSURA\\PNA GERATRIZ")]
    // Formatação aplicada pelo desenhista é preservada.
    [InlineData("{\\fArial|b1;12,00 m}\\PTRINCA", "12,00 m\\PFISSURA", "{\\fArial|b1;12,00 m}\\PFISSURA")]
    [InlineData("12,00 m TRINCA E RAIZ", "12,00 m\\PTRINCA", "12,00 m TRINCA")]
    [InlineData("12,00 m TRINCA", "12,00 m\\PTRINCA E RAIZ", "12,00 m TRINCA E RAIZ")]
    // Diferença só de maiúsculas não conta como mudança.
    [InlineData("12,00 m Trinca", "12,00 m\\PTRINCA", "12,00 m Trinca")]
    [InlineData(
        "FIM: 12,00 m\\PDESLOCAMENTO DAS JUNTAS",
        "FIM: 12,00 m\\PDESLOCAMENTO DAS JUNTAS\\PLATERAL",
        "FIM: 12,00 m\\PDESLOCAMENTO DAS JUNTAS LATERAL")]
    [InlineData("TRECHO 01", "TRECHO 02", "TRECHO 02")]
    // Caractere salvo pelo AutoCAD como código Unicode equivale ao caractere gerado.
    [InlineData("\\U+00D8150", "Ø150", "\\U+00D8150")]
    public void Merge_ChangesOnlyTheWordsThatChanged(string current, string generated, string expected)
    {
        Assert.Equal(expected, MTextContentMerger.Merge(current, generated));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void Planner_KeepsLinesJoinedByTheDrafter()
    {
        var reportId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var call = Assert.Single(InspectionAnnotationPlanner.Create(
        [
            new PositionedInspectionEvent(
                Guid.NewGuid(),
                InspectionLocationMode.Point,
                "Trinca",
                [
                    new PositionedInspectionLocation(
                        0,
                        PositionedLocationRole.Point,
                        12,
                        12,
                        new LinearPoint(12, 0, 0),
                        new LinearVector(1, 0, 0)),
                ],
                [],
                true),
        ]));
        var existing = new InspectionExistingCalloutText(
            InspectionTextRefreshPlanner.StableKey(reportId, sectionId, call),
            "12,00 m TRINCA",
            12);

        var item = Assert.Single(
            InspectionTextRefreshPlanner.Plan(reportId, sectionId, [call], [existing]).Items);

        Assert.Equal(InspectionTextRefreshOutcome.Unchanged, item.Outcome);
        Assert.Equal("12,00 m TRINCA", item.DesiredContents);
    }
}
