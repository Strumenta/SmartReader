using System;

namespace SmartReader
{
    internal static class ArrayExtensions
    {
        public static bool Contains(this string[] list, string? value)
        {
            if (value is null) return false;

            foreach (var item in list)
            {
                if (item.Equals(value, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
