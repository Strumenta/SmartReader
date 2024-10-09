using System;
using System.Collections.Generic;

namespace SmartReaderTests
{
    public sealed class ArticleMetadata
    {
        public string Url { get; init; }

        public bool Readerable { get; init; }

        public string Title { get; init; }

        public string Dir { get; init; }

        public string Byline { get; init; }

        public string Author { get; init; }

        public string PublicationDate { get; init; }

        public string Language { get; init; }

        public Dictionary<string, Uri> AlternativeLanguageUris { get; set; } = new();

        public string Excerpt { get; init; }

        public string SiteName { get; init; }

        public string TimeToRead { get; init; }

        public string FeaturedImage { get; init; }
    }
}