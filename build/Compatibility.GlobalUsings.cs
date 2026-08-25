#if NETSTANDARD2_0 || NETFRAMEWORK
global using ArgumentException = GeoAutoEngine.Domain.Compatibility.ArgumentExceptionCompat;
global using ArgumentNullException = GeoAutoEngine.Domain.Compatibility.ArgumentNullExceptionCompat;
global using ArgumentOutOfRangeException = GeoAutoEngine.Domain.Compatibility.ArgumentOutOfRangeExceptionCompat;

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}
#endif
