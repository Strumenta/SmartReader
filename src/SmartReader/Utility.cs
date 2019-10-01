using AngleSharp.Dom;
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
            return (node.GetStyle() == null || node?.GetStyle()?.GetDisplay() != "none") && !node.HasAttribute("hidden");
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
        * Removes the class="" attribute from every element in the given
        * subtree, except those that match the classesToPreserve array        
        *
        * @param Element
        * @return void
        */
        public static void CleanClasses(IElement node, string[] classesToPreserve)
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
    }
}
