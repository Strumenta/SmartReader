using AngleSharp.Dom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Globalization;
using System.Net.Http;
using System.Text;

namespace SmartReader
{
    /// <summary>
    /// Parsed article
    /// </summary>
    public class Article
    {
        public Uri Uri { get; private set; }
        public String Title { get; private set; }
        public String Byline { get; private set; }
        public String Dir { get; private set; }
        public String FeaturedImage { get; private set; }
        public String Content { get; private set; }
        public String TextContent { get; private set; }
        public String Excerpt { get; private set; }
        public String Language { get; private set; }
        public String Author { get; private set; }
        public String SiteName { get; private set; }
        public int Length { get; private set; }
        public TimeSpan TimeToRead { get; private set; }
        public DateTime? PublicationDate { get; private set; }
        public bool IsReadable { get; private set; }        

        private IElement article = null;

        public Article(Uri uri, string title, string byline, string dir, string language, string author, IElement article, Metadata metadata, bool readable)
        {
            Uri = uri;
            Title = title;
            Byline = String.IsNullOrEmpty(metadata.Byline) ? byline : metadata.Byline;
            Dir = dir;
            Content = article.InnerHtml;
            TextContent = article.TextContent;
            Excerpt = metadata.Excerpt;
            Length = article.TextContent.Length;
            Language = String.IsNullOrEmpty(metadata.Language) ? language : metadata.Language;
            PublicationDate = metadata.PublicationDate;
            Author = String.IsNullOrEmpty(metadata.Author) ? author : metadata.Author;
            SiteName = metadata.SiteName;
            IsReadable = readable;
            // based on http://iovs.arvojournals.org/article.aspx?articleid=2166061
            TimeToRead = TimeSpan.FromMinutes(article.TextContent.Count(x => x != ' ' && !Char.IsPunctuation(x)) / GetWeightTimeToRead()) > TimeSpan.Zero ? TimeSpan.FromMinutes(article.TextContent.Count(x => x != ' ' && !Char.IsPunctuation(x)) / GetWeightTimeToRead()) : TimeSpan.FromMinutes(1);
            FeaturedImage = metadata.FeaturedImage;

            this.article = article;
        }

        private int GetWeightTimeToRead()
        {
            CultureInfo culture = CultureInfo.InvariantCulture;

            try
            {
                if (!String.IsNullOrWhiteSpace(Language))
                    culture = new CultureInfo(Language);
            }
            catch(CultureNotFoundException)
            { }

            Dictionary<String, int> CharactersMinute = new Dictionary<string, int>()
            {
                { "Arabic", 612 },
                { "Chinese", 255 },
                { "Dutch", 978 },
                { "English", 987 },
                { "Finnish", 1078 },
                { "French", 998 },
                { "German", 920 },
                { "Hebrew", 833 },
                { "Italian", 950 },
                { "Japanese", 357 },
                { "Polish", 916 },
                { "Portuguese", 913 },
                { "Swedish", 917 },
                { "Slovenian", 885 },
                { "Spanish", 1025 },
                { "Russian", 986 },
                { "Turkish", 1054 }
            };

            var cpm = CharactersMinute.FirstOrDefault(x => culture.EnglishName.StartsWith(x.Key));

            // 960 is the average excluding the three outliers languages
            int weight = cpm.Value > 0 ? cpm.Value : 960;

            return weight;
        }

        public Article(Uri uri, string title, bool readable)
        {
            IsReadable = readable;
            Uri = uri;
            Title = title;
            Dir = "";
            Byline = "";
            Content = "";
            TextContent = "";
            Excerpt = "";
            Length = 0;
            Language = "";
            PublicationDate = new DateTime();
            Author = "";
            TimeToRead = new TimeSpan();
            FeaturedImage = "";
        }

        /// <summary>
        /// Finds images contained in the article.
        /// </summary>
        /// <param name="minSize">The minium size in bytes to be considered a image.</param>        
        /// <returns>
        /// A Task object with the images found
        /// </returns>  
        public async Task<IEnumerable<Image>> GetImagesAsync(long minSize = 75000)
        {
            List<Image> images = new List<Image>();

            var imgs = article != null ? article.QuerySelectorAll("img") : null;

            if (imgs != null)
            {
                foreach (var img in imgs)
                {
                    if (!String.IsNullOrEmpty(img.GetAttribute("src")))
                    {
                        long size = 0;

                        Uri imageUri = new Uri(img.GetAttribute("src"));

                        try
                        {
                            imageUri = new Uri(this.Uri.ToAbsoluteURI(imageUri.ToString()));
                            size = await Reader.GetImageSizeAsync(imageUri);
                        }
                        catch (Exception e) { }

                        string description = img.GetAttribute("alt");
                        string title = img.GetAttribute("title");

                        if (size > minSize)
                        {
                            images.Add(new Image()
                            {
                                Size = size,
                                Source = imageUri,
                                Description = description,
                                Title = title
                            });
                        }
                    }
                }

                // if there is no featured image, let's set the first one we found
                if (String.IsNullOrEmpty(FeaturedImage) && images.Count > 0)
                    FeaturedImage = images[0].Source.ToString();
            }

            return images;
        }
    }
}
