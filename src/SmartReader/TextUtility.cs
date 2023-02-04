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
                if (str[0].IsXmlNameStart())
                    sb.Append(str[0]);

                for (var i = 1; i < str.Length; i++)
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
