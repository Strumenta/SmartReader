using System;
using System.Globalization;
using System.Text;

namespace SmartReader
{
    internal static class UriExtensions
    {
        internal static string GetBase(this Uri startUri)
        {
            var sb = new StringBuilder(startUri.Scheme + "://");

            if (!string.IsNullOrEmpty(startUri.UserInfo))
            {
                sb.Append(startUri.UserInfo);
                sb.Append('@');
            }

            sb.Append(startUri.Host);

            if (!startUri.IsDefaultPort)
            {
                sb.Append(':');
                sb.Append(startUri.Port.ToString(CultureInfo.InvariantCulture));
            }

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

            // if the uri is empty, just return pathBase
            if (uriToCheck.Length == 0)
                return pathBase;

            // If this is already an absolute URI, return it.
            if (Uri.IsWellFormedUriString(uriToCheck, UriKind.Absolute))
                return uriToCheck;

            // Ignore hash URIs
            if (uriToCheck[0] == '#')
                return uriToCheck;

            // Scheme-rooted relative URI.
            if (uriToCheck.StartsWith("//", StringComparison.Ordinal))
                return scheme + "://" + uriToCheck.Substring(2);

            // Prepath-rooted relative URI.
            if (uriToCheck[0] == '/')
                return prePath + uriToCheck;

            // Dotslash relative URI.
            if (uriToCheck.StartsWith("./", StringComparison.Ordinal))
                return pathBase + uriToCheck.Substring(2);

            // Ignore data URI.
            // Note that data URI encoded in base64 are already ignored by the
            // IsWellFormedUriString check. This check is necessary for dataURI in UTF-8
            if (uriToCheck.StartsWith("data:", StringComparison.Ordinal))
                return uriToCheck;

            // Standard relative URI; add entire path. pathBase already includes a
            // trailing "/".
            return pathBase + uriToCheck;
        }
    }
}
