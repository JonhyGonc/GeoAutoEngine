#if GEOAUTO_CAD_2024
namespace GeoAutoEngine.Plugin.VideoInspection.Transport;

internal static class TaskCompletionSourceCompatibility
{
    public static bool TrySetCanceled<T>(
        this TaskCompletionSource<T> completion,
        CancellationToken cancellationToken) => completion.TrySetCanceled();
}
#endif
