using AngleSharp.Dom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;

namespace SmartReader
{
    /// <summary>
    /// Parsed article
    /// </summary>
    /// <remarks>
    /// You should check the property <c>IsReadable</c> to know whether an article was actually found
    /// </remarks>
    public class Article
    {
        /// <value>The original URI of the source</value>
        public Uri Uri { get; private set; }
        /// <value>The clean title</value>
        public String Title { get; private set; }
        /// <value>The parsed byline</value>
        public String Byline { get; private set; }
        /// <value>The direction of the writing</value>
        public String Dir { get; private set; }
        /// <value>The URI of the main image</value>
        public String FeaturedImage { get; private set; }
        /// <value>The HTML content</value>
        public String Content { get; private set; }
        /// <value>The pure-text content cleaned to be readable</value>
        public String TextContent { get; private set; }
        /// <value>The excerpt provided by the metadata</value>
        public String Excerpt { get; private set; }
        /// <value>The language provided by the metadata</value>
        public String Language { get; private set; }
        /// <value>The author, which can be parsed or read in the metadata</value>
        public String Author { get; private set; }
        /// <value>The name of the website, which can be parsed or read in the metadata </value>
        public String SiteName { get; private set; }
        /// <value>The length in bytes of <c>Content</c></value>
        public int Length { get; private set; }
        /// <value>The average time to read</value>
        /// <remarks>It is based on http://iovs.arvojournals.org/article.aspx?articleid=2166061</remarks>
        public TimeSpan TimeToRead { get; private set; }
        /// <value>The publication date, which can be parsed or read in the metadata</value>
        public DateTime? PublicationDate { get; private set; }
        /// <value>It indicates whether an article was actually found</value>
        public bool IsReadable { get; private set; }        

        private IElement article = null;

        internal Article(Uri uri, string title, string byline, string dir, string language, string author, IElement article, Metadata metadata, bool readable)
        {
            Uri = uri;
            Title = title;
            Byline = String.IsNullOrEmpty(metadata.Byline) ? byline : metadata.Byline;
            Dir = dir;
            Content = article.InnerHtml;
            TextContent = ConvertToPlaintext(article);
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

        /// <summary>
        /// The constructor used when we fail to find an actual article
        /// </summary>
        internal Article(Uri uri, string title, bool readable)
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
                            imageUri = new Uri(Uri.ToAbsoluteURI(imageUri.ToString()));
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

        /// <summary>
        /// Convert images contained in the article to their data URI scheme representation
        /// </summary>
        /// <param name="minSize">The minium size in bytes to be considered a image. Smaller images are removed</param>        
        /// <returns>
        /// An empty Task object
        /// </returns>  
        public async Task ConvertImagesToDataUriAsync(long minSize = 75000)
        {
            var imgs = article != null ? article.QuerySelectorAll("img") : null;            

            if (imgs != null)
            {
                foreach (var img in imgs)
                {
                    if (!String.IsNullOrEmpty(img.GetAttribute("src")))
                    {
                        Uri imageUri = new Uri(img.GetAttribute("src"));

                        try
                        {
                            // download image
                            byte[] bytes = await Reader.GetImageBytesAsync(imageUri);

                            if (bytes.LongLength > minSize)
                            {
                                // convert it to data uri scheme and replace the original source
                                img.SetAttribute("src", Image.ConvertImageToDataUri(imageUri.AbsolutePath, bytes));
                            }
                            else
                            {
                                img.Remove();
                            }                            
                        }
                        catch (Exception e) { }                                                
                    }
                }

                // we update the affected properties
                Content = article.InnerHtml;
            }
        }

        /// <summary>
        /// Convert the article content from HTML to Text cleaning the results
        /// </summary>      
        /// <returns>
        /// A string representing the text
        /// </returns>  
        private static string ConvertToPlaintext(IElement doc)
        {
            StringWriter writer = new StringWriter();

            String text = ConvertToText(doc, writer);

            bool previousSpace = false;            
            bool previousNewline = false;
            int index = 0;

            // fix whitespace 
            // replace tabs with one space
            text = Regex.Replace(text, "\t+", " ");

            // replace multiple newlines with max two
            text = Regex.Replace(text, "(\\r?\\n){3,}", $"{writer.NewLine}{writer.NewLine}");

            StringBuilder stringBuilder = new StringBuilder(text);

            while (index < stringBuilder.Length)
            {
                // carriage return and line feed are not separator characters
                bool isSpace = Char.IsSeparator(stringBuilder[index]);              
                bool isNewline = stringBuilder[index] == '\r' || stringBuilder[index] == '\n';

                // we remove a space before a newline
                if (previousSpace && isNewline)
                    stringBuilder.Remove(index - 1, 1);
                // we remove a space after a newline
                else if (previousNewline && isSpace)
                    stringBuilder.Remove(index, 1);
                // we remove series of spaces
                else if (previousSpace && isSpace)
                    stringBuilder.Remove(index, 1);
                else
                    index++;

                previousSpace = isSpace;
                previousNewline = isNewline;                
            }

            // we trim all whitespace
            text = stringBuilder.ToString().Trim();

            // replace multiple newlines with max two
            text = Regex.Replace(text, "(\\r?\\n){3,}", $"{writer.NewLine}{writer.NewLine}");

            return text;
        }

        /// <summary>
        /// The main function that converts HTML markup to text
        /// </summary>
        private static string ConvertToText(IElement doc, StringWriter text)
        {
            if (doc.NodeType == NodeType.Element && doc.NodeName == "P")
                text.Write(text.NewLine);
            else if (doc.NodeType == NodeType.Element && doc.NodeName == "BR")
                text.Write(text.NewLine);
            
            if (doc.HasChildNodes)
            {
                foreach (INode el in doc.ChildNodes)
                {
                    // if the element has other elements we look inside of them
                    if (el.NodeType == NodeType.Element
                        || el.NodeType == NodeType.EntityReference)
                        ConvertToText(el as IElement, text);
                    // if the element has children text nodes we extract the text
                    if (el.NodeType == NodeType.Text)
                        text.Write(el.TextContent);
                }
            }

            if (doc.NodeType == NodeType.Element && doc.NodeName == "P")
                text.Write(text.NewLine);

            return text.ToString();
        }
    }
}
