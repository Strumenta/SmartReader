using System;
using System.Collections.Generic;
using System.Text;

namespace SmartReader
{
    public class Image
    {
        public Uri Source { get; set; } = null;
        public long Size { get; set; } = 0;
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";

        public static string ConvertImageToDataUri(string path, byte[] bytes)
        {
            return "data:"
                        + MimeMapping.MimeUtility.GetMimeMapping(path)
                        + ";base64,"
                        + Convert.ToBase64String(bytes);
        }
    }
}
