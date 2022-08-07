using AngleSharp.Browser;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartReader
{
    internal class HeaderEncodingProvider : IEncodingProvider
    {
        private string _charset;
        public HeaderEncodingProvider(string charset)
        {
            _charset = charset;
        }

        public Encoding Suggest(string locale)
        {
            // this method will return the provided encoding whatever the current locale            
            return Encoding.GetEncoding(_charset);
        }
    }
}
