using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using AngleSharp;
using AngleSharp.Common;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

namespace SmartReader
{
    /// <summary>The main Reader class</summary>
    /// <remarks>
	/// <para>This code is based on a port of the readability library of Firefox Reader View
    /// available at: https://github.com/mozilla/readability. Which is heavily based on Arc90's readability.js (1.7f.1) script available at: http://code.google.com/p/arc90labs-readability </para>
    /// </remarks>
    public class Reader : IDisposable
    {
        private static Lazy<HttpMessageHandler> _httpClientHandler = new Lazy<HttpMessageHandler>(() => new HttpClientHandler());
        private string? _userAgent = "SmartReader Library";

        private readonly Uri uri;
        private IHtmlDocument? doc;
        private string articleTitle;
        private string? articleByline;
        private string? articleDir;
        private string? language;
        private string? author;
        private string? charset;

        private readonly struct Attempt
        {
            public Attempt(IElement content, long length)
            {
                Content = content;
                Length = length;
            }

            public IElement Content { get; }

            public long Length { get; }
        }

        private List<Attempt> attempts = new();

        // Start with all flags set        
        private Flags flags = Flags.StripUnlikelys | Flags.WeightClasses | Flags.CleanConditionally;

        /// <summary>Max number of nodes supported by this parser</summary>
        /// <value>Default: 0 (no limit)</value>        
        public int MaxElemsToParse { get; set; } = 0;

        /// <summary>The number of top candidates to consider when analysing how tight the competition is among candidates</summary>
        /// <value>Default: 5</value>
        public int NTopCandidates { get; set; } = 5;

        /// <summary>
        /// The default number of characters an article must have in order to return a result
        /// </summary>
        /// <value>Default: 500</value>
        public int CharThreshold { get; set; } = 500;

        /// <summary>
        /// The default level of depth a node must have to be used for scoring
        /// Nodes without as many ancestors as this level are not counted
        /// </summary>
        /// <value>Default: 5</value>
        public int AncestorsDepth { get; set; } = 5;

        /// <summary>
        /// The default number of characters a paragraph must have in order to be used for scoring
        /// </summary>
        /// <value>Default: 25</value>
        public int ParagraphThreshold { get; set; } = 25;

        private static readonly IEnumerable<string> s_page = new string[] { "page" };

        private string[] classesToPreserve = { "page" };

        /// <summary>
        /// The classes that must be preserved
        /// </summary>
        /// <value>Default: "page"</value>
        public string[] ClassesToPreserve
        {
            get
            {
                return classesToPreserve;
            }
            set
            {
                classesToPreserve = value;

                classesToPreserve = classesToPreserve.Union(s_page).ToArray();
            }
        }

        /// <summary>
        /// Whether to preserve classes
        /// </summary>
        /// <value>Default: false</value>
        public bool KeepClasses { get; set; } = false;

        /// <summary>Set the Debug option and write the data with logger</summary>
        /// <value>Default: false</value>
        public bool Debug { get; set; } = false;

        /// <summary>Set the amount of information written to the logger</summary>
        /// <value>Default: ReportLevel.Issue</value>
        public ReportLevel Logging { get; set; } = ReportLevel.Issue;

        /// <summary>The action that will log any message</summary>
        /// <value>Default: empty action</value>       
        public Action<string> LoggerDelegate { get; set; } = new Action<string>((msg) => { });

        /// <summary>The library tries to determine if it will find an article before actually trying to do it. This option decides whether to continue if the library heuristics fails. This value is ignored if Debug is set to true</summary>
        /// <value>Default: true</value>
        public bool ContinueIfNotReadable { get; set; } = true;

        /// <summary>Element tags to score by default. These must be alphanumerically sorted.</summary>
        /// <value>Default: false</value>
        public readonly string[] TagsToScore = new[] { "H2", "H3", "H4", "H5", "H6", "P", "PRE", "SECTION", "TD" };

        /// <summary>The library look first at JSON-LD to determine metadata.
        /// This setting gives you the option of disabling it</summary>
        /// <value>Default: false</value>          
        public bool DisableJSONLD { get; set; } = false;

        /// <summary>The minimum node content length used to decide if the document is readerable.
        /// You can set language-based values.</summary>
        /// <value>Default: 140</value>        
        public Dictionary<string, int> MinContentLengthReadearable { get; set; } = new()
        {
            { "Default", 140 },
            { "English", 140 }
        };

        /// <summary>The minumum cumulated 'score' used to determine if the document is readerable.</summary>
        /// <value>Default: 20</value>        
        public int MinScoreReaderable { get; set; } = 20;

        /// <summary>The function used to determine if a node is visible. Used in the process of determinting if the document is readerable.</summary>
        /// <value>Default: NodeUtility.IsProbablyVisible</value>        
        public Func<IElement, bool> IsNodeVisible { get; set; } = NodeUtility.IsProbablyVisible;

        /// <summary>
        /// Whether to force the encoding provided in the response header
        /// </summary>
        /// <value>Default: false</value>
        public bool ForceHeaderEncoding { get; set; } = false;

        /// <summary>
        /// A number that is added to the base link density threshold during the shadiness checks. This can be used to penalize nodes with a high link density or vice versa
        /// </summary>
        /// <value>Default: false</value>
        public double LinkDensityModifier { get; set; } = 0.0;


        // All of the regular expressions in use within readability.
        // Defined up here so we don't instantiate them repeatedly in loops.

        private Regex RE_UnlikelyCandidates   = G_RE_UnlikelyCandidates;
        private Regex RE_OkMaybeItsACandidate = G_RE_OkMaybeItsACandidate;
        private Regex RE_Positive             = G_RE_Positive;
        private Regex RE_Negative             = G_RE_Negative;
        private Regex RE_Extraneous           = G_RE_Extraneous;
        private Regex RE_Byline               = G_RE_Byline;
        private Regex RE_ReplaceFonts         = G_RE_ReplaceFonts;        
        private Regex RE_Videos               = G_RE_Videos;
        private Regex RE_NextLink             = G_RE_NextLink;
        private Regex RE_PrevLink             = G_RE_PrevLink;
        private Regex RE_ShareElements        = G_RE_ShareElements;        

        // Use global Regex that are pre-compiled and shared across instances (that have not customized anything)
        private static readonly Regex G_RE_UnlikelyCandidates = new Regex(@"-ad-|ai2html|banner|breadcrumbs|combx|comment|community|cover-wrap|disqus|extra|footer|gdpr|header|legends|menu|related|remark|replies|rss|shoutbox|sidebar|skyscraper|social|sponsor|supplemental|ad-break|agegate|pagination|pager|popup|yom-remote|reacties|commentaires|Kommentare|comentarios", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex G_RE_OkMaybeItsACandidate = new Regex(@"and|article|body|column|content|main|shadow", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex G_RE_Positive = new Regex(@"article|body|content|entry|hentry|h-entry|main|page|pagination|post|text|blog|story", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex G_RE_Negative = new Regex(@"-ad-|hidden|^hid$|hid$|hid|^hid|banner|combx|comment|com-|contact|footer|gdpr|masthead|media|meta|outbrain|promo|related|scroll|share|shoutbox|sidebar|skyscraper|sponsor|shopping|tags|widget", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex G_RE_Extraneous = new Regex(@"print|archive|comment|discuss|e[\-]?mail|share|reply|all|login|sign|single|utility", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex G_RE_Byline = new Regex(@"byline|author|dateline|writtenby|p-author", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex G_RE_ReplaceFonts = new Regex(@"<(\/?)font[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex G_RE_Videos = new Regex(@"\/\/(www\.)?((dailymotion|youtube|youtube-nocookie|player\.vimeo|v\.qq)\.com|(archive|upload\.wikimedia)\.org|player\.twitch\.tv)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex G_RE_NextLink = new Regex(@"(next|weiter|continue|>([^\|]|$)|»([^\|]|$))", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex G_RE_PrevLink = new Regex(@"(prev|earl|old|new|<|«)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex G_RE_ShareElements = new Regex(@"(\b|_)(share|sharedaddy)(\b|_)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex G_RE_B64DataUrl = new Regex(@"^data:\s*([^\s;,]+)\s*;\s*base64\s*,", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        // Commas as used in Latin, Sindhi, Chinese and various other scripts.
        // see: https://en.wikipedia.org/wiki/Comma#Comma_variants
        private static readonly Regex G_RE_Commas = new Regex(@"\u002C|\u060C|\uFE50|\uFE10|\uFE11|\u2E41|\u2E34|\u2E32|\uFF0C", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // used to see if a node's content matches words commonly used for ad blocks or loading indicators
        private static readonly Regex G_AdWords = new Regex(@"^(ad(vertising|vertisement)?|pub(licité)?|werb(ung)?|广告|Реклама|Anuncio|pubblicità)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex G_LoadingWords = new Regex(@"^((loading|正在加载|Загрузка|chargement|cargando|caricamento)(…|\.\.\.)?)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex RE_Whitespace = new Regex(@"^\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly string[] alterToDivExceptions = { "ARTICLE", "DIV", "P", "SECTION", "OL", "UL" };

        private static readonly string[] unlikelyRoles = { "menu", "menubar", "complementary", "navigation", "alert", "alertdialog", "dialog" };

        private readonly List<Action<IElement>> CustomOperationsStart = new();

        private readonly List<Action<IElement>> CustomOperationsEnd = new();

        private static readonly string[] s_p_pre_article = { "p", "pre", "article" };
        private static readonly string[] s_img_picture_figure = { "img", "picture", "figure" };
        private static readonly string[] s_ul_ol = { "ul", "ol" };
        private static readonly string[] s_h1_h2 = { "h1", "h2" };
        private static readonly string[] s_h1_h2_h3_h4_h5_h6 = { "h1", "h2", "h3", "h4", "h5", "h6" };
        private static readonly string[] s_IMG_PICTURE = { "IMG", "PICTURE" };
        
        // internal flag for standard dispose implementation
        private bool disposedValue;

        /// <summary>
        /// Reads content from the given URI.
        /// </summary>
        /// <param name="uri">A string representing the URI from which to extract the content.</param>
        /// <returns>
        /// An initialized SmartReader object
        /// </returns>        
        public Reader(string uri)
        {
            this.uri = new Uri(uri);

            articleTitle = "";
        }

        /// <summary>
        /// Reads content from the given text. It needs the uri to make some checks.
        /// </summary>
        /// <param name="uri">A string representing the original URI of the article.</param>
        /// <param name="text">A string from which to extract the article.</param>
        /// <returns>
        /// An initialized SmartReader object
        /// </returns>        
        public Reader(string uri, string text)
        {
            this.uri = new Uri(uri);

            var context = BrowsingContext.New(Configuration.Default);
            var parser = new HtmlParser(new HtmlParserOptions { IsScripting = true }, context);
            doc = parser.ParseDocument(text);

            articleTitle = "";
        }

        /// <summary>
        /// Reads content from the given stream. It needs the uri to make some checks.
        /// </summary>
        /// <param name="uri">A string representing the original URI of the article.</param>
        /// <param name="source">A stream from which to extract the article.</param>
        /// <returns>
        /// An initialized SmartReader object
        /// </returns>        
        public Reader(string uri, Stream source)
        {
            this.uri = new Uri(uri);

            var context = BrowsingContext.New(Configuration.Default);
            var parser = new HtmlParser(new HtmlParserOptions { IsScripting = true }, context);
            doc = parser.ParseDocument(source);

            articleTitle = "";
        }

        /// <summary>
        /// Inizialize Reader with provided IHtmlDocument. It needs the uri to make some checks.
        /// </summary>
        /// <param name="uri">A string representing the original URI of the article.</param>
        /// <param name="html">An existing IHtmlDocument created with AngleSharp.</param>
        /// <returns>
        /// An initialized SmartReader object
        /// </returns>        
        public Reader(string uri, IHtmlDocument html)
        {
            this.uri = new Uri(uri);

            doc = html;

            articleTitle = "";
        }

        /// <summary>
        /// Add a custom operation to be performed before the article is parsed
        /// </summary>
        /// <param name="operation">The operation that will receive the HTML content before any operation</param>
        public Reader AddCustomOperationStart(Action<IElement> operation)
        {
            CustomOperationsStart.Add(operation);
            return this;
        }

        /// <summary>
        /// Remove a custom operation to be performed before the article is parsed
        /// </summary>
        /// <param name="operation">The operation to remove</param>
        public Reader RemoveCustomOperationStart(Action<IElement> operation)
        {
            CustomOperationsStart.Remove(operation);
            return this;
        }

        /// <summary>
        /// Remove all custom operation to be performed before the article is parsed
        /// </summary>
        public Reader RemoveAllCustomOperationsStart()
        {
            CustomOperationsStart.Clear();
            return this;
        }

        /// <summary>
        /// Add a custom operation to be performed after the article is parsed
        /// </summary>
        /// <param name="operation">The operation that will receive the final article</param>
        public Reader AddCustomOperationEnd(Action<IElement> operation)
        {
            CustomOperationsEnd.Add(operation);
            return this;
        }

        /// <summary>
        /// Remove a custom operation to be performed after the article is parsed
        /// </summary>    
        /// <param name="operation">The operation to remove</param>
        public Reader RemoveCustomOperationEnd(Action<IElement> operation)
        {
            CustomOperationsEnd.Remove(operation);
            return this;
        }

        /// <summary>
        /// Remove all custom operation to be performed after the article is parsed
        /// </summary>    
        public Reader RemoveAllCustomOperationsEnd()
        {
            CustomOperationsEnd.Clear();
            return this;
        }

        /// <summary>
        /// Remove all custom operations
        /// </summary>    
        public Reader RemoveAllCustomOperations()
        {
            CustomOperationsStart.Clear();
            CustomOperationsEnd.Clear();
            return this;
        }

        /// <summary>
        /// Read and parse the article asynchronously from the given URI.
        /// </summary>
        /// <returns>
        /// An async Task Article object with all the data extracted
        /// </returns>    
        public async Task<Article> GetArticleAsync()
        {
            try
            {
                if (doc is null)
                {
                    var stream = await GetStreamAsync(uri).ConfigureAwait(false);
                    var context = string.IsNullOrEmpty(charset) ? BrowsingContext.New(Configuration.Default)
                                                                : BrowsingContext.New(Configuration.Default.With(new HeaderEncodingProvider(charset!)));
                    var parser = new HtmlParser(new HtmlParserOptions { IsScripting = true }, context);

                    // this is necessary because AngleSharp consider the encoding set in BrowsingContext
                    // just as a suggestion. It can ignore it, if it believes it is wrong.
                    // In case it ignores, it uses the default UTF8 encoding
                    if (!string.IsNullOrEmpty(charset) && ForceHeaderEncoding)
                    {
                        var bytes = Encoding.Convert(Encoding.GetEncoding(charset), Encoding.UTF8, ((MemoryStream)stream).ToArray());
                        stream = new MemoryStream(bytes);
                    }

                    doc = parser.ParseDocument(stream);
                }

            
                return Parse();
            }
            catch (Exception ex)
            {
                return new Article(uri, articleTitle, ex);
            }
            
        }

        /// <summary>
        /// Read and parse the article from the given URI.
        /// </summary>    
        /// <returns>
        /// An Article object with all the data extracted
        /// </returns>    

        public Article GetArticle()
        {
            try
            {
                if (doc is null)
                {
                    var stream = GetStreamAsync(uri).GetAwaiter().GetResult();
                    var context = string.IsNullOrEmpty(charset) ? BrowsingContext.New(Configuration.Default)
                                                                : BrowsingContext.New(Configuration.Default.With(new HeaderEncodingProvider(charset!)));
                    var parser = new HtmlParser(new HtmlParserOptions { IsScripting = true }, context);

                    // this is necessary because AngleSharp consider the encoding set in BrowsingContext
                    // just as a suggestion. It can ignore it, if it believes it is wrong.
                    // In case it ignores, it uses the default UTF8 encoding
                    if (!string.IsNullOrEmpty(charset) && ForceHeaderEncoding)
                    {
                        var bytes = Encoding.Convert(Encoding.GetEncoding("iso-8859-1"), Encoding.UTF8, ((MemoryStream)stream).ToArray());
                        stream = new MemoryStream(bytes);
                    }

                    doc = parser.ParseDocument(stream);
                }

                return Parse();
            }
            catch(Exception ex)
            {
                return new Article(uri, articleTitle, ex);
            }
        }

        /// <summary>
        /// Read and parse asynchronously the article from the given URI.
        /// </summary>
        /// <param name="uri">A string representing the original URI to extract the content from.</param>
        /// <param name="userAgent">A string representing a custom user agent.</param>
        /// <returns>
        /// An async Task Article object with all the data extracted
        /// </returns>    
        public static async Task<Article> ParseArticleAsync(string uri, string? userAgent = null)
        {
            return await new Reader(uri).SetCustomUserAgent(userAgent).GetArticleAsync();
        }

        /// <summary>
        /// Read and parse the article from the given URI.
        /// </summary>
        /// <param name="uri">A string representing the original URI to extract the content from.</param>
        /// <param name="userAgent">A string representing a custom user agent.</param>
        /// <returns>
        /// An Article object with all the data extracted
        /// </returns>    

        public static Article ParseArticle(string uri, string? userAgent = null)
        {
            try
            {
                var smartReader = new Reader(uri).SetCustomUserAgent(userAgent);

                var stream = smartReader.GetStreamAsync(new Uri(uri)).GetAwaiter().GetResult();
                var context = BrowsingContext.New(Configuration.Default);
                var parser = new HtmlParser(new HtmlParserOptions { IsScripting = true }, context);

                smartReader.doc = parser.ParseDocument(stream);

                return smartReader.Parse();
            }
            catch(Exception ex)
            {
                return new Article(new Uri(uri), "", ex);
            }
        }

        /// <summary>
        /// Read and parse the article from the given text. It needs the uri to make some checks.
        /// </summary>
        /// <param name="uri">A string representing the original URI of the article.</param>
        /// <param name="text">A string from which to extract the article.</param>
        /// <param name="userAgent">A string representing a custom user agent.</param>
        /// <returns>
        /// An article object with all the data extracted
        /// </returns>    
        public static Article ParseArticle(string uri, string text, string? userAgent = null)
        {
            try
            {
                return new Reader(uri, text).SetCustomUserAgent(userAgent).Parse();
            }
            catch (Exception ex)
            {
                return new Article(new Uri(uri), "", ex);
            }
        }

        /// <summary>
        /// Read and parse the article from the given stream. It needs the uri to make some checks.
        /// </summary>
        /// <param name="uri">A string representing the original URI of the article.</param>
        /// <param name="source">A stream from which to extract the article.</param>
        /// <returns>
        /// An article object with all the data extracted
        /// </returns>    
        public static Article ParseArticle(string uri, Stream source)
        {
            try
            {
                return new Reader(uri, source).Parse();
            }
            catch (Exception ex)
            {
                return new Article(new Uri(uri), "", ex);
            }
        }


        /// <summary>
        /// Run any post-process modifications to article content as necessary.
        /// </summary>
        /// <param name="articleContent">A string representing the original URI of the article.</param>
        /// <returns>
        /// void
        /// </returns>  
        private void PostProcessContent(IElement articleContent)
        {
            // Readability cannot open relative uris so we convert them to absolute uris.
            Readability.FixRelativeUris(articleContent, this.uri, this.doc!);

            Readability.SimplifyNestedElements(articleContent);

            // Remove classes
            if (!KeepClasses)
                Readability.CleanClasses(articleContent, this.ClassesToPreserve);

            // Remove attributes we set
            if (!Debug)
            {
                CleanReaderAttributes(articleContent, "dataTable");
                CleanReaderAttributes(articleContent, "readability-score");
            }
        }

        /// <summary>
        /// <para>Prepare the HTML document for readability to scrape it.</para>
        /// <para>This includes things like stripping javascript, CSS, and handling terrible markup.</para>
        /// </summary>     
        /// <returns>
        /// void
        /// </returns> 
        private void PrepDocument()
        {
            // Remove all style tags in head
            NodeUtility.RemoveNodes(doc!.GetElementsByTagName("style"), null);

            if (doc.Body != null)
            {
                ReplaceBrs(doc.Body);
            }

            NodeUtility.ReplaceNodeTags(doc.GetElementsByTagName("font"), "SPAN");
        }

        /// <summary>
        /// <para>Replaces 2 or more successive &lt;br&gt; elements with a single &lt;p&gt;.</para>                
        /// <para>Whitespace between &lt;br&gt; elements are ignored. For example:</para>
        /// <para>&lt;div&gt;foo&lt;br&gt;bar&lt;br&gt; &lt;br&gt;&lt;br&gt;abc&lt;/div></para>
        /// <para>will become:</para>
        /// <para>&lt;div&gt;foo&lt;br&gt;bar&lt;p&gt;abc&lt;/p&gt;&lt;/div&gt;</para>
        /// </summary>
        private void ReplaceBrs(IElement elem)
        {
            NodeUtility.ForEachElement(elem.GetElementsByTagName("br"), br =>
            {
                var next = br.NextSibling;

                // Whether 2 or more <br> elements have been found and replaced with a
                // <p> block.
                var replaced = false;

                // If we find a <br> chain, remove the <br>s until we hit another element
                // or non-whitespace. This leaves behind the first <br> in the chain
                // (which will be replaced with a <p> later).
                while ((next = NodeUtility.NextElement(next, RE_Whitespace)) is { NodeName: "BR" })
                {
                    replaced = true;
                    var brSibling = next.NextSibling;
                    next.Parent!.RemoveChild(next);
                    next = brSibling;
                }

                // If we removed a <br> chain, replace the remaining <br> with a <p>. Add
                // all sibling nodes as children of the <p> until we hit another <br>
                // chain.
                if (replaced)
                {
                    var p = doc!.CreateElement("p");
                    br.Parent!.ReplaceChild(p, br);

                    next = p.NextSibling;
                    while (next != null)
                    {
                        // If we've hit another <br><br>, we're done adding children to this <p>.
                        if ((next as IElement)?.TagName is "BR")
                        {
                            var nextElem = NodeUtility.NextElement(next.NextSibling, RE_Whitespace);
                            if (nextElem is { TagName: "BR" })
                                break;
                        }

                        if (!NodeUtility.IsPhrasingContent(next))
                            break;

                        // Otherwise, make this node a child of the new <p>.
                        var sibling = next.NextSibling;
                        p.AppendChild(next);
                        next = sibling;
                    }

                    while (p.LastChild != null && NodeUtility.IsWhitespace(p.LastChild))
                        p.RemoveChild(p.LastChild);

                    if (p.Parent!.NodeName is "P")
                        NodeUtility.SetNodeTag(p.ParentElement!, "DIV");

                }
            });
        }

        /// <summary>
        /// Remove attributes Reader added to store values.
        /// </summary>
        private void CleanReaderAttributes(IElement? node, string attribute)
        {
            if (!string.IsNullOrEmpty(node?.GetAttribute(attribute)))
            {
                node?.RemoveAttribute(attribute);
            }

            for (node = node?.FirstElementChild; node != null; node = node?.NextElementSibling)
            {
                CleanReaderAttributes(node, attribute);
            }
        }

        /// <summary>
        /// Prepare the article node for display. Clean out any inline styles,
        /// iframes, forms, strip extraneous &lt;p&gt; tags, etc.
        /// </summary>
        private void PrepArticle(IElement articleContent)
        {
            NodeUtility.CleanStyles(articleContent);

            // Check for data tables before we continue, to avoid removing items in
            // those tables, which will often be isolated even though they're
            // visually linked to other content-ful elements (text, images, etc.).
            MarkDataTables(articleContent);

            FixLazyImages(articleContent);

            CleanConditionally(articleContent, "form");
            CleanConditionally(articleContent, "fieldset");
            Clean(articleContent, "object");
            Clean(articleContent, "embed");
            Clean(articleContent, "footer");
            Clean(articleContent, "link");
            Clean(articleContent, "aside");

            // Clean out elements with little content that have "share" in their id/class combinations from final top candidates,
            // which means we don't remove the top candidates even they have "share".

            var shareElementThreshold = CharThreshold;

            NodeUtility.ForEachElement(articleContent.Children, (topCandidate) =>
            {
                NodeUtility.CleanMatchedNodes(topCandidate, (node, matchString) =>
                {
                    return RE_ShareElements.IsMatch(matchString) && node.TextContent.Length < shareElementThreshold;
                });
            });

            Clean(articleContent, "iframe");
            Clean(articleContent, "input");
            Clean(articleContent, "textarea");
            Clean(articleContent, "select");
            Clean(articleContent, "button");
            CleanHeaders(articleContent);

            // Do these last as the previous stuff may have removed junk
            // that will affect these
            CleanConditionally(articleContent, "table");
            CleanConditionally(articleContent, "ul");
            CleanConditionally(articleContent, "div");

            // replace H1 with H2 as H1 should be only title that is displayed separately
            NodeUtility.ReplaceNodeTags(articleContent.GetElementsByTagName("h1"), "h2");

            // Remove extra paragraphs
            NodeUtility.RemoveNodes(articleContent.GetElementsByTagName("p"), static paragraph =>
            {
                // At this point, nasty iframes have been removed, only remain embedded video ones.
                var contentElementCount = NodeUtility.GetAllNodesWithTag(paragraph, new string[] { 
                        "img", "embed", "object", "iframe" 
                    }).Length;
                
                return contentElementCount == 0 && string.IsNullOrEmpty(NodeUtility.GetInnerText(paragraph, false));
            });

            NodeUtility.ForEachElement(articleContent.GetElementsByTagName("br"), static br =>
            {
                var next = NodeUtility.NextElement(br.NextSibling, RE_Whitespace);
                if (next is { TagName: "P" })
                    br.Parent!.RemoveChild(br);
            });

            // Remove single-cell tables
            NodeUtility.ForEachElement(articleContent.GetElementsByTagName("table"), static tableEl =>
            {
                var tbody = NodeUtility.HasSingleTagInsideElement(tableEl, "TBODY") ? tableEl.FirstElementChild! : tableEl;
                if (NodeUtility.HasSingleTagInsideElement(tbody, "TR"))
                {
                    var row = tbody.FirstElementChild!;
                    if (NodeUtility.HasSingleTagInsideElement(row, "TD"))
                    {
                        var cell = row.FirstElementChild!;
                        cell = NodeUtility.SetNodeTag(cell, cell.ChildNodes.All(NodeUtility.IsPhrasingContent) ? "P" : "DIV");
                        tableEl.Parent!.ReplaceChild(cell, tableEl);
                    }
                }
            });
        }

        /// <summary>
        /// Initialize a node with the readability object. Also checks the
        /// className/id for special names to add to its score.
        /// </summary>
        private void InitializeNode(IElement node)
        {
            SetReadabilityScore(node, 0);

            switch (node.TagName)
            {
                case "DIV":
                    AddToReadabilityScore(node, 5);
                    break;

                case "PRE":
                case "TD":
                case "BLOCKQUOTE":
                    AddToReadabilityScore(node, 3);
                    break;

                case "ADDRESS":
                case "OL":
                case "UL":
                case "DL":
                case "DD":
                case "DT":
                case "LI":
                case "FORM":
                    AddToReadabilityScore(node, -3);
                    break;

                case "H1":
                case "H2":
                case "H3":
                case "H4":
                case "H5":
                case "H6":
                case "TH":
                    AddToReadabilityScore(node, -5);
                    break;
            }

            AddToReadabilityScore(node, GetClassWeight(node));
        }

        private void AddToReadabilityScore(IElement node, double score)
        {
            double current;

            if ((current = GetReadabilityScore(node)) > 0d)
            {
                node.SetAttribute("readability-score", (current + score).ToString(CultureInfo.InvariantCulture.NumberFormat));
            }
            else
            {
                SetReadabilityScore(node, score);
            }
        }

        private void SetReadabilityScore(IElement node, double score)
        {
            node.SetAttribute("readability-score", score.ToString(CultureInfo.InvariantCulture.NumberFormat));
        }

        private double GetReadabilityScore(IElement node)
        {
            return node.GetAttribute("readability-score") is { Length: > 0 } readabilityScore
                ? double.Parse(readabilityScore, CultureInfo.InvariantCulture.NumberFormat)
                : 0D;
        }

        /// <summary>
        /// <para>Check and assign whether an element node contains a valid byline.</para>
        /// </summary>
        /// <param name="node">the node to check</param>
        /// <param name="matchString">a string representing the node to match for a byline</param>
        /// <returns>Whether the input string is a byline</returns>  
        private bool CheckByline(IElement node, string matchString)
        {
            if (!string.IsNullOrEmpty(articleByline))
            {
                return false;
            }

            string? rel = null;
            string? itemprop = null;
            int bylineLength = node.TextContent.Trim().Length;

            if (node is IElement && node.GetAttribute("rel") is { Length: > 0 } relValue)
            {
                rel = relValue;                
            }

            if (node is IElement && node.GetAttribute("itemprop") is { Length: > 0 } itemValue)
            {
                itemprop = itemValue;                
            }

            if ((rel is "author" || (itemprop is { Length: > 0 } && itemprop.Contains("author")) || RE_Byline.IsMatch(matchString)) && (bylineLength is > 0 and < 100))
            {
                if (rel is "author")
                {
                    author = node.TextContent.Trim();
                }
                else
                {
                    if (node.QuerySelector("[rel=\"author\"]") is IElement tempAuthor)
                    {
                        author = tempAuthor.TextContent.Trim();
                    }
                }                

                // Find child node matching [itemprop="name"] and use that if it exists for a more accurate author name byline
                var endOfSearchMarkerNode = NodeUtility.GetNextNode(node, true);
                var next = NodeUtility.GetNextNode(node);
                IElement itemPropNameNode = null;
                while (next != null && next != endOfSearchMarkerNode)
                {
                    itemprop = next.GetAttribute("itemprop");
                    if (itemprop != null && itemprop.Contains("name"))
                    {
                        itemPropNameNode = next;
                        break;
                    }
                    else
                    {
                        next = NodeUtility.GetNextNode(next);
                    }
                }

                if (itemPropNameNode != null && itemPropNameNode.TextContent.Trim().Length > 0)
                    articleByline = itemPropNameNode.TextContent.Trim();
                else if (node != null && node.TextContent.Trim().Length > 0)
                    articleByline = node.TextContent.Trim();
                
                if (articleByline != null)
                {
                    // we remove residual mustache (or similar templating) references
                    articleByline = Regex.Replace(articleByline.StartsWith("by", StringComparison.Ordinal) ? articleByline.Substring(2) : articleByline, @"{{.*?}}", "").Trim();
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// grabArticle - Using a variety of metrics (content score, classname, element types), find the content that is
        /// most likely to be the stuff a user wants to read.Then return it wrapped up in a div.
        /// </summary>
        /// <param name="page">a document to run upon. Needs to be a full document, complete with body</param>
        private IElement? GrabArticle(IElement? page = null)
        {
            if (Debug || Logging == ReportLevel.Info)
                LoggerDelegate("**** grabArticle ****");

            var doc = this.doc;
            var isPaging = (page != null);
            page ??= this.doc!.Body;

            // We can't grab an article if we don't have a page!
            if (page is null)
            {
                LoggerDelegate("No body found in document. Abort.");
                return null;
            }
            else
            {
                LoggerDelegate($"Original Body:");
                LoggerDelegate(page.OuterHtml);
            }

            var pageCacheHtml = page.InnerHtml;

            while (true)
            {
                if (Debug || Logging == ReportLevel.Info)
                    LoggerDelegate("Starting grabArticle loop");

                var stripUnlikelyCandidates = FlagIsActive(Flags.StripUnlikelys);

                // First, node prepping. Trash nodes that look cruddy (like ones with the
                // class name "comment", etc), and turn divs into P tags where they have been
                // used inappropriately (as in, where they contain no other block level elements.)
                var elementsToScore = new List<IElement>();
                var node = this.doc!.DocumentElement;

                var shouldRemoveTitleHeader = true;

                while (node != null)
                {
                    var matchString = node.ClassName + " " + node.Id;

                    if (!NodeUtility.IsProbablyVisible(node))
                    {
                        if (Debug || Logging == ReportLevel.Info)
                            LoggerDelegate("Removing hidden node - " + matchString);
                        node = NodeUtility.RemoveAndGetNext(node);
                        continue;
                    }

                    // User is not able to see elements applied with both "aria-modal = true" and "role = dialog"
                    if (node.GetAttribute("aria-modal") is "true" && node.GetAttribute("role") is "dialog")
                    {
                        node = NodeUtility.RemoveAndGetNext(node);
                        continue;
                    }

                    // Check to see if this node is a byline, and remove it if it is.
                    if (string.IsNullOrEmpty(articleByline) && CheckByline(node, matchString))
                    {                        
                        node = NodeUtility.RemoveAndGetNext(node);
                        continue;
                    }

                    if (shouldRemoveTitleHeader && HeaderDuplicatesTitle(node))
                    {
                        if (Debug || Logging == ReportLevel.Info)
                            LoggerDelegate($"Removing header: {node.TextContent.Trim()} {articleTitle.Trim()}");

                        shouldRemoveTitleHeader = false;
                        node = NodeUtility.RemoveAndGetNext(node);
                        continue;
                    }

                    // Remove unlikely candidates
                    if (stripUnlikelyCandidates)
                    {
                        if (RE_UnlikelyCandidates.IsMatch(matchString) &&
                            !RE_OkMaybeItsACandidate.IsMatch(matchString) &&
                            !HasAncestorTag(node, "table") &&
                            !HasAncestorTag(node, "code") &&
                            node.TagName is not "BODY" &&
                            node.TagName is not "A")
                        {
                            if (Debug || Logging == ReportLevel.Info)
                                LoggerDelegate("Removing unlikely candidate - " + matchString);
                            node = NodeUtility.RemoveAndGetNext(node);
                            continue;
                        }
                    }

                    // Remove nodes with unlikely roles
                    if (unlikelyRoles.Contains(node.GetAttribute("role")))
                    {
                        LoggerDelegate($"Removing content with role {node.GetAttribute("role")} -  {matchString}");
                        node = NodeUtility.RemoveAndGetNext(node);
                        continue;
                    }

                    // Remove DIV, SECTION, and HEADER nodes without any content(e.g. text, image, video, or iframe).
                    if ((node.TagName is "DIV" or "SECTION" or "HEADER"
                        or "H1" or "H2" or "H3" or "H4" or "H5" or "H6") &&
                        NodeUtility.IsElementWithoutContent(node))
                    {
                        node = NodeUtility.RemoveAndGetNext(node);
                        continue;
                    }

                    if (TagsToScore.Contains(node.TagName))
                    {
                        elementsToScore.Add(node);
                    }

                    // Turn all divs that don't have children block level elements into p's
                    if (node.TagName is "DIV")
                    {
                        // Put phrasing content into paragraphs.
                        INode? p = null;
                        var childNode = node.FirstChild;
                        while (childNode != null)
                        {
                            var nextSibling = childNode.NextSibling;
                            if (NodeUtility.IsPhrasingContent(childNode))
                            {
                                if (p != null)
                                {
                                    p.AppendChild(childNode);
                                }
                                else if (!NodeUtility.IsWhitespace(childNode))
                                {
                                    p = doc!.CreateElement("p");
                                    node.ReplaceChild(p, childNode);
                                    p.AppendChild(childNode);
                                }
                            }
                            else if (p != null)
                            {
                                while (p.LastChild != null && NodeUtility.IsWhitespace(p.LastChild))
                                {
                                    p.RemoveChild(p.LastChild);
                                }

                                p = null;
                            }
                            childNode = nextSibling;
                        }

                        // Sites like http://mobile.slate.com encloses each paragraph with a DIV
                        // element. DIVs with only a P element inside and no text content can be
                        // safely converted into plain P elements to avoid confusing the scoring
                        // algorithm with DIVs with are, in practice, paragraphs.
                        if (NodeUtility.HasSingleTagInsideElement(node, "P") && NodeUtility.GetLinkDensity(node) < 0.25)
                        {
                            var newNode = node.Children[0];
                            // preserve the old DIV classes into the new P node
                            newNode.ClassName += " " + node.ClassName;
                            node.Parent!.ReplaceChild(newNode, node);
                            node = newNode;
                            elementsToScore.Add(node);
                        }
                        else if (!NodeUtility.HasChildBlockElement(node))
                        {
                            node = NodeUtility.SetNodeTag(node, "P");
                            elementsToScore.Add(node);
                        }
                    }
                    node = NodeUtility.GetNextNode(node);
                }

                /*
				 * Loop through all paragraphs, and assign a score to them based on how content-y they look.
				 * Then add their score to their parent node.
				 *
				 * A score is determined by things like number of commas, class names, etc. Maybe eventually link density.
				*/
                var candidates = new List<IElement>();

                foreach (var elementToScore in elementsToScore)
                {
                    if (elementToScore.Parent is null)
                        continue;

                    // If this paragraph is less than 25 characters, don't even count it.
                    string innerText = NodeUtility.GetInnerText(elementToScore);
                    if (innerText.Length < ParagraphThreshold)
                        continue;

                    // Exclude nodes with no ancestor.
                    var ancestors = NodeUtility.GetNodeAncestors(elementToScore, AncestorsDepth);
                    if (ancestors.Count is 0)
                        continue;

                    double contentScore = 0;

                    // Add a point for the paragraph itself as a base.
                    contentScore += 1;

                    // Add points for any commas within this paragraph.
                    contentScore += G_RE_Commas.Split(innerText).Length;

                    // For every 100 characters in this paragraph, add another point. Up to 3 points.
                    contentScore += Math.Min(Math.Floor(innerText.Length / 100.0), 3);

                    // Initialize and score ancestors.                    
                    NodeUtility.ForEachNode(ancestors, (ancestor, level) =>
                    {
                        var ancestorEl = ancestor as IElement;
                        if (ancestorEl is null || string.IsNullOrEmpty(ancestorEl.TagName) ||
                            ancestorEl.ParentElement is null ||
                            string.IsNullOrEmpty(ancestorEl.ParentElement?.TagName))
                            return;

                        if (GetReadabilityScore(ancestorEl).CompareTo(0.0) == 0)
                        {
                            InitializeNode(ancestorEl);
                            candidates.Add(ancestorEl);
                        }

                        // Node score divider:
                        // - parent:             1 (no division)
                        // - grandparent:        2
                        // - great grandparent+: ancestor level * 3
                        var scoreDivider = 0;
                        if (level == 0)
                            scoreDivider = 1;
                        else if (level == 1)
                            scoreDivider = 2;
                        else
                            scoreDivider = level * 3;

                        AddToReadabilityScore(ancestorEl, contentScore / scoreDivider);
                    }, 0);
                }

                // After we've calculated scores, loop through all of the possible
                // candidate nodes we found and find the one with the highest score.
                var topCandidates = new List<IElement>();
                for (int c = 0, cl = candidates?.Count ?? 0; c < cl; c += 1)
                {
                    var candidate = candidates![c];

                    // Scale the final candidates score based on link density. Good content
                    // should have a relatively small link density (5% or less) and be mostly
                    // unaffected by this operation.
                    var candidateScore = GetReadabilityScore(candidate) * (1 - NodeUtility.GetLinkDensity(candidate));
                    SetReadabilityScore(candidate, candidateScore);

                    for (var t = 0; t < NTopCandidates; t++)
                    {
                        IElement? aTopCandidate = null;
                        if (t < topCandidates.Count)
                            aTopCandidate = topCandidates[t];

                        if (aTopCandidate is null || candidateScore > GetReadabilityScore(aTopCandidate))
                        {
                            topCandidates.Insert(t, candidate);
                            if (topCandidates.Count > NTopCandidates)
                                topCandidates.Remove(topCandidates.Last());
                            break;
                        }
                    }
                }

                var topCandidate = topCandidates.ElementAtOrDefault(0);
                var neededToCreateTopCandidate = false;
                IElement parentOfTopCandidate;

                // If we still have no top candidate, just use the body as a last resort.
                // We also have to copy the body node so it is something we can modify.
                if (topCandidate is null || topCandidate.TagName is "BODY")
                {
                    // Move all of the page's children into topCandidate
                    topCandidate = doc!.CreateElement("DIV");
                    neededToCreateTopCandidate = true;
                    // Move everything (not just elements, also text nodes etc.) into the container
                    // so we even include text directly in the body:
                    while (page.FirstChild != null)
                    {
                        if (Debug || Logging == ReportLevel.Info)
                            LoggerDelegate($"Moving child out: {page.FirstChild}");
                        topCandidate.AppendChild(page.FirstChild);
                    }

                    page.AppendChild(topCandidate);

                    InitializeNode(topCandidate);
                }
                else if (topCandidate != null)
                {
                    // Find a better top candidate node if it contains (at least three) nodes which belong to `topCandidates` array
                    // and whose scores are quite closed with current `topCandidate` node.

                    var alternativeCandidateAncestors = new List<List<INode>>();
                    for (var i = 1; i < topCandidates.Count; i++)
                    {
                        if (GetReadabilityScore(topCandidates[i]) / GetReadabilityScore(topCandidate) >= 0.75)
                        {
                            alternativeCandidateAncestors.Add(NodeUtility.GetNodeAncestors(topCandidates[i]));
                        }
                    }
                    const int MINIMUM_TOPCANDIDATES = 3;
                    if (alternativeCandidateAncestors.Count >= MINIMUM_TOPCANDIDATES)
                    {
                        parentOfTopCandidate = topCandidate.ParentElement!;
                        while (parentOfTopCandidate.TagName is not "BODY")
                        {
                            var listsContainingThisAncestor = 0;
                            for (var ancestorIndex = 0; ancestorIndex < alternativeCandidateAncestors.Count && listsContainingThisAncestor < MINIMUM_TOPCANDIDATES; ancestorIndex++)
                            {
                                listsContainingThisAncestor += (alternativeCandidateAncestors[ancestorIndex].Contains(parentOfTopCandidate)) ? 1 : 0;
                            }
                            if (listsContainingThisAncestor >= MINIMUM_TOPCANDIDATES)
                            {
                                topCandidate = parentOfTopCandidate;
                                break;
                            }
                            parentOfTopCandidate = parentOfTopCandidate.ParentElement!;
                        }
                    }

                    if (GetReadabilityScore(topCandidate!).CompareTo(0.0) == 0)
                    {
                        InitializeNode(topCandidate);
                    }

                    // Because of our bonus system, parents of candidates might have scores
                    // themselves. They get half of the node. There won't be nodes with higher
                    // scores than our topCandidate, but if we see the score going *up* in the first
                    // few steps up the tree, that's a decent sign that there might be more content
                    // lurking in other places that we want to unify in. The sibling stuff
                    // below does some of that - but only if we've looked high enough up the DOM
                    // tree.
                    parentOfTopCandidate = topCandidate.ParentElement!;

                    var lastScore = GetReadabilityScore(topCandidate);
                    // The scores shouldn't get too low.
                    var scoreThreshold = lastScore / 3;
                    while (parentOfTopCandidate.TagName is not "BODY")
                    {
                        if (GetReadabilityScore(parentOfTopCandidate).CompareTo(0.0) == 0)
                        {
                            parentOfTopCandidate = parentOfTopCandidate.ParentElement!;
                            continue;
                        }

                        var parentScore = GetReadabilityScore(parentOfTopCandidate);
                        if (parentScore < scoreThreshold)
                            break;
                        if (parentScore > lastScore)
                        {
                            // Alright! We found a better parent to use.
                            topCandidate = parentOfTopCandidate;
                            break;
                        }

                        lastScore = GetReadabilityScore(parentOfTopCandidate);
                        parentOfTopCandidate = parentOfTopCandidate.ParentElement!;
                    }

                    // If the top candidate is the only child, use parent instead. This will help sibling
                    // joining logic when adjacent content is actually located in parent's sibling node.
                    parentOfTopCandidate = topCandidate.ParentElement!;
                    while (parentOfTopCandidate.TagName is not "BODY" && parentOfTopCandidate.Children.Length == 1)
                    {
                        topCandidate = parentOfTopCandidate;
                        parentOfTopCandidate = topCandidate.ParentElement!;
                    }

                    if (GetReadabilityScore(topCandidate).CompareTo(0.0) == 0)
                    {
                        InitializeNode(topCandidate);
                    }
                }

                // Now that we have the top candidate, look through its siblings for content
                // that might also be related. Things like preambles, content split by ads
                // that we removed, etc.
                var articleContent = doc!.CreateElement("DIV");
                if (isPaging)
                    articleContent.Id = "readability-content";

                var siblingScoreThreshold = Math.Max(10, GetReadabilityScore(topCandidate!) * 0.2);
                // Keep potential top candidate's parent node to try to get text direction of it later.
                parentOfTopCandidate = topCandidate!.ParentElement!;
                var siblings = parentOfTopCandidate.Children;

                for (int s = 0, sl = siblings.Length; s < sl; s++)
                {
                    var sibling = siblings[s];
                    var append = false;

                    if (sibling == topCandidate)
                    {
                        append = true;
                    }
                    else
                    {
                        double contentBonus = 0;

                        // Give a bonus if sibling nodes and top candidates have the example same classname
                        if (string.Equals(sibling.ClassName, topCandidate.ClassName, StringComparison.Ordinal) && topCandidate.ClassName is not "")
                            contentBonus += GetReadabilityScore(topCandidate) * 0.2;

                        if (GetReadabilityScore(sibling) > 0 &&
                        ((GetReadabilityScore(sibling) + contentBonus) >= siblingScoreThreshold))
                        {
                            append = true;
                        }
                        else if (sibling.NodeName is "P")
                        {
                            var linkDensity = NodeUtility.GetLinkDensity(sibling);
                            var nodeContent = NodeUtility.GetInnerText(sibling);
                            var nodeLength = nodeContent.Length;

                            if (nodeLength > 80 && linkDensity < 0.25)
                            {
                                append = true;
                            }
                            else if (nodeLength < 80 && nodeLength > 0 && linkDensity.CompareTo(0) == 0 &&
                                 new Regex(@"\.( |$)", RegexOptions.IgnoreCase).IsMatch(nodeContent))
                            {
                                append = true;
                            }
                        }
                    }

                    if (append)
                    {
                        if (!alterToDivExceptions.Contains(sibling.NodeName))
                        {
                            // We have a node that isn't a common block level element, like a form or td tag.
                            // Turn it into a div so it doesn't get filtered out later by accident.

                            sibling = NodeUtility.SetNodeTag(sibling, "DIV");
                        }

                        articleContent.AppendChild(sibling);
                        // Fetch children again to make it compatible
                        // with DOM parsers without live collection support.
                        siblings = parentOfTopCandidate.Children;
                        // siblings is a reference to the children array, and
                        // sibling is removed from the array when we call appendChild().
                        // As a result, we must revisit this index since the nodes
                        // have been shifted.
                        s -= 1;
                        sl -= 1;
                    }
                }

                if (Debug || Logging == ReportLevel.Info)
                    LoggerDelegate("<h2>Article content pre-prep:</h2>" + articleContent.InnerHtml);

                // So we have all of the content that we need. Now we clean it up for presentation.
                PrepArticle(articleContent);

                if (Debug || Logging == ReportLevel.Info)
                    LoggerDelegate("<h2>Article content post-prep:</h2>" + articleContent.InnerHtml);

                if (neededToCreateTopCandidate)
                {
                    // We already created a fake div thing, and there wouldn't have been any siblings left
                    // for the previous loop, so there's no point trying to create a new div, and then
                    // move all the children over. Just assign IDs and class names here. No need to append
                    // because that already happened anyway.
                    topCandidate.Id = "readability-page-1";
                    topCandidate.ClassName = "page";
                }
                else
                {
                    var div = doc.CreateElement("DIV");
                    div.Id = "readability-page-1";
                    div.ClassName = "page";
                    while (articleContent.FirstChild is not null)
                    {
                        div.AppendChild(articleContent.FirstChild);
                    }
                    articleContent.AppendChild(div);
                }

                if (Debug || Logging == ReportLevel.Info)
                    LoggerDelegate("<h2>Article content after paging:</h2>" + articleContent.InnerHtml);

                var parseSuccessful = true;

                // Now that we've gone through the full algorithm, check to see if
                // we got any meaningful content. If we didn't, we may need to re-run
                // grabArticle with different flags set. This gives us a higher likelihood of
                // finding the content, and the sieve approach gives us a higher likelihood of
                // finding the -right- content.
                var textLength = NodeUtility.GetInnerText(articleContent, true).Length;
                if (textLength < CharThreshold)
                {
                    parseSuccessful = false;
                    page.InnerHtml = pageCacheHtml;

                    if (FlagIsActive(Flags.StripUnlikelys))
                    {
                        RemoveFlag(Flags.StripUnlikelys);
                        attempts.Add(new Attempt(articleContent, textLength));
                    }
                    else if (FlagIsActive(Flags.WeightClasses))
                    {
                        RemoveFlag(Flags.WeightClasses);
                        attempts.Add(new Attempt(articleContent, textLength));
                    }
                    else if (FlagIsActive(Flags.CleanConditionally))
                    {
                        RemoveFlag(Flags.CleanConditionally);
                        attempts.Add(new Attempt(articleContent, textLength));
                    }
                    else
                    {
                        attempts.Add(new Attempt(articleContent, textLength));
                        // No luck after removing flags, just return the longest text we found during the different loops
                        attempts = attempts.OrderByDescending(x => x.Length).ToList();

                        // But first check if we actually have something
                        if (attempts.Count == 0)
                        {
                            return null;
                        }

                        articleContent = attempts[0].Content;
                        parseSuccessful = true;
                    }
                }

                if (parseSuccessful)
                {
                    // Find out text direction from ancestors of final top candidate.
                    IEnumerable<IElement> ancestors = new IElement[] { parentOfTopCandidate, topCandidate }.Concat(NodeUtility.GetElementAncestors(parentOfTopCandidate));
                    ancestors.Any(ancestor =>
                    {
                        if (string.IsNullOrEmpty(ancestor.TagName))
                            return false;

                        if (ancestor.GetAttribute("dir") is { Length: > 0 } dir)
                        {
                            this.articleDir = dir;
                            return true;
                        }
                        return false;
                    });

                    return articleContent;
                }
            }
        }

        /// <summary>
        /// Get an elements class/id weight. Uses regular expressions to tell if this
        /// element looks good or bad.
        /// </summary>
        private int GetClassWeight(IElement e)
        {
            if (!FlagIsActive(Flags.WeightClasses))
                return 0;

            var weight = 0;

            // Look for a special classname
            if (!string.IsNullOrEmpty(e.ClassName))
            {
                if (RE_Negative.IsMatch(e.ClassName))
                    weight -= 25;

                if (RE_Positive.IsMatch(e.ClassName))
                    weight += 25;
            }

            // Look for a special ID
            if (e.Id != null && e.Id is not "")
            {
                if (RE_Negative.IsMatch(e.Id))
                    weight -= 25;

                if (RE_Positive.IsMatch(e.Id))
                    weight += 25;
            }

            return weight;
        }

        /// <summary>
        /// Clean a node of all elements of type "tag".
		/// (Unless it's a youtube/vimeo video. People love movies.)
        /// </summary>
        /// <param name="e">Node to be cleaned</param>
        /// <param name="tag">Tag to remove</param>
        private void Clean(IElement e, string tag)
        {
            static bool IsEmbed(string tag)
            {
                return tag is "object" or "embed" or "iframe";
            }

            var isEmbed = IsEmbed(tag);

            NodeUtility.RemoveNodes(e.GetElementsByTagName(tag), (element) =>
            {
                // Allow youtube and vimeo videos through as people usually want to see those.
                if (isEmbed)
                {
                    // First, check the elements attributes to see if any of them contain youtube or vimeo
                    for (var i = 0; i < element.Attributes.Length; i++)
                    {
                        if (RE_Videos.IsMatch(element.Attributes[i]!.Value))
                        {
                            return false;
                        }
                    }

                    // For embed with <object> tag, check inner HTML as well.
                    if (element.TagName is "OBJECT" && RE_Videos.IsMatch(element.InnerHtml))
                    {
                        return false;
                    }
                }

                return true;
            });
        }

        /// <summary>
        /// Check if a given node has one of its ancestor tag name matching the
        /// provided one.
        /// </summary>
        /// <param name="node">Node to operate one</param>
        /// <param name="tagName">Tag to check</param>
        /// <param name="maxDepth">Maximum depth of parent to search</param>
        /// <param name="filterFn">Filter to ignore some matching tags</param>
        private static bool HasAncestorTag(IElement node, string tagName, int maxDepth = 3, Func<IElement, bool>? filterFn = null)
        {
            var depth = 0;

            while (node.ParentElement != null)
            {
                if (maxDepth > 0 && depth > maxDepth)
                    return false;
                if (string.Equals(node.ParentElement.TagName, tagName, StringComparison.OrdinalIgnoreCase)
                    && (filterFn is null || filterFn(node.ParentElement)))
                    return true;
                node = node.ParentElement;
                depth++;
            }
            return false;
        }

        private static bool IsDataTable(IElement node)
        {
            return node.GetAttribute("dataTable") is { Length: > 0 } datatable && datatable.Contains("true");
        }

        /// <summary>
        /// Return an object indicating how many rows and columns this table has.
        /// </summary>
        private (int Rows, int Columns) GetRowAndColumnCount(IElement table)
        {
            var rows = 0;
            var columns = 0;
            var trs = table.GetElementsByTagName("tr");
            for (var i = 0; i < trs.Length; i++)
            {
                string? rowspan = trs[i].GetAttribute("rowspan");
                int rowSpanInt = 0;
                if (rowspan is { Length: > 0 })
                {
                    int.TryParse(rowspan, out rowSpanInt);
                }
                rows += rowSpanInt == 0 ? 1 : rowSpanInt;
                // Now look for column-related info
                var columnsInThisRow = 0;
                var cells = trs[i].GetElementsByTagName("td");
                for (var j = 0; j < cells.Length; j++)
                {
                    string? colspan = cells[j].GetAttribute("colspan");
                    int colSpanInt = 0;
                    if (colspan is { Length: > 0 })
                    {
                        int.TryParse(colspan, out colSpanInt);
                    }
                    columnsInThisRow += colSpanInt == 0 ? 1 : colSpanInt;
                }
                columns = Math.Max(columns, columnsInThisRow);
            }
            return (rows, columns);
        }

        private static readonly string[] dataTableDescendantTagNames = { "col", "colgroup", "tfoot", "thead", "th" };

        /// <summary>
        /// Look for 'data' (as opposed to 'layout') tables, for which we use
        /// similar checks as
        /// https://searchfox.org/mozilla-central/rev/f82d5c549f046cb64ce5602bfd894b7ae807c8f8/accessible/generic/TableAccessible.cpp#19
        /// </summary>
        private void MarkDataTables(IElement root)
        {
            var tables = root.GetElementsByTagName("table");
            for (var i = 0; i < tables.Length; i++)
            {
                var table = tables[i];

                if (table.GetAttribute("role") is "presentation")
                {
                    table.SetAttribute("dataTable", "false");
                    continue;
                }

                if (table.GetAttribute("dataTable") is "0")
                {
                    table.SetAttribute("dataTable", "false");
                    continue;
                }

                if (table.GetAttribute("summary") is { Length: > 0 })
                {
                    table.SetAttribute("dataTable", "true");
                    continue;
                }

                var caption = table.GetElementsByTagName("caption")?.ElementAtOrDefault(0);
                if (caption != null && caption.ChildNodes.Length > 0)
                {
                    table.SetAttribute("dataTable", "true");
                    continue;
                }

                // If the table has a descendant with a COL, COLGROUP, TFOOT, THEAD, or TH tag, consider a data table:
                bool descendantExists(string tag)
                {
                    return table.GetElementsByTagName(tag).Length > 0;
                }

                if (dataTableDescendantTagNames.Any(descendantExists))
                {
                    if (Debug || Logging == ReportLevel.Info)
                        LoggerDelegate("Data table because found data-y descendant");
                    table.SetAttribute("dataTable", "true");
                    continue;
                }

                // Nested tables indicate a layout table:
                if (table.GetElementsByTagName("table").ElementAtOrDefault(0) != null)
                {
                    table.SetAttribute("dataTable", "false");
                    continue;
                }

                var sizeInfo = GetRowAndColumnCount(table);

                if (sizeInfo.Columns == 1 || sizeInfo.Rows == 1)
                {
                    // single colum/row tables are commonly used for page layout purposes.
                    table.SetAttribute("dataTable", "false");
                    continue;
                }

                if (sizeInfo.Rows >= 10 || sizeInfo.Columns > 4)
                {
                    table.SetAttribute("dataTable", "true");
                    continue;
                }
                // Now just go by size entirely:
                if (sizeInfo.Rows * sizeInfo.Columns > 10)
                    table.SetAttribute("dataTable", "true");
            }
        }

        /// <summary>
        /// convert images and figures that have properties like data-src into images that can be loaded without JS
        /// </summary>
        private void FixLazyImages(IElement root)
        {
            NodeUtility.ForEachElement(NodeUtility.GetAllNodesWithTag(root, s_img_picture_figure), (elem) =>
            {
                // In some sites (e.g. Kotaku), they put 1px square image as base64 data uri in the src attribute.
                // So, here we check if the data uri is too short, just might as well remove it.
                string? src = elem.GetAttribute("src");

                if (src != null && G_RE_B64DataUrl.IsMatch(src))
                {
                    // Make sure it's not SVG, because SVG can have a meaningful image in under 133 bytes.
                    var parts = G_RE_B64DataUrl.Match(src);
                    if (parts.Groups[1].Value is "image/svg+xml")
                    {
                        return;
                    }

                    // Make sure this element has other attributes which contains image.
                    // If it doesn't, then this src is important and shouldn't be removed.
                    var srcCouldBeRemoved = false;
                    for (var i = 0; i < elem.Attributes.Length; i++)
                    {
                        var attr = elem.Attributes[i]!;
                        if (attr.Name is "src")
                        {
                            continue;
                        }

                        if (Regex.IsMatch(attr.Value, @"\.(jpg|jpeg|png|webp)"))
                        {
                            srcCouldBeRemoved = true;
                            break;
                        }
                    }

                    // Here we assume if image is less than 100 bytes (or 133 after encoded to base64)
                    // it will be too small, therefore it might be placeholder image.
                    if (srcCouldBeRemoved)
                    {
                        var b64starts = parts.Groups[0].Length;
                        var b64length = src.Length - b64starts;
                        if (b64length < 133)
                        {
                            elem.RemoveAttribute("src");
                        }
                    }
                }

                string? srcset = elem.GetAttribute("srcset");

                if ((!String.IsNullOrEmpty(src) || !String.IsNullOrEmpty(srcset))
                && (elem.ClassName is { Length: > 0 } className && className.IndexOf("lazy", StringComparison.OrdinalIgnoreCase) == -1))
                {
                    return;
                }

                for (var i = 0; i < elem.Attributes.Length; i++)
                {
                    var attr = elem.Attributes[i]!;

                    if (attr.Name is "src" or "srcset" or "alt")
                    {
                        continue;
                    }
                    string copyTo = "";
                    if (Regex.IsMatch(attr.Value, @"\.(jpg|jpeg|png|webp)\s+\d", RegexOptions.IgnoreCase))
                    {
                        copyTo = "srcset";
                    }
                    else if (Regex.IsMatch(attr.Value, @"^\s*\S+\.(jpg|jpeg|png|webp)\S*\s*$", RegexOptions.IgnoreCase))
                    {
                        copyTo = "src";
                    }

                    if (!string.IsNullOrEmpty(copyTo))
                    {
                        //if this is an img or picture, set the attribute directly
                        if (elem.TagName is "IMG" or "PICTURE")
                        {
                            elem.SetAttribute(copyTo, attr.Value);
                        }
                        else if (elem.TagName is "FIGURE"
                        && NodeUtility.GetAllNodesWithTag(elem, s_IMG_PICTURE).Length == 0)
                        {
                            //if the item is a <figure> that does not contain an image or picture, create one and place it inside the figure
                            //see the nytimes-3 testcase for an example
                            var img = doc!.CreateElement("img");
                            img.SetAttribute(copyTo, attr.Value);
                            elem.AppendChild(img);
                        }
                    }
                }
            });
        }

        /// <summary>
        /// <para>Calculate text density.</para>
        /// </summary>
        private double GetTextDensity(IElement e, string[] tags)
        {
            var textLength = NodeUtility.GetInnerText(e, true).Length;
            if (textLength is 0)
            {
                return 0;
            }
            var childrenLength = 0;
            var children = NodeUtility.GetAllNodesWithTag(e, tags);

            foreach (var child in children)
            {
                childrenLength += NodeUtility.GetInnerText(child, true).Length;
            }

            return (double) childrenLength / textLength;
        }

        /// <summary>
        /// <para>Clean an element of all tags of type "tag" if they look fishy.</para>
        /// <para>Fishy" is an algorithm based on content length, classnames, link density, number of images and embeds, etc.</para>
        /// </summary>
        private void CleanConditionally(IElement e, string tag)
        {
            if (!FlagIsActive(Flags.CleanConditionally))
                return;

            // Gather counts for other typical elements embedded within.
            // Traverse backwards so we can remove nodes at the same time
            // without effecting the traversal.
            //
            // TODO: Consider taking into account original contentScore here.
            NodeUtility.RemoveNodes(e.GetElementsByTagName(tag), node =>
            {
                var isList = tag is "ul" or "ol";
                if (!isList)
                {
                    var listLength = 0;
                    var listNodes = NodeUtility.GetAllNodesWithTag(node, s_ul_ol);

                    foreach (var list in listNodes)
                    {
                        listLength += NodeUtility.GetInnerText(list).Length;
                    }

                    if (NodeUtility.GetInnerText(node).Length > 0)
                        isList = listLength / NodeUtility.GetInnerText(node).Length > 0.9;
                }

                // First check if this node IS data table, in which case don't remove it.
                if (tag is "table" && IsDataTable(node))
                {
                    return false;
                }

                // Next check if we're inside a data table, in which case don't remove it as well.
                if (HasAncestorTag(node, "table", -1, IsDataTable))
                {
                    return false;
                }

                if (HasAncestorTag(node, "code"))
                {
                    return false;
                }

                // keep element if it has a data tables
                if (node.GetElementsByTagName("table").Any(tbl => IsDataTable(tbl)))
                {
                    return false;
                }

                int weight = GetClassWeight(node);

                if (Debug || Logging == ReportLevel.Info)
                    LoggerDelegate($"Cleaning Conditionally {node}");

                var contentScore = 0;

                if (weight + contentScore < 0)
                {
                    return true;
                }

                if (NodeUtility.GetCharCount(node, ',') < 10)
                {
                    // Readability.js algorithm
                    var p = 0d;                         // var p = node.getElementsByTagName("p").length;
                    var img = 0d;                       // var img = node.getElementsByTagName("img").length;
                    var li = -100d;                     // var li = node.getElementsByTagName("li").length - 100;
                    var input = 0d;                     // var input = node.getElementsByTagName("input").length;
                    var embeds = new List<IElement>();  // this._getAllNodesWithTag(node, ["object", "embed", "iframe"]);

                    foreach (var descendentNode in node.Descendants())
                    {
                        if (descendentNode is IElement el)
                        {
                            switch (el.TagName)
                            {
                                case "P":
                                    p++;
                                    break;
                                case "IMG":
                                    img++;
                                    break;
                                case "LI":
                                    li++;
                                    break;
                                case "INPUT":
                                    input++;
                                    break;
                                case "OBJECT" or "EMBED" or "IFRAME":
                                    embeds.Add(el);
                                    break;
                            }
                        }
                    }

                    // If there are not very many commas, and the number of
                    // non-paragraph elements is more than paragraphs or other
                    // ominous signs, remove the element.

                    double headingDensity = GetTextDensity(node, s_h1_h2_h3_h4_h5_h6);

                    var embedCount = 0;

                    for (var i = 0; i < embeds.Count; i++)
                    {
                        // If this embed has attribute that matches video regex, don't delete it.
                        for (var j = 0; j < embeds[i].Attributes.Length; j++)
                        {
                            if (RE_Videos.IsMatch(embeds[i].Attributes[j]!.Value))
                            {
                                return false;
                            }
                        }

                        // For embed with <object> tag, check inner HTML as well.
                        if (embeds[i].TagName is "OBJECT" && RE_Videos.IsMatch(embeds[i].InnerHtml))
                        {
                            return false;
                        }

                        embedCount++;
                    }

                    var innerText = NodeUtility.GetInnerText(node);

                    // toss any node whose inner text contains nothing but suspicious words
                    if (G_AdWords.IsMatch(innerText) || G_LoadingWords.IsMatch(innerText))
                    {
                        return true;
                    }
                    
                    double linkDensity = NodeUtility.GetLinkDensity(node);
                    var contentLength = NodeUtility.GetInnerText(node).Length;
                    var textishTags = NodeUtility.TextishTags;
                    var textDensity = GetTextDensity(node, textishTags);
                    var isFigureChild = HasAncestorTag(node, "figure");

                    Func<bool> shouldRemoveNode = () => {
                        List<string> errs = new List<string>();

                        if (!isFigureChild && img > 1 && p / img < 0.5)
                        {
                            errs.Add($"Bad p to img ratio (img={img}, p={p})");
                        }

                        if (!isList && li > p)
                        {
                            errs.Add($"Too many li's outside of a list. (li={li} > p={p})");
                        }

                        if (input > Math.Floor(p / 3))
                        {
                            errs.Add($"Too many inputs per p. (input={input}, p={p})");
                        }

                        if (!isList && !isFigureChild && headingDensity < 0.9f && contentLength < 25 && (img.CompareTo(0) == 0 || img > 2) && linkDensity > 0)
                        {
                            errs.Add($"Suspiciously short. (headingDensity={headingDensity}, img={img}, linkDensity={linkDensity})");
                        }

                        if (!isList && weight < 25 && linkDensity > (0.2 + LinkDensityModifier))
                        {
                            errs.Add($"Low weight and a little linky. (linkDensity={linkDensity})");
                        }

                        if (weight >= 25 && linkDensity > (0.5 + LinkDensityModifier))
                        {
                            errs.Add($"High weight and mostly links. (linkDensity={linkDensity})");
                        }

                        if ((embedCount == 1 && contentLength < 75) || embedCount > 1)
                        {
                            errs.Add($"Suspicious embed. (embedCount={embedCount}, contentLength={contentLength})");
                        }

                        if (img.CompareTo(0) == 0 && textDensity.CompareTo(0) == 0)
                        {
                            errs.Add($"No useful content. (img={img}, textDensity={textDensity})");
                        }

                        if (errs.Count > 0)
                        {
                            if (Debug || Logging == ReportLevel.Info)
                                LoggerDelegate($"Checks failed: {errs}");
                            return true;
                        }

                        return false;
                    };

                    bool haveToRemove = shouldRemoveNode();


                    // Allow simple lists of images to remain in pages
                    if (isList && haveToRemove)
                    {
                        for (var x = 0; x < node.Children.Length; x++)
                        {
                            var child = node.Children[x];
                            // Don't filter in lists with li's that contain more than one child
                            if (child.Children.Length > 1)
                            {
                                return haveToRemove;
                            }
                        }
                        var li_count = node.GetElementsByTagName("li").Length;
                        // Only allow the list to remain if every li contains an image
                        if (img == li_count)
                        {
                            return false;
                        }
                    }

                    return haveToRemove;
                }
                return false;
            });
        }

        /// <summary>
        /// Clean out spurious headers from an Element.
        /// </summary>
        private void CleanHeaders(IElement e)
        {
            var headingNodes = NodeUtility.GetAllNodesWithTag(e, s_h1_h2);
            NodeUtility.RemoveNodes(headingNodes, (node) =>
            {
                var shouldRemove = GetClassWeight(node) < 0;
                if (shouldRemove)
                {
                    if (Debug || Logging == ReportLevel.Info)
                        LoggerDelegate($"Removing header with low class weight: {node}");
                }
                return shouldRemove;
            });
        }

        /// <summary>
        /// Check if this node is an H1 or H2 element whose content is mostly
        /// the same as the article title.
        /// </summary>
        /// <param name="node">The node to check.</param>
        /// <returns>
        /// Boolean indicating whether this is a title-like header.
        /// </returns>    
        private bool HeaderDuplicatesTitle(IElement node)
        {
            if (node.TagName is not "H1" && node.TagName is not "H2")
            {
                return false;
            }
            var heading = NodeUtility.GetInnerText(node, false);
            if (Debug || Logging == ReportLevel.Info)
                LoggerDelegate($"Evaluating similarity of header> {heading} {articleTitle}");

            return Readability.TextSimilarity(articleTitle, heading) > 0.75;
        }

        private bool FlagIsActive(Flags flag)
        {
            return (flags & flag) > 0;
        }

        private void RemoveFlag(Flags flag)
        {
            flags &= ~flag;
        }

        /// <summary>
        /// Decides whether or not the document is reader-able without parsing the whole thing.
        /// </summary>
        /// <returns>Whether or not we suspect parse method will suceeed at returning an article object.</returns>
        private bool IsProbablyReaderable()
        {
            var nodes = NodeUtility.GetAllNodesWithTag(doc!.DocumentElement, s_p_pre_article);

            // Get <div> nodes which have <br> node(s) and append them into the `nodes` variable.
            // Some articles' DOM structures might look like
            // <div>
            //   Sentences<br>
            //   <br>
            //   Sentences<br>
            // </div>
            var brNodes = doc.DocumentElement.QuerySelectorAll("div > br");
            IEnumerable<IElement> totalNodes = nodes;
            if (brNodes.Length > 0)
            {
                var set = new HashSet<IElement>();

                foreach (var node in brNodes)
                {
                    set.Add(node.ParentElement!);
                }

                totalNodes = nodes.Concat(set.ToArray());
            }

            double score = 0;
            // This is a little cheeky, we use the accumulator 'score' to decide what to return from
            // this callback:			
            return totalNodes.Any(node =>
            {
                if (!IsNodeVisible(node))
                    return false;

                var matchString = node.ClassName + " " + node.Id;

                if (RE_UnlikelyCandidates.IsMatch(matchString) &&
                    !RE_OkMaybeItsACandidate.IsMatch(matchString))
                {
                    return false;
                }

                if (node.Matches("li p"))
                {
                    return false;
                }

                var textContentLength = node.TextContent.AsSpan().Trim().Length;
                if (textContentLength < GetMinContentLengthBasedOnLanguage())
                {
                    return false;
                }

                score += Math.Sqrt(textContentLength - GetMinContentLengthBasedOnLanguage());

                if (score > MinScoreReaderable)
                {
                    return true;
                }
                else
                    return false;
            });
        }

        /// <summary>
        /// Parse the article.
        /// </summary>        
        /// <returns>
        /// An article object with all the data extracted
        /// </returns> 
        private Article Parse()
        {
            if (doc == null)
                throw new Exception("No document found");

            // Avoid parsing too large documents, as per configuration option
            if (MaxElemsToParse > 0)
            {
                var numTags = doc.GetElementsByTagName("*").Length;
                if (numTags > MaxElemsToParse)
                {
                    throw new Exception("Aborting parsing document; " + numTags + " elements found");
                }
            }

            var isReadable = IsProbablyReaderable();

            // we stop only if it's not readable and we are not debugging
            if (isReadable == false)
            {
                LoggerDelegate("<h2>Warning: article probably not readable</h2>");

                if (ContinueIfNotReadable == false)
                    return new Article(uri, articleTitle, false);
            }

            // perform custom operations at the start
            foreach (var operation in CustomOperationsStart)
                operation(doc.DocumentElement);

            // Unwrap image from noscript            
            NodeUtility.UnwrapNoscriptImages(doc);

            // Extract JSON-LD metadata before removing scripts
            var jsonLd = DisableJSONLD ? new Dictionary<string, string>() : Readability.GetJSONLD(this.doc);

            // Remove script tags from the document.            
            NodeUtility.RemoveScripts(doc.DocumentElement);

            PrepDocument();

            if (Debug || Logging == ReportLevel.Info)
                LoggerDelegate("<h2>Pre-GrabArticle:</h2>" + doc.DocumentElement.InnerHtml);

            var metadata = Readability.GetArticleMetadata(this.doc, this.uri, this.language, jsonLd);
            articleTitle = metadata.Title ?? "";

            var articleContent = GrabArticle();
            if (articleContent is null)
                return new Article(uri, articleTitle, false);

            if (Debug || Logging == ReportLevel.Info)
                LoggerDelegate("<h2>Grabbed:</h2>" + articleContent.InnerHtml);

            PostProcessContent(articleContent);

            // perform custom operations at the end
            foreach (var operation in CustomOperationsEnd)
                operation(articleContent);

            if (Debug || Logging == ReportLevel.Info)
                LoggerDelegate("<h2>Post Process result:</h2>" + articleContent.InnerHtml);

            // If we haven't found an excerpt in the article's metadata, use the article's
            // first paragraph as the excerpt. This is used for displaying a preview of
            // the article's content.
            if (string.IsNullOrEmpty(metadata.Excerpt))
            {
                var paragraphs = articleContent.GetElementsByTagName("p");
                if (paragraphs.Length > 0)
                {
                    metadata.Excerpt = paragraphs[0].TextContent.Trim();
                }
            }

            return new Article(uri, articleTitle, articleByline, articleDir, language, author, articleContent, metadata, isReadable, this);
        }

        private async Task<Stream> GetStreamAsync(Uri resource)
        {
            using var httpClient = new HttpClient(_httpClientHandler.Value, false);

            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(_userAgent);

            var response = await httpClient.GetAsync(resource).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Cannot GET resource {resource}. StatusCode: {response.StatusCode}");
            }

            if (response.Content.Headers.TryGetValues("Content-Language", out var contentLanguageHeader))
            {
                language = contentLanguageHeader.First();
            }

            if (response.Content.Headers.TryGetValues("Content-Type", out var contentTypeHeader))
            {
                string contentType = contentTypeHeader.First().ToLower();

                int charSetIndex = contentType.IndexOf("charset=");

                if (charSetIndex != -1)
                {
                    charset = contentType.Substring(charSetIndex + 8);
                }
            }

            return await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        }

        internal async Task<long> GetImageSizeAsync(Uri imageSrc)
        {
            using var httpClient = new HttpClient(_httpClientHandler.Value, false);

            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(_userAgent);

            var headRequest = new HttpRequestMessage(HttpMethod.Head, imageSrc);

            using var response = await httpClient.SendAsync(headRequest, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);

            long size = 0;

            if (response.IsSuccessStatusCode)
            {
                if (response.Content.Headers.ContentLength is long contentLength)
                {
                    size = contentLength;
                }
            }

            return size;
        }

        internal async Task<byte[]> GetImageBytesAsync(Uri resource)
        {
            using var httpClient = new HttpClient(_httpClientHandler.Value, false);

            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(_userAgent);

            using var response = await httpClient.GetAsync(resource).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Cannot GET resource {resource}. StatusCode: {response.StatusCode}");
            }

            return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        }

        /// <summary>Allow to replace the default regular expressions</summary>
        /// <param name="expression">A RegularExpression indicating the expression to change</param>
        /// <param name="newExpression">A string representing the new option</param> 
        public void ReplaceRegularExpression(RegularExpressions expression, string newExpression)
        {
            switch (expression)
            {
                case RegularExpressions.UnlikelyCandidates:
                    RE_UnlikelyCandidates = new Regex(newExpression, RegexOptions.IgnoreCase);
                    break;
                case RegularExpressions.PossibleCandidates:
                    RE_OkMaybeItsACandidate = new Regex(newExpression, RegexOptions.IgnoreCase);
                    break;
                case RegularExpressions.Positive:
                    RE_Positive = new Regex(newExpression, RegexOptions.IgnoreCase);
                    break;
                case RegularExpressions.Negative:
                    RE_Negative = new Regex(newExpression, RegexOptions.IgnoreCase);
                    break;
                case RegularExpressions.Extraneous:
                    RE_Extraneous = new Regex(newExpression, RegexOptions.IgnoreCase);
                    break;
                case RegularExpressions.Byline:
                    RE_Byline = new Regex(newExpression, RegexOptions.IgnoreCase);
                    break;
                case RegularExpressions.Videos:
                    RE_Videos = new Regex(newExpression, RegexOptions.IgnoreCase);
                    break;
                case RegularExpressions.ShareElements:
                    RE_ShareElements = new Regex(newExpression, RegexOptions.IgnoreCase);
                    break;
                default:
                    break;
            }
        }

        /// <summary>Allow to add an option to the default regular expressions</summary>
        /// <param name="expression">A RegularExpression indicating the expression to change</param>
        /// <param name="option">A string representing the new option</param>  
        public void AddOptionToRegularExpression(RegularExpressions expression, string option)
        {
            switch (expression)
            {
                case RegularExpressions.UnlikelyCandidates:
                    RE_UnlikelyCandidates = new Regex($"{RE_UnlikelyCandidates}|{option}", RegexOptions.IgnoreCase);
                    break;
                case RegularExpressions.PossibleCandidates:
                    RE_OkMaybeItsACandidate = new Regex($"{RE_OkMaybeItsACandidate}|{option}", RegexOptions.IgnoreCase);
                    break;
                case RegularExpressions.Positive:
                    RE_Positive = new Regex($"{RE_Positive}|{option}", RegexOptions.IgnoreCase);
                    break;
                case RegularExpressions.Negative:
                    RE_Negative = new Regex($"{RE_Negative}|{option}", RegexOptions.IgnoreCase);
                    break;
                case RegularExpressions.Extraneous:
                    RE_Extraneous = new Regex($"{RE_Extraneous}|{option}", RegexOptions.IgnoreCase);
                    break;
                case RegularExpressions.Byline:
                    RE_Byline = new Regex($"{RE_Byline}|{option}", RegexOptions.IgnoreCase);
                    break;
                case RegularExpressions.Videos:
                    string original = RE_Videos.ToString().Substring(0, RE_Videos.ToString().Length - 1);
                    RE_Videos = new Regex($"{original}|{option})", RegexOptions.IgnoreCase);
                    break;
                case RegularExpressions.ShareElements:
                    RE_ShareElements = new Regex($"(\b|_)(share|sharedaddy|{option})(\b|_)", RegexOptions.IgnoreCase);
                    break;
                default:
                    break;
            }
        }
        /// <summary>Allow to set an user agent</summary>
        /// <param name="userAgent">A string indicating the User Agent used for web requests made by this library</param>
        public Reader SetCustomUserAgent(string? userAgent)
        {
            if (!string.IsNullOrWhiteSpace(userAgent))
            {
                _userAgent = userAgent;
            }
            return this;
        }

        /// <summary>Allow to set a custom HttpClient</summary>
        /// <param name="clientHandler">The new HttpClientHandler for all web requests made by this library</param>
        public static void SetBaseHttpClientHandler(HttpMessageHandler clientHandler)
        {
            _httpClientHandler = new Lazy<HttpMessageHandler>(() => clientHandler);
        }

        /// <summary>Simple method to safely get minimum content length based on language</summary>        
        private int GetMinContentLengthBasedOnLanguage()
        {
            if (string.IsNullOrEmpty(this.language))
                return MinContentLengthReadearable.GetOrDefault("Default", 140);

            CultureInfo culture = CultureInfo.InvariantCulture;

            try
            {
                culture = new CultureInfo(this.language);
            }
            catch (CultureNotFoundException)
            { }


            var length = MinContentLengthReadearable.FirstOrDefault(x => culture.EnglishName.StartsWith(x.Key, StringComparison.Ordinal));

            return length.Value > 0 ? length.Value : MinContentLengthReadearable.GetOrDefault("Default", 140);
        }

        /// The following methods implement the standard dispose pattern
        ~Reader() => Dispose(false);

        /// <summary>Protected implementation of Dispose pattern.</summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    doc?.Dispose();
                }

                disposedValue = true;
            }
        }

        /// <summary>Public implementation of Dispose pattern callable by consumers.</summary> 
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
