using System.IO;
using System.Reflection;
using System.Threading;

namespace GeoAutoEngine.Plugin;

internal static class NetFrameworkDependencyResolver
{
#if GEOAUTO_CAD_2024
    private static readonly HashSet<string> PrivateDependencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft.Bcl.AsyncInterfaces",
        "System.Buffers",
        "System.IO.Pipelines",
        "System.Memory",
        "System.Numerics.Vectors",
        "System.Runtime.CompilerServices.Unsafe",
        "System.Text.Encoding.CodePages",
        "System.Text.Encodings.Web",
        "System.Text.Json",
        "System.Threading.Tasks.Extensions",
        "System.ValueTuple",
    };

    private static int registered;
#endif

    public static void Register()
    {
#if GEOAUTO_CAD_2024
        if (Interlocked.Exchange(ref registered, 1) == 0)
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolvePrivateDependency;
        }
#endif
    }

#if GEOAUTO_CAD_2024
    private static Assembly? ResolvePrivateDependency(object? sender, ResolveEventArgs eventArgs)
    {
        var requestedName = new AssemblyName(eventArgs.Name).Name;
        if (string.IsNullOrWhiteSpace(requestedName) || !PrivateDependencies.Contains(requestedName))
        {
            return null;
        }

        var loadedAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly => string.Equals(
                assembly.GetName().Name,
                requestedName,
                StringComparison.OrdinalIgnoreCase));

        if (loadedAssembly is not null)
        {
            return loadedAssembly;
        }

        var pluginDirectory = Path.GetDirectoryName(typeof(PluginEntry).Assembly.Location);
        if (string.IsNullOrWhiteSpace(pluginDirectory))
        {
            return null;
        }

        var dependencyPath = Path.Combine(pluginDirectory, $"{requestedName}.dll");
        return File.Exists(dependencyPath) ? Assembly.LoadFrom(dependencyPath) : null;
    }
#endif
}
