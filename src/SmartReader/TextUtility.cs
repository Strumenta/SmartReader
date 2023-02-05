using System;
using System.Text;
using AngleSharp.Dom;
using AngleSharp.Text;

namespace SmartReader
{
    internal static class TextUtility
    {
        internal static int CountWordsSeparatedByComma(ReadOnlySpan<char> text)
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

        internal static string CleanXmlName(this string str)
        {
            if (str.Length > 0)
            {
                StringBuilder sb = new StringBuilder();
                int startI = 0;
                // Characters that are valid as part of the name might be invalid at the start.
                // We need to make sure the first new character is valid
                while(startI < str.Length && !str[startI].IsXmlNameStart())
                {
                    startI++;
                }                               

                for (var i = startI; i < str.Length; i++)
                {
                    if (str[i].IsXmlName())
                    {
                        sb.Append(str[i]);
                    }
                }

                return sb.ToString();
            }
            else
                return str;
        }
    }
}
