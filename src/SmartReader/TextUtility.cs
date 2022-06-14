using System;

using AngleSharp.Dom;

namespace SmartReader
{
    internal static class TextUtility
    {
        public static int CountWordsSeparatedByComma(ReadOnlySpan<char> text)
        {
            int commaCount = 0;
            int commaIndex;

            while ((commaIndex = text.IndexOf(',')) > -1)
            {
                text = text.Slice(commaIndex + 1);

                commaCount++;
            }

            return commaCount + 1;
        }


    }
}
