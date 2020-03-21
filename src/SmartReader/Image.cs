using System;
using System.Collections.Generic;
using System.Text;

namespace SmartReader
{
    /// <summary>
    /// Metadata for images found in the parsed article
    /// </summary>
    public class Image
    {
        /// <value>The original URI of the source</value>
        public Uri Source { get; set; } = null;
        /// <value>The size in bytes of the image</value>
        public long Size { get; set; } = 0;
        /// <value>The value of the attribute title of &lt;img&gt; tag</value>
        public string Title { get; set; } = "";
        /// <value>The value of the attribute alt of &lt;img&gt; tag</value>
        public string Description { get; set; } = "";

        /// <summary>
        /// Convert an image in a data URI string
        /// </summary>
        /// <param name="path">The path is used just to determine the mime type</param>
        /// <param name="bytes">The actual binary content of the image</param>
        internal static string ConvertImageToDataUri(string path, byte[] bytes)
        {
            return "data:"
                        + MimeMapping.MimeUtility.GetMimeMapping(path)
                        + ";base64,"
                        + Convert.ToBase64String(bytes);
        }
    }
}
