using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using LanguageTeller;
using SmartReader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;

namespace SmartReaderTests
{
    public class NLPTests
    {
        [Fact]
        public void TestLanguageIdentification()
        {
            FastText fastText = new FastText();
            Assert.Equal("en", fastText.TellLanguage("Hello! I am here.").Language);
        }

        [Fact]
        public void TestLanguageIdentificationFail()
        {
            FastText fastText = new FastText();
            // Haitian instead of Latin
            Assert.Equal("ht", fastText.TellLanguage("Hic et nunc.").Language);
        }
    }
}
