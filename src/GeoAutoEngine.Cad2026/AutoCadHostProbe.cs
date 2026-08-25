using System.Globalization;
using AutoCadApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace GeoAutoEngine.Cad2026;

public sealed record AutoCadHostSnapshot(
    string AcadVersion,
    int DatabaseModificationState,
    string DrawingName);

public static class AutoCadHostProbe
{
    public static AutoCadHostSnapshot Capture()
    {
        var document = AutoCadApplication.DocumentManager.MdiActiveDocument
            ?? throw new InvalidOperationException("AutoCAD has no active document.");
        var acadVersion = Convert.ToString(
            AutoCadApplication.GetSystemVariable("ACADVER"),
            CultureInfo.InvariantCulture) ?? string.Empty;
        var databaseModificationState = Convert.ToInt32(
            AutoCadApplication.GetSystemVariable("DBMOD"),
            CultureInfo.InvariantCulture);

        return new AutoCadHostSnapshot(acadVersion, databaseModificationState, document.Name);
    }
}
