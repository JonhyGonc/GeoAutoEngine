namespace GeoAutoEngine.Application.VideoInspection;

public enum SectionLabelSyncAction
{
    /// <summary>As palavras do rótulo já correspondem à inspeção; nada é alterado.</summary>
    Unchanged,

    /// <summary>
    /// Só as palavras mudam. Posição, rotação, altura, layer, estilo e formatação inline
    /// do rótulo ficam como o desenhista deixou.
    /// </summary>
    UpdateContents,

    /// <summary>O rótulo não existe no desenho e é recriado na posição e no estilo padrão.</summary>
    Recreate,
}

public sealed record SectionLabelSyncDecision(SectionLabelSyncAction Action, string Contents);

/// <summary>
/// Decide o que o GEOAUTO3VISYNC faz com um rótulo de trecho (nome ou Ø), usando o mesmo
/// critério do GEOAUTO3VIUPDATETEXTS: compara só as palavras e preserva a formatação
/// manual. Nunca calcula posição nem estilo.
/// </summary>
public static class SectionLabelSyncPlanner
{
    /// <param name="currentContents">
    /// Conteúdo do MText existente no desenho, ou <c>null</c> se o rótulo não existir.
    /// </param>
    /// <param name="generatedContents">Conteúdo gerado a partir da inspeção, já escapado para MText.</param>
    public static SectionLabelSyncDecision Decide(string? currentContents, string generatedContents)
    {
        ArgumentNullException.ThrowIfNull(generatedContents);
        if (currentContents is null)
        {
            return new SectionLabelSyncDecision(SectionLabelSyncAction.Recreate, generatedContents);
        }

        var merged = MTextContentMerger.Merge(currentContents, generatedContents);
        return string.Equals(merged, currentContents, StringComparison.Ordinal)
            ? new SectionLabelSyncDecision(SectionLabelSyncAction.Unchanged, currentContents)
            : new SectionLabelSyncDecision(SectionLabelSyncAction.UpdateContents, merged);
    }
}
