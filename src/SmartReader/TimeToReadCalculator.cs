using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SmartReader
{
    internal static class TimeToReadCalculator
    {
        private static readonly Dictionary<string, int> charactersMinute = new()
        {
            { "Arabic", 612 },
            { "Chinese", 255 },
            { "Dutch", 978 },
            { "English", 987 },
            { "Finnish", 1078 },
            { "French", 998 },
            { "German", 920 },
            { "Hebrew", 833 },
            { "Italian", 950 },
            { "Japanese", 357 },
            { "Polish", 916 },
            { "Portuguese", 913 },
            { "Swedish", 917 },
            { "Slovenian", 885 },
            { "Spanish", 1025 },
            { "Russian", 986 },
            { "Turkish", 1054 }
        };  

        // based on http://iovs.arvojournals.org/article.aspx?articleid=2166061

        public static TimeSpan Calculate(Article article)
        {
            if (string.IsNullOrEmpty(article.TextContent))
            {
                return TimeSpan.Zero;
            }

            int weight = GetWeight(article);

            int letterCount = article.Element?.TextContent.Count(x => x != ' ' && !char.IsPunctuation(x)) ?? 0;

            var result = TimeSpan.FromMinutes(letterCount / weight);

            return result > TimeSpan.Zero ? result : TimeSpan.FromMinutes(1);
        }

        private static int GetWeight(Article article)
        {
            CultureInfo culture = CultureInfo.InvariantCulture;

            if (!string.IsNullOrEmpty(article.Language))
            {
                try
                {
                    culture = new CultureInfo(article.Language);
                }
                catch (CultureNotFoundException)
                { }
            }

            var cpm = charactersMinute.FirstOrDefault(x => culture.EnglishName.StartsWith(x.Key, StringComparison.Ordinal));

            // 960 is the average excluding the three outliers languages
            int weight = cpm.Value > 0 ? cpm.Value : 960;

            return weight;
        }
    }
}
