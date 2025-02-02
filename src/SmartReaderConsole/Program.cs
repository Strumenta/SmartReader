﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using SmartReader;
using SmartReader.NaturalLanguageProcessing;

namespace SmartReaderConsole
{
    class Program
    {
        static void AddInfo(AngleSharp.Dom.IElement element)
        {
            if (element.QuerySelector("div")?.LastElementChild != null)
                element.QuerySelector("div").LastElementChild.InnerHtml += "<p>Article parsed by SmartReader</p>";
        }

        static void RemoveElement(AngleSharp.Dom.IElement element)
        {
            element.QuerySelector(".removeable")?.Remove();
        }

        static string RemoveSpace(AngleSharp.Dom.IElement element)
        {
            return Regex.Replace(Regex.Replace(element?.InnerHtml, @"(?<endBefore></.*?>)\s+(?<startAfter><[^/]>)", "${endBefore}${startAfter}"), @"(?<endBefore><((?!pre).)*?>)\s+", "${endBefore}");
        }

        static void RunRandomExampleWithNaturalLanguageProcessing(int num = -1)
        {
            // At the present moment most content is UTF8, so this increases the chances
            // to see the text as you would see in a browser
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            NLP.Enable();

            var pages = Directory.EnumerateDirectories(Path.Combine("..", "SmartReaderTests/test-pages"));

            Random random = new Random();
            var index = random.Next(pages.Count());

            if (num != -1)
                index = num;

            string sourceContent = File.ReadAllText(Path.Combine(pages.ElementAt(index), "source.html"));

            Reader reader = new Reader("https://localhost/", sourceContent);
            
            reader.Debug = false;
            reader.LoggerDelegate = Console.WriteLine;

            // get the article
            Article article = reader.GetArticle();
            
            Console.WriteLine($"Is Readable: {article.IsReadable}");
            Console.WriteLine($"Uri: {article.Uri}");
            Console.WriteLine($"Title: {article.Title}");
            Console.WriteLine($"Site Name: {article.SiteName}");
            Console.WriteLine($"Excerpt: {article.Excerpt}");
            Console.WriteLine($"Detected Language: {article.Language}");
            Console.WriteLine($"TextContent:\n {article.TextContent}");                                   
        }

        static void RunRandomExample(int num = -1)
        {
            // At the present moment most content is UTF8, so this increases the chances
            // to see the text as you would see in a browser
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            Article.Serializer = Program.RemoveSpace;

            var pages = Directory.EnumerateDirectories(Path.Combine("..", "SmartReaderTests/test-pages"));

            Random random = new Random();
            var index = random.Next(pages.Count());

            if (num != -1)
                index = num;

            string sourceContent = File.ReadAllText(Path.Combine(pages.ElementAt(index), "source.html"));

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
            reader.AddOptionToRegularExpression(RegularExpressions.Positive, "principale");

            reader.ReplaceRegularExpression(RegularExpressions.Videos, @"\/\/(www\.)?(dailymotion\.com|youtube\.com|youtube-nocookie\.com|player\.vimeo\.com)");

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
            Console.WriteLine($"Images Found: {images.Result?.Count}");
            Console.WriteLine($"Alternative language URIs: {AlternativeLanguageUrisToString(article.AlternativeLanguageUris)}");

            article.ConvertImagesToDataUriAsync().Wait();

            Console.WriteLine($"Article with Images Converted: {article.Content}");
        }

        static void AddFieldToMetadataJsonForTests(string field)
        {
            var pages = Directory.EnumerateDirectories(@"..\..\..\..\SmartReaderTests\test-pages\");
            foreach (var p in pages)
            {
                string sourceContent = File.ReadAllText(Path.Combine(p, "source.html"));

                Reader reader = new Reader("https://localhost/", sourceContent);

                // get the article
                Article article = reader.GetArticle();

                List<string> lines;

                lines = File.ReadAllLines(Path.Combine(p, "expected-metadata.json")).ToList();

                // add a comma after the last element, if none is present
                if (lines[lines.Count - 2][lines[lines.Count - 2].Length - 1] != ',')
                    lines[lines.Count - 2] += ",";

                // insert the new field before the end of the JSON object
                lines.Insert(lines.Count - 1, $"  \"{field.First().ToString().ToLower() + field.Substring(1)}\": \"{article.GetType().GetProperty(field).GetValue(article)}\"");

                File.WriteAllLines(Path.Combine(p, "expected-metadata.json"), lines);
            }
        }

        static void AddTest(string name, string url)
        {
            string directory = @"..\..\..\..\SmartReaderTests\test-pages\" + name;
            Directory.CreateDirectory(directory);

            WebClient client = new WebClient();
            client.DownloadFile(url, (Path.Combine(directory, "source.html")));

            string sourceContent = File.ReadAllText(Path.Combine(directory, "source.html"));

            Reader reader = new Reader("https://localhost/", sourceContent);

            Article article = reader.GetArticle();

            File.WriteAllText(Path.Combine(directory, "expected.html"), article.Content);

            var jso = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                // to safely print non-ASCII characters
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            var obj = new
            {
                title = article.Title,
                byline = article.Byline,
                dir = article.Dir,
                excerpt = article.Excerpt,
                readerable = article.IsReadable,
                language = article.Language,
                timeToRead = article.TimeToRead.ToString(),
                publicationDate = article.PublicationDate,
                author = article.Author,
                siteName = article.SiteName,
                featuredImage = article.FeaturedImage
            };

            File.WriteAllText(Path.Combine(directory, @"expected-metadata.json"), JsonSerializer.Serialize(obj, jso), System.Text.Encoding.UTF8);
        }

        static void SimpleTestUrl(string url)
        {
            Reader reader = new Reader(url);
            Article article = reader.GetArticle();

            Console.WriteLine(article.Content);
            Console.WriteLine(article.Title);
        }

        static string AlternativeLanguageUrisToString(Dictionary<string, Uri> alternativeLanguageUris)
        {
            StringBuilder sb = new();

            foreach (var item in alternativeLanguageUris)
            {
                sb.Append($"[{item.Key}]-[{item.Value}], ");
            }

            return sb.ToString();
        }
        static void Main(string[] args)
        {
            RunRandomExampleWithNaturalLanguageProcessing();
        }
    }
}