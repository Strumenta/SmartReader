using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AngleSharp.Html;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using AngleSharp.Css.Dom;
using AngleSharp.Css.Parser;
using AngleSharp;
using AngleSharp.Html.Dom;
using System.Text;

namespace SmartReader
{
    internal static class NodeUtility
    {
        /**
        * Iterates over a NodeList, and calls _setNodeTag for each node.
        *
        * @param NodeList nodeList The nodes to operate on
        * @param String newTagName the new tag name to use
        * @return void
        */

        // All of the regular expressions in use within readability.
        // Defined up here so we don't instantiate them repeatedly in loops.
        private static Dictionary<string, Regex> regExps = new Dictionary<string, Regex>() {       
        { "byline", new Regex(@"byline|author|dateline|writtenby|p-author", RegexOptions.IgnoreCase) },
        { "replaceFonts", new Regex(@"<(\/?)font[^>]*>", RegexOptions.IgnoreCase) },
        { "normalize", new Regex(@"\s{2,}", RegexOptions.IgnoreCase) },
        { "nextLink", new Regex(@"(next|weiter|continue|>([^\|]|$)|»([^\|]|$))", RegexOptions.IgnoreCase) },
        { "prevLink", new Regex(@"(prev|earl|old|new|<|«)", RegexOptions.IgnoreCase) },
        { "whitespace", new Regex(@"^\s*$", RegexOptions.IgnoreCase) },
        { "hasContent", new Regex(@"\S$", RegexOptions.IgnoreCase) }
        };

        private static String[] divToPElems = { "A", "BLOCKQUOTE", "DL", "DIV", "IMG", "OL", "P", "PRE", "TABLE", "UL", "SELECT" };
        
        private static String[] presentationalAttributes = { "align", "background", "bgcolor", "border", "cellpadding", "cellspacing", "frame", "hspace", "rules", "style", "valign", "vspace" };

        private static String[] deprecatedSizeAttributeElems = { "TABLE", "TH", "TD", "HR", "PRE" };

        // The commented out elements qualify as phrasing content but tend to be
        // removed by readability when put into paragraphs, so we ignore them here.
        private static String[] phrasingElems = {
          // "CANVAS", "IFRAME", "SVG", "VIDEO",
            "ABBR", "AUDIO", "B", "BDO", "BR", "BUTTON", "CITE", "CODE", "DATA",
            "DATALIST", "DFN", "EM", "EMBED", "I", "IMG", "INPUT", "KBD", "LABEL",
            "MARK", "MATH", "METER", "NOSCRIPT", "OBJECT", "OUTPUT", "PROGRESS", "Q",
            "RUBY", "SAMP", "SCRIPT", "SELECT", "SMALL", "SPAN", "STRONG", "SUB",
            "SUP", "TEXTAREA", "TIME", "VAR", "WBR"
        };

        public static void ReplaceNodeTags(IHtmlCollection<IElement> nodeList, string newTagName)
        {
            for (var i = nodeList.Count() - 1; i >= 0; i--)
            {
                var node = nodeList[i];
                SetNodeTag(node, newTagName);
            }
        }

        public static IElement SetNodeTag(IElement node, string tag)
        {
            var replacement = node.Owner.CreateElement(tag);
            while (node.FirstChild != null)
            {
                replacement.AppendChild(node.FirstChild);
            }
            node.Parent.ReplaceChild(replacement, node);

            for (var i = 0; i < node.Attributes.Length; i++)
            {
                // the possible result of malformed HTML
                if (!node.Attributes[i].Name.Contains("<") && !node.Attributes[i].Name.Contains(">"))
                    replacement.SetAttribute(node.Attributes[i].Name, node.Attributes[i].Value);
            }
            return replacement;
        }

        public static bool IsVisible(IElement element)
        {
            if (element.GetStyle()?.GetDisplay() != null && element.GetStyle()?.GetDisplay() == "none")
                return false;
            else
                return true;
        }

        public static bool IsProbablyVisible(IElement node)
        {
            // Have to null-check node.style and node.className.indexOf to deal with SVG and MathML nodes.
            return (node.GetStyle() == null || node?.GetStyle()?.GetDisplay() != "none")
                && !node.HasAttribute("hidden")
                // check for "fallback-image" so that wikimedia math images are displayed
                && (!node.HasAttribute("aria-hidden") || node.GetAttribute("aria-hidden") != "true" || (node?.ClassName != null && node.ClassName.IndexOf("fallback-image") != -1));                    
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
        public static void RemoveNodes(IHtmlCollection<IElement> nodeList, Func<IElement, bool> filterFn = null)
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
        public static void ForEachNode(IEnumerable<INode> nodeList, Action<INode> fn)
        {
            if (nodeList != null)
            {
                for (int a = 0; a < nodeList.Count(); a++)
                {
                    fn(nodeList.ElementAt(a));
                }
            }
        }

        public static void ForEachNode(IEnumerable<INode> nodeList, Action<INode, int> fn, int level)
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
        public static bool SomeNode(IEnumerable<IElement> nodeList, Func<IElement, bool> fn)
        {
            if (nodeList != null)
                return nodeList.Any(fn);

            return false;
        }

        public static bool SomeNode(INodeList nodeList, Func<INode, bool> fn)
        {
            if (nodeList != null)
                return nodeList.Any(fn);

            return false;
        }

        /**
        * Iterate over a NodeList, return true if all of the provided iterate
        * function calls return true, false otherwise.
        *
        * For convenience, the current object context is applied to the
        * provided iterate function.
        *
        * @param  NodeList nodeList The NodeList.
        * @param  Function fn       The iterate function.
        * @return Boolean
        */
        public static bool EveryNode(INodeList nodeList, Func<INode, bool> fn)
        {
            if (nodeList != null)
                return nodeList.All(fn);

            return false;
        }

        /**
		 * Concat all nodelists passed as arguments.
		 *
		 * @return ...NodeList
		 * @return Array
		 */
        public static IEnumerable<IElement> ConcatNodeLists(params IEnumerable<IElement>[] arguments)
        {
            List<IElement> result = new List<IElement>();

            foreach (var arg in arguments)
            {
                result = result.Concat(arg).ToList();
            }

            return result;
        }

        public static IHtmlCollection<IElement> GetAllNodesWithTag(IElement node, string[] tagNames)
        {
            return node.QuerySelectorAll(String.Join(",", tagNames));
        }

        /**
		 * Removes script tags from the document.
		 *
		 * @param Element
		**/
        public static void RemoveScripts(IElement element)
        {
            NodeUtility.RemoveNodes(element.GetElementsByTagName("script"), (scriptNode) =>
            {
                scriptNode.NodeValue = "";
                scriptNode.RemoveAttribute("src");
                return true;
            });
            NodeUtility.RemoveNodes(element.GetElementsByTagName("noscript"));
        }

        /**
		 * Check if this node has only whitespace and a single element with given tag
		 * Returns false if the DIV node contains non-empty text nodes
		 * or if it contains no element with given tag or more than 1 element.
		 *
		 * @param Element
         * @param string tag of child element
		**/
        public static bool HasSingleTagInsideElement(IElement element, string tag)
        {
            // There should be exactly 1 element child with given tag:
            if (element.Children.Length != 1 || element.Children[0].TagName != tag)
            {
                return false;
            }

            // And there should be no text nodes with real content
            return !NodeUtility.SomeNode(element.ChildNodes, (node) =>
            {
                return node.NodeType == NodeType.Text &&
                       regExps["hasContent"].IsMatch(node.TextContent);
            });
        }

        public static bool IsElementWithoutContent(IElement node)
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
        public static bool HasChildBlockElement(IElement element)
        {
            var b = NodeUtility.SomeNode(element?.ChildNodes, (node) =>
            {
                return divToPElems.ToList().IndexOf((node as IElement)?.TagName) != -1
                || HasChildBlockElement(node as IElement);
            });
            var d = element?.TextContent;

            return b;
        }

        /***
        * Determine if a node qualifies as phrasing content.
        * https://developer.mozilla.org/en-US/docs/Web/Guide/HTML/Content_categories#Phrasing_content
        **/
        public static bool IsPhrasingContent(INode node)
        {
            return node.NodeType == NodeType.Text || Array.IndexOf(phrasingElems, node.NodeName) != -1 ||
              ((node.NodeName == "A" || node.NodeName == "DEL" || node.NodeName == "INS") &&
                NodeUtility.EveryNode(node.ChildNodes, IsPhrasingContent));
        }

        public static bool IsWhitespace(INode node)
        {
            return (node.NodeType == NodeType.Text && node.TextContent.Trim().Length == 0) ||
                   (node.NodeType == NodeType.Element && node.NodeName == "BR");
        }

        /**
		 * Get the inner text of a node - cross browser compatibly.
		 * This also strips out any excess whitespace to be found.
		 *
		 * @param Element
		 * @param Boolean normalizeSpaces (default: true)
		 * @return string
		**/
        public static string GetInnerText(IElement e, bool normalizeSpaces = true)
        {
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
        public static int GetCharCount(IElement e, String s = ",")
        {
            return GetInnerText(e).Split(s.ToCharArray()).Length - 1;
        }

        /**
		 * Remove the style attribute on every e and under.
		 * TODO: Test if getElementsByTagName(*) is faster.
		 *
		 * @param Element
		 * @return void
		**/
        public static void CleanStyles(IElement e = null)
        {            
            if (e == null || e.TagName.ToLower() == "svg")
                return;

            // Remove `style` and deprecated presentational attributes
            for (var i = 0; i < presentationalAttributes.Length; i++)
            {
                e.RemoveAttribute(presentationalAttributes[i]);
            }

            if (deprecatedSizeAttributeElems.FirstOrDefault(x => x == e.TagName) != null)
            {
                e.RemoveAttribute("width");
                e.RemoveAttribute("height");
            }

            var cur = e.FirstElementChild;
            while (cur != null)
            {
                CleanStyles(cur as IElement);
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
        public static float GetLinkDensity(IElement element)
        {
            var textLength = NodeUtility.GetInnerText(element).Length;
            if (textLength == 0)
                return 0;

            float linkLength = 0;

            // XXX implement _reduceNodeList?
            NodeUtility.ForEachNode(element.GetElementsByTagName("a"), (linkNode) =>
            {
                linkLength += NodeUtility.GetInnerText(linkNode as IElement).Length;
            });

            return linkLength / textLength;
        }

        public static INode RemoveAndGetNext(INode node)
        {
            var nextNode = GetNextNode(node as IElement, true);
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
        public static IElement GetNextNode(IElement node, bool ignoreSelfAndKids = false)
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

            return node?.NextElementSibling;
        }

        /**
		 * Clean out elements that match the specified conditions
         * 
		 *
		 * @param Element
		 * @param RegExp match id/class combination.
		 * @return void
		 **/
        public static void CleanMatchedNodes(IElement e, Func<IElement, string, bool> filter = null)
        {
            var endOfSearchMarkerNode = NodeUtility.GetNextNode(e, true);
            var next = NodeUtility.GetNextNode(e);
            while (next != null && next != endOfSearchMarkerNode)
            {
                if (filter(next, next.ClassName + " " + next.Id))
                {
                    next = NodeUtility.RemoveAndGetNext(next as INode) as IElement;
                }
                else
                {
                    next = NodeUtility.GetNextNode(next);
                }
            }
        }

        public static bool IsDataTable(IElement node)
        {
            return !String.IsNullOrEmpty(node.GetAttribute("datatable")) ? node.GetAttribute("datatable").Contains("true") : false;
        }

        /**
		* Return an object indicating how many rows and columns this table has.
		*/
        public static Tuple<int, int> GetRowAndColumnCount(IElement table)
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

        public static IEnumerable<IElement> GetElementAncestors(IElement node, int maxDepth = 0)
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

        public static IEnumerable<INode> GetNodeAncestors(INode node, int maxDepth = 0)
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
    }    
}
