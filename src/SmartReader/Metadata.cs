using System;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("SmartReaderTests")]
namespace SmartReader
{    
    internal class Metadata
    {
        internal string Byline { get; set; } = "";
        internal string Title { get; set; } = "";
        internal string Excerpt { get; set; } = "";
        internal string Language { get; set; } = "";
        internal string FeaturedImage { get; set; } = "";
        internal DateTime? PublicationDate { get; set; } = null;
        internal string Author { get; set; } = "";
        internal string SiteName { get; set; } = "";
    }
}
