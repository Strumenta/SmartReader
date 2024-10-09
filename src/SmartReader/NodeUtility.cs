using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Text;

namespace SmartReader
{
    internal static class NodeUtility
    {
        // All of the regular expressions in use within readability.
        // Defined up here so we don't instantiate them repeatedly in loops.
        private static readonly Regex RE_HasContent = new Regex(@"\S$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex RE_HashUrl    = new Regex(@"^#.+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly string[] divToPElems = { "BLOCKQUOTE", "DL", "DIV", "IMG", "OL", "P", "PRE", "TABLE", "UL" };

        private static readonly string[] presentationalAttributes = { "align", "background", "bgcolor", "border", "cellpadding", "cellspacing", "frame", "hspace", "rules", "style", "valign", "vspace" };

        private static readonly string[] deprecatedSizeAttributeElems = { "TABLE", "TH", "TD", "HR", "PRE" };

        // The commented out elements qualify as phrasing content but tend to be
        // removed by readability when put into paragraphs, so we ignore them here.
        private static readonly string[] phrasingElems = {
          // "CANVAS", "IFRAME", "SVG", "VIDEO",
            "ABBR", "AUDIO", "B", "BDO", "BR", "BUTTON", "CITE", "CODE", "DATA",
            "DATALIST", "DFN", "EM", "EMBED", "I", "IMG", "INPUT", "KBD", "LABEL",
            "MARK", "MATH", "METER", "NOSCRIPT", "OBJECT", "OUTPUT", "PROGRESS", "Q",
            "RUBY", "SAMP", "SCRIPT", "SELECT", "SMALL", "SPAN", "STRONG", "SUB",
            "SUP", "TEXTAREA", "TIME", "VAR", "WBR"
        };

        public static readonly string[] TextishTags = new [] { "SPAN", "LI", "TD" }.Concat(divToPElems).ToArray();

        internal static void ReplaceNodeTags(IHtmlCollection<IElement> nodeList, string newTagName)
        {
            foreach (var node in nodeList)
            {
                SetNodeTag(node, newTagName);
            }
        }

        /// <summary>
        /// Replaces the node with a new tag
        /// </summary>
        /// <param name="node">The node to operate on</param>
        /// <param name="tag">The new tag name to use</param>
        internal static IElement SetNodeTag(IElement node, string tag)
        {
            var replacement = node.Owner!.CreateElement(tag);
            while (node.FirstChild != null)
            {
                replacement.AppendChild(node.FirstChild);
            }
            node.Parent!.ReplaceChild(replacement, node);

            for (var i = 0; i < node.Attributes.Length; i++)
            {
                var attr = node.Attributes[i]!;

                NodeUtility.SafeSetAttribute(replacement, attr);                
            }
            return replacement;
        }

        internal static bool IsVisible(IElement element)
        {
            return !IsHidden(element);
        }

        internal static bool IsProbablyVisible(IElement node)
        {
            // Have to null-check node.style and node.className.indexOf to deal with SVG and MathML nodes.
            return !IsHidden(node)
                && !node.HasAttribute("hidden")
                // check for "fallback-image" so that wikimedia math images are displayed
                && (!node.HasAttribute("aria-hidden") || node.GetAttribute("aria-hidden") is not "true" || (node?.ClassName != null && node.ClassName.Contains("fallback-image")));
        }

        internal static bool IsHidden(IElement element)
        {
            return element.GetAttribute("style") is string style && (GetDisplayFromStyle(style).Equals("none".AsSpan(), StringComparison.Ordinal) || GetVisibilityFromStyle(style).Equals("hidden".AsSpan(), StringComparison.Ordinal) || GetVisibilityFromStyle(style).Equals("collapse".AsSpan(), StringComparison.Ordinal));
        }

        internal static ReadOnlySpan<char> GetVisibilityFromStyle(string style)
        {
            var visibility = style.Split(';')
                .Select(x => x.Split(':'))
                .Where(x => x.Length is 2)
                .Select(x => new { key = x[0].Trim(), value = x[1].Trim() })
                .Where(x => x.key == "visibility")
                .Select(x => x.value)
                .FirstOrDefault();

            return visibility is null ? null : visibility.AsSpan();
        }

        internal static ReadOnlySpan<char> GetDisplayFromStyle(string style)
        {
            int displayIndex = style.IndexOf("display:", StringComparison.OrdinalIgnoreCase);

            if (displayIndex > -1)
            {
                var value = style.AsSpan(displayIndex + 7).Trim();

                int colonIndex = value.IndexOf(':');

                if (colonIndex is -1)
                {
                    return null;
                }

                value = value.Slice(colonIndex + 1);

                int semicolonIndex = value.IndexOf(';');

                if (semicolonIndex > -1)
                {
                    value = value.Slice(0, semicolonIndex - colonIndex).Trim();
                }

                return value;
            }

            return null;
        }

        /// <summary>
        /// <para>Iterates over a NodeList, calls <c>filterFn</c> for each node and removes node
		/// if function returned<c>true</c>.</para>
		/// 
		/// <para>If function is not passed, removes all the nodes in node list.</para>
        /// </summary>
        /// <param name="elementList">The elements to operate on</param>
        /// <param name="filterFn">The filter that dictates which nodes to remove</param>
        internal static void RemoveNodes(IHtmlCollection<IElement> elementList, Func<IElement, bool>? filterFn = null)
        {
            for (var i = elementList.Length - 1; i >= 0; i--)
            {
                var node = elementList[i];
                var parentNode = node.Parent;
                if (parentNode != null)
                {
                    if (filterFn is null || filterFn(node))
                    {
                        parentNode.RemoveChild(node);
                    }
                }
            }
        }

        /// <summary>
        /// <para>Iterate over a NodeList, which doesn't natively fully implement the Array
		/// interface.</para>		
		/// <para>For convenience, the current object context is applied to the provided
		/// iterate function.</para>
        /// </summary>
        /// <param name="nodeList">The nodes to operate on</param>
        /// <param name="fn">The iterate function</param>
        /// <return>void</return>
        internal static void ForEachElement(IHtmlCollection<IElement> nodeList, Action<IElement> fn)
        {
            for (int a = 0; a < nodeList.Length; a++)
            {
                fn(nodeList[a]);
            }
        }

        internal static void ForEachNode(IEnumerable<INode> nodeList, Action<INode, int> fn, int level)
        {
            foreach (var node in nodeList)
                fn(node, level++);
        }

        /// <summary>        
        /// Concat all nodelists passed as arguments.
        /// </summary>
        /// <param name="arguments">The nodes to operate on</param>        
        /// <returns>List of concatenated elements</returns>
        internal static IList<IElement> ConcatNodeLists(params IHtmlCollection<IElement>[] arguments)
        {
            var result = new List<IElement>();

            foreach (var arg in arguments)
            {
                result.AddRange(arg);
            }

            return result;
        }

        internal static IHtmlCollection<IElement> GetAllNodesWithTag(IElement node, string[] tagNames)
        {
            return node.QuerySelectorAll(string.Join(",", tagNames));
        }

        /// <summary>
        /// Check if node is image, or if node contains exactly only one image
        /// whether as a direct child or as its descendants.
        /// </summary>        
        /// <param name="element">The element to operate on</param>  
        internal static bool IsSingleImage(IElement element)
        {
            while(element != null)
            {
                if (element.TagName is "IMG") return true;
                if (element.Children.Length != 1 || element.TextContent.AsSpan().Trim().Length != 0) return false;
                element = element.Children[0];
            }

            return false;
        }

        /// <summary>
        /// Find all &lt;noscript&gt; that are located after &lt;img&gt; nodes, and which contain
        /// only one single&lt;img&gt; element. Replace the first image from inside the
        /// &lt;noscript&gt; tag and remove the &lt;noscript&gt; tag. This improves the quality of the
        /// images we use on some sites (e.g.Medium)
        /// </summary>        
        /// <param name="doc">The document to operate on</param>        
        internal static void UnwrapNoscriptImages(IHtmlDocument doc)
        {
            // Find img without source or attributes that might contains image, and remove it.
            // This is done to prevent a placeholder img is replaced by img from noscript in next step.           
            var imgs = doc.GetElementsByTagName("img");
            ForEachElement(imgs, static img =>
            {
                for (var i = 0; i < img.Attributes.Length; i++)
                {
                    var attr = img.Attributes[i]!;

                    if (attr.Name is "src" or "srcset" or "data-src" or "data-srcset")
                    {
                        return;
                    }

                    if (Regex.IsMatch(attr.Value, @"\.(jpg|jpeg|png|webp)"))
                    {
                        return;
                    }
                }

                img.Parent!.RemoveChild(img);
            });

            // Next find noscript and try to extract its image
            var noscripts = doc.GetElementsByTagName("noscript");
            ForEachElement(noscripts, static noscript =>
            {
                // Parse content of noscript and make sure it only contains image
                var doc = (IHtmlDocument)noscript.GetRoot();

                var tmp = doc.CreateElement("div");
                tmp.InnerHtml = noscript.InnerHtml;

                if (!IsSingleImage(tmp))
                    return;

                // If noscript has previous sibling and it only contains image,
                // replace it with noscript content. However we also keep old
                // attributes that might contains image.
                if (noscript.PreviousElementSibling is IElement prevElement && IsSingleImage(prevElement))
                {
                    var prevImg = prevElement;
                    if (prevImg.TagName is not "IMG")
                    {
                        prevImg = prevElement.GetElementsByTagName("img")[0];
                    }

                    var newImg = tmp.GetElementsByTagName("img")[0];
                    for (var i = 0; i < prevImg.Attributes.Length; i++)
                    {
                        var attr = prevImg.Attributes[i]!;
                        if (attr.Value is "")
                        {
                            continue;
                        }

                        if (attr.Name is "src" or "srcset"
                        || Regex.IsMatch(attr.Value, @"\.(jpg|jpeg|png|webp)"))
                        {
                            if (string.Equals(newImg.GetAttribute(attr.Name), attr.Value, StringComparison.Ordinal))
                            {
                                continue;
                            }

                            var attrName = attr.Name;
                            if (newImg.HasAttribute(attrName))
                            {
                                attrName = "data-old-" + attrName;
                            }

                            newImg.SetAttribute(attrName, attr.Value);
                        }
                    }

                    noscript.Parent!.ReplaceChild(tmp.FirstElementChild!, prevElement);
                }
            });
        }

        /// <summary>
        /// Removes script tags from the element
        /// </summary>
        /// <param name="element">The element to operate on</param>
        internal static void RemoveScripts(IElement element)
        {
            RemoveNodes(GetAllNodesWithTag(element, new string[] { "script", "noscript" }));
        }

        /// <summary>
        /// Removes comments from the element
        /// </summary>
        /// <param name="element">The element to operate on</param>
        internal static void RemoveComments(IElement element)
        {
            element.Descendants<IComment>().ToList().ForEach(el => el.Remove());
        }            

        /// <summary>
        /// Check if this node has only whitespace and a single element with given tag
        /// Returns false if the DIV node contains non-empty text nodes
        /// or if it contains no element with given tag or more than 1 element.
        /// </summary>
        /// <param name="element">Element to operate on</param>
        /// <param name="tagName">Tag of the child element</param>
        /// <returns>bool</returns>
        internal static bool HasSingleTagInsideElement(IElement element, string tagName)
        {
            // There should be exactly 1 element child with given tag:
            if (element.Children.Length != 1 || !string.Equals(element.Children[0].TagName, tagName, StringComparison.Ordinal))
            {
                return false;
            }

            // And there should be no text nodes with real content

            foreach (var child in element.ChildNodes)
            {
                if (child.NodeType == NodeType.Text && RE_HasContent.IsMatch(child.TextContent))
                {
                    return false;
                }
            }

            return true;
        }

        internal static bool IsElementWithoutContent(IElement node)
        {
            return node.NodeType == NodeType.Element &&
                       node.TextContent.AsSpan().Trim().Length == 0 &&
                       (node.Children.Length == 0 ||
                        node.Children.Length == node.GetElementsByTagName("br").Length + node.GetElementsByTagName("hr").Length);
        }

        /// <summary>
        /// Determine whether element has any children block level elements.
        /// </summary>
        /// <param name="element">Element to operate on</param>
        /// <returns>bool</returns>
        internal static bool HasChildBlockElement(IElement? element)
        {
            if (element is null) return false;

            foreach (var child in element.ChildNodes)
            {
                if (child is Element el && (divToPElems.Contains(el.TagName) || HasChildBlockElement(el)))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determine if a node qualifies as phrasing content, which is defined at https://developer.mozilla.org/en-US/docs/Web/Guide/HTML/Content_categories#Phrasing_content
        /// </summary>
        /// <param name="node">Node to operate on</param>
        /// <returns>bool</returns>
        internal static bool IsPhrasingContent(INode node)
        {
            return node.NodeType == NodeType.Text || phrasingElems.Contains(node.NodeName) ||
              (node.NodeName is "A" or "DEL" or "INS" && node.ChildNodes.All(IsPhrasingContent));
        }

        internal static bool IsWhitespace(INode node)
        {
            return (node.NodeType == NodeType.Text && node.TextContent.AsSpan().Trim().Length == 0) ||
                   (node is { NodeType: NodeType.Element, NodeName: "BR" });
        }

        /// <summary>
        /// <para>Get the inner text of a node - cross browser compatibly.</para>
        /// <para>This also strips out any excess whitespace to be found.</para>
        /// </summary>
        /// <param name="node">Node to operate on</param>
        /// <param name="normalizeSpaces">Bool to set whether to normalize whitespace</param>
        /// <returns>String with the text of the node</returns>
        internal static string GetInnerText(INode node, bool normalizeSpaces = true)
        {
            var textContent = node.TextContent.Trim();

            return (normalizeSpaces)
                ? AngleSharp.Text.StringExtensions.Collapse(textContent)
                : textContent;
        }

        /// <summary>
        /// Get the number of times a string s appears in the node e.
        /// </summary>
        /// <param name="el">Element to operate on</param>
        /// <param name="s">The string to check</param>
        /// <returns>int</returns>
        internal static int GetCharCount(IElement el, char s = ',')
        {
            var text = GetInnerText(el).AsSpan();

            int count = 0;
            int index;

            while ((index = text.IndexOf(s)) > -1)
            {
                text = text.Slice(index + 1);

                count++;
            }

            return count;
        }

        /// <summary>
        /// <para>Remove the style attribute on every e and under.</para>
        /// <para>TODO: Test if getElementsByTagName(*) is faster.</para>
        /// </summary>
        /// <param name="el">Element to operate on</param>
        internal static void CleanStyles(IElement? el = null)
        {
            if (el is null || el.TagName.Equals("svg", StringComparison.OrdinalIgnoreCase))
                return;

            // Remove `style` and deprecated presentational attributes
            for (var i = 0; i < presentationalAttributes.Length; i++)
            {
                el.RemoveAttribute(presentationalAttributes[i]);
            }

            if (deprecatedSizeAttributeElems.Contains(el.TagName))
            {
                el.RemoveAttribute("width");
                el.RemoveAttribute("height");
            }

            var cur = el.FirstElementChild;
            while (cur != null)
            {
                CleanStyles(cur);
                cur = cur.NextElementSibling;

            }
        }

        /// <summary>
        /// <para>Get the density of links as a percentage of the content.</para>
        /// <para>This is the amount of text that is inside a link divided by the totaltextinthenode.</para>
        /// </summary>
        /// <param name="element">Element to operate on</param>        
        internal static double GetLinkDensity(IElement element)
        {
            var textLength = GetInnerText(element).Length;
            if (textLength == 0)
                return 0;

            double linkLength = 0;

            // XXX implement _reduceNodeList?
            foreach (var linkEl in element.GetElementsByTagName("a"))
            {
                var href = linkEl.GetAttribute("href");
                var coefficient = href is { Length: > 0 } && RE_HashUrl.IsMatch(href) ? 0.3 : 1;
                linkLength += GetInnerText(linkEl).Length * coefficient;
            }

            return linkLength / textLength;
        }

        internal static IElement? RemoveAndGetNext(IElement element)
        {
            var nextNode = GetNextNode(element, true);
            element.Parent!.RemoveChild(element);
            return nextNode;
        }

        /// <summary>
        /// <para>Traverse the DOM from node to node, starting at the node passed in.</para>
        /// <para>Pass true for the second parameter to indicate this node itself
		/// (and its kids) are going away, and we want the next node over.</para>
        /// <para>Calling this in a loop will traverse the DOM depth-first.</para>
        /// </summary>
        /// <param name="node">Node to operate on</param>  
        /// <param name="ignoreSelfAndKids">Whether to ignore this node and his kids</param>  
        /// <returns>The next node</returns>
        internal static IElement? GetNextNode(IElement? node, bool ignoreSelfAndKids = false)
        {
            // First check for kids if those aren't being ignored
            if (!ignoreSelfAndKids && node?.FirstElementChild != null)
            {
                return node.FirstElementChild;
            }
            // Then for siblings...
            if (node?.NextElementSibling != null)
            {
                return node?.NextElementSibling;
            }
            // And finally, move up the parent chain *and* find a sibling
            // (because this is depth-first traversal, we will have already
            // seen the parent nodes themselves).
            do
            {
                node = node?.ParentElement;
            } while (node != null && node.NextElementSibling is null);

            return node?.NextElementSibling;
        }

        /// <summary>
        /// Clean out elements that match the specified conditions
        /// </summary>
        /// <param name="e">Element to operate on</param>  
        /// <param name="filter">Filter function on match id/class combination</param> 
        internal static void CleanMatchedNodes(IElement e, Func<IElement, string, bool> filter)
        {
            var endOfSearchMarkerNode = GetNextNode(e, true);
            var next = GetNextNode(e);
            while (next != null && next != endOfSearchMarkerNode)
            {
                if (filter(next, next.ClassName + " " + next.Id))
                {
                    next = RemoveAndGetNext(next);
                }
                else
                {
                    next = GetNextNode(next);
                }
            }
        }

        internal static bool IsDataTable(IElement node)
        {
            return node.GetAttribute("datatable") is { Length: > 0 } datatable && datatable.Contains("true");
        }

        /// <summary>
        /// Return an object indicating how many rows and columns this table has.
        /// </summary>
        /// <param name="table">The table element</param>
        /// <returns>A tuple with the numbers of rows and columns</returns>
        internal static Tuple<int, int> GetRowAndColumnCount(IElement table)
        {
            var rows = 0;
            var columns = 0;
            var trs = table.GetElementsByTagName("tr");
            for (var i = 0; i < trs.Length; i++)
            {
                string rowspan = trs[i].GetAttribute("rowspan") ?? "";
                int rowSpanInt = 0;
                if (!string.IsNullOrEmpty(rowspan))
                {
                    int.TryParse(rowspan, out rowSpanInt);
                }
                rows += rowSpanInt == 0 ? 1 : rowSpanInt;
                // Now look for column-related info
                var columnsInThisRow = 0;
                var cells = trs[i].GetElementsByTagName("td");
                for (var j = 0; j < cells.Length; j++)
                {
                    int colSpanInt = 0;
                    if (cells[j].GetAttribute("colspan") is { Length: > 0 } colspan)
                    {
                        int.TryParse(colspan, NumberStyles.None, CultureInfo.InvariantCulture, out colSpanInt);
                    }
                    columnsInThisRow += colSpanInt == 0 ? 1 : colSpanInt;
                }
                columns = Math.Max(columns, columnsInThisRow);
            }
            return Tuple.Create(rows, columns);
        }

        internal static List<IElement> GetElementAncestors(IElement node, int maxDepth = 0)
        {
            var i = 0;
            var ancestors = new List<IElement>();
            while (node.ParentElement != null)
            {
                ancestors.Add(node.ParentElement);
                if (maxDepth != 0 && ++i == maxDepth)
                    break;
                node = node.ParentElement;
            }
            return ancestors;
        }

        internal static List<INode> GetNodeAncestors(INode node, int maxDepth = 0)
        {
            var i = 0;
            var ancestors = new List<INode>();
            while (node?.Parent != null)
            {
                ancestors.Add(node.Parent);
                if (maxDepth != 0 && ++i == maxDepth)
                    break;
                node = node.Parent;
            }
            return ancestors;
        }

        /// <summary>
        /// Finds the next element, starting from the given node, and ignoring
        /// whitespace in between. If the given node is an element, the same node is
        /// returned.
        /// </summary>  
        internal static IElement? NextElement(INode? node, Regex whitespace)
        {
            var next = node;
            while (next != null
                && (next.NodeType != NodeType.Element)
                && whitespace.IsMatch(next.TextContent))
            {
                next = next.NextSibling;
            }
            return next as IElement;
        }

        /// <summary>
        /// This is a safe method to write attributes
        /// because AngleSharp cannot handle attribute names that would be invalid in XML
        /// </summary>  
        internal static void SafeSetAttribute(this IElement child, IAttr attribute)
        {
            if (attribute.Name.IsXmlName())
                child.SetAttribute(attribute.Name, attribute.Value);
            else
            {
                if(!String.IsNullOrEmpty(attribute.Name.CleanXmlName()))
                    child.SetAttribute(attribute.Name.CleanXmlName(), attribute.Value);
            }                
        }
    }
}
