using System;
namespace SmartReader
{
    public class Metadata
    {
        public string Byline { get; set; } = "";
        public string Title { get; set; } = "";
        public string Excerpt { get; set; } = "";
        public string Language { get; set; } = "";
        public string FeaturedImage { get; set; } = "";
        public DateTime? PublicationDate { get; set; } = null;
        public string Author { get; set; } = "";
        public string SiteName { get; set; } = "";
    }
}
