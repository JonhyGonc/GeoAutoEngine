#if NETSTANDARD2_0 || NETFRAMEWORK
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace GeoAutoEngine.Domain.Compatibility
{
    [Serializable]
    public class ArgumentExceptionCompat : System.ArgumentException
    {
        public ArgumentExceptionCompat() { }
        public ArgumentExceptionCompat(string message) : base(message) { }
        public ArgumentExceptionCompat(string message, string? parameterName) : base(message, parameterName) { }
        protected ArgumentExceptionCompat(SerializationInfo info, StreamingContext context) : base(info, context) { }

        public static void ThrowIfNullOrWhiteSpace(string? value, string? parameterName = null)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentExceptionCompat("Value cannot be null or whitespace.", parameterName);
            }
        }
    }

    [Serializable]
    public class ArgumentNullExceptionCompat : System.ArgumentNullException
    {
        public ArgumentNullExceptionCompat() { }
        public ArgumentNullExceptionCompat(string? parameterName) : base(parameterName) { }
        protected ArgumentNullExceptionCompat(SerializationInfo info, StreamingContext context) : base(info, context) { }

        public static void ThrowIfNull(object? value, string? parameterName = null)
        {
            if (value == null)
            {
                throw new ArgumentNullExceptionCompat(parameterName);
            }
        }
    }

    [Serializable]
    public class ArgumentOutOfRangeExceptionCompat : System.ArgumentOutOfRangeException
    {
        public ArgumentOutOfRangeExceptionCompat() { }
        public ArgumentOutOfRangeExceptionCompat(string? parameterName) : base(parameterName) { }
        public ArgumentOutOfRangeExceptionCompat(string? parameterName, string? message) : base(parameterName, message) { }
        protected ArgumentOutOfRangeExceptionCompat(SerializationInfo info, StreamingContext context) : base(info, context) { }

        public static void ThrowIfEqual<T>(T value, T other, string? parameterName = null)
        {
            if (EqualityComparer<T>.Default.Equals(value, other))
            {
                throw new ArgumentOutOfRangeExceptionCompat(parameterName);
            }
        }

        public static void ThrowIfNegative(int value, string? parameterName = null)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeExceptionCompat(parameterName);
            }
        }

        public static void ThrowIfLessThan(int value, int other, string? parameterName = null)
        {
            if (value < other)
            {
                throw new ArgumentOutOfRangeExceptionCompat(parameterName);
            }
        }
    }
}

namespace System
{
    public static class StringCompatibilityExtensions
    {
        public static bool Contains(this string value, string comparisonValue, StringComparison comparisonType)
        {
            return value.IndexOf(comparisonValue, comparisonType) >= 0;
        }

        public static bool Contains(this string value, char comparisonValue, StringComparison comparisonType)
        {
            return value.IndexOf(comparisonValue.ToString(), comparisonType) >= 0;
        }

        public static string Replace(this string value, string oldValue, string newValue, StringComparison comparisonType)
        {
            var index = value.IndexOf(oldValue, comparisonType);
            if (index < 0)
            {
                return value;
            }

            var result = new Text.StringBuilder();
            var position = 0;
            while (index >= 0)
            {
                result.Append(value, position, index - position);
                result.Append(newValue);
                position = index + oldValue.Length;
                index = value.IndexOf(oldValue, position, comparisonType);
            }

            result.Append(value, position, value.Length - position);
            return result.ToString();
        }
    }
}

#endif
