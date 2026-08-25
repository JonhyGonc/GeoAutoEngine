using System.Collections.ObjectModel;

namespace GeoAutoEngine.Domain;

internal static class CollectionSnapshot
{
    public static IReadOnlyList<T> List<T>(IEnumerable<T> values, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        return Array.AsReadOnly(values.ToArray());
    }

    public static IReadOnlyDictionary<string, T> Dictionary<T>(
        IEnumerable<KeyValuePair<string, T>> values,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        var copy = new SortedDictionary<string, T>(StringComparer.Ordinal);
        foreach (var pair in values)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pair.Key, parameterName);
            copy.Add(pair.Key, pair.Value);
        }

        return new ReadOnlyDictionary<string, T>(copy);
    }
}
