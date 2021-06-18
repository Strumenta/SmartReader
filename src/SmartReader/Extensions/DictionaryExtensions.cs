using System.Collections.Generic;

namespace SmartReader
{
    internal static class DictionaryExtensions
    {
        public static string? GetValueOrDefault(this Dictionary<string, string> dictionary, string key)
        {
            return dictionary.TryGetValue(key, out string? value) ? value : null;
        }
    }
}
