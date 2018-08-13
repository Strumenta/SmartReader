using Newtonsoft.Json.Linq;
using SmartReader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SmartReaderConsole
{
    class Program
    {
        static void AddInfo(AngleSharp.Dom.IElement element)
        {       
            element.QuerySelector("div").LastElementChild.InnerHtml += "<p>Article parsed by SmartReader</p>";
        }

        static void Main(string[] args)
        {
            var pages = Directory.EnumerateDirectories(@"..\..\..\..\SmartReaderTests\test-pages\");
            Random random = new Random();
            var index = random.Next(pages.Count());            

            String sourceContent = File.ReadAllText(Path.Combine(pages.ElementAt(index), "source.html"));

            Reader reader = new Reader("https://localhost/", sourceContent);

            reader.AddCustomOperation(AddInfo);
            Article article = reader.GetArticle();            
            var images = article.GetImagesAsync();
            images.Wait();

            Console.WriteLine($"Is Readable: {article.IsReadable}");
            Console.WriteLine($"Uri: {article.Uri}");
            Console.WriteLine($"Title: {article.Title}");
            Console.WriteLine($"Byline: {article.Byline}");
            Console.WriteLine($"Author: {article.Author}");
            Console.WriteLine($"Publication Date: {article.PublicationDate}");
            Console.WriteLine($"Direction of the Text: {article.Dir}");
            Console.WriteLine($"Language: {article.Language}");
            Console.WriteLine($"TimeToRead: {article.TimeToRead}");
            Console.WriteLine($"Excerpt: {article.Excerpt}");
            Console.WriteLine($"TextContent:\n {article.TextContent}");
            Console.WriteLine($"Content:\n {article.Content}");
            Console.WriteLine($"Featured Image: {article.FeaturedImage}");
            Console.WriteLine($"Images Found: {images.Result?.Count()}");
        }
    }
}