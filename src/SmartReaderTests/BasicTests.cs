using System;
using Xunit;
using SmartReader;
using System.IO;
using Moq;
using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Linq;
using Xunit.Abstractions;
using System.Collections.Generic;
using AngleSharp.Html.Parser;
using AngleSharp.Html.Dom;
using RichardSzalay.MockHttp;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace SmartReaderTests
{   
    public class BasicTests
    {
        private readonly ITestOutputHelper _output;
        public BasicTests(ITestOutputHelper output)
        {
            _output = output;            
        }

        [Fact]
        public void TestCleanTitleNoSitename()
        {           
            Assert.Equal("Big title ", Readability.CleanTitle("Big title ", "Wikipedia"));
        }

        [Fact]
        public void TestCleanTitlePipe()
        {            
            Assert.Equal("Big title", Readability.CleanTitle("Big title | Wikipedia", "Wikipedia"));
        }

        [Fact]
        public void TestCleanTitleBackslash()
        {
            Assert.Equal("Big title", Readability.CleanTitle("Big title / Wikipedia", "Wikipedia"));
        }

        [Fact]
        public void TestCleanTitleMark()
        {
            Assert.Equal("Big title", Readability.CleanTitle("Big title » Wikipedia", "Wikipedia"));
        }

        [Fact]
        public void TestCleanTitleNoSeparator()
        {
            Assert.Equal("Big title Wikipedia", Readability.CleanTitle("Big title Wikipedia", "Wikipedia"));
        }

        [Fact]
        public void TestCleanTitleNonStandardFormat()
        {
            Assert.Equal("Big title [ Wikipedia ]", Readability.CleanTitle("Big title [ Wikipedia ]", "Wikipedia"));
        }

        [Fact]
        public void TestGetArticleTitleIdTitle()
        {
            HtmlParser parser = new HtmlParser(new HtmlParserOptions());
            IHtmlDocument doc = parser.ParseDocument(@"<html>
               <head><title>An article with a complex idea</title></head>
               <body></body>
               </html>");
           
            Assert.Equal("An article with a complex idea", Readability.GetArticleTitle(doc));
        }

        [Fact]
        public void TestGetArticleTitleSeparator()
        {
            HtmlParser parser = new HtmlParser(new HtmlParserOptions());
            IHtmlDocument doc = parser.ParseDocument(@"<html>
               <head><title>An article with a complex idea » By SomeSite</title></head>
               <body></body>
               </html>");

            Assert.Equal("An article with a complex idea", Readability.GetArticleTitle(doc));
        }

        [Fact]
        public void TestGetArticleTitleSeparatorNoSpace()
        {
            HtmlParser parser = new HtmlParser(new HtmlParserOptions());
            IHtmlDocument doc = parser.ParseDocument(@"<html>
               <head><title>An article with a complex idea-error</title></head>
               <body></body>
               </html>");

            Assert.Equal("An article with a complex idea-error", Readability.GetArticleTitle(doc));
        }

        [Fact]
        public void TestGetArticleTitleSeparatorFewWords()
        {
            HtmlParser parser = new HtmlParser(new HtmlParserOptions());
            IHtmlDocument doc = parser.ParseDocument(@"<html>
               <head><title>SomeSite - An  incredibly  smart title</title></head>
               <body></body>
               </html>");

            Assert.Equal("SomeSite - An incredibly smart title", Readability.GetArticleTitle(doc));
        }

        [Fact]
        public void TestGetArticleTitleSeparatorTooMuchWordsRemoved()
        {
            HtmlParser parser = new HtmlParser(new HtmlParserOptions());
            IHtmlDocument doc = parser.ParseDocument(@"<html>
               <head><title>By SomeSite - An  incredibly  smart title</title></head>
               <body></body>
               </html>");

            Assert.Equal("By SomeSite - An incredibly smart title", Readability.GetArticleTitle(doc));
        }

        [Fact]
        public void TestGetArticleTitleColon()
        {
            HtmlParser parser = new HtmlParser(new HtmlParserOptions());
            IHtmlDocument doc = parser.ParseDocument(@"<html>
               <head><title>SomeSite: An  incredibly  smart true title</title></head>
               <body></body>
               </html>");

            Assert.Equal("An incredibly smart true title", Readability.GetArticleTitle(doc));
        }

        [Fact]
        public void TestGetArticleTitleH1()
        {
            HtmlParser parser = new HtmlParser(new HtmlParserOptions());
            IHtmlDocument doc = parser.ParseDocument(@"<html>
               <head><title>SomeSite</title></head>
               <body><h1>The right idea for you</h1></body>
               </html>");

            Assert.Equal("The right idea for you", Readability.GetArticleTitle(doc));
        }

        [Fact]
        public void TestGetMetadataDescription()
        {
            HtmlParser parser = new HtmlParser(new HtmlParserOptions());
            IHtmlDocument doc = parser.ParseDocument(@"<html>
               <head>                   
                    <meta name=""og:description"" content=""The best article there is. Right here""/>
               </head>
               <body></body>
               </html>");

            Assert.Equal("The best article there is. Right here", Readability.GetArticleMetadata(doc, new Uri("https://localhost/"), "en").Excerpt);
        }

        [Fact]
        public void TestGetMetadataSiteName()
        {
            HtmlParser parser = new HtmlParser(new HtmlParserOptions());
            IHtmlDocument doc = parser.ParseDocument(@"<html>
               <head>                    
                    <meta name=""og:site_name"" content=""Some Good Site""/>
               </head>
               <body></body>
               </html>");

            Assert.Equal("Some Good Site", Readability.GetArticleMetadata(doc, new Uri("https://localhost/"), "en").SiteName);
        }

        [Fact]
        public void TestGetMetadataTitle()
        {
            HtmlParser parser = new HtmlParser(new HtmlParserOptions());
            IHtmlDocument doc = parser.ParseDocument(@"<html>
               <head>
                    <title>Some title</title>
                    <meta property=""twitter:title"" content=""Some Good Idea""/>
               </head>
               <body></body>
               </html>");

            Assert.Equal("Some Good Idea", Readability.GetArticleMetadata(doc, new Uri("https://localhost/"), "en").Title);
        }

        [Fact]
        public void TestGetMetadataLanguage()
        {
            HtmlParser parser = new HtmlParser(new HtmlParserOptions());
            IHtmlDocument doc = parser.ParseDocument(@"<html>
               <head>
                    <title>Some title</title>
                    <meta http-equiv=""Content-Language"" content=""it"">
               </head>
               <body></body>
               </html>");

            Assert.Equal("it", Readability.GetArticleMetadata(doc, new Uri("https://localhost/"), "").Language);
        }

        [Fact]
        public void TestGetMetadataFeaturedImage()
        {
            HtmlParser parser = new HtmlParser(new HtmlParserOptions());
            IHtmlDocument doc = parser.ParseDocument(@"<html>
               <head>
                    <meta name=""weibo:article:image"" content=""https://it.wikipedia.org/static/images/project-logos/itwiki-2x.png"">
               </head>
               <body></body>
               </html>");

            Assert.Equal("https://it.wikipedia.org/static/images/project-logos/itwiki-2x.png", Readability.GetArticleMetadata(doc, new Uri("https://localhost/"), "").FeaturedImage);
        }

        [Fact]
        public void TestGetMetadataAuthor()
        {
            HtmlParser parser = new HtmlParser(new HtmlParserOptions());
            IHtmlDocument doc = parser.ParseDocument(@"<html>
               <head>                    
                    <meta name=""author"" content=""Secret Man"">
               </head>
               <body></body>
               </html>");

            Assert.Equal("Secret Man", Readability.GetArticleMetadata(doc, new Uri("https://localhost/"), "").Author);
        }

        [Fact]
        public void TestGetMetadataDateNoDate()
        {
            HtmlParser parser = new HtmlParser(new HtmlParserOptions());
            IHtmlDocument doc = parser.ParseDocument(@"<html>
               <head></head>
               <body></body>
               </html>");

            Assert.Null(Readability.GetArticleMetadata(doc, new Uri("https://localhost/"), "").PublicationDate);
        }

        [Fact]
        public void TestGetMetadataDateMeta()
        {
            HtmlParser parser = new HtmlParser(new HtmlParserOptions());
            IHtmlDocument doc = parser.ParseDocument(@"<html>
               <head>                    
                    <meta itemprop=""datePublished"" content=""2110-10-21"" />
               </head>
               <body></body>
               </html>");

            Assert.Equal(new DateTime(2110, 10, 21), Readability.GetArticleMetadata(doc, new Uri("https://localhost/"), "").PublicationDate);
        }

        [Fact]
        public void TestGetMetadataDateTimeTag()
        {
            HtmlParser parser = new HtmlParser(new HtmlParserOptions());
            IHtmlDocument doc = parser.ParseDocument(@"<html>
               <head></head>
               <body><p>Hello. I am talking to you, <time datetime=""01-09-1980"" pubDate=""pubDate"">now</time></p></body>
               </html>");

            Assert.Equal(new DateTime(1980, 9, 1), Readability.GetArticleMetadata(doc, new Uri("https://localhost/"), "").PublicationDate);
        }

        [Fact]
        public void TestGetMetadataDateUrl()
        {
            HtmlParser parser = new HtmlParser(new HtmlParserOptions());
            IHtmlDocument doc = parser.ParseDocument(@"<html>
               <head></head>
               <body></body>
               </html>");

            Assert.Equal(new DateTime(2110, 10, 21), Readability.GetArticleMetadata(doc, new Uri("https://localhost/2110/10/21"), "").PublicationDate);
        }

        [Fact]
        public void TestConvertImagesAsDataURI()
        {
            // creating element
            HtmlParser parser = new HtmlParser(new HtmlParserOptions());
            IHtmlDocument doc = parser.ParseDocument(@"<html>
               <head></head>
               <body>
                    <p>This is a paragraph with some text.</p>
                    <p>This is a paragraph with some other text.</p>
                    <p>This is a paragraph with an image <img src=""https://localhost/small_image.png"" alt=""Nothing valuable""></img>.</p>
                    <p>This is a paragraph with an image <img src=""https://localhost/big_image.jpg"" alt=""Something very valuable""></img>.</p>
               </body>
               </html>");

            // setting up mocking HttpClient
            var mockHttp = new MockHttpMessageHandler();

            mockHttp.When("https://localhost/small_image.png")
                    .Respond("image/png", File.OpenRead(@"..\..\..\test-images\small_image.png"));

            mockHttp.When("https://localhost/big_image.jpg")
                    .Respond("image/jpeg", File.OpenRead(@"..\..\..\test-images\big_image.jpg"));

            Reader.SetCustomHttpClient(mockHttp.ToHttpClient());

            Article article = new Article(new Uri("https://localhost/article"),
                                            "Great article", "by Ulysses", "", "en", "Nobody",
                                            doc.Body, new Metadata(), true);
            
            article.ConvertImagesToDataUriAsync().Wait();

            // check that there is one image
            Assert.Equal(1, Regex.Matches(article.Content, "<img").Count);
            int start = article.Content.IndexOf("src=") + 4;
            int end = article.Content.IndexOf("\"", start + 1);
            // check that the src attribute is of the expected length
            Assert.Equal(572400, end - start);
        }
    }
}

