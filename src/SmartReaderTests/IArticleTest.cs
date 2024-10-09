using System;
using System.Collections.Generic;

namespace SmartReaderTests
{
    public interface IArticleTest
    {
        Uri Uri { get; set; }
        string Title { get; set; }
        string Byline { get; set; }
        string Dir { get; set; }
        string Content { get; set; }
        string TextContent { get; set; }
        string Excerpt { get; set; }
        string Language { get; set; }
        Dictionary<string, Uri> AlternativeLanguageUris { get; set; }
        string Author { get; set; }
        string SiteName { get; set; }
        string FeaturedImage { get; set; }
        int Length { get; set; }
        TimeSpan TimeToRead { get; set; }
        DateTime? PublicationDate { get; set; }
        bool IsReadable { get; set; }
    }
}