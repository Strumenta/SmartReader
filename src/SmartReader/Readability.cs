using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Html;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using AngleSharp.Css.Dom;
using AngleSharp.Css.Parser;
using AngleSharp;
using AngleSharp.Html.Dom;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("SmartReaderTests")]
namespace SmartReader
{
    /// <summary>
    /// <para>This class contains the heuristics and utility functions used to make an article readable.</para>
    /// <para>Put in a separate class to allow easier testing</para>
    /// </summary>
    internal static class Readability
    {
        private static Dictionary<string, Regex> regExps = new Dictionary<string, Regex>() {        
        { "normalize", new Regex(@"\s{2,}", RegexOptions.IgnoreCase) }
        };

        /// <summary>
        /// Removes the class attribute from every element in the given
        /// subtree, except those that match the classesToPreserve array
        /// </summary>
        /// <param name="node">The root node from which all classes must be removed.</param>
        /// <param name="classesToPreserve">The classes to preserve.</param>
        internal static void CleanClasses(IElement node, string[] classesToPreserve)
        {
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
                CleanClasses(node, classesToPreserve);
            }
        }

        /// <summary>
        /// Converts each &lt;a&gt; and &lt;img&gt; uri in the given element, and its descendants, to an absolute URI,
        /// ignoring #ref URIs.
        /// </summary>
        /// <param name="articleContent">The node in which to fix all relative uri</param>   
        /// <param name="uri">The base uri</param>  
        /// <param name="doc">The document to operate on</param>  
        internal static void FixRelativeUris(IElement articleContent, Uri uri, IHtmlDocument doc)
        {
            var scheme = uri.Scheme;
            var prePath = uri.GetBase();
            var pathBase = uri.Scheme + "://" + uri.Host + uri.AbsolutePath.Substring(0, uri.AbsolutePath.LastIndexOf('/') + 1);

            var links = NodeUtility.GetAllNodesWithTag(articleContent, new string[] { "a" });

            NodeUtility.ForEachNode(links, (link) =>
            {
                var href = (link as IElement).GetAttribute("href");
                if (!String.IsNullOrWhiteSpace(href))
                {
                    // Remove links with javascript: URIs, since
                    // they won't work after scripts have been removed from the page.
                    if (href.IndexOf("javascript:") == 0)
                    {
                        // if the link only contains simple text content, it can be converted to a text node
                        if (link.ChildNodes.Length == 1 && link.ChildNodes[0].NodeType == NodeType.Text)
                        {
                            var text = doc.CreateTextNode(link.TextContent);
                            link.Parent.ReplaceChild(text, link);
                        }
                        else
                        {
                            // if the link has multiple children, they should all be preserved
                            var container = doc.CreateElement("span");
                            while (link.ChildNodes.Length > 0)
                            {
                                container.AppendChild(link.ChildNodes[0]);
                            }
                            link.Parent.ReplaceChild(container, link);
                        }
                    }
                    else
                    {
                        (link as IElement).SetAttribute("href", uri.ToAbsoluteURI(href));
                    }
                }
            });

            var imgs = NodeUtility.GetAllNodesWithTag(articleContent, new string[] { "img" });
            NodeUtility.ForEachNode(imgs, (img) =>
            {
                var src = (img as IElement).GetAttribute("src");
                if (!String.IsNullOrWhiteSpace(src))
                {
                    (img as IElement).SetAttribute("src", uri.ToAbsoluteURI(src));
                }
            });
        }

        /// <summary>
        /// Clean the article title found in a tag
        /// </summary>
        /// <param name="title">Starting title</param>        
        /// <param name="siteName">Name of the site</param>        
        /// <returns>
        /// The clean title
        /// </returns>
        internal static string CleanTitle(string title, string siteName)
        {
            // eliminate any text after a separator
            if (!String.IsNullOrEmpty(siteName) && title.IndexOfAny(new char[] { '|', '-', '»', '/', '>' }) != -1)
            {

                // we eliminate the text after the separator only if it is the site name
                title = Regex.Replace(title, $"(.*) [\\|\\-\\\\/>»] {siteName}.*", "$1", RegexOptions.IgnoreCase);
            }

            title = regExps["normalize"].Replace(title, " ");

            return title;
        }

        /// <summary>
        /// Get the article title
        /// </summary>
        /// <param name="doc">The document</param>        
        /// <returns>
        /// The clean title
        /// </returns>
        internal static string GetArticleTitle(IHtmlDocument doc)
        {
            var curTitle = "";
            var origTitle = "";

            try
            {
                curTitle = origTitle = doc.Title.Trim();

                // If they had an element with id "title" in their HTML
                if (typeof(string) != curTitle.GetType())
                    curTitle = origTitle = NodeUtility.GetInnerText(doc.GetElementsByTagName("title")[0]);
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
                titleHadHierarchicalSeparators = curTitle.IndexOfAny(new char[] { '\\', '»', '/', '>' }) != -1;
                curTitle = Regex.Replace(origTitle, @"(.*) [\|\-\\\/>»] .*", "$1", RegexOptions.IgnoreCase);

                // If the resulting title is too short (3 words or fewer), remove
                // the first part instead:
                if (wordCount(curTitle) < 3)
                    curTitle = Regex.Replace(origTitle, @"[^\|\-\\\/>»]* [\|\-\\\/>»](.*)", "$1", RegexOptions.IgnoreCase);
            }
            else if (curTitle.IndexOf(": ") != -1)
            {
                // Check if we have an heading containing this exact string, so we
                // could assume it's the full title.
                var headings = NodeUtility.ConcatNodeLists(
                  doc.GetElementsByTagName("h1"),
                  doc.GetElementsByTagName("h2")
                );
                var trimmedTitle = curTitle.Trim();
                var match = NodeUtility.SomeNode(headings, (heading) =>
                {
                    return heading.TextContent.Trim() == trimmedTitle;
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
                    curTitle = NodeUtility.GetInnerText(hOnes[0]);
            }

            curTitle = curTitle.Trim();

            // If we now have 4 words or fewer as our title, and either no
            // 'hierarchical' separators (\, /, > or ») were found in the original
            // title or we decreased the number of words by more than 1 word, use
            // the original title.
            var curTitleWordCount = wordCount(curTitle);
            if (curTitleWordCount <= 4 && (
                !titleHadHierarchicalSeparators ||
                curTitleWordCount != wordCount(Regex.Replace(origTitle, @"[\|\-\\\/>»: ]+", " ", RegexOptions.IgnoreCase)) - 1))
            {
                curTitle = origTitle;
            }

            return curTitle;
        }

        /// <summary>
        /// <para>Check whether the input string could be a byline.</para>
        /// <para>This verifies that the input is a string, and that the length
		/// is less than 100 chars.</para> 
        /// </summary>
        /// <param name="byline">a string to check whether its a byline</param>
        /// <returns>Whether the input string is a byline</returns>
        internal static bool IsValidByline(string byline)
        {
            if (!String.IsNullOrEmpty(byline))
            {
                byline = byline.Trim();
                return (byline.Length > 0) && (byline.Length < 100);
            }
            return false;
        }

        /// <summary>
        /// Attempts to get metadata for the article.
        /// </summary>
        /// <param name="doc">The document</param>
        /// <param name="uri">The uri, possibly used to check for a date</param>
        /// <param name="language">The language that was possibly found in the headers of the response</param>
        /// <returns>The metadata object with all the info found</returns>
        internal static Metadata GetArticleMetadata(IHtmlDocument doc, Uri uri, string language)
        {
            Metadata metadata = new Metadata();
            Dictionary<string, string> values = new Dictionary<string, string>();
            var metaElements = doc.GetElementsByTagName("meta");

            // Match "description", or Twitter's "twitter:description" (Cards)
            // in name attribute.
            // name is a single value
            var namePattern = @"^\s*((?:(dc|dcterm|og|twitter|weibo:(article|webpage))\s*[\.:]\s*)?(author|creator|description|title|image|site_name)|name)\s*$";

            // Match Facebook's Open Graph title & description properties.
            // property is a space-separated list of values
            var propertyPattern = @"\s*(dc|dcterm|og|twitter|article)\s*:\s*(author|creator|description|title|published_time|image|site_name)(\s+|$)";

            var itemPropPattern = @"\s*datePublished\s*";

            // Find description tags.
            NodeUtility.ForEachNode(metaElements, (element) =>
            {
                var elementName = (element as IElement).GetAttribute("name") ?? "";
                var elementProperty = (element as IElement).GetAttribute("property") ?? "";
                var itemProp = (element as IElement).GetAttribute("itemprop") ?? "";
                var content = (element as IElement).GetAttribute("content");

                // avoid issues with no meta tags
                if (String.IsNullOrEmpty(content))
                {
                    return;
                }
                MatchCollection matches = null;
                String name = "";

                if (new string[] { elementName, elementProperty, itemProp }.ToList().IndexOf("author") != -1)
                {
                    metadata.Byline = (element as IElement).GetAttribute("content");
                    metadata.Author = (element as IElement).GetAttribute("content");
                    return;
                }

                if (!String.IsNullOrEmpty(elementProperty))
                {
                    matches = Regex.Matches(elementProperty, propertyPattern);
                    if (matches.Count > 0)
                    {
                        for (int i = matches.Count - 1; i >= 0; i--)
                        {
                            // Convert to lowercase, and remove any whitespace
                            // so we can match below.
                            name = Regex.Replace(matches[i].Value.ToLower(), @"\s+", "");

                            // multiple authors
                            values[name] = content.Trim();
                        }
                    }
                }

                if ((matches == null || matches.Count == 0)
                  && !String.IsNullOrEmpty(elementName) && Regex.IsMatch(elementName, namePattern, RegexOptions.IgnoreCase))
                {
                    name = elementName;
                    if (!String.IsNullOrEmpty(content))
                    {
                        // Convert to lowercase, remove any whitespace, and convert dots
                        // to colons so we can match below.
                        name = Regex.Replace(Regex.Replace(name.ToLower(), @"\s+", ""), @"\.", ":");
                        values[name] = content.Trim();
                    }

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
                    content = (element as IElement).GetAttribute("content");
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

            // Find the the description of the article
            IEnumerable<string> DescriptionKeys()
            {
                yield return values.ContainsKey("description") ? values["description"] : null;
                yield return values.ContainsKey("dc:description") ? values["dc:description"] : null;
                yield return values.ContainsKey("dcterm:description") ? values["dcterm:description"] : null;
                yield return values.ContainsKey("og:description") ? values["og:description"] : null;
                yield return values.ContainsKey("weibo:article:description") ? values["weibo:article:description"] : null;
                yield return values.ContainsKey("weibo:webpage:description") ? values["weibo:webpage:description"] : null;
                yield return values.ContainsKey("twitter:description") ? values["twitter:description"] : null;
            }

            metadata.Excerpt = DescriptionKeys().FirstOrDefault(l => !String.IsNullOrEmpty(l)) ?? "";

            // Get the name of the site
            if (values.ContainsKey("og:site_name"))
                metadata.SiteName = values["og:site_name"];

            // Find the title of the article
            IEnumerable<string> TitleKeys()
            {
                yield return values.ContainsKey("dc:title") ? values["dc:title"] : null;
                yield return values.ContainsKey("dcterm:title") ? values["dcterm:title"] : null;
                yield return values.ContainsKey("og:title") ? values["og:title"] : null;
                yield return values.ContainsKey("weibo:article:title") ? values["weibo:article:title"] : null;
                yield return values.ContainsKey("weibo:webpage:title") ? values["weibo:webpage:title"] : null;
                yield return values.ContainsKey("twitter:title") ? values["twitter:title"] : null;
                yield return values.ContainsKey("title") ? values["title"] : null;
            }

            metadata.Title = TitleKeys().FirstOrDefault(l => !String.IsNullOrEmpty(l)) ?? "";

            // Let's try to eliminate the site name from the title
            metadata.Title = Readability.CleanTitle(metadata.Title, metadata.SiteName);

            // We did not find any title,
            // we try to get it from the title tag
            if (String.IsNullOrEmpty(metadata.Title))
                metadata.Title = Readability.GetArticleTitle(doc);

            // added language extraction            
            IEnumerable<string> LanguageHeuristics()
            {
                yield return language;
                yield return doc.GetElementsByTagName("html")[0].GetAttribute("lang");
                yield return doc.GetElementsByTagName("html")[0].GetAttribute("xml:lang");
                yield return doc.QuerySelector("meta[http-equiv=\"Content-Language\"]")?.GetAttribute("content");
                // this is wrong, but it's used
                yield return doc.QuerySelector("meta[name=\"lang\"]")?.GetAttribute("value");
            }

            metadata.Language = LanguageHeuristics().FirstOrDefault(l => !String.IsNullOrEmpty(l)) ?? "";

            // Find the featured image of the article
            IEnumerable<string> FeaturedImageKeys()
            {
                yield return values.ContainsKey("og:image") ? values["og:image"] : null;
                yield return values.ContainsKey("twitter:image") ? values["twitter:image"] : null;
                yield return values.ContainsKey("weibo:article:image") ? values["weibo:article:image"] : null;
                yield return values.ContainsKey("weibo:webpage:image") ? values["weibo:webpage:image"] : null;
            }

            metadata.FeaturedImage = FeaturedImageKeys().FirstOrDefault(l => !String.IsNullOrEmpty(l)) ?? "";

            if (String.IsNullOrEmpty(metadata.Author))
            {
                // We try to find a meta tag for the author.
                // Note that there is Open Grapg tag for an author,
                // but it usually contains a profile URL of the author.
                // So we do not use it
                IEnumerable<string> AuthorKeys()
                {
                    yield return values.ContainsKey("dc:creator") ? values["dc:creator"] : null;
                    yield return values.ContainsKey("dcterm:creator") ? values["dcterm:creator"] : null;
                    yield return values.ContainsKey("author") ? values["author"] : null;
                }

                metadata.Author = AuthorKeys().FirstOrDefault(l => !String.IsNullOrEmpty(l)) ?? "";
            }

            // added date extraction
            DateTime date;

            // added language extraction            
            IEnumerable<DateTime?> DateHeuristics()
            {
                yield return values.ContainsKey("article:published_time")
                    && DateTime.TryParse(values["article:published_time"], out date) ?
                    date : DateTime.MinValue;

                yield return values.ContainsKey("date")
                    && DateTime.TryParse(values["date"], out date) ?
                    date : DateTime.MinValue;

                yield return values.ContainsKey("datepublished")
                  && DateTime.TryParse(values["datepublished"], out date) ?
                  date : DateTime.MinValue;

                yield return values.ContainsKey("weibo:article:create_at")
                  && DateTime.TryParse(values["weibo:article:create_at"], out date) ?
                  date : DateTime.MinValue;

                yield return values.ContainsKey("weibo:webpage:create_at")
                  && DateTime.TryParse(values["weibo:webpage:create_at"], out date) ?
                  date : DateTime.MinValue;
            }

            metadata.PublicationDate = DateHeuristics().FirstOrDefault(d => d != DateTime.MinValue);

            if (metadata.PublicationDate == null)
            {
                var times = doc.GetElementsByTagName("time");

                Console.WriteLine($"times: {times.Length}");

                foreach (var time in times)
                {
                    if (!String.IsNullOrEmpty(time.GetAttribute("pubDate"))
                        && DateTime.TryParse(time.GetAttribute("datetime"), out date))
                    {
                        metadata.PublicationDate = date;
                    }
                }
            }

            if (metadata.PublicationDate == null)
            {
                // as a last resort check the URL for a date
                Match maybeDate = Regex.Match(uri.PathAndQuery, "/(?<year>[0-9]{4})/(?<month>[0-9]{2})/(?<day>[0-9]{2})?");
                if (maybeDate.Success)
                {
                    metadata.PublicationDate = new DateTime(int.Parse(maybeDate.Groups["year"].Value),
                        int.Parse(maybeDate.Groups["month"].Value),
                        !String.IsNullOrEmpty(maybeDate.Groups["day"].Value) ? int.Parse(maybeDate.Groups["day"].Value) : 1);
                }
            }

            return metadata;
        }
    }
}
