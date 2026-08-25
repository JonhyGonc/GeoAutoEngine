using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

namespace GeoAutoEngine.CadAudit;

public sealed class CadAuditCommands
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    [CommandMethod("GEOAUTOCADAUDIT", CommandFlags.Session | CommandFlags.NoUndoMarker)]
    public static void Audit()
    {
        Document document = Application.DocumentManager.MdiActiveDocument
            ?? throw new InvalidOperationException("No active document.");
        string outputPath = Environment.GetEnvironmentVariable("GEOAUTO_CAD_AUDIT_OUTPUT")
            ?? throw new InvalidOperationException("GEOAUTO_CAD_AUDIT_OUTPUT is not set.");
        string sourcePath = Environment.GetEnvironmentVariable("GEOAUTO_CAD_AUDIT_SOURCE")
            ?? document.Name;
        string sourceHash = Environment.GetEnvironmentVariable("GEOAUTO_CAD_AUDIT_SHA256") ?? string.Empty;
        Database database = document.Database;
        int dbmodBefore = Convert.ToInt32(Application.GetSystemVariable("DBMOD"), CultureInfo.InvariantCulture);
        Trace(outputPath, "command-start");

        var result = new Dictionary<string, object?>
        {
            ["audit_schema_version"] = "1.0",
            ["audit_timestamp_utc"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            ["read_only"] = true,
            ["source_path"] = sourcePath,
            ["source_sha256"] = sourceHash,
            ["host"] = new Dictionary<string, object?>
            {
                ["product"] = Application.GetSystemVariable("PRODUCT"),
                ["acadver"] = Application.GetSystemVariable("ACADVER"),
                ["civil_runtime_loaded"] = AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name?.StartsWith("Aecc", StringComparison.OrdinalIgnoreCase) == true),
            },
        };

        using (Transaction transaction = database.TransactionManager.StartOpenCloseTransaction())
        {
            Trace(outputPath, "database-start");
            result["database"] = ReadDatabase(database, transaction, outputPath);
            Trace(outputPath, "database-complete");
            result["layers"] = ReadLayers(database, transaction, outputPath);
            Trace(outputPath, "layers-complete");
            result["linetypes"] = ReadLinetypes(database, transaction);
            Trace(outputPath, "linetypes-complete");
            result["text_styles"] = ReadTextStyles(database, transaction);
            Trace(outputPath, "text-styles-complete");
            result["layouts"] = ReadLayouts(database, transaction);
            Trace(outputPath, "layouts-complete");
            result["block_definitions"] = ReadBlockDefinitions(database, transaction);
            Trace(outputPath, "block-definitions-complete");
            result["spaces"] = ReadSpaces(database, transaction);
            Trace(outputPath, "spaces-complete");
        }

        int dbmodAfter = Convert.ToInt32(Application.GetSystemVariable("DBMOD"), CultureInfo.InvariantCulture);
        result["dbmod_before"] = dbmodBefore;
        result["dbmod_after"] = dbmodAfter;
        result["dbmod_unchanged"] = dbmodBefore == dbmodAfter;

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, JsonSerializer.Serialize(result, JsonOptions));
        Trace(outputPath, "json-written");
        document.Editor.WriteMessage($"\nGEOAUTOCADAUDIT_OK {outputPath}\n");
    }

    private static void Trace(string outputPath, string message)
    {
        File.AppendAllText(outputPath + ".trace.log", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) + " " + message + Environment.NewLine);
    }

    private static Dictionary<string, object?> ReadDatabase(Database database, Transaction transaction, string outputPath)
    {
        Trace(outputPath, "database-basic-start");
        var data = new Dictionary<string, object?>
        {
            ["filename_seen_by_host"] = database.Filename,
            ["original_file_version"] = database.OriginalFileVersion.ToString(),
            ["last_saved_as_version"] = database.LastSavedAsVersion.ToString(),
            ["insunits"] = database.Insunits.ToString(),
            ["measurement"] = database.Measurement.ToString(),
            ["tile_mode"] = database.TileMode,
            ["extmin"] = Point(database.Extmin),
            ["extmax"] = Point(database.Extmax),
            ["limmin"] = Point(database.Limmin),
            ["limmax"] = Point(database.Limmax),
            ["elevation_model_space"] = database.Elevation,
            ["elevation_paper_space"] = database.Pelevation,
            ["angbase"] = database.Angbase,
            ["angdir"] = database.Angdir,
        };
        Trace(outputPath, "database-basic-complete");

        // Database.GeoDataObject can terminate Core Console natively for drawings
        // without GeoData. CGEOCS exposes the assigned code without opening it.
        data["coordinate_system_code"] = TryGet(() => NormalizePropertyValue(Application.GetSystemVariable("CGEOCS")));
        data["geodata"] = null;
        Trace(outputPath, "database-geodata-complete");

        try
        {
            Trace(outputPath, "database-summary-start");
            DatabaseSummaryInfo info = database.SummaryInfo;
            data["summary_info"] = new Dictionary<string, object?>
            {
                ["title"] = info.Title,
                ["subject"] = info.Subject,
                ["author"] = info.Author,
                ["keywords"] = info.Keywords,
                ["comments"] = info.Comments,
                ["last_saved_by"] = info.LastSavedBy,
                ["revision_number"] = info.RevisionNumber,
                ["hyperlink_base"] = info.HyperlinkBase,
                ["custom_properties"] = ReadDictionary(info.CustomProperties),
            };
        }
        catch (System.Exception exception)
        {
            data["summary_info_error"] = exception.Message;
        }
        Trace(outputPath, "database-summary-complete");

        return data;
    }

    private static List<Dictionary<string, object?>> ReadLayers(Database database, Transaction transaction, string outputPath)
    {
        var result = new List<Dictionary<string, object?>>();
        var table = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
        foreach (ObjectId id in table)
        {
            var layer = (LayerTableRecord)transaction.GetObject(id, OpenMode.ForRead);
            Trace(outputPath, "layer-start " + layer.Name);
            result.Add(new Dictionary<string, object?>
            {
                ["name"] = layer.Name,
                ["handle"] = layer.Handle.ToString(),
                ["color"] = ReadColor(layer.Color),
                ["linetype"] = ReadSymbolName(layer.LinetypeObjectId, transaction),
                ["lineweight"] = layer.LineWeight.ToString(),
                ["description"] = layer.Description,
                ["is_off"] = layer.IsOff,
                ["is_frozen"] = layer.IsFrozen,
                ["is_locked"] = layer.IsLocked,
                ["is_plottable"] = layer.IsPlottable,
            });
            Trace(outputPath, "layer-complete " + layer.Name);
        }
        return result.OrderBy(x => x["name"]?.ToString(), StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<Dictionary<string, object?>> ReadLinetypes(Database database, Transaction transaction)
    {
        var result = new List<Dictionary<string, object?>>();
        var table = (LinetypeTable)transaction.GetObject(database.LinetypeTableId, OpenMode.ForRead);
        foreach (ObjectId id in table)
        {
            var item = (LinetypeTableRecord)transaction.GetObject(id, OpenMode.ForRead);
            result.Add(new Dictionary<string, object?>
            {
                ["name"] = item.Name,
                ["description"] = item.AsciiDescription,
                ["pattern_length"] = item.PatternLength,
                ["num_dashes"] = item.NumDashes,
            });
        }
        return result.OrderBy(x => x["name"]?.ToString(), StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<Dictionary<string, object?>> ReadTextStyles(Database database, Transaction transaction)
    {
        var result = new List<Dictionary<string, object?>>();
        var table = (TextStyleTable)transaction.GetObject(database.TextStyleTableId, OpenMode.ForRead);
        foreach (ObjectId id in table)
        {
            var item = (TextStyleTableRecord)transaction.GetObject(id, OpenMode.ForRead);
            result.Add(new Dictionary<string, object?>
            {
                ["name"] = item.Name,
                ["font"] = item.FileName,
                ["big_font"] = item.BigFontFileName,
                ["text_size"] = item.TextSize,
                ["x_scale"] = item.XScale,
                ["obliquing_angle"] = item.ObliquingAngle,
            });
        }
        return result.OrderBy(x => x["name"]?.ToString(), StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<Dictionary<string, object?>> ReadLayouts(Database database, Transaction transaction)
    {
        var result = new List<Dictionary<string, object?>>();
        var dictionary = (DBDictionary)transaction.GetObject(database.LayoutDictionaryId, OpenMode.ForRead);
        foreach (DBDictionaryEntry entry in dictionary)
        {
            var layout = (Layout)transaction.GetObject(entry.Value, OpenMode.ForRead);
            result.Add(new Dictionary<string, object?>
            {
                ["name"] = layout.LayoutName,
                ["model_type"] = layout.ModelType,
                ["tab_order"] = layout.TabOrder,
                ["paper_size"] = new[] { layout.PlotPaperSize.X, layout.PlotPaperSize.Y },
                ["plot_configuration"] = layout.PlotConfigurationName,
                ["canonical_media_name"] = layout.CanonicalMediaName,
                ["plot_rotation"] = layout.PlotRotation.ToString(),
                ["block_record_handle"] = transaction.GetObject(layout.BlockTableRecordId, OpenMode.ForRead).Handle.ToString(),
            });
        }
        return result.OrderBy(x => Convert.ToInt32(x["tab_order"], CultureInfo.InvariantCulture)).ToList();
    }

    private static List<Dictionary<string, object?>> ReadBlockDefinitions(Database database, Transaction transaction)
    {
        var result = new List<Dictionary<string, object?>>();
        var table = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
        foreach (ObjectId id in table)
        {
            var record = (BlockTableRecord)transaction.GetObject(id, OpenMode.ForRead);
            var entities = new List<Dictionary<string, object?>>();
            foreach (ObjectId entityId in record)
            {
                if (transaction.GetObject(entityId, OpenMode.ForRead) is Entity entity)
                {
                    entities.Add(ReadEntity(entity, transaction, includeGeometry: !record.IsLayout));
                }
            }
            result.Add(new Dictionary<string, object?>
            {
                ["name"] = record.Name,
                ["handle"] = record.Handle.ToString(),
                ["is_anonymous"] = record.IsAnonymous,
                ["is_layout"] = record.IsLayout,
                ["is_from_xref"] = record.IsFromExternalReference,
                ["is_xref_resolved"] = record.IsResolved,
                ["xref_status"] = record.IsFromExternalReference ? record.XrefStatus.ToString() : null,
                ["path_name"] = record.PathName,
                ["origin"] = Point(record.Origin),
                ["entity_count"] = entities.Count,
                ["entity_type_counts"] = entities.GroupBy(e => e["object_type"]?.ToString() ?? "UNKNOWN").ToDictionary(g => g.Key, g => g.Count()),
                ["entities"] = record.IsLayout ? null : entities,
            });
        }
        return result.OrderBy(x => x["name"]?.ToString(), StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<Dictionary<string, object?>> ReadSpaces(Database database, Transaction transaction)
    {
        var result = new List<Dictionary<string, object?>>();
        var table = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
        foreach (string name in new[] { BlockTableRecord.ModelSpace, BlockTableRecord.PaperSpace })
        {
            if (!table.Has(name))
            {
                continue;
            }
            var record = (BlockTableRecord)transaction.GetObject(table[name], OpenMode.ForRead);
            var entities = new List<Dictionary<string, object?>>();
            foreach (ObjectId id in record)
            {
                if (transaction.GetObject(id, OpenMode.ForRead) is Entity entity)
                {
                    entities.Add(ReadEntity(entity, transaction, includeGeometry: true));
                }
            }
            result.Add(new Dictionary<string, object?>
            {
                ["name"] = name,
                ["entity_count"] = entities.Count,
                ["entity_type_counts"] = entities.GroupBy(e => e["object_type"]?.ToString() ?? "UNKNOWN").ToDictionary(g => g.Key, g => g.Count()),
                ["entities"] = entities,
            });
        }
        return result;
    }

    private static Dictionary<string, object?> ReadEntity(Entity entity, Transaction transaction, bool includeGeometry)
    {
        RXClass rx = entity.GetRXClass();
        var data = new Dictionary<string, object?>
        {
            ["object_type"] = rx.Name,
            ["dxf_name"] = rx.DxfName,
            ["managed_type"] = entity.GetType().FullName,
            ["handle"] = entity.Handle.ToString(),
            ["layer"] = entity.Layer,
            ["color"] = ReadColor(entity.Color),
            ["linetype"] = entity.Linetype,
            ["linetype_scale"] = entity.LinetypeScale,
            ["lineweight"] = entity.LineWeight.ToString(),
            ["visible"] = entity.Visible,
            ["space"] = entity.BlockName,
            ["xdata"] = ReadXData(entity.XData),
        };

        try
        {
            Extents3d extents = entity.GeometricExtents;
            data["extents"] = new Dictionary<string, object?> { ["min"] = Point(extents.MinPoint), ["max"] = Point(extents.MaxPoint) };
        }
        catch (System.Exception exception)
        {
            data["extents_error"] = exception.Message;
        }

        if (includeGeometry)
        {
            AddGeometry(data, entity, transaction);
        }

        if (entity.GetType().Namespace?.StartsWith("Autodesk.Civil", StringComparison.Ordinal) == true ||
            rx.Name.StartsWith("Aecc", StringComparison.OrdinalIgnoreCase))
        {
            data["civil_properties"] = ReadSelectedProperties(entity, [
                "Name", "Description", "StyleName", "StyleId", "NetworkId", "NetworkName",
                "PartFamilyName", "PartSizeName", "InnerDiameterOrWidth", "InnerHeight", "OuterDiameterOrWidth",
                "StartPoint", "EndPoint", "StartPointLocation", "EndPointLocation", "StartStructureId", "EndStructureId",
                "Position", "Location", "RimElevation", "SumpElevation", "Elevation", "Slope", "Length2D", "Length3D",
                "PointNumber", "RawDescription", "FullDescription", "Northing", "Easting", "Level", "Station",
                "ConnectedPipesCount", "Domain", "PartType", "StructureType", "PipeType"
            ]);
        }
        return data;
    }

    private static void AddGeometry(Dictionary<string, object?> data, Entity entity, Transaction transaction)
    {
        switch (entity)
        {
            case DBText text:
                data["text"] = text.TextString;
                data["position"] = Point(text.Position);
                data["alignment_point"] = Point(text.AlignmentPoint);
                data["height"] = text.Height;
                data["rotation"] = text.Rotation;
                data["width_factor"] = text.WidthFactor;
                data["oblique"] = text.Oblique;
                data["text_style"] = ReadSymbolName(text.TextStyleId, transaction);
                data["normal"] = Vector(text.Normal);
                break;
            case MText text:
                data["text"] = text.Text;
                data["contents"] = text.Contents;
                data["position"] = Point(text.Location);
                data["text_height"] = text.TextHeight;
                data["rotation"] = text.Rotation;
                data["width"] = text.Width;
                data["text_style"] = ReadSymbolName(text.TextStyleId, transaction);
                data["attachment"] = text.Attachment.ToString();
                data["normal"] = Vector(text.Normal);
                break;
            case BlockReference block:
                data["block_name"] = ReadBlockName(block.BlockTableRecord, transaction);
                data["dynamic_block_name"] = block.IsDynamicBlock ? ReadBlockName(block.DynamicBlockTableRecord, transaction) : null;
                data["position"] = Point(block.Position);
                data["rotation"] = block.Rotation;
                data["scale"] = new[] { block.ScaleFactors.X, block.ScaleFactors.Y, block.ScaleFactors.Z };
                data["normal"] = Vector(block.Normal);
                data["attributes"] = ReadAttributes(block, transaction);
                break;
            case Line line:
                data["start"] = Point(line.StartPoint);
                data["end"] = Point(line.EndPoint);
                data["length"] = line.Length;
                data["normal"] = Vector(line.Normal);
                break;
            case Polyline polyline:
                var vertices = new List<Dictionary<string, object?>>();
                for (int i = 0; i < polyline.NumberOfVertices; i++)
                {
                    Point2d point = polyline.GetPoint2dAt(i);
                    vertices.Add(new Dictionary<string, object?>
                    {
                        ["index"] = i,
                        ["point"] = new[] { point.X, point.Y, polyline.Elevation },
                        ["bulge"] = polyline.GetBulgeAt(i),
                        ["start_width"] = polyline.GetStartWidthAt(i),
                        ["end_width"] = polyline.GetEndWidthAt(i),
                    });
                }
                data["vertices"] = vertices;
                data["closed"] = polyline.Closed;
                data["elevation"] = polyline.Elevation;
                data["length"] = SafeCurveLength(polyline);
                data["normal"] = Vector(polyline.Normal);
                break;
            case Polyline2d polyline:
                data["vertices"] = ReadVertexCollection(polyline, transaction);
                data["closed"] = polyline.Closed;
                data["length"] = SafeCurveLength(polyline);
                data["normal"] = Vector(polyline.Normal);
                break;
            case Polyline3d polyline:
                data["vertices"] = ReadVertexCollection(polyline, transaction);
                data["closed"] = polyline.Closed;
                data["poly_type"] = polyline.PolyType.ToString();
                data["length"] = SafeCurveLength(polyline);
                break;
            case Arc arc:
                data["center"] = Point(arc.Center);
                data["radius"] = arc.Radius;
                data["start"] = Point(arc.StartPoint);
                data["end"] = Point(arc.EndPoint);
                data["start_angle"] = arc.StartAngle;
                data["end_angle"] = arc.EndAngle;
                data["length"] = arc.Length;
                data["normal"] = Vector(arc.Normal);
                break;
            case Circle circle:
                data["center"] = Point(circle.Center);
                data["radius"] = circle.Radius;
                data["circumference"] = circle.Circumference;
                data["area"] = circle.Area;
                data["normal"] = Vector(circle.Normal);
                break;
            case DBPoint point:
                data["position"] = Point(point.Position);
                data["normal"] = Vector(point.Normal);
                break;
            case Hatch hatch:
                data["pattern_name"] = hatch.PatternName;
                data["pattern_type"] = hatch.PatternType.ToString();
                data["associative"] = hatch.Associative;
                data["loop_count"] = hatch.NumberOfLoops;
                data["area"] = TryGet(() => hatch.Area);
                break;
            case Dimension dimension:
                data["dimension_text"] = dimension.DimensionText;
                data["measurement"] = dimension.Measurement;
                data["dimension_style"] = ReadSymbolName(dimension.DimensionStyle, transaction);
                data["text_position"] = Point(dimension.TextPosition);
                break;
            case Leader leader:
                data["vertices"] = Enumerable.Range(0, leader.NumVertices).Select(i => Point(leader.VertexAt(i))).ToList();
                data["annotation_handle"] = leader.Annotation.IsNull ? null : transaction.GetObject(leader.Annotation, OpenMode.ForRead).Handle.ToString();
                break;
            case Curve curve:
                data["start"] = TryGet(() => Point(curve.StartPoint));
                data["end"] = TryGet(() => Point(curve.EndPoint));
                data["length"] = SafeCurveLength(curve);
                data["sample_points"] = SampleCurve(curve, 17);
                break;
        }
    }

    private static List<Dictionary<string, object?>> ReadAttributes(BlockReference block, Transaction transaction)
    {
        var attributes = new List<Dictionary<string, object?>>();
        foreach (ObjectId id in block.AttributeCollection)
        {
            if (transaction.GetObject(id, OpenMode.ForRead) is AttributeReference attribute)
            {
                attributes.Add(new Dictionary<string, object?>
                {
                    ["tag"] = attribute.Tag,
                    ["text"] = attribute.TextString,
                    ["position"] = Point(attribute.Position),
                    ["rotation"] = attribute.Rotation,
                    ["height"] = attribute.Height,
                    ["invisible"] = attribute.Invisible,
                    ["constant"] = attribute.IsConstant,
                    ["preset"] = attribute.IsPreset,
                    ["verifiable"] = attribute.IsVerifiable,
                });
            }
        }
        return attributes;
    }

    private static List<object?> ReadVertexCollection(Entity polyline, Transaction transaction)
    {
        var vertices = new List<object?>();
        if (polyline is IEnumerable ids)
        {
            foreach (object? value in ids)
            {
                if (value is ObjectId id && transaction.GetObject(id, OpenMode.ForRead) is Vertex2d vertex2d)
                {
                    vertices.Add(new Dictionary<string, object?> { ["point"] = Point(vertex2d.Position), ["bulge"] = vertex2d.Bulge, ["start_width"] = vertex2d.StartWidth, ["end_width"] = vertex2d.EndWidth });
                }
                else if (value is ObjectId id3d && transaction.GetObject(id3d, OpenMode.ForRead) is PolylineVertex3d vertex3d)
                {
                    vertices.Add(Point(vertex3d.Position));
                }
            }
        }
        return vertices;
    }

    private static List<double[]> SampleCurve(Curve curve, int count)
    {
        var points = new List<double[]>();
        try
        {
            double start = curve.StartParam;
            double end = curve.EndParam;
            for (int i = 0; i < count; i++)
            {
                double parameter = start + ((end - start) * i / (count - 1));
                points.Add(Point(curve.GetPointAtParameter(parameter)));
            }
        }
        catch
        {
            // Some proxy/custom curves do not expose their parameter domain safely.
        }
        return points;
    }

    private static double? SafeCurveLength(Curve curve)
    {
        try
        {
            return curve.GetDistanceAtParameter(curve.EndParam) - curve.GetDistanceAtParameter(curve.StartParam);
        }
        catch
        {
            return null;
        }
    }

    private static Dictionary<string, object?> ReadSelectedProperties(object instance, IEnumerable<string> names)
    {
        var result = new Dictionary<string, object?>();
        Type type = instance.GetType();
        foreach (string name in names)
        {
            PropertyInfo? property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (property is null || property.GetIndexParameters().Length != 0)
            {
                continue;
            }
            try
            {
                result[property.Name] = NormalizePropertyValue(property.GetValue(instance));
            }
            catch (System.Exception exception)
            {
                result[property.Name + "_error"] = exception.GetBaseException().Message;
            }
        }
        return result;
    }

    private static Dictionary<string, object?> ReadDictionary(IDictionaryEnumerator enumerator)
    {
        var result = new Dictionary<string, object?>();
        while (enumerator.MoveNext())
        {
            string key = Convert.ToString(enumerator.Key, CultureInfo.InvariantCulture) ?? string.Empty;
            result[key] = NormalizePropertyValue(enumerator.Value);
        }
        return result;
    }

    private static object? NormalizePropertyValue(object? value)
    {
        if (value is null || value is string || value is bool || value is byte || value is short || value is int || value is long || value is float || value is double || value is decimal)
        {
            return value;
        }
        if (value is Enum)
        {
            return value.ToString();
        }
        if (value is ObjectId id)
        {
            return id.IsNull ? null : id.Handle.ToString();
        }
        if (value is Point2d point2d)
        {
            return new[] { point2d.X, point2d.Y };
        }
        if (value is Point3d point3d)
        {
            return Point(point3d);
        }
        if (value is Vector2d vector2d)
        {
            return new[] { vector2d.X, vector2d.Y };
        }
        if (value is Vector3d vector3d)
        {
            return Vector(vector3d);
        }
        if (value is IEnumerable enumerable)
        {
            var items = new List<object?>();
            foreach (object? item in enumerable)
            {
                if (items.Count == 100)
                {
                    items.Add("TRUNCATED_AT_100");
                    break;
                }
                items.Add(NormalizePropertyValue(item));
            }
            return items;
        }
        return value.ToString();
    }

    private static List<Dictionary<string, object?>>? ReadXData(ResultBuffer? buffer)
    {
        if (buffer is null)
        {
            return null;
        }
        using (buffer)
        {
            return buffer.Cast<TypedValue>().Select(value => new Dictionary<string, object?>
            {
                ["type_code"] = value.TypeCode,
                ["value"] = NormalizePropertyValue(value.Value),
            }).ToList();
        }
    }

    private static Dictionary<string, object?> ReadColor(Color color) => new()
    {
        ["method"] = color.ColorMethod.ToString(),
        ["index"] = color.ColorIndex,
    };

    private static string? ReadSymbolName(ObjectId id, Transaction transaction)
    {
        if (id.IsNull || !id.IsValid)
        {
            return null;
        }
        return transaction.GetObject(id, OpenMode.ForRead) is SymbolTableRecord record ? record.Name : id.Handle.ToString();
    }

    private static string? ReadBlockName(ObjectId id, Transaction transaction)
    {
        return id.IsNull || !id.IsValid ? null : ((BlockTableRecord)transaction.GetObject(id, OpenMode.ForRead)).Name;
    }

    private static T? TryGet<T>(Func<T> getter)
    {
        try
        {
            return getter();
        }
        catch
        {
            return default;
        }
    }

    private static double[] Point(Point2d point) => [point.X, point.Y];
    private static double[] Point(Point3d point) => [point.X, point.Y, point.Z];
    private static double[] Vector(Vector3d vector) => [vector.X, vector.Y, vector.Z];
}
