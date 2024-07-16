using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using AngleSharp.Dom;
using DetectLanguage;

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
        public Uri Uri { get; }

        /// <value>The clean title</value>
        public string Title { get; }

        /// <value>The parsed byline</value>
        public string? Byline { get; }

        /// <value>The direction of the writing</value>
        public string? Dir { get; }

        /// <value>The URI of the main image</value>
        public string? FeaturedImage { get; private set; }

        /// <value>The HTML content</value>
        public string Content { get; private set; }

        /// <value>The excerpt provided by the metadata</value>
        public string? Excerpt { get; }

        /// <value>The language provided by the metadata or detected from DetectLanguage function when metatadata is missing</value>
        public string? Language { get; }

        /// <value>The author, which can be parsed or read in the metadata</value>
        public string? Author { get; }

        /// <value>The name of the website, which can be parsed or read in the metadata </value>
        public string? SiteName { get; }

        /// <value>The publication date, which can be parsed or read in the metadata</value>
        public DateTime? PublicationDate { get; }

        /// <value>It indicates whether an article was actually found</value>
        public bool IsReadable { get; }

        /// <value>It contains the list of errors/exceptions</value>
        public List<Exception> Errors { get; } = new List<Exception>();

        /// <value>It indicates whether the process completed correctly</value>
        public bool Completed
        {
            get
            {
                return Errors.Count == 0;
            }
        }

        /// <summary>The function that will serialize the HTML content of the article</summary>
        /// <value>Default: return InnerHTML property</value>       
        public static Func<IElement, string> Serializer { get; set; } = new Func<IElement, string>(el => el.InnerHtml);

        /// <summary>The function that will extract the text from the HTML content</summary>
        /// <value>Default: the ConvertToPlaintext method</value>       
        public static Func<IElement, string> Converter { get; set; } = ConvertToPlaintext;

        private readonly IElement? _element = null;
        private readonly Reader? _reader;
        private TimeSpan? _timeToRead = null;

        internal IElement? Element => _element;

        /// <value>The average time to read</value>
        /// <remarks>It is based on http://iovs.arvojournals.org/article.aspx?articleid=2166061</remarks>
        public TimeSpan TimeToRead => _timeToRead ??= TimeToReadCalculator.Calculate(this);

        private string? _textContent;

        /// <value>The pure-text content cleaned to be readable</value>
        public string TextContent
        {
            get
            {
                if (_element is null) return string.Empty;

                return _textContent ??= Converter(_element);
            }
        }

        /// <value>The length in chars of <c>TextContent</c></value>
        public int Length => TextContent.Length;

        private static readonly Regex RE_EliminateTabs = new Regex("\t+", RegexOptions.Compiled);
        private static readonly Regex RE_NormalizeNewLines = new Regex("(\\r?\\n){3,}", RegexOptions.Compiled);


        internal Article(Uri uri, string title, string? byline, string? dir, string? language, string? author, IElement element, Metadata metadata, bool readable, Reader reader)
        {
            _element = element;
            _reader = reader;

            Uri = uri;
            Title = title;
            Byline = string.IsNullOrWhiteSpace(byline) ? metadata.Author : byline;
            Dir = dir;
            Content = Serializer(element);
            Excerpt = metadata.Excerpt;
            /* When the language code is missing from the retrieved metadata, we call the DetectLanguage function,
             either passing the article title (if present, to lighten the function load) or the article content. */
            Language = string.IsNullOrWhiteSpace(metadata.Language) ? DetectLanguage(metadata.Title ?? Content).Result : metadata.Language;
            PublicationDate = metadata.PublicationDate;
            Author = string.IsNullOrWhiteSpace(metadata.Author) ? author : metadata.Author;
            SiteName = metadata.SiteName;
            IsReadable = readable;
            FeaturedImage = metadata.FeaturedImage;
        }

        /// <summary>
        /// The constructor used when we fail to find an actual article
        /// </summary>
        internal Article(Uri uri, string title, bool readable)
        {
            IsReadable = readable;
            Uri = uri;
            Title = title;
            Content = "";
            PublicationDate = new DateTime();
        }

        /// <summary>
        /// The constructor used when we fail to find an actual article because of an exception
        /// </summary>
        internal Article(Uri uri, string title, Exception exception)
        {
            IsReadable = false;
            Uri = uri;
            Title = title;
            Content = "";
            PublicationDate = new DateTime();
            Errors.Add(exception);
        }

        /// <summary>
        /// Detects the language of a given piece of text.
        /// </summary>
        /// <param name="pieceOfText">The text to detect the language of.</param>
        /// <returns>The detected language code.</returns>
        public async Task<string> DetectLanguage(string pieceOfText)
        {
            try
            {
                string envName = "DETECT_LANGUAGE_API_KEY";
                string apiKey = Environment.GetEnvironmentVariable(envName);

                if (string.IsNullOrEmpty(apiKey))
                {
                    Console.WriteLine($"---- API key not found in environment variable: {envName}. Skipping language detection ----");
                    return string.Empty; // return empty string
                }

                Console.WriteLine("---- Undetected language code from article metadata: attempting automatic detection ----");
                Console.WriteLine("---- Initializing DetectLanguageClient ----");

                DetectLanguageClient client = new(apiKey);
                UserStatus userStatus = await client.GetUserStatusAsync();

                // Checking the availability of the API service; the free plan offers 1000 call per day with a overall limit of 1MB of text
                if (userStatus.status == "ACTIVE")
                {
                    Console.WriteLine($"---- Service available: {1000 - userStatus.requests} requests left ----");

                    string languageCode = await client.DetectCodeAsync(pieceOfText);
                    Console.WriteLine($"---- Language Code Detected: {languageCode} ----");
                    return languageCode;
                }
                else
                {
                    Console.WriteLine("---- Service unavailable: requests limit reached ----");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"---- Error in DetectLanguage: {ex.Message} ----");
                Errors.Add(ex); // Add exceptions to error list
            }

            return string.Empty;
        }



        /// <summary>
        /// Finds images contained in the article.
        /// </summary>
        /// <param name="minSize">The minium size in bytes to be considered a image.</param>        
        /// <returns>
        /// A Task object with the images found
        /// </returns>  
        public async Task<IReadOnlyList<Image>> GetImagesAsync(long minSize = 75_000)
        {
            var imgs = _element?.GetElementsByTagName("img");

            if (imgs is null)
            {
                return Array.Empty<Image>();
            }

            var images = new List<Image>();

            foreach (var img in imgs)
            {
                if (img.GetAttribute("src") is { Length: > 0 } src)
                {
                    long size = 0;

                    var imageUri = new Uri(src);

                    try
                    {
                        imageUri = new Uri(Uri.ToAbsoluteURI(imageUri.ToString()));
                        size = await _reader!.GetImageSizeAsync(imageUri);
                    }
                    catch { }

                    string? description = img.GetAttribute("alt");
                    string? title = img.GetAttribute("title");

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
            if (string.IsNullOrEmpty(FeaturedImage) && images.Count > 0)
            {
                FeaturedImage = images[0].Source!.ToString();
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
            if (_element is null) return;

            foreach (var img in _element.GetElementsByTagName("img"))
            {
                if (img.GetAttribute("src") is string src)
                {
                    var imageUri = new Uri(src);

                    try
                    {
                        // download image
                        byte[] bytes = await _reader!.GetImageBytesAsync(imageUri).ConfigureAwait(false);

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
                    catch { }
                }
            }

            // we update the affected properties
            Content = _element.InnerHtml;
        }

        /// <summary>
        /// Convert the article content from HTML to Text cleaning the results
        /// </summary>      
        /// <returns>
        /// A string representing the text
        /// </returns>  
        private static string ConvertToPlaintext(IElement doc)
        {
            var sb = new StringBuilder();

            ConvertToText(doc, sb);

            bool previousSpace = false;
            bool previousNewline = false;
            int index = 0;

            string text = sb.ToString();
            // fix whitespace 
            // replace tabs with one space
            text = RE_EliminateTabs.Replace(text, " ");

            var stringBuilder = new StringBuilder(text);

            while (index < stringBuilder.Length)
            {
                // carriage return and line feed are not separator characters
                bool isSpace = char.IsSeparator(stringBuilder[index]);
                bool isNewline = stringBuilder[index] is '\r' or '\n';

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
            text = RE_NormalizeNewLines.Replace(text, $"{Environment.NewLine}{Environment.NewLine}");

            return text;
        }

        /// <summary>
        /// The function that converts HTML markup to text
        /// </summary>
        private static void ConvertToText(IElement doc, StringBuilder text)
        {
            if (doc.NodeType == NodeType.Element && doc.NodeName is "P" or "BR")
            {
                text.AppendLine();
            }

            if (doc.HasChildNodes)
            {
                foreach (INode el in doc.ChildNodes)
                {
                    // if the element has other elements we look inside of them
                    if (el.NodeType is NodeType.Element or NodeType.EntityReference)
                    {
                        ConvertToText((IElement)el, text);
                    }

                    // if the element has children text nodes we extract the text
                    else if (el.NodeType == NodeType.Text)
                    {
                        text.Append(el.TextContent);
                    }
                }
            }

            if (doc.NodeType is NodeType.Element && doc.NodeName is "P")
                text.AppendLine();
        }
    }
}
