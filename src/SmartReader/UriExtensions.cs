using System;
using System.Collections.Generic;
using System.Text;

namespace SmartReader
{
   internal static class UriExtensions
    {
        internal static string GetBase(this Uri startUri)
        {
            StringBuilder sb = new StringBuilder(startUri.Scheme + "://");

            if (!String.IsNullOrEmpty(startUri.UserInfo))
                sb.Append(startUri.UserInfo + "@");

            sb.Append(startUri.Host);

            if (startUri.Port != 80 && startUri.Port != 443)
                sb.Append(":" + startUri.Port);

            return sb.ToString();
        }

        internal static string GetPathBase(this Uri startUri)
        {
            return GetBase(startUri) + startUri.AbsolutePath.Substring(0, startUri.AbsolutePath.LastIndexOf("/") + 1);
        }

        internal static string ToAbsoluteURI(this Uri pageUri, string uriToCheck)
        {
            var scheme = pageUri.Scheme;
            var prePath = GetBase(pageUri);
            var pathBase = GetPathBase(pageUri);            

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
                return prePath + uriToCheck;

            // Dotslash relative URI.
            if (uriToCheck.IndexOf("./") == 0)
                return pathBase + uriToCheck.Substring(2);

            // Standard relative URI; add entire path. pathBase already includes a
            // trailing "/".
            return pathBase + uriToCheck;
        }
    }
}
