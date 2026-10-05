#if NETFRAMEWORK
// Revit 2024 and earlier run the add-in on .NET Framework 4.8. These are the few language
// support types and BCL overloads the code uses that framework does not ship. Revit 2025+
// (.NET 8) has them built in, so this file compiles to nothing there.

namespace System.Runtime.CompilerServices
{
    /// <summary>Enables <c>init</c> accessors and records on .NET Framework.</summary>
    internal static class IsExternalInit
    {
    }
}

namespace System
{
    internal static class StringPolyfills
    {
        /// <summary>The .NET Core <c>string.Contains(string, StringComparison)</c> overload.</summary>
        public static bool Contains(this string source, string value, StringComparison comparison) =>
            source.IndexOf(value, comparison) >= 0;
    }
}
#endif
