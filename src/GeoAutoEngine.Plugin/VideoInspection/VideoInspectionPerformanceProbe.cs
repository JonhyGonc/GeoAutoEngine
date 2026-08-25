using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows.Threading;

namespace GeoAutoEngine.Plugin.VideoInspection;

internal static class VideoInspectionPerformanceProbe
{
    private static ImportProfile? _active;

    internal static void BeginImport(string path)
    {
        _active = new ImportProfile(path);
    }

    internal static IDisposable Measure(string stage) =>
        _active is null ? EmptyScope.Instance : new MeasureScope(stage);

    internal static void Record(string stage, double milliseconds)
    {
        _active?.Add(stage, milliseconds);
    }

    internal static void Count(string operation)
    {
        _active?.Increment(operation);
    }

    internal static void CompleteImport(
        int sectionCount,
        int occurrenceCount,
        Dispatcher dispatcher)
    {
        var profile = _active;
        if (profile is null)
        {
            return;
        }

        profile.SectionCount = sectionCount;
        profile.OccurrenceCount = occurrenceCount;
        profile.SynchronousMilliseconds = profile.Total.Elapsed.TotalMilliseconds;

        dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
        {
            profile.Add(
                "UI binding/render",
                Math.Max(0, profile.Total.Elapsed.TotalMilliseconds - profile.SynchronousMilliseconds));
            profile.Add("TOTAL", profile.Total.Elapsed.TotalMilliseconds);
            PluginLog.Information(profile.Format());
            if (ReferenceEquals(_active, profile))
            {
                _active = null;
            }
        }));
    }

    internal static void FailImport(Exception exception)
    {
        var profile = _active;
        if (profile is null)
        {
            return;
        }

        profile.Add("TOTAL until failure", profile.Total.Elapsed.TotalMilliseconds);
        PluginLog.Information(profile.Format() + $"; failure={exception.GetType().Name}: {exception.Message}");
        _active = null;
    }

    internal static void CompleteSectionSwitch(
        Guid sectionId,
        int occurrenceCount,
        Stopwatch total,
        double synchronousMilliseconds,
        Dispatcher dispatcher)
    {
        dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
        {
            PluginLog.Information(
                "[VI PERF] Section switch: " +
                $"SectionId={sectionId:D}; occurrences={occurrenceCount}; " +
                $"synchronous={Milliseconds(synchronousMilliseconds)} ms; " +
                $"binding/render={Milliseconds(Math.Max(0, total.Elapsed.TotalMilliseconds - synchronousMilliseconds))} ms; " +
                $"TOTAL={Milliseconds(total.Elapsed.TotalMilliseconds)} ms.");
        }));
    }

    private static string Milliseconds(double value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);

    private sealed class MeasureScope : IDisposable
    {
        private readonly string _stage;
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

        internal MeasureScope(string stage)
        {
            _stage = stage;
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            Record(_stage, _stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    private sealed class EmptyScope : IDisposable
    {
        internal static EmptyScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }

    private sealed class ImportProfile
    {
        private readonly Dictionary<string, double> _stages = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _counts = new(StringComparer.Ordinal);

        internal ImportProfile(string path)
        {
            SourceName = Path.GetFileName(path);
            SourceBytes = File.Exists(path) ? new FileInfo(path).Length : 0;
        }

        internal Stopwatch Total { get; } = Stopwatch.StartNew();
        internal string SourceName { get; }
        internal long SourceBytes { get; }
        internal int SectionCount { get; set; }
        internal int OccurrenceCount { get; set; }
        internal double SynchronousMilliseconds { get; set; }

        internal void Add(string stage, double milliseconds)
        {
            _stages[stage] = _stages.TryGetValue(stage, out var current)
                ? current + milliseconds
                : milliseconds;
        }

        internal string Format()
        {
            var stages = string.Join(
                "; ",
                _stages.Select(item => $"{item.Key}={Milliseconds(item.Value)} ms"));
            return
                $"[VI PERF] JSON import: file={SourceName}; bytes={SourceBytes}; " +
                $"sections={SectionCount}; occurrences={OccurrenceCount}; {stages}.";
        }
    }
}
