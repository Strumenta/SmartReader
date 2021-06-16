#nullable enable

using System;

using AngleSharp.Io;

namespace SmartReader
{
    /// <summary>
    /// Metadata for images found in the parsed article
    /// </summary>
    public class Image
    {
        /// <value>The original URI of the source</value>
        public Uri? Source { get; set; }

        /// <value>The size in bytes of the image</value>
        public long Size { get; set; }

        /// <value>The value of the attribute title of &lt;img&gt; tag</value>
        public string? Title { get; set; }

        /// <value>The value of the attribute alt of &lt;img&gt; tag</value>
        public string? Description { get; set; }

        /// <summary>
        /// Convert an image in a data URI string
        /// </summary>
        /// <param name="path">The path is used just to determine the mime type</param>
        /// <param name="bytes">The actual binary content of the image</param>
        internal static string ConvertImageToDataUri(string path, byte[] bytes)
        {
            int dotIndex = path.LastIndexOf('.');
            string extension = dotIndex > 0 ? path.Substring(dotIndex) : string.Empty;
            string mime = MimeTypeNames.FromExtension(extension);

            return $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
        }
    }
}