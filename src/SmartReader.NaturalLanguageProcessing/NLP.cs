using System;
using System.Threading.Tasks;
using System.IO;
using System.Reflection;
using LanguageTeller;

namespace SmartReader.NaturalLanguageProcessing
{
    public static class NLP
    {
        private static FastText LanguageTeller = new FastText();
        
        /// <summary>
        /// Enable the automatic identification of the language.
        /// </summary>
        public static void Enable()
        {
            Article.LanguageIdentification = IdentifyLanguageUsingNLP;
        }

        /// <summary>
        /// Restore default behavior, which consist in returning the metadata.
        /// </summary>
        public static void RestoreDefaults()
        {
            Article.LanguageIdentification = (text, metadata) => metadata;
        }

        private static string IdentifyLanguageUsingNLP(string text, string? language)
        {
            return LanguageTeller.TellLanguage(text).Language;            
        }
    }
}
