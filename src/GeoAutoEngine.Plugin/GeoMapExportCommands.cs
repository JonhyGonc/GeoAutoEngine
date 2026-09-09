using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using GeoAutoEngine.Application;
using GeoAutoEngine.Application.GeoMapExport;
using GeoAutoEngine.Cad2026.GeoMapExport;
using AutoCadApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(GeoAutoEngine.Plugin.GeoMapExportCommands))]

namespace GeoAutoEngine.Plugin;

public sealed class GeoMapExportCommands
{
    [CommandMethod("GEOAUTO3GEOMAPEXPORT", CommandFlags.Modal | CommandFlags.NoUndoMarker)]
    public void ExportPolylines()
    {
        var document = AutoCadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        try
        {
            var selection = SelectPolylines(document.Editor);
            if (selection is null)
            {
                document.Editor.WriteMessage("\nGeoAutoEngine: exportação GeoMap cancelada.\n");
                return;
            }

            var features = AutoCadGeoMapPolylineReader.Read(document, selection);
            var inferredNetwork = InferNetwork(features);
            var drawingName = Path.GetFileNameWithoutExtension(document.Name);

            if (!TryPrompt(document.Editor, "Identificador do projeto", drawingName, out var projectExternalId) ||
                !TryPrompt(document.Editor, "Nome do projeto", drawingName, out var projectName) ||
                !TryPrompt(
                    document.Editor,
                    "Identificador da rede",
                    $"{projectExternalId}:{inferredNetwork.NetworkId}",
                    out var networkExternalId) ||
                !TryPrompt(document.Editor, "Nome da rede", inferredNetwork.DisplayName, out var networkName) ||
                !TryPrompt(document.Editor, "Tipo da rede", CanonicalNetworkType(inferredNetwork.NetworkId), out var networkType) ||
                !TryPrompt(document.Editor, "CRS de origem (ex.: EPSG:31982)", null, out var sourceCrs) ||
                !TryPrompt(
                    document.Editor,
                    "Unidade (metre, foot, us-survey-foot ou degree)",
                    DrawingUnit(document.Database),
                    out var units))
            {
                document.Editor.WriteMessage("\nGeoAutoEngine: exportação GeoMap cancelada.\n");
                return;
            }

            sourceCrs = NormalizeEpsg(sourceCrs);
            var request = new GeoMapExportRequest(
                sourceCrs,
                units.Trim().ToLowerInvariant(),
                projectExternalId,
                projectName,
                networkExternalId,
                networkName,
                networkType,
                DateTimeOffset.UtcNow,
                features);
            var json = GeoMapExportSerializer.Serialize(request);

            using var dialog = new SaveFileDialog
            {
                AddExtension = true,
                DefaultExt = "geomap.json",
                FileName = SafeFileName($"{projectExternalId}-{inferredNetwork.NetworkId}.geomap.json"),
                Filter = "Contrato GeoMap (*.geomap.json)|*.geomap.json",
                OverwritePrompt = true,
                RestoreDirectory = true,
                Title = "Exportar rede para o GeoMap",
            };
            if (dialog.ShowDialog() != DialogResult.OK)
            {
                document.Editor.WriteMessage("\nGeoAutoEngine: exportação GeoMap cancelada.\n");
                return;
            }

            File.WriteAllText(dialog.FileName, json, new UTF8Encoding(false));
            document.Editor.WriteMessage(
                $"\nGeoAutoEngine: {features.Count} Polyline(s) exportada(s) para {dialog.FileName}.\n");
        }
        catch (System.Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
        {
            PluginLog.Error("Exportação de Polyline para o GeoMap", exception);
            document.Editor.WriteMessage($"\nGeoAutoEngine: não foi possível exportar para o GeoMap. {exception.Message}\n");
        }
    }

    private static ObjectId[]? SelectPolylines(Editor editor)
    {
        var options = new PromptSelectionOptions
        {
            MessageForAdding = "\nSelecione as Polylines 2D da rede: ",
            MessageForRemoval = "\nRemova as Polylines da seleção: ",
        };
        var filter = new SelectionFilter(
        [
            new TypedValue((int)DxfCode.Start, "LWPOLYLINE"),
        ]);
        var result = editor.GetSelection(options, filter);
        return result.Status == PromptStatus.OK ? result.Value.GetObjectIds() : null;
    }

    private static bool TryPrompt(
        Editor editor,
        string label,
        string? defaultValue,
        out string value)
    {
        var suffix = string.IsNullOrWhiteSpace(defaultValue) ? ": " : $" <{defaultValue}>: ";
        var options = new PromptStringOptions($"\n{label}{suffix}")
        {
            AllowSpaces = true,
        };
        if (!string.IsNullOrWhiteSpace(defaultValue))
        {
            options.DefaultValue = defaultValue;
            options.UseDefaultValue = true;
        }

        var result = editor.GetString(options);
        value = result.StringResult?.Trim() ?? string.Empty;
        return result.Status == PromptStatus.OK;
    }

    private static NetworkCadDefinition InferNetwork(IReadOnlyList<GeoMapExportFeature> features)
    {
        var matches = features
            .Select(feature => NetworkCadCatalog.All.FirstOrDefault(definition =>
                definition.CurveLayers.Contains(feature.SourceLayer, StringComparer.OrdinalIgnoreCase)))
            .Where(definition => definition is not null)
            .Distinct()
            .ToArray();

        return matches.Length == 1
            ? matches[0]!
            : NetworkCadCatalog.All.First(definition => definition.NetworkId == "nao_identificada");
    }

    private static string CanonicalNetworkType(string networkId) => networkId switch
    {
        "agua_potavel" => "potable_water",
        "aguas_pluviais" => "stormwater",
        "esgoto_sanitario" => "sanitary_sewer",
        "esgoto_industrial" => "industrial_sewer",
        "rede_eletrica" => "electricity",
        "telefone" => "telephone",
        "fibra_optica" => "fiber_optic",
        "rede_incendio" => "fire_network",
        "gas" => "gas",
        "outra" => "other",
        _ => "unidentified",
    };

    private static string? DrawingUnit(Database database) => database.Insunits.ToString() switch
    {
        "Meters" => "metre",
        "Feet" => "foot",
        "USSurveyFeet" => "us-survey-foot",
        _ => null,
    };

    private static string NormalizeEpsg(string value)
    {
        value = value.Trim().ToUpperInvariant();
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var epsg) && epsg > 0
            ? $"EPSG:{epsg}"
            : value;
    }

    private static string SafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(character => invalid.Contains(character) ? '-' : character).ToArray());
    }
}
