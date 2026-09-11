using Autodesk.AutoCAD.ApplicationServices;
using GeoAutoEngine.Application.VideoInspection;

namespace GeoAutoEngine.Cad2026.VideoInspection;

public sealed class AutoCadInspectionSharedCoverageService
{
    private readonly AutoCadInspectionSharedCoverageRenderer _renderer = new();

    public AutoCadInspectionCoverageResult Apply(
        Document document,
        VideoInspectionSession session,
        InspectionSectionSession section,
        IReadOnlyList<PositionedInspectionEvent> positionedEvents,
        AutoCadInspectionCoverageOptions options) =>
        _renderer.Apply(document, session, section, positionedEvents, options);

    public void RemoveContribution(
        Document document,
        VideoInspectionSession session,
        InspectionSectionSession section,
        bool keepCoverageLines) =>
        _renderer.RemoveContribution(document, session, section, keepCoverageLines);
}
