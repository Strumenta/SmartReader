using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using AngleSharp.Parser.Html;
using AngleSharp.Dom;
using AngleSharp.Dom.Html;

namespace SmartReader
{
	[Flags]
	public enum Flags
	{
		None = 0,
		StripUnlikelys = 1,
		WeightClasses = 2,
		CleanConditionally = 4
	}

	public class Page
	{
		public double Score { get; set; }
		public String LinkText { get; set; }
		public String Href { get; set; }
	}

	public class Metadata
	{
		public String Byline { get; set; } = "";
		public String Title { get; set; } = "";
		public String Excerpt { get; set; } = "";
		public String Language { get; set; } = "";
		public DateTime? PublicationDate { get; set; } = null;
		public String Author { get; set; } = "";
	}

    /// <summary>
    /// SmartReader
    /// </summary>
    public class Reader
    {
        /*
		* This code is, for the most part, a port of the readibility library of Firefox Reader View
		* available at: https://github.com/mozilla/readability
		* which is in turn heavily based on Arc90's readability.js (1.7f.1) script
		* available at: http://code.google.com/p/arc90labs-readability
		*         
		*/

        private static readonly HttpClient httpClient = new HttpClient();

        private Uri uri;
        private IHtmlDocument doc;
        private string articleTitle;
        private string articleByline;
        private string articleDir;
        private string language;
        private string author;
        private string charset;

        // A list of scores
        private Dictionary<IElement, double> readabilityScores = new Dictionary<IElement, double>();

        // A list of datatables
        private List<IElement> readabilityDataTable = new List<IElement>();

        // Start with all flags set        
        Flags flags = Flags.StripUnlikelys | Flags.WeightClasses | Flags.CleanConditionally;

        // The list of pages we've parsed in this call of readability,
        // for autopaging. As a key store for easier searching.
        //List<String> parsedPages = new List<String>();

        // A list of the ETag headers of pages we've parsed, in case they happen to match,
        // we'll know it's a duplicate.
        //List<string> pageETags = new List<string>();

        // Make an AJAX request for each page and append it to the document.
        //int curPageNum = 1;

        //var logEl;

        /// <summary>Max number of nodes supported by this parser</summary>
        /// <value>Default: 0 (no limit)</value>        
        public int MaxElemsToParse { get; set; } = 0;


        /// <summary>The number of top candidates to consider when analysing how tight the competition is among candidates</summary>
        /// <value>Default: 5</value>
        public int NTopCandidates { get; set; } = 5;

        /// <summary>
        /// The default number of words an article must have in order to return a result
        /// </summary>
        /// <value>Default: 500</value>
        public int WordThreshold { get; set; } = 500;

        // These are the IDs and classes that readability sets itself.
        private String[] idsToPreserve = { "readability-content", "readability-page-1" };

        private String[] classesToPreserve = { "readability-styled", "page" };
        public String[] ClassesToPreserve {
            get
            {
                return classesToPreserve;
            }
            set
            {                
                classesToPreserve = value;

                classesToPreserve = classesToPreserve.Union(new string[] { "readability-styled", "page" }).ToArray();
            }
        }



        /// <summary>Set the Debug option and write the data on Logger</summary>
        /// <value>Default: false</value>
        public bool Debug { get; set; } = false;

		/// <summary>Where the debug data is going to be written</summary>
		/// <value>Default: null</value>
		public TextWriter Logger { get; set; } = null;

		/// <summary>The library tries to determine if it will find an article before actually trying to do it. This option decides whether to continue if the library heuristics fails. This value is ignored if Debug is set to true</summary>
		/// <value>Default: false</value>
		public bool ContinueIfNotReadable { get; set; } = true;

		// Element tags to score by default.
		public String[] TagsToScore = "section,h2,h3,h4,h5,h6,p,td,pre".ToUpper().Split(',');

		// All of the regular expressions in use within readability.
		// Defined up here so we don't instantiate them repeatedly in loops.
		Dictionary<string, Regex> regExps = new Dictionary<string, Regex>() {
	    { "unlikelyCandidates", new Regex(@"banner|breadcrumbs|combx|comment|community|cover-wrap|disqus|extra|foot|header|legends|menu|related|remark|replies|rss|shoutbox|sidebar|skyscraper|social|sponsor|supplemental|ad-break|agegate|pagination|pager|popup|yom-remote", RegexOptions.IgnoreCase) },
	    { "okMaybeItsACandidate", new Regex(@"and|article|body|column|main|shadow", RegexOptions.IgnoreCase) },
	    { "positive", new Regex(@"article|body|content|entry|hentry|h-entry|main|page|pagination|post|text|blog|story", RegexOptions.IgnoreCase) },
	    { "negative", new Regex(@"hidden|^hid$|hid$|hid|^hid|banner|combx|comment|com-|contact|foot|footer|footnote|masthead|media|meta|outbrain|promo|related|scroll|share|shoutbox|sidebar|skyscraper|sponsor|shopping|tags|tool|widget", RegexOptions.IgnoreCase) },
	    { "extraneous", new Regex(@"print|archive|comment|discuss|e[\-]?mail|share|reply|all|login|sign|single|utility", RegexOptions.IgnoreCase) },
	    { "byline", new Regex(@"byline|author|dateline|writtenby|p-author", RegexOptions.IgnoreCase) },
	    { "replaceFonts", new Regex(@"<(\/?)font[^>]*>", RegexOptions.IgnoreCase) },
	    { "normalize", new Regex(@"\s{2,}", RegexOptions.IgnoreCase) },
	    { "videos", new Regex(@"\/\/(www\.)?(dailymotion|youtube|youtube-nocookie|player\.vimeo)\.com", RegexOptions.IgnoreCase) },
	    { "nextLink", new Regex(@"(next|weiter|continue|>([^\|]|$)|»([^\|]|$))", RegexOptions.IgnoreCase) },
	    { "prevLink", new Regex(@"(prev|earl|old|new|<|«)", RegexOptions.IgnoreCase) },
	    { "whitespace", new Regex(@"^\s*$", RegexOptions.IgnoreCase) },
	    { "hasContent", new Regex(@"\S$", RegexOptions.IgnoreCase) }
	    };


		private String[] DivToPElems = { "A", "BLOCKQUOTE", "DL", "DIV", "IMG", "OL", "P", "PRE", "TABLE", "UL", "SELECT" };

		private String[] AlterToDivExceptions = { "DIV", "ARTICLE", "SECTION", "P" };

        private String[] PresentationalAttributes = { "align", "background", "bgcolor", "border", "cellpadding", "cellspacing", "frame", "hspace", "rules", "style", "valign", "vspace" };

        private String[] DeprecatedSizeAttributeElems = { "TABLE", "TH", "TD", "HR", "PRE" };

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

			// solves encoding problems
			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

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
        public Reader(string uri, string text)
        {
            this.uri = new Uri(uri);

            // solves encoding problems
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            HtmlParser parser = new HtmlParser();
            doc = parser.Parse(text);

            //var biggestFrame = false;
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
        public Reader(string uri, Stream source)
        {
            this.uri = new Uri(uri);

            // solves encoding problems
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            HtmlParser parser = new HtmlParser();
            doc = parser.Parse(source);

            //var biggestFrame = false;
            articleTitle = "";
            articleByline = "";
            articleDir = "";
        }

        /// <summary>
        /// Read and parse the article asynchronously from the given URI.
        /// </summary>
        /// <param name="uri">A string representing the original URI to extract the content from.</param>
        /// <returns>
        /// An article object with all the data extracted
        /// </returns>    
        public async Task<Article> GetArticleAsync()
        {
            HtmlParser parser = new HtmlParser();

            doc = parser.Parse(await GetStreamAsync(uri));

            return Parse();
        }

        /// <summary>
        /// Read and parse the article from the given URI.
        /// </summary>
        /// <param name="uri">A string representing the original URI to extract the content from.</param>
        /// <returns>
        /// An article object with all the data extracted
        /// </returns>    
        public Article GetArticle()
        {
            HtmlParser parser = new HtmlParser();

            Task<Stream> result = GetStreamAsync(uri);
            result.Wait();
            Stream stream = result.Result;

            doc = parser.Parse(stream);

            return Parse();
        }

        /// <summary>
        /// Read and parse asynchronously the article from the given URI.
        /// </summary>
        /// <param name="uri">A string representing the original URI to extract the content from.</param>
        /// <returns>
        /// An article object with all the data extracted
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
        /// An article object with all the data extracted
        /// </returns>    
        public static Article ParseArticle(string uri)
        {
            Reader smartReader = new Reader(uri);

            Task<Stream> result = smartReader.GetStreamAsync(new Uri(uri));
            result.Wait();
            Stream stream = result.Result;

            HtmlParser parser = new HtmlParser();

            smartReader.doc = parser.Parse(stream);

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

		/**
		* Run any post-process modifications to article content as necessary.
		*
		* @param Element
		* @return void
		**/
		private void postProcessContent(IElement articleContent)
		{
			// Readability cannot open relative uris so we convert them to absolute uris.
			fixRelativeUris(articleContent);

            // Remove IDs and classes.
            cleanIDsAndClasses(articleContent);
        }

        /**
		 * Iterates over a NodeList, calls `filterFn` for each node and removes node
		 * if function returned `true`.
		 *
		 * If function is not passed, removes all the nodes in node list.
		 *
		 * @param NodeList nodeList The nodes to operate on
		 * @param Function filterFn the function to use as a filter
		 * @return void
		 */
        private void removeNodes(IHtmlCollection<IElement> nodeList, Func<IElement, bool> filterFn = null)
		{
			for (var i = nodeList.Count() - 1; i >= 0; i--)
			{
				var node = nodeList[i];
				var parentNode = node.Parent;
				if (parentNode != null)
				{
					if (filterFn == null || filterFn(node))
					{
						parentNode.RemoveChild(node);
					}
				}
			}
		}

		/**
		 * Iterates over a NodeList, and calls _setNodeTag for each node.
		 *
		 * @param NodeList nodeList The nodes to operate on
		 * @param String newTagName the new tag name to use
		 * @return void
		 */
		private void replaceNodeTags(IHtmlCollection<IElement> nodeList, string newTagName)
		{
			for (var i = nodeList.Count() - 1; i >= 0; i--)
			{
				var node = nodeList[i];
				setNodeTag(node, newTagName);
			}
		}

		/**
		* Iterate over a NodeList, which doesn't natively fully implement the Array
		* interface.
		*
		* For convenience, the current object context is applied to the provided
		* iterate function.
		*
		* @param  NodeList nodeList The NodeList.
		* @param  Function fn       The iterate function.
		* @return void
		*/
		private void forEachNode(IEnumerable<INode> nodeList, Action<INode> fn)
		{
			if (nodeList != null)
			{
				for (int a = 0; a < nodeList.Count(); a++)
				{
					fn(nodeList.ElementAt(a));
				}
			}
		}

		private void forEachNode(IEnumerable<INode> nodeList, Action<INode, int> fn, int level)
		{
			foreach (var node in nodeList)
				fn(node, level++);
		}
		/**
		 * Iterate over a NodeList, return true if any of the provided iterate
		 * function calls returns true, false otherwise.
		 *
		 * For convenience, the current object context is applied to the
		 * provided iterate function.
		 *
		 * @param  NodeList nodeList The NodeList.
		 * @param  Function fn       The iterate function.
		 * @return Boolean
		 */
		private bool someNode(IEnumerable<IElement> nodeList, Func<IElement, bool> fn)
		{
			if (nodeList != null)
				return nodeList.Any(fn);

			return false;
		}

		private bool someNode(INodeList nodeList, Func<INode, bool> fn)
		{
			if (nodeList != null)
				return nodeList.Any(fn);

			return false;			
		}

		/**
		 * Concat all nodelists passed as arguments.
		 *
		 * @return ...NodeList
		 * @return Array
		 */
		private IEnumerable<IElement> concatNodeLists(params IEnumerable<IElement>[] arguments)
		{
            List<IElement> result = new List<IElement>();

			foreach (var arg in arguments)
			{
				result = result.Concat(arg).ToList();
			}

			return result;
		}

		private IHtmlCollection<IElement> getAllNodesWithTag(IElement node, string[] tagNames)
		{
			return node.QuerySelectorAll(String.Join(",", tagNames));
			//return [].concat.apply([], tagNames.map(function(tag) {
			//    var collection = node.getElementsByTagName(tag);
			//    return Array.isArray(collection) ? collection : Array.from(collection);
			//}));
		}

        /**
        * Removes the id="" and class="" attribute from every element in the given
        * subtree, except those that match IDS_TO_PRESERVE, CLASSES_TO_PRESERVE and
        * the classesToPreserve array from the options object.
        *
        * @param Element
        * @return void
        */
        private void cleanIDsAndClasses(IElement node)
        {
            if (!this.idsToPreserve.Contains(node.Id))
            {
                node.RemoveAttribute("id");
            }

            var classesToPreserve = this.classesToPreserve;
            var className = "";

            if (!String.IsNullOrEmpty(node.GetAttribute("class")))
                className = String.Join(" ", node.GetAttribute("class").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(x => classesToPreserve.Contains(x)));

            if (!String.IsNullOrEmpty(className))
            {
                node.SetAttribute("class", className);
            }
            else
            {
                node.RemoveAttribute("class");
            }

            for (node = node.FirstElementChild; node != null; node = node.NextElementSibling)
            {
                cleanIDsAndClasses(node);
            }
        }

		/**
		 * Converts each <a> and <img> uri in the given element to an absolute URI,
		 * ignoring #ref URIs.
		 *
		 * @param Element
		 * @return void
		 */

		private string getBase(Uri startUri)
		{
			StringBuilder sb = new StringBuilder(startUri.Scheme + ":");

			if (!String.IsNullOrEmpty(startUri.UserInfo))
				sb.Append(startUri.UserInfo + "@");

			sb.Append(startUri.Host);

			if (startUri.Port != 80)
				sb.Append(":" + startUri.Port);

			return sb.ToString();
		}

		private string toAbsoluteURI(string uriToCheck)
		{
			var scheme = uri.Scheme;
			var prePath = getBase(uri);
			var pathBase = uri.Scheme + "://" + uri.Host + uri.AbsolutePath.Substring(0, uri.AbsolutePath.LastIndexOf('/') + 1);

			// If this is already an absolute URI, return it.
			if (Uri.IsWellFormedUriString(uriToCheck, UriKind.Absolute))
				return uriToCheck;

			// Ignore hash URIs
			if (uriToCheck[0] == '#')
				return uriToCheck;

			// Scheme-rooted relative URI.
			if (uriToCheck.Length >= 2 && uriToCheck.Substring(0, 2) == "//")
				return scheme + "://" + uriToCheck.Substring(2);

			// Prepath-rooted relative URI.
			if (uriToCheck[0] == '/')
				return prePath + uri;

			// Dotslash relative URI.
			if (uriToCheck.IndexOf("./") == 0)
				return pathBase + uriToCheck.Substring(2);

			// Standard relative URI; add entire path. pathBase already includes a
			// trailing "/".
			return pathBase + uri;
		}

		private void fixRelativeUris(IElement articleContent)
		{
			var scheme = uri.Scheme;
			var prePath = getBase(uri);
			var pathBase = uri.Scheme + "://" + uri.Host + uri.AbsolutePath.Substring(0, uri.AbsolutePath.LastIndexOf('/') + 1); ;

			var links = articleContent.GetElementsByTagName("a");

			forEachNode(links, (link) =>
			{
				var href = (link as IElement).GetAttribute("href");
				if (!String.IsNullOrWhiteSpace(href))
				{
					// Replace links with javascript: URIs with text content, since
					// they won't work after scripts have been removed from the page.
					if (href.IndexOf("javascript:") == 0)
					{
						var text = this.doc.CreateTextNode(link.TextContent);
						link.Parent.ReplaceChild(text, link);
					}
					else
					{
						(link as IElement).SetAttribute("href", toAbsoluteURI(href));
					}
				}
			});

			var imgs = articleContent.GetElementsByTagName("img");
			forEachNode(imgs, (img) =>
			{
				var src = (img as IElement).GetAttribute("src");
				if (!String.IsNullOrWhiteSpace(src))
				{
					(img as IElement).SetAttribute("src", toAbsoluteURI(src));
				}
			});
		}

		/**
		* Get the article title as an H1.
		*
		* @return string
		**/
		private string getArticleTitle()
		{
			var curTitle = "";
			var origTitle = "";

			try
			{
				curTitle = origTitle = doc.Title;

				// If they had an element with id "title" in their HTML
				//if (typeof curTitle !== "string")
				//    curTitle = origTitle = this._getInnerText(doc.getElementsByTagName('title')[0]);
			}
			catch (Exception e) {/* ignore exceptions setting the title. */}
            
            var titleHadHierarchicalSeparators = false;
            int wordCount(String str)
            {
                return Regex.Split(str, @"\s+").Length;
            }

            // If there's a separator in the title, first remove the final part
            if (curTitle.IndexOfAny(new char[] { '|', '-', '»', '/', '>' }) != -1)
            {
                titleHadHierarchicalSeparators = curTitle.IndexOfAny(new char[] { '|', '-', '»', '/', '>' }) != 1;                
                curTitle = Regex.Replace(origTitle, @"(.*)[\|\-\\\/>»].*", "$1", RegexOptions.IgnoreCase);

                // If the resulting title is too short (3 words or fewer), remove
                // the first part instead:
                if (wordCount(curTitle) < 3)
                    curTitle = Regex.Replace(origTitle, @"[^\|\-\\\/>»] *[\|\-\\\/>»](.*)", "$1", RegexOptions.IgnoreCase);
			}
			else if (curTitle.IndexOf(": ") != -1)
			{
				// Check if we have an heading containing this exact string, so we
				// could assume it's the full title.
				var headings = concatNodeLists(
				  doc.GetElementsByTagName("h1"),
				  doc.GetElementsByTagName("h2")
				);
                var match = someNode(headings, (heading) =>
				{
					return heading.TextContent == curTitle;
				});

				// If we don't, let's extract the title out of the original title string.
				if (!match)
				{
					curTitle = origTitle.Substring(origTitle.LastIndexOf(':') + 1);

                    // If the title is now too short, try the first colon instead:
                    if (wordCount(curTitle) < 3)
                        curTitle = origTitle.Substring(origTitle.IndexOf(':') + 1);
				}
			}
			else if (curTitle.Length > 150 || curTitle.Length < 15)
			{
				var hOnes = doc.GetElementsByTagName("h1");

				if (hOnes.Length == 1)
					curTitle = getInnerText(hOnes[0]);
			}

			curTitle = curTitle.Trim();

            // If we now have 4 words or fewer as our title, and either no
            // 'hierarchical' separators (\, /, > or ») were found in the original
            // title or we decreased the number of words by more than 1 word, use
            // the original title.

            var curTitleWordCount = wordCount(curTitle);
            if (curTitleWordCount <= 4 && (
                !titleHadHierarchicalSeparators ||
                curTitleWordCount != wordCount(Regex.Replace(origTitle, @"[\|\-\\\/>» ] +", " ", RegexOptions.IgnoreCase)) - 1)) {
                curTitle = origTitle;
            }
            
            return curTitle;
		}

		/**
		 * Prepare the HTML document for readability to scrape it.
		 * This includes things like stripping javascript, CSS, and handling terrible markup.
		 *
		 * @return void
		 **/
		private void prepDocument()
		{
			// Remove all style tags in head
			removeNodes(doc.GetElementsByTagName("style"), null);

			if (doc.Body != null)
			{
				replaceBrs(doc.Body);
			}

			replaceNodeTags(doc.GetElementsByTagName("font"), "SPAN");
		}

		/**
		 * Finds the next element, starting from the given node, and ignoring
		 * whitespace in between. If the given node is an element, the same node is
		 * returned.
		 */
		private IElement nextElement(INode node)
		{
			var next = node;
			while (next != null
			    && (next.NodeType != NodeType.Element)
			    && regExps["whitespace"].IsMatch(next.TextContent))
			{
				next = next.NextSibling;
			}
			return next as IElement;
		}

		/**
		 * Replaces 2 or more successive <br> elements with a single <p>.
		 * Whitespace between <br> elements are ignored. For example:
		 *   <div>foo<br>bar<br> <br><br>abc</div>
		 * will become:
		 *   <div>foo<br>bar<p>abc</p></div>
		 */
		private void replaceBrs(IElement elem)
		{
			forEachNode(getAllNodesWithTag(elem, new string[] { "br" }), (br) =>
			{
				var next = br.NextSibling;

				// Whether 2 or more <br> elements have been found and replaced with a
				// <p> block.
				var replaced = false;

				// If we find a <br> chain, remove the <br>s until we hit another element
				// or non-whitespace. This leaves behind the first <br> in the chain
				// (which will be replaced with a <p> later).
				while ((next = nextElement(next)) != null && ((next as IElement).TagName == "BR"))
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
							var nextElem = nextElement(next);
							if (nextElem != null && (nextElem as IElement).TagName == "BR")
								break;
						}

						// Otherwise, make this node a child of the new <p>.
						var sibling = next.NextSibling;
						p.AppendChild(next);
						next = sibling;
					}
				}
			});
		}

		private IElement setNodeTag(IElement node, string tag)
		{
			var replacement = node.Owner.CreateElement(tag);
			while (node.FirstChild != null)
			{
				replacement.AppendChild(node.FirstChild);
			}
			node.Parent.ReplaceChild(replacement, node);
			//if (node.readability)
			//    replacement.readability = node.readability;

			for (var i = 0; i < node.Attributes.Length; i++)
			{
                // the possible result of malformed HTML
                if (!node.Attributes[i].Name.Contains("<") && !node.Attributes[i].Name.Contains(">"))
                    replacement.SetAttribute(node.Attributes[i].Name, node.Attributes[i].Value);
			}
			return replacement;
		}

		/**
		 * Prepare the article node for display. Clean out any inline styles,
		 * iframes, forms, strip extraneous <p> tags, etc.
		 *
		 * @param Element
		 * @return void
		 **/
		private void prepArticle(IElement articleContent)
		{
			cleanStyles(articleContent);

			// Check for data tables before we continue, to avoid removing items in
			// those tables, which will often be isolated even though they're
			// visually linked to other content-ful elements (text, images, etc.).
			markDataTables(articleContent);

			cleanConditionally(articleContent, "form");
			cleanConditionally(articleContent, "fieldset");
			clean(articleContent, "object");
			clean(articleContent, "embed");
			clean(articleContent, "h1");
			clean(articleContent, "footer");

			// Clean out elements have "share" in their id/class combinations from final top candidates,
			// which means we don't remove the top candidates even they have "share".
			forEachNode(articleContent.Children, (topCandidate) =>
			{
				cleanMatchedNodes(topCandidate as IElement, "/ share /");
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
                        //clean(articleContent.DocumentElement, "h2");
                        clean(articleContent, "h2");
                    }
                }
            }

            clean(articleContent, "iframe");
			clean(articleContent, "input");
			clean(articleContent, "textarea");
			clean(articleContent, "select");
			clean(articleContent, "button");
			cleanHeaders(articleContent);

			// Do these last as the previous stuff may have removed junk
			// that will affect these
			cleanConditionally(articleContent, "table");
			cleanConditionally(articleContent, "ul");
			cleanConditionally(articleContent, "div");

			// Remove extra paragraphs
			removeNodes(articleContent.GetElementsByTagName("p"), (paragraph) =>
			{
				var imgCount = paragraph.GetElementsByTagName("img").Length;
				var embedCount = paragraph.GetElementsByTagName("embed").Length;
				var objectCount = paragraph.GetElementsByTagName("object").Length;
				// At this point, nasty iframes have been removed, only remain embedded video ones.
				var iframeCount = paragraph.GetElementsByTagName("iframe").Length;
				var totalCount = imgCount + embedCount + objectCount + iframeCount;

				return totalCount == 0 && String.IsNullOrEmpty(getInnerText(paragraph, false));
			});

			forEachNode(getAllNodesWithTag(articleContent, new string[] { "br" }), (br) =>
			{
				var next = nextElement(br.NextSibling);
				if (next != null && (next as IElement).TagName == "P")
					br.Parent.RemoveChild(br);
			});
		}

		/**
		 * Initialize a node with the readability object. Also checks the
		 * className/id for special names to add to its score.
		 *
		 * @param Element
		 * @return void
		**/
		private void initializeNode(IElement node)
		{
			//node.readability = { "contentScore": 0};
			//node.SetAttribute("contentScore", "0");
			readabilityScores.Add(node, 0);

			switch (node.TagName)
			{
				case "DIV":
					//node.readability.contentScore += 5;
					readabilityScores[node] += 5;
					break;

				case "PRE":
				case "TD":
				case "BLOCKQUOTE":
					//node.readability.contentScore += 3;
					readabilityScores[node] += 3;
					break;

				case "ADDRESS":
				case "OL":
				case "UL":
				case "DL":
				case "DD":
				case "DT":
				case "LI":
				case "FORM":
					//node.readability.contentScore -= 3;
					readabilityScores[node] -= 3;
					break;

				case "H1":
				case "H2":
				case "H3":
				case "H4":
				case "H5":
				case "H6":
				case "TH":
					//node.readability.contentScore -= 5;
					readabilityScores[node] -= 5;
					break;
			}

			//node.readability.contentScore += this._getClassWeight(node);
			readabilityScores[node] += getClassWeight(node);
		}

		private INode removeAndGetNext(INode node)
		{
			var nextNode = getNextNode(node as IElement, true);
			node.Parent.RemoveChild(node);
			return nextNode;
		}

		/**
		 * Traverse the DOM from node to node, starting at the node passed in.
		 * Pass true for the second parameter to indicate this node itself
		 * (and its kids) are going away, and we want the next node over.
		 *
		 * Calling this in a loop will traverse the DOM depth-first.
		 */
		private IElement getNextNode(IElement node, bool ignoreSelfAndKids = false)
		{
			// First check for kids if those aren't being ignored
			if (!ignoreSelfAndKids && node.FirstElementChild != null)
			{
				return node.FirstElementChild;
			}
			// Then for siblings...
			if (node.NextElementSibling != null)
			{
				return node.NextElementSibling;
			}
			// And finally, move up the parent chain *and* find a sibling
			// (because this is depth-first traversal, we will have already
			// seen the parent nodes themselves).
			do
			{
				node = node.ParentElement;
			} while (node != null && node.NextElementSibling == null);

			//return node && node.NextElementSibling;
			//return node.NextSibling as IElement;
			return node?.NextElementSibling;
		}

		private bool checkByline(IElement node, string matchString)
		{
			if (!String.IsNullOrEmpty(articleByline))
			{
				return false;
			}

			String rel = "";

			if (node is IElement && !String.IsNullOrEmpty(node.GetAttribute("rel")))
			{
				rel = node.GetAttribute("rel");
			}

			if ((rel == "author" || regExps["byline"].IsMatch(matchString)) && isValidByline(node.TextContent))
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

        private IEnumerable<IElement> getElementAncestors(IElement node, int maxDepth = 0)
        {
            var i = 0;
            List<IElement> ancestors = new List<IElement>();
            while (node.ParentElement != null)
            {
                ancestors.Add(node.ParentElement);
                if (maxDepth != 0 && ++i == maxDepth)
                    break;
                node = node.ParentElement;
            }
            return ancestors;
        }

        private IEnumerable<INode> getNodeAncestors(INode node, int maxDepth = 0)
		{
			var i = 0;
			List<INode> ancestors = new List<INode>();
			while (node.Parent != null)
			{
				ancestors.Add(node.Parent);
				if (maxDepth != 0 && ++i == maxDepth)
					break;
				node = node.Parent;
			}
			return ancestors;
		}

		/***
		 * grabArticle - Using a variety of metrics (content score, classname, element types), find the content that    is
		 *         most likely to be the stuff a user wants to read. Then return it wrapped up in a div.
		 *
		 * @param page a document to run upon. Needs to be a full document, complete with body.
		 * @return Element
		**/
		private IElement grabArticle(IElement page = null)
		{
			//this.log("**** grabArticle ****");
			var doc = this.doc;
			var isPaging = (page != null ? true : false);
			page = page != null ? page : this.doc.Body;

			// We can't grab an article if we don't have a page!
			if (page == null)
			{
				//this.log("No body found in document. Abort.");
				return null;
			}

			var pageCacheHtml = page.InnerHtml;

			while (true)
			{
				var stripUnlikelyCandidates = flagIsActive(Flags.StripUnlikelys);

				// First, node prepping. Trash nodes that look cruddy (like ones with the
				// class name "comment", etc), and turn divs into P tags where they have been
				// used inappropriately (as in, where they contain no other block level elements.)
				//var elementsToScore = [];
				List<IElement> elementsToScore = new List<IElement>();
				var node = this.doc.DocumentElement;

				while (node != null)
				{
					var matchString = node.ClassName + " " + node.Id;

					// Check to see if this node is a byline, and remove it if it is.
					if (checkByline(node, matchString))
					{
						node = removeAndGetNext(node) as IElement;
						continue;
					}

					// Remove unlikely candidates
					if (stripUnlikelyCandidates)
					{
						if (regExps["unlikelyCandidates"].IsMatch(matchString) &&
						    !regExps["okMaybeItsACandidate"].IsMatch(matchString) &&
						    node.TagName != "BODY" &&
						    node.TagName != "A")
						{
							//this.log("Removing unlikely candidate - " + matchString);
							node = removeAndGetNext(node) as IElement;
							continue;
						}
					}

                   // Remove DIV, SECTION, and HEADER nodes without any content(e.g. text, image, video, or iframe).

                    if ((node.TagName == "DIV" || node.TagName == "SECTION" || node.TagName == "HEADER" ||
					     node.TagName == "H1" || node.TagName == "H2" || node.TagName == "H3" ||
					     node.TagName == "H4" || node.TagName == "H5" || node.TagName == "H6") &&
					    isElementWithoutContent(node))
					{
						node = removeAndGetNext(node) as IElement;
						continue;
					}

					if (TagsToScore.ToList().IndexOf(node.TagName) != -1)
					{
						elementsToScore.Add(node);
					}

					// Turn all divs that don't have children block level elements into p's
					if (node.TagName == "DIV")
					{
						// Sites like http://mobile.slate.com encloses each paragraph with a DIV
						// element. DIVs with only a P element inside and no text content can be
						// safely converted into plain P elements to avoid confusing the scoring
						// algorithm with DIVs with are, in practice, paragraphs.
						if (hasSinglePInsideElement(node))
						{
							var newNode = node.Children[0];
							node.Parent.ReplaceChild(newNode, node);
							node = newNode;
                            elementsToScore.Add(node);
                        }
                        else if (!hasChildBlockElement(node))
						{
							node = setNodeTag(node, "P");
							elementsToScore.Add(node);
						}
						else
						{
							// EXPERIMENTAL
							forEachNode(node.ChildNodes, (childNode) =>
							{
								if (childNode.NodeType == NodeType.Text && childNode.TextContent.Trim().Length > 0)
								{
									var p = doc.CreateElement("p");
									p.TextContent = childNode.TextContent;
									p.SetAttribute("style", "display: inline;");
									p.ClassName = "readability-styled";
									node.ReplaceChild(p, childNode);
								}
							});
						}
					}
					node = getNextNode(node);
				}

				/**
				 * Loop through all paragraphs, and assign a score to them based on how content-y they look.
				 * Then add their score to their parent node.
				 *
				 * A score is determined by things like number of commas, class names, etc. Maybe eventually link density.
				**/
				//var candidates = [];                
				List<IElement> candidates = new List<IElement>();
				forEachNode(elementsToScore, (elementToScore) =>
				{
					//if (!elementToScore.parentNode || typeof(elementToScore.parentNode.tagName) === 'undefined')
					if (elementToScore.Parent == null || elementToScore.Parent.NodeType != NodeType.Element)
						return;

					// If this paragraph is less than 25 characters, don't even count it.
					string innerText = getInnerText(elementToScore as IElement);
					if (innerText.Length < 25)
						return;

					// Exclude nodes with no ancestor.
					var ancestors = getNodeAncestors(elementToScore, 3);
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
					forEachNode(ancestors, (ancestor, level) =>
					{
						if (String.IsNullOrEmpty((ancestor as IElement)?.TagName))
							return;

						//if (typeof(ancestor.readability) === 'undefined')
						if (!readabilityScores.ContainsKey(ancestor as IElement))
						{
							initializeNode(ancestor as IElement);
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
						readabilityScores[ancestor as IElement] += contentScore / scoreDivider;
					}, 0);
				});

				// After we've calculated scores, loop through all of the possible
				// candidate nodes we found and find the one with the highest score.
				//var topCandidates = [];
				List<IElement> topCandidates = new List<IElement>();
				for (int c = 0, cl = candidates?.Count ?? 0; c < cl; c += 1)
				{
					var candidate = candidates[c];

					// Scale the final candidates score based on link density. Good content
					// should have a relatively small link density (5% or less) and be mostly
					// unaffected by this operation.
					var candidateScore = readabilityScores[candidate] * (1 - getLinkDensity(candidate));
					readabilityScores[candidate] = candidateScore;

					//this.log('Candidate:', candidate, "with score " + candidateScore);

					for (var t = 0; t < NTopCandidates; t++)
					{
						IElement aTopCandidate = null;
						if (t < topCandidates.Count)
							aTopCandidate = topCandidates[t];

						if (aTopCandidate == null || candidateScore > readabilityScores[aTopCandidate])
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
						//this.log("Moving child out:", kids[0]);
						topCandidate.AppendChild(kids[0]);
					}

					page.AppendChild(topCandidate);

					initializeNode(topCandidate);
				}
				else if (topCandidate != null)
				{
					// Find a better top candidate node if it contains (at least three) nodes which belong to `topCandidates` array
					// and whose scores are quite closed with current `topCandidate` node.
					//var alternativeCandidateAncestors = [];                    
					List<IElement> alternativeCandidateAncestors = new List<IElement>();
					for (var i = 1; i < topCandidates.Count; i++)
					{
						if (readabilityScores[topCandidates[i]] / readabilityScores[topCandidate] >= 0.75)
						{
							//alternativeCandidateAncestors.Add(getNodeAncestors(topCandidates[i]) as IElement);
							var possibleAncestor = getNodeAncestors(topCandidates[i]) as IElement;
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
					if (!readabilityScores.ContainsKey(topCandidate))
					{
						initializeNode(topCandidate);
					}

					// Because of our bonus system, parents of candidates might have scores
					// themselves. They get half of the node. There won't be nodes with higher
					// scores than our topCandidate, but if we see the score going *up* in the first
					// few steps up the tree, that's a decent sign that there might be more content
					// lurking in other places that we want to unify in. The sibling stuff
					// below does some of that - but only if we've looked high enough up the DOM
					// tree.
					parentOfTopCandidate = topCandidate.Parent as IElement;
					var lastScore = readabilityScores[topCandidate];
					// The scores shouldn't get too low.
					var scoreThreshold = lastScore / 3;
					while (parentOfTopCandidate.TagName != "BODY")
					{
						if (!readabilityScores.ContainsKey(parentOfTopCandidate))
						{
							parentOfTopCandidate = parentOfTopCandidate.Parent as IElement;
							continue;
						}
						var parentScore = readabilityScores[parentOfTopCandidate];
						if (parentScore < scoreThreshold)
							break;
						if (parentScore > lastScore)
						{
							// Alright! We found a better parent to use.
							topCandidate = parentOfTopCandidate;
							break;
						}
						lastScore = readabilityScores[parentOfTopCandidate];
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
					if (!readabilityScores.ContainsKey(topCandidate))
					{
						initializeNode(topCandidate);
					}
				}

				// Now that we have the top candidate, look through its siblings for content
				// that might also be related. Things like preambles, content split by ads
				// that we removed, etc.
				var articleContent = doc.CreateElement("DIV");
				if (isPaging)
					articleContent.Id = "readability-content";

				var siblingScoreThreshold = Math.Max(10, readabilityScores[topCandidate] * 0.2);
				// Keep potential top candidate's parent node to try to get text direction of it later.
				parentOfTopCandidate = topCandidate.ParentElement;
				var siblings = parentOfTopCandidate.Children;

				for (int s = 0, sl = siblings.Length; s < sl; s++)
				{
					var sibling = siblings[s];
					var append = false;

					//this.log("Looking at sibling node:", sibling, sibling.readability ? ("with score " + sibling.readability.contentScore) : '');
					//this.log("Sibling has score", sibling.readability ? sibling.readability.contentScore : 'Unknown');

					if (sibling == topCandidate)
					{
						append = true;
					}
					else
					{
						double contentBonus = 0;

						// Give a bonus if sibling nodes and top candidates have the example same classname
						if (sibling.ClassName == topCandidate.ClassName && topCandidate.ClassName != "")
							contentBonus += readabilityScores[topCandidate] * 0.2;

						if (readabilityScores.ContainsKey(sibling) &&
						    ((readabilityScores[sibling] + contentBonus) >= siblingScoreThreshold))
						{
							append = true;
						}
						else if (sibling.NodeName == "P")
						{
							var linkDensity = getLinkDensity(sibling);
							var nodeContent = getInnerText(sibling);
							var nodeLength = nodeContent.Length;

							if (nodeLength > 80 && linkDensity < 0.25)
							{
								append = true;
							}
							else if (nodeLength < 80 && nodeLength > 0 && linkDensity == 0 &&
								 new Regex(@"\.( |$)", RegexOptions.IgnoreCase).IsMatch(nodeContent))
							{
								append = true;
							}
						}
					}

					if (append)
					{
						//this.log("Appending node:", sibling);

						if (AlterToDivExceptions.ToList().IndexOf(sibling.NodeName) == -1)
						{
							// We have a node that isn't a common block level element, like a form or td tag.
							// Turn it into a div so it doesn't get filtered out later by accident.
							//this.log("Altering sibling:", sibling, 'to div.');

							sibling = setNodeTag(sibling, "DIV");
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

				if (Debug)
					Logger.WriteLine("<h2>Article content pre-prep:</h2>" + articleContent.InnerHtml);

				// So we have all of the content that we need. Now we clean it up for presentation.
				prepArticle(articleContent);

				if (Debug)
					Logger.WriteLine("<h2>Article content post-prep:</h2>" + articleContent.InnerHtml);


                //if (curPageNum == 1)
				//{
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
				//}

				if (Debug)
					Logger.WriteLine("<h2>Article content after paging:</h2>" + articleContent.InnerHtml);

				// Now that we've gone through the full algorithm, check to see if
				// we got any meaningful content. If we didn't, we may need to re-run
				// grabArticle with different flags set. This gives us a higher likelihood of
				// finding the content, and the sieve approach gives us a higher likelihood of
				// finding the -right- content.
				if (getInnerText(articleContent, true).Length < WordThreshold)
				{
					page.InnerHtml = pageCacheHtml;

					if (flagIsActive(Flags.StripUnlikelys))
					{
						removeFlag(Flags.StripUnlikelys);
					}
					else if (flagIsActive(Flags.WeightClasses))
					{
						removeFlag(Flags.WeightClasses);
					}
					else if (flagIsActive(Flags.CleanConditionally))
					{
						removeFlag(Flags.CleanConditionally);
					}
					else
					{
						return null;
					}
				}
				//else if (!String.IsNullOrEmpty(doc.Direction))
				//{
				//	articleDir = doc.Direction;
                //
				//	return articleContent;
				//}
				else
				{
					// Find out text direction from ancestors of final top candidate.
					IEnumerable<IElement> ancestors = new IElement[] { parentOfTopCandidate, topCandidate }.Concat(getElementAncestors(parentOfTopCandidate)) as IEnumerable<IElement>;                    
					someNode(ancestors, (ancestor) =>
					{
						if (String.IsNullOrEmpty(ancestor.TagName))
							return false;
						var articleDir = ancestor.GetAttribute("dir");
						if (!String.IsNullOrEmpty(articleDir))
						{
							this.articleDir = articleDir;
							return true;
						}
						return false;
					});

					return articleContent;
				}
			}
		}

		/**
		 * Check whether the input string could be a byline.
		 * This verifies that the input is a string, and that the length
		 * is less than 100 chars.
		 *
		 * @param possibleByline {string} - a string to check whether its a byline.
		 * @return Boolean - whether the input string is a byline.
		 */
		private bool isValidByline(string byline)
		{
			if (!String.IsNullOrEmpty(byline))
			{
				byline = byline.Trim();
				return (byline.Length > 0) && (byline.Length < 100);
			}
			return false;
		}

		/**
		 * Attempts to get excerpt and byline metadata for the article.
		 *
		 * @return Object with optional "excerpt" and "byline" properties
		 */
		private Metadata getArticleMetadata()
		{
			//dynamic metadata = new ExpandoObject();            
			Metadata metadata = new Metadata();
			Dictionary<string, string> values = new Dictionary<string, string>();
			var metaElements = doc.GetElementsByTagName("meta");

			// Match "description", or Twitter's "twitter:description" (Cards)
			// in name attribute.
			var namePattern = @"^\s*((((twitter)\s*:\s*)?(description|title))|name)\s*$";

			// Match Facebook's Open Graph title & description properties.
			var propertyPattern = @"^\s*(og|article)\s*:\s*(description|title|published_time)\s*$";

            var itemPropPattern = @"\s*datePublished\s*";

            // Find description tags.
            forEachNode(metaElements, (element) =>
			{
				var elementName = (element as IElement).GetAttribute("name") ?? "";
				var elementProperty = (element as IElement).GetAttribute("property") ?? "";
                var itemProp = (element as IElement).GetAttribute("itemprop") ?? "";

                if (new string[] { elementName, elementProperty, itemProp }.ToList().IndexOf("author") != -1)
				{
					metadata.Byline = (element as IElement).GetAttribute("content");
					metadata.Author = (element as IElement).GetAttribute("content");
					return;
				}
                
                String name = "";
				if (Regex.IsMatch(elementName, namePattern, RegexOptions.IgnoreCase))
				{
					name = elementName;
				}
				else if (Regex.IsMatch(elementProperty, propertyPattern, RegexOptions.IgnoreCase))
				{
					name = elementProperty;
				}
                else if (Regex.IsMatch(itemProp, itemPropPattern, RegexOptions.IgnoreCase))
                {
                    name = itemProp;
                }

                if (!String.IsNullOrEmpty(name))
				{
					var content = (element as IElement).GetAttribute("content");
					if (!String.IsNullOrEmpty(content))
					{
						// Convert to lowercase and remove any whitespace
						// so we can match below.
						name = Regex.Replace(name.ToLower(), @"\s", "", RegexOptions.IgnoreCase);
						if (!values.ContainsKey(name))
							values.Add(name, content.Trim());
					}
				}
			});

            //Logger.WriteLine("Meta Values");
            //foreach (var value in values)
            //{
            //	Logger.WriteLine($"Key: {value.Key} Value: {value.Value}");
            //}

            if (values.ContainsKey("description"))
			{
				metadata.Excerpt = values["description"];
			}
			else if (values.ContainsKey("og:description"))
			{
				// Use facebook open graph description.
				metadata.Excerpt = values["og:description"];
			}
			else if (values.ContainsKey("twitter:description"))
			{
				// Use twitter cards description.
				metadata.Excerpt = values["twitter:description"];
			}

			metadata.Title = getArticleTitle();
			if (String.IsNullOrEmpty(metadata.Title))
			{
				if (values.ContainsKey("og:title"))
				{
					// Use facebook open graph title.
					metadata.Title = values["og:title"];
				}
				else if (values.ContainsKey("twitter:title"))
				{
					// Use twitter cards title.
					metadata.Title = values["twitter:title"];
				}
			}

			// added language extraction
			if (String.IsNullOrEmpty(language))
			{
				metadata.Language = doc.GetElementsByTagName("html")[0].GetAttribute("lang");

				if (String.IsNullOrEmpty(metadata.Language))
				{
					metadata.Language = doc.QuerySelector("meta[http-equiv=\"Content-Language\"]")?.GetAttribute("content");
					if (String.IsNullOrEmpty(metadata.Language))
					{
						// this is wrong, but it's used
						metadata.Language = doc.QuerySelector("meta[name=\"lang\"]")?.GetAttribute("value");

						if (String.IsNullOrEmpty(metadata.Language))
							metadata.Language = "";
					}
				}
			}

			// added date extraction
			DateTime date;

			if (values.ContainsKey("article:published_time")
			    && DateTime.TryParse(values["article:published_time"], out date))
			{
				metadata.PublicationDate = date;
			}
			else if (values.ContainsKey("date")
			    && DateTime.TryParse(values["date"], out date))
			{
				metadata.PublicationDate = date;
			}
            else if (values.ContainsKey("datepublished")
                && DateTime.TryParse(values["datepublished"], out date))
            {
                metadata.PublicationDate = date;
            }
            else
			{
				var times = doc.GetElementsByTagName("time");

				foreach (var time in times)
				{
					if (!String.IsNullOrEmpty(time.GetAttribute("pubDate"))
					    && DateTime.TryParse(time.GetAttribute("pubDate"), out date))
					{
						metadata.PublicationDate = date;
					}
				}
			}

			if (metadata.PublicationDate == null)
			{
				// as a last resort check the URL for a data
				Match maybeDate = Regex.Match(uri.PathAndQuery, "/(?<year>[0-9]{4})/(?<month>[0-9]{2})/(?<day>[0-9]{2})?");
				if (maybeDate.Success)
				{
					//metadata.PublicationDate = DateTime.Parse(maybeDate.Value);                
					metadata.PublicationDate = new DateTime(int.Parse(maybeDate.Groups["year"].Value),
					    int.Parse(maybeDate.Groups["month"].Value),
					    !String.IsNullOrEmpty(maybeDate.Groups["day"].Value) ? int.Parse(maybeDate.Groups["day"].Value) : 1);
				}
			}

			return metadata;
		}

		/**
		 * Removes script tags from the document.
		 *
		 * @param Element
		**/
		private void removeScripts(IElement doc)
		{
			removeNodes(doc.GetElementsByTagName("script"), (scriptNode) =>
			{
				scriptNode.NodeValue = "";
				scriptNode.RemoveAttribute("src");
				return true;
			});
			removeNodes(doc.GetElementsByTagName("noscript"));
		}

		/**
		 * Check if this node has only whitespace and a single P element
		 * Returns false if the DIV node contains non-empty text nodes
		 * or if it contains no P or more than 1 element.
		 *
		 * @param Element
		**/
		private bool hasSinglePInsideElement(IElement element)
		{
			// There should be exactly 1 element child which is a P:
			if (element.Children.Length != 1 || element.Children[0].TagName != "P")
			{
				return false;
			}

			// And there should be no text nodes with real content
			return !someNode(element.ChildNodes, (node) =>
			{
				return node.NodeType == NodeType.Text &&
				       regExps["hasContent"].IsMatch(node.TextContent);
			});
		}

		private bool isElementWithoutContent(IElement node)
		{
            return node.NodeType == NodeType.Element &&
                node.TextContent.Trim().Length == 0 &&
                (node.Children.Length == 0 ||
                  node.Children.Length == node.GetElementsByTagName("br").Length + node.GetElementsByTagName("hr").Length);
			  
		}

		/**
		 * Determine whether element has any children block level elements.
		 *
		 * @param Element
		 */
		private bool hasChildBlockElement(IElement element)
		{
			//return someNode(element.ChildNodes, (node) => {                
			var b = someNode(element?.ChildNodes, (node) =>
			{
				return DivToPElems.ToList().IndexOf((node as IElement)?.TagName) != -1
                || hasChildBlockElement(node as IElement);
			});
            var d = element?.TextContent;

            return b;
		}

		/**
		 * Get the inner text of a node - cross browser compatibly.
		 * This also strips out any excess whitespace to be found.
		 *
		 * @param Element
		 * @param Boolean normalizeSpaces (default: true)
		 * @return string
		**/
		private string getInnerText(IElement e, bool normalizeSpaces = true)
		{
			//normalizeSpaces = (typeof normalizeSpaces === 'undefined') ? true : normalizeSpaces;
			var textContent = e.TextContent.Trim();

			if (normalizeSpaces)
			{
				return regExps["normalize"].Replace(textContent, " ");
			}
			return textContent;
		}

		/**
		 * Get the number of times a string s appears in the node e.
		 *
		 * @param Element
		 * @param string - what to split on. Default is ","
		 * @return number (integer)
		**/
		private int getCharCount(IElement e, String s = ",")
		{
			return getInnerText(e).Split(s.ToCharArray()).Length - 1;
		}

		/**
		 * Remove the style attribute on every e and under.
		 * TODO: Test if getElementsByTagName(*) is faster.
		 *
		 * @param Element
		 * @return void
		**/
		private void cleanStyles(IElement e = null)
		{
			e = e ?? doc as IElement;			
            if (e == null || e.TagName.ToLower() == "svg")
                return;			

            if (e.ClassName != "readability-styled")
            {
                // Remove `style` and deprecated presentational attributes
                for (var i = 0; i < PresentationalAttributes.Length; i++)
                {
                    e.RemoveAttribute(PresentationalAttributes[i]);
                }

                if (DeprecatedSizeAttributeElems.FirstOrDefault(x => x == e.TagName) != null)
                {
                    e.RemoveAttribute("width");
                    e.RemoveAttribute("height");
                }
            }
			
            var cur = e.FirstElementChild;
            while (cur != null)
            {
                cleanStyles(cur as IElement);
                cur = cur.NextElementSibling;

            }
        }

        /**
         * Get the density of links as a percentage of the content
         * This is the amount of text that is inside a link divided by the totaltextinthenode.
         *
         * @param Element
         * @return number (float)
        **/
        private float getLinkDensity(IElement element)
		{
			var textLength = getInnerText(element).Length;
			if (textLength == 0)
				return 0;

			var linkLength = 0;

			// XXX implement _reduceNodeList?
			forEachNode(element.GetElementsByTagName("a"), (linkNode) =>
			{
				linkLength += getInnerText(linkNode as IElement).Length;
			});

			return linkLength / textLength;
		}				

		/**
		 * Get an elements class/id weight. Uses regular expressions to tell if this
		 * element looks good or bad.
		 *
		 * @param Element
		 * @return number (Integer)
		**/
		private int getClassWeight(IElement e)
		{
			if (!flagIsActive(Flags.WeightClasses))
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

		/**
		 * Clean a node of all elements of type "tag".
		 * (Unless it's a youtube/vimeo video. People love movies.)
		 *
		 * @param Element
		 * @param string tag to clean
		 * @return void
		 **/
		private void clean(IElement e, string tag)
		{
			var isEmbed = new List<string>() { "object", "embed", "iframe" }.IndexOf(tag) != -1;

			removeNodes(e.GetElementsByTagName(tag), (element) =>
			{
				// Allow youtube and vimeo videos through as people usually want to see those.
				if (isEmbed)
				{
					StringBuilder attributeValues = new StringBuilder();
					foreach (var attr in element.Attributes)
						attributeValues.Append(attr.Value + "|");

					// First, check the elements attributes to see if any of them contain youtube or vimeo
					if (regExps["videos"].IsMatch(attributeValues.ToString()))
						return false;

					// Then check the elements inside this element for the same.
					if (regExps["videos"].IsMatch(element.InnerHtml))
						return false;
				}

				return true;
			});
		}

		/**
		 * Check if a given node has one of its ancestor tag name matching the
		 * provided one.
		 * @param  HTMLElement node
		 * @param  String      tagName
		 * @param  Number      maxDepth
		 * @return Boolean
		 */
		private bool hasAncestorTag(IElement node, string tagName, int maxDepth = 3, Func<IElement, bool> filterFn = null)
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

		private bool isDataTable(IElement node)
		{
			return readabilityDataTable.Contains(node);
		}

		/**
		* Return an object indicating how many rows and columns this table has.
		*/

		private Tuple<int, int> getRowAndColumnCount(IElement table)
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

		/**
		 * Look for 'data' (as opposed to 'layout') tables, for which we use
		 * similar checks as
		 * https://dxr.mozilla.org/mozilla-central/rev/71224049c0b52ab190564d3ea0eab089a159a4cf/accessible/html/    HTMLTableAccessible.cpp#920
		 */
		private void markDataTables(IElement root)
		{
			var tables = root.GetElementsByTagName("table");
			for (var i = 0; i < tables.Length; i++)
			{
				var table = tables[i];
				var role = table.GetAttribute("role");
				if (role == "presentation")
				{
					//table._readabilityDataTable = false;
					continue;
				}
				var datatable = table.GetAttribute("datatable");
				if (datatable == "0")
				{
					//table._readabilityDataTable = false;
					continue;
				}
				var summary = table.GetAttribute("summary");
				if (!String.IsNullOrEmpty(summary))
				{
					readabilityDataTable.Add(table);
					continue;
				}

				var caption = table.GetElementsByTagName("caption")?.ElementAtOrDefault(0);
				if (caption != null && caption.ChildNodes.Length > 0)
				{
					readabilityDataTable.Add(table);
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
					if (Debug)
						Logger.WriteLine("Data table because found data-y descendant");
					//table._readabilityDataTable = true;
					readabilityDataTable.Add(table);
					continue;
				}

				// Nested tables indicate a layout table:
				if (table.GetElementsByTagName("table").ElementAtOrDefault(0) != null)
				{
					//table._readabilityDataTable = false;
					continue;
				}

				var sizeInfo = getRowAndColumnCount(table);
				if (sizeInfo.Item1 >= 10 || sizeInfo.Item2 > 4)
				{
					//table._readabilityDataTable = true;
					readabilityDataTable.Add(table);
					continue;
				}
				// Now just go by size entirely:
				if (sizeInfo.Item1 * sizeInfo.Item2 > 10)
					readabilityDataTable.Add(table);
			}
		}

		/**
		 * Clean an element of all tags of type "tag" if they look fishy.
		 * "Fishy" is an algorithm based on content length, classnames, link density, number of images & embeds, etc.
		 *
		 * @return void
		 **/
		private void cleanConditionally(IElement e, string tag)
		{
            if(tag == "div")
                { }

            if (!flagIsActive(Flags.CleanConditionally))
				return;

			var isList = tag == "ul" || tag == "ol";

			// Gather counts for other typical elements embedded within.
			// Traverse backwards so we can remove nodes at the same time
			// without effecting the traversal.
			//
			// TODO: Consider taking into account original contentScore here.
			removeNodes(e.GetElementsByTagName(tag), (node) =>
			{
				// First check if we're in a data table, in which case don't remove us.                
				if (hasAncestorTag(node, "table", -1, isDataTable))
				{
					return false;
				}

				var weight = getClassWeight(node);
				var contentScore = 0;

				//this.log("Cleaning Conditionally", node);

				if (weight + contentScore < 0)
				{
					return true;
				}
                
				if (getCharCount(node, ",") < 10)
				{
					// If there are not very many commas, and the number of
					// non-paragraph elements is more than paragraphs or other
					// ominous signs, remove the element.
					var p = node.GetElementsByTagName("p").Length;
					var img = node.GetElementsByTagName("img").Length;
					var li = node.GetElementsByTagName("li").Length - 100;
					var input = node.GetElementsByTagName("input").Length;

					var embedCount = 0;
					var embeds = node.GetElementsByTagName("embed");
					for (int ei = 0, il = embeds.Length; ei < il; ei += 1)
					{
						if (!regExps["videos"].IsMatch(embeds[ei].Attributes["Src"].Value))
							embedCount += 1;
					}

					var linkDensity = getLinkDensity(node);
					var contentLength = getInnerText(node).Length;

					var haveToRemove =
					  (img > 1 && p / img < 0.5 && !hasAncestorTag(node, "figure")) ||
					  (!isList && li > p) ||
					  (input > Math.Floor((double)p / 3)) ||
					  (!isList && contentLength < 25 && (img == 0 || img > 2) && !hasAncestorTag(node, "figure")) ||
					  (!isList && weight < 25 && linkDensity > 0.2) ||
					  (weight >= 25 && linkDensity > 0.5) ||
					  ((embedCount == 1 && contentLength < 75) || embedCount > 1);
					return haveToRemove;
				}
				return false;
			});
		}

		/**
		 * Clean out elements whose id/class combinations match specific string.
		 *
		 * @param Element
		 * @param RegExp match id/class combination.
		 * @return void
		 **/
		private void cleanMatchedNodes(IElement e, string regex)
		{
			var endOfSearchMarkerNode = getNextNode(e, true);
			var next = getNextNode(e);
			while (next != null && next != endOfSearchMarkerNode)
			{
				if (Regex.IsMatch(next.ClassName + " " + next.Id, regex))
				{
					next = removeAndGetNext(next as INode) as IElement;
				}
				else
				{
					next = getNextNode(next);
				}
			}
		}

		/**
		 * Clean out spurious headers from an Element. Checks things like classnames and link density.
		 *
		 * @param Element
		 * @return void
		**/
		private void cleanHeaders(IElement e)
		{
			for (var headerIndex = 1; headerIndex < 3; headerIndex += 1)
			{
				removeNodes(e.GetElementsByTagName("h" + headerIndex), (header) =>
				{
					return getClassWeight(header) < 0;
				});
			}
		}

		private bool flagIsActive(Flags flag)
		{
			return (flags & flag) > 0;
		}

		private void removeFlag(Flags flag)
		{
			flags = flags & ~flag;
		}

		private bool isVisible(IElement element)
		{
			if (element.Style?.Display != null && element.Style.Display == "none")
				return false;
			else
				return true;
		}

		private double getScore(IElement node, double score, Func<IElement, bool> helperIsVisible)
		{
			if (helperIsVisible != null && !helperIsVisible(node))
				return 0;
			var matchString = node.ClassName + " " + node.Id;

			if (regExps["unlikelyCandidates"].IsMatch(matchString) &&
			    !regExps["okMaybeItsACandidate"].IsMatch(matchString))
			{
				return 0;
			}

			if (node.Matches("li p"))
			{
				return 0;
			}

			var textContentLength = node.TextContent.Trim().Length;
			if (textContentLength < 140)
			{
				return 0;
			}

			score += Math.Sqrt(textContentLength - 140);

			return score;
		}

		/**
		 * Decides whether or not the document is reader-able without parsing the whole thing.
		 *
		 * @return boolean Whether or not we suspect parse() will suceeed at returning an article object.
		 */
		private bool isProbablyReaderable(Func<IElement, bool> helperIsVisible = null)
		{
			var nodes = getAllNodesWithTag(doc.DocumentElement, new string[] { "p", "pre" });

			// Get <div> nodes which have <br> node(s) and append them into the `nodes` variable.
			// Some articles' DOM structures might look like
			// <div>
			//   Sentences<br>
			//   <br>
			//   Sentences<br>
			// </div>
			var brNodes = getAllNodesWithTag(doc.DocumentElement, new string[] { "div > br" });
			if (brNodes.Length > 0)
			{
				var set = new HashSet<INode>();

				foreach (var node in brNodes)
				{
					set.Add(node.Parent);
				}

				nodes = nodes.Concat(set.ToArray()) as IHtmlCollection<IElement>;
			}

			// FIXME we should have a fallback for helperIsVisible, but this is
			// problematic because of jsdom's elem.style handling - see
			// https://github.com/mozilla/readability/pull/186 for context.

			double score = 0;
			// This is a little cheeky, we use the accumulator 'score' to decide what to return from
			// this callback:			
			return someNode(nodes, (node) =>
			{				
				score += getScore(node, score, helperIsVisible);
				
				if (score > 20)
					return true;
				else
					return false;
			});
		}

		/**
		 * Runs readability.
		 *
		 * Workflow:
		 *  1. Prep the document by removing script tags, css, etc.
		 *  2. Build readability's DOM tree.
		 *  3. Grab the article content from the current dom tree.
		 *  4. Replace the current DOM tree with the new one.
		 *  5. Read peacefully.
		 *
		 * @return void
		 **/
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

			//if (typeof this._doc.documentElement.firstElementChild === "undefined") {
			//    this._getNextNode = this._getNextNodeNoElementProperties;
			//}
			// Remove script tags from the document.
			//removeScripts(doc as IElement);
			removeScripts(doc.DocumentElement);

			prepDocument();

            var isReadable = isProbablyReaderable(isVisible);

            // we stop only if it's not readable and we are not debugging
            if (isReadable == false)
			{
				if (Debug == true)
				{
					Logger.WriteLine("<h2>Warning: article probably not readable</h2>");
				}
				else if (ContinueIfNotReadable == false)
					return new Article(uri, articleTitle, false);
			}

			var metadata = getArticleMetadata();
			articleTitle = metadata.Title;

			var articleContent = grabArticle();
			if (articleContent == null)
				return new Article(uri, articleTitle, false);

			if (Debug)
				Logger.WriteLine("<h2>Grabbed:</h2>" + articleContent.InnerHtml);

			postProcessContent(articleContent);

			if (Debug)
				Logger.WriteLine("<h2>Post Process result:</h2>" + articleContent.InnerHtml);

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

			//var textContent = articleContent.TextContent;

			Article article;

			article = new Article(uri, articleTitle, articleByline, articleDir, language, author, articleContent, metadata, isReadable);

			return article;
		}

		private async Task<Stream> GetStreamAsync(Uri resource)
		{
			var response = await httpClient.GetAsync(resource).ConfigureAwait(false);
			Stream dati = null;

			if (response.IsSuccessStatusCode)
			{
				//if (response.Headers?.ETag != null)
				//	pageETags.Add(response.Headers.ETag.Tag);

				var headLan = response.Headers.FirstOrDefault(x => x.Key.ToLower() == "content-language");
				if (headLan.Value != null && headLan.Value.Count() > 0)
					language = headLan.Value.ElementAt(0);

				var headCont = response.Headers.FirstOrDefault(x => x.Key.ToLower() == "content-type");
				if (headCont.Value != null && headCont.Value.Count() > 0)
				{
					int index = headCont.Value.ElementAt(0).IndexOf("charset=");
					if (index != -1)
						charset = headCont.Value.ElementAt(0).Substring(index + 8);
				}

				dati = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
			}

			return dati;
		}

		private static async Task<HttpResponseMessage> RequestPageAsync(Uri resource)
		{
			return await httpClient.GetAsync(resource).ConfigureAwait(false);
		}
	}
}