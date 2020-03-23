using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SmartReader
{
    /// <summary>Flags that sets how aggressively remove potentially useless content</summary>
    [Flags]
    public enum Flags
    {
        /// <summary>Do not perform any cleaning</summary>
        None = 0,
        /// <summary>Remove unlikely content</summary>
        StripUnlikelys = 1,
        /// <summary>Remove content according that does not pass a certain threshold</summary>
        WeightClasses = 2,
        /// <summary>Clean content that does not look promising</summary>
        CleanConditionally = 4
    }

    /// <summary>The level of debug information to record</summary>
    public enum ReportLevel
    {
        /// <summary>Only issues</summary>
        Issue,
        /// <summary>Every useful information</summary>
        Info
    }

    /// <summary>The different kinds regular expressions used to filter elements</summary>
    public enum RegularExpressions
    {
        /// <summary>To remove elements unlikely to contain useful content</summary>
        UnlikelyCandidates,
        /// <summary>To find elements likely to contain useful content</summary>
        PossibleCandidates,
        /// <summary>Classes and tags that increases chances to keep the element</summary>
        Positive,
        /// <summary>Classes and tags that decreases chances to keep the element</summary>
        Negative,
        /// <summary>Extraneous elements</summary>
        /// <remarks>Nota that this regular expression is not used anywhere at the moment</remarks>
        Extraneous,
        /// <summary>To detect byline</summary>
        Byline,
        /// <summary>To keep only useful videos</summary>
        Videos,
        /// <summary>To find sharing elements</summary>
        ShareElements
    }

    /// <summary>The main Reader class</summary>
    /// <remarks>
	/// <para>This code is based on a port of the readability library of Firefox Reader View
    /// available at: https://github.com/mozilla/readability. Which is heavily based on Arc90's readability.js (1.7f.1) script available at: http://code.google.com/p/arc90labs-readability </para>
    /// </remarks>
    public class Reader
    {        
        private static HttpClient httpClient = new HttpClient();
        private Uri uri;
        private IHtmlDocument doc = null;
        private string articleTitle;
        private string articleByline;
        private string articleDir;
        private string language;
        private string author;
        private string charset;
        private class Attempt { public IElement content; public long length; }
        private List<Attempt> attempts = new List<Attempt>();

        // Start with all flags set        
        Flags flags = Flags.StripUnlikelys | Flags.WeightClasses | Flags.CleanConditionally;

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
        
        private String[] classesToPreserve = { "page" };        
        /// <summary>
        /// The classes that must be preserved
        /// </summary>
        /// <value>Default: "page"</value>
        public String[] ClassesToPreserve
        {
            get
            {
                return classesToPreserve;
            }
            set
            {
                classesToPreserve = value;

                classesToPreserve = classesToPreserve.Union(new string[] { "page" }).ToArray();
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

        /// <summary>Element tags to score by default.</summary>
        /// <value>Default: false</value>
        public String[] TagsToScore = "section,h2,h3,h4,h5,h6,p,td,pre".ToUpper().Split(',');        

        // All of the regular expressions in use within readability.
        // Defined up here so we don't instantiate them repeatedly in loops.
        Dictionary<string, Regex> regExps = new Dictionary<string, Regex>() {
        { "unlikelyCandidates", new Regex(@"-ad-|ai2html|banner|breadcrumbs|combx|comment|community|cover-wrap|disqus|extra|footer|gdpr|header|legends|menu|related|remark|replies|rss|shoutbox|sidebar|skyscraper|social|sponsor|supplemental|ad-break|agegate|pagination|pager|popup|yom-remote", RegexOptions.IgnoreCase) },
        { "okMaybeItsACandidate", new Regex(@"and|article|body|column|content|main|shadow", RegexOptions.IgnoreCase) },
        { "positive", new Regex(@"article|body|content|entry|hentry|h-entry|main|page|pagination|post|text|blog|story", RegexOptions.IgnoreCase) },
        { "negative", new Regex(@"hidden|^hid$|hid$|hid|^hid|banner|combx|comment|com-|contact|foot|footer|footnote|gdpr|masthead|media|meta|outbrain|promo|related|scroll|share|shoutbox|sidebar|skyscraper|sponsor|shopping|tags|tool|widget", RegexOptions.IgnoreCase) },
        { "extraneous", new Regex(@"print|archive|comment|discuss|e[\-]?mail|share|reply|all|login|sign|single|utility", RegexOptions.IgnoreCase) },
        { "byline", new Regex(@"byline|author|dateline|writtenby|p-author", RegexOptions.IgnoreCase) },
        { "replaceFonts", new Regex(@"<(\/?)font[^>]*>", RegexOptions.IgnoreCase) },
        { "normalize", new Regex(@"\s{2,}", RegexOptions.IgnoreCase) },
        { "videos", new Regex(@"\/\/(www\.)?((dailymotion|youtube|youtube-nocookie|player\.vimeo|v\.qq)\.com|(archive|upload\.wikimedia)\.org|player\.twitch\.tv)", RegexOptions.IgnoreCase) },
        { "nextLink", new Regex(@"(next|weiter|continue|>([^\|]|$)|»([^\|]|$))", RegexOptions.IgnoreCase) },
        { "prevLink", new Regex(@"(prev|earl|old|new|<|«)", RegexOptions.IgnoreCase) },
        { "whitespace", new Regex(@"^\s*$", RegexOptions.IgnoreCase) },       
        { "shareElements", new Regex(@"(\b|_)(share|sharedaddy)(\b|_)", RegexOptions.IgnoreCase) }           
        };

        private String[] alterToDivExceptions = { "DIV", "ARTICLE", "SECTION", "P" };        

        private List<Action<IElement>> CustomOperationsStart = new List<Action<IElement>>();

        private List<Action<IElement>> CustomOperationsEnd = new List<Action<IElement>>();

        private Reader()
        {
            // setting the default user agent
            if (httpClient.DefaultRequestHeaders.UserAgent.Count == 0)                       
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("SmartReader Library");
        }

        /// <summary>
        /// Reads content from the given URI.
        /// </summary>
        /// <param name="uri">A string representing the URI from which to extract the content.</param>
        /// <returns>
        /// An initialized SmartReader object
        /// </returns>        
        public Reader(string uri) : this()
        {
            this.uri = new Uri(uri);
            
            articleTitle = "";
            articleByline = "";
            articleDir = "";                   
        }

        /// <summary>
        /// Reads content from the given text. It needs the uri to make some checks.
        /// </summary>
        /// <param name="uri">A string representing the original URI of the article.</param>
        /// <param name="text">A string from which to extract the article.</param>
        /// <returns>
        /// An initialized SmartReader object
        /// </returns>        
        public Reader(string uri, string text) : this()
        {
            this.uri = new Uri(uri);
                        
            var context = BrowsingContext.New(Configuration.Default.WithCss());
            HtmlParser parser = new HtmlParser(new HtmlParserOptions(), context);
            doc = parser.ParseDocument(text);

            articleTitle = "";
            articleByline = "";
            articleDir = "";
        }

        /// <summary>
        /// Reads content from the given stream. It needs the uri to make some checks.
        /// </summary>
        /// <param name="uri">A string representing the original URI of the article.</param>
        /// <param name="source">A stream from which to extract the article.</param>
        /// <returns>
        /// An initialized SmartReader object
        /// </returns>        
        public Reader(string uri, Stream source) : this()
        {
            this.uri = new Uri(uri);
            
            var context = BrowsingContext.New(Configuration.Default.WithCss());
            HtmlParser parser = new HtmlParser(new HtmlParserOptions(), context);
            doc = parser.ParseDocument(source);

            articleTitle = "";
            articleByline = "";
            articleDir = "";
        }

        /// <summary>
        /// Add a custom operation to be performed before the article is parsed
        /// </summary>
        /// <param name="operation">The operation that will receive the HTML content before any operation</param>
        public void AddCustomOperationStart(Action<IElement> operation)
        {
            CustomOperationsStart.Add(operation);
        }

        /// <summary>
        /// Remove a custom operation to be performed before the article is parsed
        /// </summary>
        /// <param name="operation">The operation to remove</param>
        public void RemoveCustomOperationStart(Action<IElement> operation)
        {
            CustomOperationsStart.Remove(operation);
        }

        /// <summary>
        /// Remove all custom operation to be performed before the article is parsed
        /// </summary>
        public void RemoveAllCustomOperationsStart()
        {
            CustomOperationsStart.Clear();
        }

        /// <summary>
        /// Add a custom operation to be performed after the article is parsed
        /// </summary>
        /// <param name="operation">The operation that will receive the final article</param>
        public void AddCustomOperationEnd(Action<IElement> operation)
        {
            CustomOperationsEnd.Add(operation);
        }

        /// <summary>
        /// Remove a custom operation to be performed after the article is parsed
        /// </summary>    
        /// <param name="operation">The operation to remove</param>
        public void RemoveCustomOperationEnd(Action<IElement> operation)
        {
            CustomOperationsEnd.Remove(operation);
        }

        /// <summary>
        /// Remove all custom operation to be performed after the article is parsed
        /// </summary>    
        public void RemoveAllCustomOperationsEnd()
        {
            CustomOperationsEnd.Clear();
        }

        /// <summary>
        /// Remove all custom operations
        /// </summary>    
        public void RemoveAllCustomOperations()
        {
            CustomOperationsStart.Clear();
            CustomOperationsEnd.Clear();
        }

        /// <summary>
        /// Read and parse the article asynchronously from the given URI.
        /// </summary>
        /// <returns>
        /// An async Task Article object with all the data extracted
        /// </returns>    
        public async Task<Article> GetArticleAsync()
        {
            var context = BrowsingContext.New(Configuration.Default.WithCss());
            HtmlParser parser = new HtmlParser(new HtmlParserOptions(), context);
            
            if (doc == null)
                doc = parser.ParseDocument(await GetStreamAsync(uri));

            return Parse();
        }

        /// <summary>
        /// Read and parse the article from the given URI.
        /// </summary>    
        /// <returns>
        /// An Article object with all the data extracted
        /// </returns>    
        public Article GetArticle()
        {
            var context = BrowsingContext.New(Configuration.Default.WithCss());
            HtmlParser parser = new HtmlParser(new HtmlParserOptions(), context);

            if (doc == null)
            {
                Task<Stream> result = GetStreamAsync(uri);
                result.Wait();
                Stream stream = result.Result;

                doc = parser.ParseDocument(stream);
            }

            return Parse();
        }

        /// <summary>
        /// Read and parse asynchronously the article from the given URI.
        /// </summary>
        /// <param name="uri">A string representing the original URI to extract the content from.</param>
        /// <returns>
        /// An async Task Article object with all the data extracted
        /// </returns>    
        public static async Task<Article> ParseArticleAsync(string uri)
        {
            Reader smartReader = new Reader(uri);            

            return await smartReader.GetArticleAsync();
        }

        /// <summary>
        /// Read and parse the article from the given URI.
        /// </summary>
        /// <param name="uri">A string representing the original URI to extract the content from.</param>
        /// <returns>
        /// An Article object with all the data extracted
        /// </returns>    
        public static Article ParseArticle(string uri)
        {
            Reader smartReader = new Reader(uri);

            Task<Stream> result = smartReader.GetStreamAsync(new Uri(uri));
            result.Wait();
            Stream stream = result.Result;

            var context = BrowsingContext.New(Configuration.Default.WithCss());
            HtmlParser parser = new HtmlParser(new HtmlParserOptions(), context);

            smartReader.doc = parser.ParseDocument(stream);

            return smartReader.Parse();
        }

        /// <summary>
        /// Read and parse the article from the given text. It needs the uri to make some checks.
        /// </summary>
        /// <param name="uri">A string representing the original URI of the article.</param>
        /// <param name="text">A string from which to extract the article.</param>
        /// <returns>
        /// An article object with all the data extracted
        /// </returns>    
        public static Article ParseArticle(string uri, string text)
        {
            Reader smartReader = new Reader(uri, text);

            return smartReader.Parse();
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
            Reader smartReader = new Reader(uri, source);

            return smartReader.Parse();
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
            Readability.FixRelativeUris(articleContent, this.uri, this.doc);

            // Remove classes
            if(!KeepClasses)
                Readability.CleanClasses(articleContent, this.ClassesToPreserve);

            // Remove attributes we set
            if (!Debug)
            {
                CleanReaderAttributes(articleContent, "datatable");
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
            NodeUtility.RemoveNodes(doc.GetElementsByTagName("style"), null);

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
            NodeUtility.ForEachNode(NodeUtility.GetAllNodesWithTag(elem, new string[] { "br" }), (br) =>
            {
                var next = br.NextSibling;

                // Whether 2 or more <br> elements have been found and replaced with a
                // <p> block.
                var replaced = false;

                // If we find a <br> chain, remove the <br>s until we hit another element
                // or non-whitespace. This leaves behind the first <br> in the chain
                // (which will be replaced with a <p> later).
                while ((next = NodeUtility.NextElement(next, regExps["whitespace"])) != null && ((next as IElement).TagName == "BR"))
                {
                    replaced = true;
                    var brSibling = next.NextSibling;
                    next.Parent.RemoveChild(next);
                    next = brSibling;
                }

                // If we removed a <br> chain, replace the remaining <br> with a <p>. Add
                // all sibling nodes as children of the <p> until we hit another <br>
                // chain.
                if (replaced)
                {
                    var p = doc.CreateElement("p");
                    br.Parent.ReplaceChild(p, br);

                    next = p.NextSibling;
                    while (next != null)
                    {
                        // If we've hit another <br><br>, we're done adding children to this <p>.
                        if ((next as IElement)?.TagName == "BR")
                        {
                            var nextElem = NodeUtility.NextElement(next.NextSibling, regExps["whitespace"]);
                            if (nextElem != null && (nextElem as IElement).TagName == "BR")
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

                    if (p.Parent.NodeName == "P")
                        NodeUtility.SetNodeTag(p.ParentElement, "DIV");

                }
            });
        }

        /// <summary>
        /// Remove attributes Reader added to store values.
        /// </summary>
        private void CleanReaderAttributes(IElement node, string attribute)
        {            
            if (!String.IsNullOrEmpty(node.GetAttribute(attribute)))
            {
                node.RemoveAttribute(attribute);
            }

            for (node = node.FirstElementChild; node != null; node = node.NextElementSibling)
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
            Clean(articleContent, "h1");
            Clean(articleContent, "footer");
            Clean(articleContent, "link");
            Clean(articleContent, "aside");

            // Clean out elements with little content that have "share" in their id/class combinations from final top candidates,
            // which means we don't remove the top candidates even they have "share".

            var shareElementThreshold = CharThreshold;

            NodeUtility.ForEachNode(articleContent.Children, (topCandidate) => {
                NodeUtility.CleanMatchedNodes(topCandidate as IElement, (node, matchString) => {
                    return regExps["shareElements"].IsMatch(matchString) &&  node.TextContent.Length < shareElementThreshold;
                    });
                });

            // If there is only one h2 and its text content substantially equals article title,
            // they are probably using it as a header and not a subheader,
            // so remove it since we already extract the title separately.
            var h2 = articleContent.GetElementsByTagName("h2");
            if (h2.Length == 1)
            {
                var lengthSimilarRate = (h2[0].TextContent.Length - articleTitle.Length) / articleTitle.Length;

                if (Math.Abs(lengthSimilarRate) < 0.5)
                {
                    var titlesMatch = false;
                    if (lengthSimilarRate > 0)
                    {
                        titlesMatch = h2[0].TextContent.Contains(articleTitle);
                    }
                    else
                    {
                        titlesMatch = articleTitle.Contains(h2[0].TextContent);
                    }
                    if (titlesMatch)
                    {
                        Clean(articleContent, "h2");
                    }
                }
            }

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

            // Remove extra paragraphs
            NodeUtility.RemoveNodes(articleContent.GetElementsByTagName("p"), (paragraph) =>
            {
                var imgCount = paragraph.GetElementsByTagName("img").Length;
                var embedCount = paragraph.GetElementsByTagName("embed").Length;
                var objectCount = paragraph.GetElementsByTagName("object").Length;
                // At this point, nasty iframes have been removed, only remain embedded video ones.
                var iframeCount = paragraph.GetElementsByTagName("iframe").Length;
                var totalCount = imgCount + embedCount + objectCount + iframeCount;

                return totalCount == 0 && String.IsNullOrEmpty(NodeUtility.GetInnerText(paragraph, false));
            });

            NodeUtility.ForEachNode(NodeUtility.GetAllNodesWithTag(articleContent, new string[] { "br" }), (br) =>
            {
                var next = NodeUtility.NextElement(br.NextSibling, regExps["whitespace"]);
                if (next != null && (next as IElement).TagName == "P")
                    br.Parent.RemoveChild(br);
            });

            // Remove single-cell tables
            NodeUtility.ForEachNode(NodeUtility.GetAllNodesWithTag(articleContent, new string[] { "table" }), (table) =>
            {
                var tbody = NodeUtility.HasSingleTagInsideElement(table as IElement, "TBODY") ? (table as IElement).FirstElementChild : table;
                if (NodeUtility.HasSingleTagInsideElement(tbody as IElement, "TR"))
                {
                    var row = (tbody as IElement).FirstElementChild;
                    if (NodeUtility.HasSingleTagInsideElement(row, "TD"))
                    {
                        var cell = row.FirstElementChild;
                        cell = NodeUtility.SetNodeTag(cell, NodeUtility.EveryNode(cell.ChildNodes, NodeUtility.IsPhrasingContent) ? "P" : "DIV");
                        table.Parent.ReplaceChild(cell, table);
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
            if (GetReadabilityScore(node) > 0)
            {
                double current = double.Parse(node.GetAttribute("readability-score"), System.Globalization.CultureInfo.InvariantCulture.NumberFormat);

                node.SetAttribute("readability-score", (current + score).ToString(System.Globalization.CultureInfo.InvariantCulture.NumberFormat));
            }
            else
                SetReadabilityScore(node, score);
        }

        private void SetReadabilityScore(IElement node, double score)
        {
            node.SetAttribute("readability-score", score.ToString(System.Globalization.CultureInfo.InvariantCulture.NumberFormat));
        }

        private double GetReadabilityScore(IElement node)
        {            
            if (!String.IsNullOrEmpty(node.GetAttribute("readability-score")))
                return double.Parse(node.GetAttribute("readability-score"), System.Globalization.CultureInfo.InvariantCulture.NumberFormat);
            else
                return 0.0;
        }        

        private bool CheckByline(IElement node, string matchString)
        {
            if (!String.IsNullOrEmpty(articleByline))
            {
                return false;
            }


            String rel = "";
            String itemprop = "";

            if (node is IElement && !String.IsNullOrEmpty(node.GetAttribute("rel")))
            {
                rel = node.GetAttribute("rel");
                itemprop = node.GetAttribute("itemprop");
            }

            if ((rel == "author" || (!String.IsNullOrEmpty(itemprop) && itemprop.IndexOf("author") != -1) || regExps["byline"].IsMatch(matchString)) && Readability.IsValidByline(node.TextContent))
            {
                if (rel == "author")
                    author = node.TextContent.Trim();
                else
                {
                    IElement tempAuth = node.QuerySelector("[rel=\"author\"]");
                    if (tempAuth != null)
                        author = tempAuth.TextContent.Trim();
                }

                articleByline = node.TextContent.Trim();
                return true;
            }

            return false;
        }

        /// <summary>
        /// grabArticle - Using a variety of metrics (content score, classname, element types), find the content that is
        /// most likely to be the stuff a user wants to read.Then return it wrapped up in a div.
        /// </summary>
        /// <param name="page">a document to run upon. Needs to be a full document, complete with body</param>
        private IElement GrabArticle(IElement page = null)
        {
            if (Debug || Logging == ReportLevel.Info)
                LoggerDelegate("**** grabArticle ****");

            var doc = this.doc;
            var isPaging = (page != null ? true : false);
            page = page != null ? page : this.doc.Body;

            // We can't grab an article if we don't have a page!
            if (page == null)
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
                var stripUnlikelyCandidates = FlagIsActive(Flags.StripUnlikelys);

                // First, node prepping. Trash nodes that look cruddy (like ones with the
                // class name "comment", etc), and turn divs into P tags where they have been
                // used inappropriately (as in, where they contain no other block level elements.)
                List<IElement> elementsToScore = new List<IElement>();
                var node = this.doc.DocumentElement;

                while (node != null)
                {
                    var matchString = node.ClassName + " " + node.Id;

                    if (!NodeUtility.IsProbablyVisible(node))
                    {
                        if (Debug || Logging == ReportLevel.Info)
                            LoggerDelegate("Removing hidden node - " + matchString);
                        node = NodeUtility.RemoveAndGetNext(node) as IElement;
                        continue;
                    }

                    // Check to see if this node is a byline, and remove it if it is.
                    if (CheckByline(node, matchString))
                    {
                        node = NodeUtility.RemoveAndGetNext(node) as IElement;
                        continue;
                    }

                    // Remove unlikely candidates
                    if (stripUnlikelyCandidates)
                    {
                        if (regExps["unlikelyCandidates"].IsMatch(matchString) &&
                            !regExps["okMaybeItsACandidate"].IsMatch(matchString) &&
                            !HasAncestorTag(node, "table") &&
                            node.TagName != "BODY" &&
                            node.TagName != "A")
                        {
                            if (Debug || Logging == ReportLevel.Info)
                                LoggerDelegate("Removing unlikely candidate - " + matchString);                            
                            node = NodeUtility.RemoveAndGetNext(node) as IElement;
                            continue;
                        }
                    }

                    // Remove DIV, SECTION, and HEADER nodes without any content(e.g. text, image, video, or iframe).

                    if ((node.TagName == "DIV" || node.TagName == "SECTION" || node.TagName == "HEADER" ||
                         node.TagName == "H1" || node.TagName == "H2" || node.TagName == "H3" ||
                         node.TagName == "H4" || node.TagName == "H5" || node.TagName == "H6") &&
                        NodeUtility.IsElementWithoutContent(node))
                    {
                        node = NodeUtility.RemoveAndGetNext(node) as IElement;
                        continue;
                    }

                    if (TagsToScore.ToList().IndexOf(node.TagName) != -1)
                    {
                        elementsToScore.Add(node);
                    }

                    // Turn all divs that don't have children block level elements into p's
                    if (node.TagName == "DIV")
                    {
                        // Put phrasing content into paragraphs.
                        INode p = null;
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
                                    p = doc.CreateElement("p");
                                    node.ReplaceChild(p, childNode);
                                    p.AppendChild(childNode);
                                }                               
                            }
                            else if (p != null)
                            {
                                while (p.LastChild != null && NodeUtility.IsWhitespace(p.LastChild))                p.RemoveChild(p.LastChild);

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
                            node.Parent.ReplaceChild(newNode, node);
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
                List<IElement> candidates = new List<IElement>();
                NodeUtility.ForEachNode(elementsToScore, (elementToScore) =>
                {
                    if (elementToScore.Parent == null)
                        return;

                    // If this paragraph is less than 25 characters, don't even count it.
                    string innerText = NodeUtility.GetInnerText(elementToScore as IElement);
                    if (innerText.Length < 25)
                        return;

                    // Exclude nodes with no ancestor.
                    var ancestors = NodeUtility.GetNodeAncestors(elementToScore, 3);
                    if (ancestors.Count() == 0)
                        return;

                    double contentScore = 0;

                    // Add a point for the paragraph itself as a base.
                    contentScore += 1;

                    // Add points for any commas within this paragraph.
                    contentScore += innerText.Split(',').Length;

                    // For every 100 characters in this paragraph, add another point. Up to 3 points.
                    contentScore += Math.Min(Math.Floor(innerText.Length / 100.0), 3);

                    // Initialize and score ancestors.                    
                    NodeUtility.ForEachNode(ancestors, (ancestor, level) =>
                    {                               
                        if (String.IsNullOrEmpty((ancestor as IElement)?.TagName) ||
                            (ancestor as IElement)?.ParentElement == null ||
                            String.IsNullOrEmpty((ancestor as IElement)?.ParentElement?.TagName))                            
                            return;
                        
                        if (GetReadabilityScore(ancestor as IElement).CompareTo(0.0) == 0)
                        {
                            InitializeNode(ancestor as IElement);
                            candidates.Add(ancestor as IElement);
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
                        
                        AddToReadabilityScore(ancestor as IElement, contentScore / scoreDivider);
                    }, 0);
                });

                // After we've calculated scores, loop through all of the possible
                // candidate nodes we found and find the one with the highest score.
                List<IElement> topCandidates = new List<IElement>();
                for (int c = 0, cl = candidates?.Count ?? 0; c < cl; c += 1)
                {
                    var candidate = candidates[c];

                    // Scale the final candidates score based on link density. Good content
                    // should have a relatively small link density (5% or less) and be mostly
                    // unaffected by this operation.
                    var candidateScore = GetReadabilityScore(candidate) * (1 - NodeUtility.GetLinkDensity(candidate));
                    SetReadabilityScore(candidate, candidateScore);
            
                    for (var t = 0; t < NTopCandidates; t++)
                    {
                        IElement aTopCandidate = null;
                        if (t < topCandidates.Count)
                            aTopCandidate = topCandidates[t];
                        
                        if (aTopCandidate == null || candidateScore > GetReadabilityScore(aTopCandidate))
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
                if (topCandidate == null || topCandidate.TagName == "BODY")
                {
                    // Move all of the page's children into topCandidate
                    topCandidate = doc.CreateElement("DIV");
                    neededToCreateTopCandidate = true;
                    // Move everything (not just elements, also text nodes etc.) into the container
                    // so we even include text directly in the body:
                    var kids = page.ChildNodes;
                    while (kids.Length > 0)
                    {
                        topCandidate.AppendChild(kids[0]);
                    }

                    page.AppendChild(topCandidate);

                    InitializeNode(topCandidate);
                }
                else if (topCandidate != null)
                {
                    // Find a better top candidate node if it contains (at least three) nodes which belong to `topCandidates` array
                    // and whose scores are quite closed with current `topCandidate` node.
  
                    List<IElement> alternativeCandidateAncestors = new List<IElement>();
                    for (var i = 1; i < topCandidates.Count; i++)
                    {                        
                        if (GetReadabilityScore(topCandidates[i]) / GetReadabilityScore(topCandidate) >= 0.75)
                        {                            
                            var possibleAncestor = NodeUtility.GetNodeAncestors(topCandidates[i]) as IElement;
                            if (possibleAncestor != null)
                                alternativeCandidateAncestors.Add(possibleAncestor);
                        }
                    }
                    var MINIMUM_TOPCANDIDATES = 3;
                    if (alternativeCandidateAncestors.Count >= MINIMUM_TOPCANDIDATES)
                    {
                        parentOfTopCandidate = topCandidate.ParentElement;
                        while (parentOfTopCandidate.TagName != "BODY")
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
                            parentOfTopCandidate = parentOfTopCandidate.Parent as IElement;
                        }
                    }
                    
                    if (GetReadabilityScore(topCandidate).CompareTo(0.0) == 0)
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
                    parentOfTopCandidate = topCandidate.Parent as IElement;
                    
                    var lastScore = GetReadabilityScore(topCandidate);
                    // The scores shouldn't get too low.
                    var scoreThreshold = lastScore / 3;
                    while (parentOfTopCandidate.TagName != "BODY")
                    {                        
                        if (GetReadabilityScore(parentOfTopCandidate).CompareTo(0.0) == 0)
                        {
                            parentOfTopCandidate = parentOfTopCandidate.Parent as IElement;
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
                        parentOfTopCandidate = parentOfTopCandidate.Parent as IElement;
                    }

                    // If the top candidate is the only child, use parent instead. This will help sibling
                    // joining logic when adjacent content is actually located in parent's sibling node.
                    parentOfTopCandidate = topCandidate.Parent as IElement;
                    while (parentOfTopCandidate.TagName != "BODY" && parentOfTopCandidate.Children.Length == 1)
                    {
                        topCandidate = parentOfTopCandidate;
                        parentOfTopCandidate = topCandidate.Parent as IElement;
                    }
                    
                    if (GetReadabilityScore(topCandidate).CompareTo(0.0) == 0)
                    {
                        InitializeNode(topCandidate);
                    }
                }

                // Now that we have the top candidate, look through its siblings for content
                // that might also be related. Things like preambles, content split by ads
                // that we removed, etc.
                var articleContent = doc.CreateElement("DIV");
                if (isPaging)
                    articleContent.Id = "readability-content";

                var siblingScoreThreshold = Math.Max(10, GetReadabilityScore(topCandidate) * 0.2);
                // Keep potential top candidate's parent node to try to get text direction of it later.
                parentOfTopCandidate = topCandidate.ParentElement;
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
                        if (sibling.ClassName == topCandidate.ClassName && topCandidate.ClassName != "")                            
                            contentBonus += GetReadabilityScore(topCandidate) * 0.2;
                                                
                        if (GetReadabilityScore(sibling) > 0 &&
                        ((GetReadabilityScore(sibling) + contentBonus) >= siblingScoreThreshold))
                        {
                            append = true;
                        }
                        else if (sibling.NodeName == "P")
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
                        if (alterToDivExceptions.ToList().IndexOf(sibling.NodeName) == -1)
                        {
                            // We have a node that isn't a common block level element, like a form or td tag.
                            // Turn it into a div so it doesn't get filtered out later by accident.

                            sibling = NodeUtility.SetNodeTag(sibling, "DIV");
                        }

                        articleContent.AppendChild(sibling);
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
                    var children = articleContent.ChildNodes;
                    while (children.Length > 0)
                    {
                        div.AppendChild(children[0]);
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
				if(textLength < CharThreshold) 
                {
                    parseSuccessful = false;
					page.InnerHtml = pageCacheHtml;

                    if (FlagIsActive(Flags.StripUnlikelys))
                    {
                        RemoveFlag(Flags.StripUnlikelys);
						attempts.Add(new Attempt() { content = articleContent, length = textLength});
                    }
                    else if (FlagIsActive(Flags.WeightClasses))
                    {
                        RemoveFlag(Flags.WeightClasses);
                        attempts.Add(new Attempt() { content = articleContent, length = textLength });
                    }
                    else if (FlagIsActive(Flags.CleanConditionally))
                    {
                        RemoveFlag(Flags.CleanConditionally);
                        attempts.Add(new Attempt() { content = articleContent, length = textLength });
                    }
                    else
                    {
                        attempts.Add(new Attempt() { content = articleContent, length = textLength });
                        // No luck after removing flags, just return the longest text we found during the different loops
                        attempts = attempts.OrderByDescending(x => x.length).ToList();
						
						// But first check if we actually have something
						if (attempts.Count == 0)
                        {
							return null;
						}
				
						articleContent = attempts[0].content;
						parseSuccessful = true;
                    }
                }
                
				if(parseSuccessful)
                {
                    // Find out text direction from ancestors of final top candidate.
                    IEnumerable<IElement> ancestors = new IElement[] { parentOfTopCandidate, topCandidate }.Concat(NodeUtility.GetElementAncestors(parentOfTopCandidate)) as IEnumerable<IElement>;                        
                    NodeUtility.SomeNode(ancestors, (ancestor) =>
                    {
                        if (String.IsNullOrEmpty(ancestor.TagName))
                            return false;
                        var _articleDir = ancestor.GetAttribute("dir");
                        if (!String.IsNullOrEmpty(_articleDir))
                        {
                            this.articleDir = _articleDir;
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
            if (e.ClassName != null && e.ClassName != "")
            {
                if (regExps["negative"].IsMatch(e.ClassName))
                    weight -= 25;

                if (regExps["positive"].IsMatch(e.ClassName))
                    weight += 25;
            }

            // Look for a special ID
            if (e.Id != null && e.Id != "")
            {
                if (regExps["negative"].IsMatch(e.Id))
                    weight -= 25;

                if (regExps["positive"].IsMatch(e.Id))
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
            var isEmbed = new List<string>() { "object", "embed", "iframe" }.IndexOf(tag) != -1;

            NodeUtility.RemoveNodes(e.GetElementsByTagName(tag), (element) =>
            {
                // Allow youtube and vimeo videos through as people usually want to see those.
                if (isEmbed)
                {                    
                    // First, check the elements attributes to see if any of them contain youtube or vimeo
                    for (var i = 0; i < element.Attributes.Length; i++)
                    {
                        if (regExps["videos"].IsMatch(element.Attributes[i].Value))
                        {
                            return false;
                        }
                    }

                    // For embed with <object> tag, check inner HTML as well.
                    if (element.TagName == "OBJECT" && regExps["videos"].IsMatch(element.InnerHtml))
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
        private bool HasAncestorTag(IElement node, string tagName, int maxDepth = 3, Func<IElement, bool> filterFn = null)
        {
            tagName = tagName.ToUpper();
            var depth = 0;

            while (node.ParentElement != null)
            {
                if (maxDepth > 0 && depth > maxDepth)
                    return false;
                if (node.ParentElement.TagName == tagName
                    && (filterFn == null || filterFn(node.ParentElement)))
                    return true;
                node = node.ParentElement;
                depth++;
            }
            return false;
        }

        private bool IsDataTable(IElement node)
        {
            return !String.IsNullOrEmpty(node.GetAttribute("datatable")) ? node.GetAttribute("datatable").Contains("true") : false;
        }

        /// <summary>
        /// Return an object indicating how many rows and columns this table has.
        /// </summary>
        private Tuple<int, int> GetRowAndColumnCount(IElement table)
        {
            var rows = 0;
            var columns = 0;
            var trs = table.GetElementsByTagName("tr");
            for (var i = 0; i < trs.Length; i++)
            {
                string rowspan = trs[i].GetAttribute("rowspan") ?? "";
                int rowSpanInt = 0;
                if (!String.IsNullOrEmpty(rowspan))
                {
                    int.TryParse(rowspan, out rowSpanInt);
                }
                rows += rowSpanInt == 0 ? 1 : rowSpanInt;
                // Now look for column-related info
                var columnsInThisRow = 0;
                var cells = trs[i].GetElementsByTagName("td");
                for (var j = 0; j < cells.Length; j++)
                {
                    string colspan = cells[j].GetAttribute("colspan");
                    int colSpanInt = 0;
                    if (!String.IsNullOrEmpty(colspan))
                    {
                        int.TryParse(colspan, out colSpanInt);
                    }
                    columnsInThisRow += colSpanInt == 0 ? 1 : colSpanInt;
                }
                columns = Math.Max(columns, columnsInThisRow);
            }
            return Tuple.Create(rows, columns);
        }
        
        /// <summary>
        /// Look for 'data' (as opposed to 'layout') tables, for which we use
        /// similar checks as
        /// https://dxr.mozilla.org/mozilla-central/rev/71224049c0b52ab190564d3ea0eab089a159a4cf/accessible/html/    HTMLTableAccessible.cpp#920
        /// </summary>
        private void MarkDataTables(IElement root)
        {
            var tables = root.GetElementsByTagName("table");
            for (var i = 0; i < tables.Length; i++)
            {
                var table = tables[i];
                var role = table.GetAttribute("role");
                if (role == "presentation")
                {                    
                    table.SetAttribute("dataTable", "false");
                    continue;
                }
                var datatable = table.GetAttribute("datatable");
                if (datatable == "0")
                {                   
                    table.SetAttribute("dataTable", "false");
                    continue;
                }
                var summary = table.GetAttribute("summary");
                if (!String.IsNullOrEmpty(summary))
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

                // If the table has a descendant with any of these tags, consider a data table:
                var dataTableDescendants = new string[] { "col", "colgroup", "tfoot", "thead", "th" };
                Func<string, bool> descendantExists = (tag) =>
                {
                    return table.GetElementsByTagName(tag).ElementAtOrDefault(0) != null;
                };
                if (dataTableDescendants.Any(descendantExists))
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
                if (sizeInfo.Item1 >= 10 || sizeInfo.Item2 > 4)
                {                    
                    table.SetAttribute("dataTable", "true");                 
                    continue;
                }
                // Now just go by size entirely:
                if (sizeInfo.Item1 * sizeInfo.Item2 > 10)
                    table.SetAttribute("dataTable", "true");
            }
        }

        /// <summary>
        /// convert images and figures that have properties like data-src into images that can be loaded without JS
        /// </summary>
        private void FixLazyImages(IElement root)
        {
            NodeUtility.ForEachNode(NodeUtility.GetAllNodesWithTag(root, new string[] { "img", "picture", "figure" }), (node) =>
            {
                var elem = node as IElement;
                var src = elem.GetAttribute("src");
                var srcset = elem.GetAttribute("srcset");                

                if ((String.IsNullOrEmpty(src) && String.IsNullOrEmpty(srcset))
                || (!String.IsNullOrEmpty(elem.ClassName) && elem.ClassName.ToLower().IndexOf("lazy") != -1))
                {
                    for (var i = 0; i < elem.Attributes.Length; i++)
                    {
                        var attr = elem.Attributes[i];
                        
                        if (attr.Name == "src" || attr.Name == "srcset")
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

                        if (!String.IsNullOrEmpty(copyTo))
                        {
                            //if this is an img or picture, set the attribute directly
                            if (elem.TagName == "IMG" || elem.TagName == "PICTURE")
                            {
                                elem.SetAttribute(copyTo, attr.Value);
                            }
                            else if (elem.TagName == "FIGURE"
                            && NodeUtility.GetAllNodesWithTag(elem, new string[] { "IMG", "PICTURE" }).Length == 0)
                            {
                                //if the item is a <figure> that does not contain an image or picture, create one and place it inside the figure
                                //see the nytimes-3 testcase for an example
                                var img = doc.CreateElement("img");
                                img.SetAttribute(copyTo, attr.Value);
                                elem.AppendChild(img);
                            }
                        }
                    }
                }
            });        
        }


        /// <summary>
        /// <para>Clean an element of all tags of type "tag" if they look fishy.</para>
        /// <para>Fishy" is an algorithm based on content length, classnames, link density, number of images and embeds, etc.</para>
        /// </summary>
        private void CleanConditionally(IElement e, string tag)
        {
            if (!FlagIsActive(Flags.CleanConditionally))
                return;

            var isList = tag == "ul" || tag == "ol";

            // Gather counts for other typical elements embedded within.
            // Traverse backwards so we can remove nodes at the same time
            // without effecting the traversal.
            //
            // TODO: Consider taking into account original contentScore here.
            NodeUtility.RemoveNodes(e.GetElementsByTagName(tag), (node) =>
            {
                // First check if this node IS data table, in which case don't remove it.
                if (tag == "table" && IsDataTable(node))
                {
                    return false;
                }

                // Next check if we're inside a data table, in which case don't remove it as well.
                if (HasAncestorTag(node, "table", -1, IsDataTable))
                {
                    return false;
                }

                var weight = GetClassWeight(node);
                var contentScore = 0;

                if (weight + contentScore < 0)
                {
                    return true;
                }

                if (NodeUtility.GetCharCount(node, ",") < 10)
                {
                    // If there are not very many commas, and the number of
                    // non-paragraph elements is more than paragraphs or other
                    // ominous signs, remove the element.
                    float p = node.GetElementsByTagName("p").Length;
                    float img = node.GetElementsByTagName("img").Length;
                    float li = node.GetElementsByTagName("li").Length - 100;
                    float input = node.GetElementsByTagName("input").Length;

                    var embedCount = 0;
                    var embeds = NodeUtility.ConcatNodeLists(
                        node.GetElementsByTagName("object"),
                        node.GetElementsByTagName("embed"),
                        node.GetElementsByTagName("iframe"));

                    for (var i = 0; i < embeds.Count(); i++)
                    {
                        // If this embed has attribute that matches video regex, don't delete it.
                        for (var j = 0; j < embeds.ElementAt(i).Attributes.Length; j++)
                        {
                            if (regExps["videos"].IsMatch(embeds.ElementAt(i).Attributes[j].Value))
                            {
                                return false;
                            }
                        }

                        // For embed with <object> tag, check inner HTML as well.
                        if (embeds.ElementAt(i).TagName == "OBJECT" && regExps["videos"].IsMatch(embeds.ElementAt(i).InnerHtml))
                        {
                            return false;
                        }

                        embedCount++;
                    }

                    var linkDensity = NodeUtility.GetLinkDensity(node);
                    var contentLength = NodeUtility.GetInnerText(node).Length;                 

                    var haveToRemove =
                      (img > 1 && p / img < 0.5 && !HasAncestorTag(node, "figure")) ||
                      (!isList && li > p) ||
                      (input > Math.Floor(p / 3)) ||
                      (!isList && contentLength < 25 && (img.CompareTo(0) == 0 || img > 2) && !HasAncestorTag(node, "figure")) ||
                      (!isList && weight < 25 && linkDensity > 0.2) ||
                      (weight >= 25 && linkDensity > 0.5) ||
                      ((embedCount == 1 && contentLength < 75) || embedCount > 1);

                    return haveToRemove;
                }
                return false;
            });
        }

        /// <summary>
        /// Clean out spurious headers from an Element. Checks things like classnames and link density.
        /// </summary>
        private void CleanHeaders(IElement e)
        {
            for (var headerIndex = 1; headerIndex < 3; headerIndex += 1)
            {
                NodeUtility.RemoveNodes(e.GetElementsByTagName("h" + headerIndex), (header) =>
                {
                    return GetClassWeight(header) < 0;
                });
            }
        }

        private bool FlagIsActive(Flags flag)
        {
            return (flags & flag) > 0;
        }

        private void RemoveFlag(Flags flag)
        {
            flags = flags & ~flag;
        }

        /// <summary>
        /// Decides whether or not the document is reader-able without parsing the whole thing.
        /// </summary>
        /// <returns>Whether or not we suspect parse method will suceeed at returning an article object.</returns>
        private bool IsProbablyReaderable(Func<IElement, bool> helperIsVisible = null)
        {            
            var nodes = NodeUtility.GetAllNodesWithTag(doc.DocumentElement, new string[] { "p", "pre" });

            // Get <div> nodes which have <br> node(s) and append them into the `nodes` variable.
            // Some articles' DOM structures might look like
            // <div>
            //   Sentences<br>
            //   <br>
            //   Sentences<br>
            // </div>
            var brNodes = NodeUtility.GetAllNodesWithTag(doc.DocumentElement, new string[] { "div > br" });
            IEnumerable<IElement> totalNodes = nodes;
            if (brNodes.Length > 0)
            {
                var set = new HashSet<IElement>();

                foreach (var node in brNodes)
                {
                    set.Add(node.ParentElement);
                }

                totalNodes = nodes.Concat(set.ToArray());
            }

            if (helperIsVisible == null)
            {
                helperIsVisible = NodeUtility.IsProbablyVisible;
            }

            double score = 0;
            // This is a little cheeky, we use the accumulator 'score' to decide what to return from
            // this callback:			
            return NodeUtility.SomeNode(totalNodes, (node) =>
            {                
                if (helperIsVisible != null && !helperIsVisible(node))
                    return false;
                
                var matchString = node.ClassName + " " + node.Id;

                if (regExps["unlikelyCandidates"].IsMatch(matchString) &&
                    !regExps["okMaybeItsACandidate"].IsMatch(matchString))
                {
                    return false;
                }

                if (node.Matches("li p"))
                {
                    return false;
                }

                var textContentLength = node.TextContent.Trim().Length;
                if (textContentLength < 140)
                {
                    return false;
                }

                score += Math.Sqrt(textContentLength - 140);

                if (score > 20)
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
            // Avoid parsing too large documents, as per configuration option
            if (MaxElemsToParse > 0)
            {
                var numTags = doc.GetElementsByTagName("*").Length;
                if (numTags > MaxElemsToParse)
                {
                    throw new Exception("Aborting parsing document; " + numTags + " elements found");
                }
            }

            var isReadable = IsProbablyReaderable(NodeUtility.IsVisible);

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

            // Remove script tags from the document.            
            NodeUtility.RemoveScripts(doc.DocumentElement);

            PrepDocument();

            if (Debug || Logging == ReportLevel.Info)
                LoggerDelegate("<h2>Pre-GrabArticle:</h2>" + doc.DocumentElement.InnerHtml);

            var metadata = Readability.GetArticleMetadata(this.doc, this.uri, this.language);
            articleTitle = metadata.Title;

            var articleContent = GrabArticle();
            if (articleContent == null)
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
            if (String.IsNullOrEmpty(metadata.Excerpt))
            {
                var paragraphs = articleContent.GetElementsByTagName("p");
                if (paragraphs.Length > 0)
                {
                    metadata.Excerpt = paragraphs[0].TextContent.Trim();
                }
            }

            Article article;

            article = new Article(uri, articleTitle, articleByline, articleDir, language, author, articleContent, metadata, isReadable);

            return article;
        }       

        private async Task<Stream> GetStreamAsync(Uri resource)
        {            
            var response = await httpClient.GetAsync(resource).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Cannot GET resource {resource}. StatusCode: {response.StatusCode}");
            }

            var headLan = response.Headers.FirstOrDefault(x => x.Key.ToLower() == "content-language");
            if (headLan.Value != null && headLan.Value.Any())
                language = headLan.Value.ElementAt(0);

            var headCont = response.Headers.FirstOrDefault(x => x.Key.ToLower() == "content-type");
            if (headCont.Value != null && headCont.Value.Any())
            {
                int index = headCont.Value.ElementAt(0).IndexOf("charset=");
                if (index != -1)
                    charset = headCont.Value.ElementAt(0).Substring(index + 8);
            }

            return await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        }

        private static async Task<HttpResponseMessage> RequestPageAsync(Uri resource)
        {
            return await httpClient.GetAsync(resource).ConfigureAwait(false);
        }

        internal static async Task<long> GetImageSizeAsync(Uri imageSrc)
        {
            HttpRequestMessage headRequest = new HttpRequestMessage(HttpMethod.Head, imageSrc);
            var response = await httpClient.SendAsync(headRequest).ConfigureAwait(false);
            long size = 0;

            if (response.IsSuccessStatusCode)
            {               
                if(response.Content.Headers.ContentLength != null)
                    size = response.Content.Headers.ContentLength.Value;
            }

            return size;
        }

        internal static async Task<Byte[]> GetImageBytesAsync(Uri resource)
        {
            var response = await httpClient.GetAsync(resource).ConfigureAwait(false);

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
                    regExps["unlikelyCandidates"] = new Regex(newExpression, RegexOptions.IgnoreCase);
                    break;
                case RegularExpressions.PossibleCandidates:
                    regExps["okMaybeItsACandidate"] = new Regex(newExpression, RegexOptions.IgnoreCase);
                    break;
                case RegularExpressions.Positive:
                    regExps["positive"] = new Regex(newExpression, RegexOptions.IgnoreCase);
                    break;
                case RegularExpressions.Negative:
                    regExps["negative"] = new Regex(newExpression, RegexOptions.IgnoreCase);
                    break;
                case RegularExpressions.Extraneous:
                    regExps["extraneous"] = new Regex(newExpression, RegexOptions.IgnoreCase);
                    break;
                case RegularExpressions.Byline:
                    regExps["byline"] = new Regex(newExpression, RegexOptions.IgnoreCase);
                    break;
                case RegularExpressions.Videos:
                    regExps["videos"] = new Regex(newExpression, RegexOptions.IgnoreCase);
                    break;
                case RegularExpressions.ShareElements:
                    regExps["shareElements"] = new Regex(newExpression, RegexOptions.IgnoreCase);
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
                    regExps["unlikelyCandidates"] = new Regex($"{regExps["unlikelyCandidates"].ToString()}|{option}", RegexOptions.IgnoreCase);
                    break;
                case RegularExpressions.PossibleCandidates:
                    regExps["okMaybeItsACandidate"] = new Regex($"{regExps["okMaybeItsACandidate"].ToString()}|{option}", RegexOptions.IgnoreCase);
                    break;
                case RegularExpressions.Positive:
                    regExps["positive"] = new Regex($"{regExps["positive"].ToString()}|{option}", RegexOptions.IgnoreCase);
                    break;
                case RegularExpressions.Negative:
                    regExps["negative"] = new Regex($"{regExps["negative"].ToString()}|{option}", RegexOptions.IgnoreCase);
                    break;
                case RegularExpressions.Extraneous:
                    regExps["extraneous"] = new Regex($"{regExps["extraneous"].ToString()}|{option}", RegexOptions.IgnoreCase);
                    break;
                case RegularExpressions.Byline:
                    regExps["byline"] = new Regex($"{regExps["byline"].ToString()}|{option}", RegexOptions.IgnoreCase);
                    break;
                case RegularExpressions.Videos:
                    string original = regExps["videos"].ToString().Substring(0, regExps["videos"].ToString().Length - 1);
                    regExps["videos"] = new Regex($"{original}|{option})", RegexOptions.IgnoreCase);
                    break;
                case RegularExpressions.ShareElements:                    
                    regExps["shareElements"] = new Regex($"(\b|_)(share|sharedaddy|{option})(\b|_)", RegexOptions.IgnoreCase);
                    break;
                default:
                    break;
            }
        }
        /// <summary>Allow to set an user agent</summary>
        /// <param name="userAgent">A string indicating the User Agent used for web requests made by this library</param>
        public static void SetCustomUserAgent(string userAgent)
        {
            httpClient.DefaultRequestHeaders.UserAgent.Clear();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        }

        /// <summary>Allow to set a custom HttpClient</summary>
        /// <param name="client">The new HttpClient for all web requests made by this library</param>
        public static void SetCustomHttpClient(HttpClient client)
        {
            httpClient = client;
        }
    }
}