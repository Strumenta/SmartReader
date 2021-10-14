using System;

namespace SmartReader
{
    internal sealed class Metadata
    {        
        internal string? Title { get; set; }

        internal string? Excerpt { get; set; }

        internal string? Language { get; set; }

        internal string? FeaturedImage { get; set; }

        internal DateTime? PublicationDate { get; set; }

        internal string? Author { get; set; }

        internal string? SiteName { get; set; }
    }
}
