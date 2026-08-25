using System.Collections.ObjectModel;

namespace GeoAutoEngine.Application;

public sealed record NetworkCadDefinition(
    string NetworkId,
    string DisplayName,
    string TextLayer,
    IReadOnlyList<string> CurveLayers,
    int DisplayOrder);

/// <summary>
/// Single source of truth for the official network names and their CAD layers.
/// Colors deliberately remain a DWG concern: the UI reads the actual layer color
/// from the active drawing instead of maintaining a second technical color table.
/// </summary>
public static class NetworkCadCatalog
{
    private static readonly NetworkCadDefinition[] OrderedDefinitions =
    [
        Definition("agua_potavel", "Água potável", "GF-Rede Água", ["GF-Rede Água", "REDE_AGUA", "AGUA", "AF", "AFT"], 0),
        Definition("aguas_pluviais", "Águas pluviais", "GF-Rede Pluvial", ["GF-Rede Pluvial", "REDE_DRENAGEM", "DRENAGEM", "PLUVIAL"], 1),
        Definition("esgoto_sanitario", "Esgoto sanitário", "GF-Rede Esgoto", ["GF-Rede Esgoto", "REDE_ESGOTO", "ESGOTO"], 2),
        Definition("esgoto_industrial", "Esgoto industrial", "GF-Rede Esgoto Industrial", ["GF-Rede Esgoto Industrial", "REDE_ESGOTO_INDUSTRIAL", "ESGOTO INDUSTRIAL"], 3),
        Definition("rede_eletrica", "Rede elétrica", "GF-Rede Eletrica", ["GF-Rede Eletrica", "REDE_ELETRICA", "ELETRICA"], 4),
        Definition("telefone", "Telefone", "GF-Rede Telefone", ["GF-Rede Telefone", "REDE_TELEFONE", "TELEFONE"], 5),
        Definition("fibra_optica", "Fibra óptica", "GF-Rede Fibra Optica", ["GF-Rede Fibra Optica", "REDE_FIBRA", "FIBRA", "FIBRA OPTICA"], 6),
        Definition("rede_incendio", "Rede de incêndio", "GF-Rede de Incendio", ["GF-Rede de Incendio", "REDE_INCENDIO", "INCENDIO"], 7),
        Definition("gas", "Gás", "GF-Rede Gás", ["GF-Rede Gás", "REDE_GAS", "GAS"], 8),
        Definition("nao_identificada", "Não identificada", "GF-Rede Sem Descrição de Uso", ["GF-Rede Sem Descrição de Uso", "REDE_NAO_IDENTIFICADA"], 9),
        Definition("outra", "Outra", "REDE_OUTRA", ["REDE_OUTRA"], 10),
    ];

    private static readonly IReadOnlyDictionary<string, NetworkCadDefinition> ByDisplayName =
        new ReadOnlyDictionary<string, NetworkCadDefinition>(
            OrderedDefinitions.ToDictionary(definition => definition.DisplayName, StringComparer.Ordinal));

    public static IReadOnlyList<NetworkCadDefinition> All { get; } = Array.AsReadOnly(OrderedDefinitions);

    public static NetworkCadDefinition Resolve(string? displayName) =>
        displayName is not null && ByDisplayName.TryGetValue(displayName.Trim(), out var definition)
            ? definition
            : OrderedDefinitions[9];

    private static NetworkCadDefinition Definition(
        string id,
        string displayName,
        string textLayer,
        string[] curveLayers,
        int displayOrder) =>
        new(id, displayName, textLayer, Array.AsReadOnly(curveLayers), displayOrder);
}
