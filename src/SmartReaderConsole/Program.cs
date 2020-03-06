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

        static void RemoveElement(AngleSharp.Dom.IElement element)
        {
            element.QuerySelector(".removeable")?.Remove();
        }

        static void RunRandomExample(int num = -1)
        {
            var pages = Directory.EnumerateDirectories(@"..\..\..\..\SmartReaderTests\test-pages\");

            Random random = new Random();
            var index = random.Next(pages.Count());

            if (num != -1)
                index = num;

            String sourceContent = File.ReadAllText(Path.Combine(pages.ElementAt(index), "source.html"));

            Reader reader = new Reader("https://localhost/", sourceContent);

            reader.ClassesToPreserve = new string[] { "info" };

            reader.Debug = true;
            reader.LoggerDelegate = Console.WriteLine;

            reader.ClassesToPreserve = reader.ClassesToPreserve.Append("info").ToArray();

            // add a custom operation at the start
            reader.AddCustomOperationStart(RemoveElement);

            // add a custom operation at the end
            reader.AddCustomOperationEnd(AddInfo);

            // add an option to a regular expression
            reader.AddOptionToRegularExpression(Reader.RegularExpressions.Positive, "principale");

            reader.ReplaceRegularExpression(Reader.RegularExpressions.Videos, @"\/\/(www\.)?(dailymotion\.com|youtube\.com|youtube-nocookie\.com|player\.vimeo\.com)");

            // get the article
            Article article = reader.GetArticle();

            // get info about images in the article
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
            Console.WriteLine($"Site Name: {article.SiteName}");
            Console.WriteLine($"TimeToRead: {article.TimeToRead}");
            Console.WriteLine($"Excerpt: {article.Excerpt}");
            Console.WriteLine($"TextContent:\n {article.TextContent}");
            Console.WriteLine($"Content:\n {article.Content}");
            Console.WriteLine($"Featured Image: {article.FeaturedImage}");
            Console.WriteLine($"Images Found: {images.Result?.Count()}");

            article.ConvertImagesToDataUriAsync().Wait();

            Console.WriteLine($"Article with Images Converted: {article.Content}");
        }

        static void AddFieldToMetadataJsonForTests(string field)
        {
            var pages = Directory.EnumerateDirectories(@"..\..\..\..\SmartReaderTests\test-pages\");            
            foreach(var p in pages)
            {                
                String sourceContent = File.ReadAllText(Path.Combine(p, "source.html"));                

                Reader reader = new Reader("https://localhost/", sourceContent);               

                // get the article
                Article article = reader.GetArticle();        
                
                List<string> lines;

                lines = File.ReadAllLines(Path.Combine(p, "expected-metadata.json")).ToList();

                // add a comma after the last element, if none is present
                if (lines[lines.Count() - 2][lines[lines.Count() - 2].Length - 1] != ',')
                    lines[lines.Count() - 2] += ",";

                // insert the new filed before the end of the JSON object
                lines.Insert(lines.Count() - 1, $"  \"{field.First().ToString().ToLower() + field.Substring(1)}\": \"{article.GetType().GetProperty(field).GetValue(article)}\"");

                File.WriteAllLines(Path.Combine(p, "expected-metadata.json"), lines);
            }                       
        }

        static void Main(string[] args)
        {
            RunRandomExample();
        }
    }
}