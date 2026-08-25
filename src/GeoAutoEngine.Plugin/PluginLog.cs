using System.IO;
using System.Text;

namespace GeoAutoEngine.Plugin;

internal static class PluginLog
{
    private static readonly object Sync = new();
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GeoAutoEngine3",
        "Logs");

    internal static string CurrentPath => Path.Combine(LogDirectory, "geoautoengine.log");

    internal static void Information(string message) => Write("INFO", message, null);

    internal static void Error(string operation, Exception exception) => Write("ERROR", operation, exception);

    private static void Write(string level, string message, Exception? exception)
    {
        try
        {
            var builder = new StringBuilder()
                .Append(DateTimeOffset.Now.ToString("O"))
                .Append(" [").Append(level).Append("] ")
                .Append(message)
                .Append(" [thread=").Append(Environment.CurrentManagedThreadId).Append(']');
            if (exception is not null)
            {
                builder.AppendLine().Append(exception);
            }

            lock (Sync)
            {
                Directory.CreateDirectory(LogDirectory);
                File.AppendAllText(CurrentPath, builder.AppendLine().ToString(), Encoding.UTF8);
            }
        }
        catch
        {
            // Logging must never affect the AutoCAD host.
        }
    }
}
