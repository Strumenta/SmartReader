using System;
using Xunit;
using SmartReader;
using System.IO;
using Xunit.Abstractions;
using AngleSharp.Html.Parser;
using RichardSzalay.MockHttp;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

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
        public void TestCleanTitleDoesNotBreakWithRegexCharacters()
        {
            Assert.Equal("* No longer! *", Readability.CleanTitle("* No longer! *", "This is a *** problem"));
            Assert.Equal("Maybe ?", Readability.CleanTitle("Maybe ?", "Is this a problem?"));
        }

        [Fact]
        public void TestGetArticleTitleIdTitle()
        {
            var parser = new HtmlParser(new HtmlParserOptions());
            var doc = parser.ParseDocument(@"<html>
               <head><title>An article with a complex idea</title></head>
               <body></body>
               </html>");

            Assert.Equal("An article with a complex idea", Readability.GetArticleTitle(doc));
        }

        [Fact]
        public void TestGetArticleTitleSeparator()
        {
            var parser = new HtmlParser(new HtmlParserOptions());
            var doc = parser.ParseDocument(@"<html>
               <head><title>An article with a complex idea » By SomeSite</title></head>
               <body></body>
               </html>");

            Assert.Equal("An article with a complex idea", Readability.GetArticleTitle(doc));
        }

        [Fact]
        public void TestGetArticleTitleSeparatorNoSpace()
        {
            var parser = new HtmlParser(new HtmlParserOptions());
            var doc = parser.ParseDocument(@"<html>
               <head><title>An article with a complex idea-error</title></head>
               <body></body>
               </html>");

            Assert.Equal("An article with a complex idea-error", Readability.GetArticleTitle(doc));
        }

        [Fact]
        public void TestGetArticleTitleSeparatorFewWords()
        {
            var parser = new HtmlParser(new HtmlParserOptions());
            var doc = parser.ParseDocument(@"<html>
               <head><title>SomeSite - An  incredibly  smart title</title></head>
               <body></body>
               </html>");

            Assert.Equal("SomeSite - An incredibly smart title", Readability.GetArticleTitle(doc));
        }

        [Fact]
        public void TestGetArticleTitleSeparatorTooMuchWordsRemoved()
        {
            var parser = new HtmlParser(new HtmlParserOptions());
            var doc = parser.ParseDocument(@"<html>
               <head><title>By SomeSite - An  incredibly  smart title</title></head>
               <body></body>
               </html>");

            Assert.Equal("By SomeSite - An incredibly smart title", Readability.GetArticleTitle(doc));
        }

        [Fact]
        public void TestGetArticleTitleColon()
        {
            var parser = new HtmlParser(new HtmlParserOptions());
            var doc = parser.ParseDocument(@"<html>
               <head><title>SomeSite: An  incredibly  smart true title</title></head>
               <body></body>
               </html>");

            Assert.Equal("An incredibly smart true title", Readability.GetArticleTitle(doc));
        }

        [Fact]
        public void TestGetArticleTitleH1()
        {
            var parser = new HtmlParser(new HtmlParserOptions());
            var doc = parser.ParseDocument(@"<html>
               <head><title>SomeSite</title></head>
               <body><h1>The right idea for you</h1></body>
               </html>");

            Assert.Equal("The right idea for you", Readability.GetArticleTitle(doc));
        }

        [Fact]
        public void TestGetMetadataDescription()
        {
            var parser = new HtmlParser(new HtmlParserOptions());
            var doc = parser.ParseDocument(@"<html>
               <head>                   
                    <meta name=""og:description"" content=""The best article there is. Right here""/>
               </head>
               <body></body>
               </html>");

            Assert.Equal("The best article there is. Right here",
                Readability.GetArticleMetadata(doc, new Uri("https://localhost/"), "en",
                    new Dictionary<string, string>()).Excerpt);
        }

        [Fact]
        public void TestGetMetadataSiteName()
        {
            var parser = new HtmlParser(new HtmlParserOptions());
            var doc = parser.ParseDocument(@"<html>
               <head>                    
                    <meta name=""og:site_name"" content=""Some Good Site""/>
               </head>
               <body></body>
               </html>");

            Assert.Equal("Some Good Site",
                Readability.GetArticleMetadata(doc, new Uri("https://localhost/"), "en",
                    new Dictionary<string, string>()).SiteName);
        }

        [Fact]
        public void TestGetMetadataTitle()
        {
            var parser = new HtmlParser(new HtmlParserOptions());
            var doc = parser.ParseDocument(@"<html>
               <head>
                    <title>Some title</title>
                    <meta property=""twitter:title"" content=""Some Good Idea""/>
               </head>
               <body></body>
               </html>");

            Assert.Equal("Some Good Idea",
                Readability.GetArticleMetadata(doc, new Uri("https://localhost/"), "en",
                    new Dictionary<string, string>()).Title);
        }

        [Fact]
        public void TestGetMetadataLanguage()
        {
            var parser = new HtmlParser(new HtmlParserOptions());
            var doc = parser.ParseDocument(@"<html>
               <head>
                    <title>Some title</title>
                    <meta http-equiv=""Content-Language"" content=""it"">
               </head>
               <body></body>
               </html>");

            Assert.Equal("it",
                Readability.GetArticleMetadata(doc, new Uri("https://localhost/"), "", new Dictionary<string, string>())
                    .Language);
        }

        [Fact]
        public void TestGetMetadataAlternativeLanguageUris_WhenNoLinkTagPresent_IsEmpty()
        {
            // Arrange
            var parser = new HtmlParser(new HtmlParserOptions());
            var doc = parser.ParseDocument(@"<html>
               <head></head>
               <body></body>
               </html>");

            // Act
            var metadata = Readability.GetArticleMetadata(doc, new Uri("https://localhost/"), "",
                new Dictionary<string, string>());

            // Assert
            Assert.Empty(metadata.AlternativeLanguageUris);
        }

        [Fact]
        public void TestGetMetadataAlternativeLanguageUris_IgnoresAlternateLinkWithoutHreflang()
        {
            // Arrange
            var parser = new HtmlParser(new HtmlParserOptions());
            var doc = parser.ParseDocument(@"<html>
               <head>
                    <link rel=""alternate"" media=""only screen and (max-width: 640px)"" href=""https://m.example.com""/>
               </head>
               <body></body>
               </html>");

            // Act
            var metadata = Readability.GetArticleMetadata(doc, new Uri("https://localhost/"), "",
                new Dictionary<string, string>());

            // Assert
            Assert.Empty(metadata.AlternativeLanguageUris);
        }

        [Fact]
        public void TestGetMetadataAlternativeLanguageUris_IgnoresXDefaultPage()
        {
            // Arrange
            var parser = new HtmlParser(new HtmlParserOptions());
            var doc = parser.ParseDocument(@"<html>
               <head>
                    <link rel=""alternate"" hreflang=""x-default"" href=""https://m.example.com"" />
               </head>
               <body></body>
               </html>");

            // Act
            var metadata = Readability.GetArticleMetadata(doc, new Uri("https://localhost/"), "",
                new Dictionary<string, string>());

            // Assert
            Assert.Empty(metadata.AlternativeLanguageUris);
        }

        [Fact]
        public void TestGetMetadataAlternativeLanguageUris_DuplicateLinks_IgnoresSecond()
        {
            // Arrange
            var parser = new HtmlParser(new HtmlParserOptions());
            var doc = parser.ParseDocument(@"<html>
               <head>
                    <link rel=""alternate"" href=""https://www.first-domain.com"" hreflang=""it-IT"">
                    <link rel=""alternate"" href=""https://www.second-domain.com"" hreflang=""it-IT"">
               </head>
               <body></body>
               </html>");
            var expected = new Dictionary<string, Uri>
            {
                {"it-IT", new Uri("https://www.first-domain.com")},
            };

            // Act
            var metadata = Readability.GetArticleMetadata(doc, new Uri("https://localhost/"), "",
                new Dictionary<string, string>());

            // Assert
            Assert.Equal(expected, metadata.AlternativeLanguageUris);
        }

        [Fact]
        public void TestGetMetadataAlternativeLanguageUris()
        {
            // Arrange
            var parser = new HtmlParser(new HtmlParserOptions());
            var doc = parser.ParseDocument(@"<html>
               <head>
                    <link rel=""alternate"" href=""https://www.apple.com/en"" hreflang=""en-US"">
                    <link rel=""alternate"" href=""https://www.apple.com/it"" hreflang=""it-IT"">
                    <link rel=""alternate"" href=""https://www.apple.com/de"" hreflang=""de-DE"">
               </head>
               <body></body>
               </html>");
            var expected = new Dictionary<string, Uri>
            {
                {"en-US", new Uri("https://www.apple.com/en")},
                {"it-IT", new Uri("https://www.apple.com/it")},
                {"de-DE", new Uri("https://www.apple.com/de")},
            };

            // Act
            var metadata = Readability.GetArticleMetadata(doc, new Uri("https://localhost/"), "",
                new Dictionary<string, string>());

            // Assert
            Assert.Equal(expected, metadata.AlternativeLanguageUris);
        }

        [Fact]
        public void TestGetMetadataFeaturedImage()
        {
            var parser = new HtmlParser(new HtmlParserOptions());
            var doc = parser.ParseDocument(@"<html>
               <head>
                    <meta name=""weibo:article:image"" content=""https://it.wikipedia.org/static/images/project-logos/itwiki-2x.png"">
               </head>
               <body></body>
               </html>");

            Assert.Equal("https://it.wikipedia.org/static/images/project-logos/itwiki-2x.png",
                Readability.GetArticleMetadata(doc, new Uri("https://localhost/"), "", new Dictionary<string, string>())
                    .FeaturedImage);
        }

        [Fact]
        public void TestGetMetadataAuthor()
        {
            var parser = new HtmlParser(new HtmlParserOptions());
            var doc = parser.ParseDocument(@"<html>
               <head>                    
                    <meta name=""author"" content=""Secret Man"">
               </head>
               <body></body>
               </html>");

            Assert.Equal("Secret Man",
                Readability.GetArticleMetadata(doc, new Uri("https://localhost/"), "", new Dictionary<string, string>())
                    .Author);
        }

        [Fact]
        public void TestGetMetadataDateNoDate()
        {
            var parser = new HtmlParser(new HtmlParserOptions());
            var doc = parser.ParseDocument(@"<html>
               <head></head>
               <body></body>
               </html>");

            Assert.Null(Readability
                .GetArticleMetadata(doc, new Uri("https://localhost/"), "", new Dictionary<string, string>())
                .PublicationDate);
        }

        [Fact]
        public void TestGetMetadataDateMeta()
        {
            var parser = new HtmlParser(new HtmlParserOptions());
            var doc = parser.ParseDocument(@"<html>
               <head>                    
                    <meta itemprop=""datePublished"" content=""2110-10-21"" />
               </head>
               <body></body>
               </html>");

            Assert.Equal(new DateTime(2110, 10, 21),
                Readability.GetArticleMetadata(doc, new Uri("https://localhost/"), "", new Dictionary<string, string>())
                    .PublicationDate);
        }

        [Fact]
        public void TestGetMetadataDateTimeTag()
        {
            var parser = new HtmlParser(new HtmlParserOptions());
            var doc = parser.ParseDocument(@"<html>
               <head></head>
               <body><p>Hello. I am talking to you, <time datetime=""1980-09-01"" pubDate=""pubDate"">now</time></p></body>
               </html>");

            Assert.Equal(new DateTime(1980, 9, 1),
                Readability.GetArticleMetadata(doc, new Uri("https://localhost/"), "", new Dictionary<string, string>())
                    .PublicationDate);
        }

        [Fact]
        public void TestGetMetadataDateUrl()
        {
            var parser = new HtmlParser(new HtmlParserOptions());
            var doc = parser.ParseDocument(@"<html>
               <head></head>
               <body></body>
               </html>");

            Assert.Equal(new DateTime(2110, 10, 21),
                Readability.GetArticleMetadata(doc, new Uri("https://localhost/2110/10/21/"), "",
                    new Dictionary<string, string>()).PublicationDate);
            Assert.Equal(new DateTime(2110, 10, 1),
                Readability.GetArticleMetadata(doc, new Uri("https://localhost/2110/10/37"), "",
                    new Dictionary<string, string>()).PublicationDate);
            Assert.Equal(new DateTime(2010, 10, 1),
                Readability.GetArticleMetadata(doc, new Uri("https://localhost/2010/10/change_of_plans.html"), "",
                    new Dictionary<string, string>()).PublicationDate);
        }

        [Fact]
        public void TestConvertImagesAsDataURI()
        {
            // creating element
            var parser = new HtmlParser(new HtmlParserOptions());
            var doc = parser.ParseDocument(@"<html>
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
                .Respond("image/png", File.OpenRead(Path.Combine("..", "..", "..", "test-images", "small_image.png")));

            mockHttp.When("https://localhost/big_image.jpg")
                .Respond("image/jpeg", File.OpenRead(Path.Combine("..", "..", "..", "test-images", "big_image.jpg")));

            var reader = new Reader("https://localhost/article");

            Reader.SetBaseHttpClientHandler(mockHttp);

            var article = new Article(new Uri("https://localhost/article"), "Great article", "by Ulysses", "", "en",
                "Nobody", doc.Body, new Metadata(), true, reader);

            article.ConvertImagesToDataUriAsync().Wait();

            // check that there is one image
            Assert.Single(Regex.Matches(article.Content, "<img"));
            int start = article.Content.IndexOf("src=") + 4;
            int end = article.Content.IndexOf("\"", start + 1);
            // check that the src attribute is of the expected length
            Assert.Equal(572400, end - start);
        }

        [Fact]
        public void TestCheckSVGDataURIIsPreserved()
        {
            var parser = new HtmlParser(new HtmlParserOptions());
            var doc = parser.ParseDocument(@"<html>
               <head></head>
               <body>
                    <p>This is a paragraph with some text.</p>
                    <p>This is a paragraph with some other text.</p>
                    <p>This is a paragraph with an image <img src=""data:image/svg+xml,%3C%3Fxml version='1.0' encoding='UTF-8'%3F%3E%3Csvg xmlns='http://www.w3.org/2000/svg' width='1' height='1'/%3E""></img>.</p>
               </body>
               </html>");

            Readability.FixRelativeUris(doc.Body, new Uri("https://localhost/article"), doc);

            if (doc.Body.GetElementsByTagName("img")[0].GetAttribute("src") is string src)
                Assert.False(src.StartsWith("https://localhost"));
        }
        
        [Fact]
        public void TestCheckBase64DataURIIsPreserved()
        {
            var parser = new HtmlParser(new HtmlParserOptions());
            var doc = parser.ParseDocument(@"<html>
               <head></head>
               <body>
                    <p>This is a paragraph with some text.</p>
                    <p>This is a paragraph with some other text.</p>
                    <p>This is a paragraph with an image <img src=""data:image/png;base64,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""></img>.</p>
               </body>
               </html>");

            Readability.FixRelativeUris(doc.Body, new Uri("https://localhost/article"), doc);

            if (doc.Body.GetElementsByTagName("img")[0].GetAttribute("src") is string src)
                Assert.False(src.StartsWith("https://localhost"));
        }

        [Fact]
        public void TestPlaintextConversion()
        {
            // creating element
            var parser = new HtmlParser(new HtmlParserOptions());
            var text = "<html>\r\n" +
                       "<head></head>\r\n" +
                       "<body>\r\n" +
                       "     <p> </p>\r\n" +
                       "     <p>This is a paragraph with some text.</p>\r\n" +
                       "\r\n" +
                       "     <p>This  	 is a paragraph   with some other text and lots of whitespace  .</p>\r\n" +
                       "\r\n" +
                       "\r\n" +
                       "\r\n" +
                       "     <p>This is 			a paragraph with different<br> other text.</p>\r\n" +
                       "</body>\r\n" +
                       "</html>";

            var doc = parser.ParseDocument(text);

            var reader = new Reader("https://localhost/article");

            var article = new Article(new Uri("https://localhost/article"), "Great article", "by Ulysses", "", "en",
                "Nobody", doc.Body, new Metadata(), true, reader);

            // check that the text returned is correct
            Assert.Equal($"This is a paragraph with some text.{Environment.NewLine}" +
                         $"{Environment.NewLine}This is a paragraph with some other text and lots of whitespace .{Environment.NewLine}" +
                         $"{Environment.NewLine}This is a paragraph with different{Environment.NewLine}other text.",
                article.TextContent);
        }

        [Fact]
        public void TestCustomSerializer()
        {
            // creating element
            var parser = new HtmlParser(new HtmlParserOptions());
            var doc = parser.ParseDocument(@"<html>
               <head></head>
               <body>
                    <p> </p>
                    <p>This is a paragraph with some text.</p>
                    <p>This  	 is a paragraph   with some other text and lots of whitespace  .</p>
                    <p>This is 			a paragraph with different<br> other text.</p>
                    <pre>   Space inside here is       magic</pre>
               </body> 
               </html>");

            var reader = new Reader("https://localhost/article");

            static string serializer(IElement element)
            {
                return Regex
                    .Replace(
                        Regex.Replace(element.InnerHtml, @"(?<endBefore></.*?>)\s+(?<startAfter><[^/]>)",
                            "${endBefore}${startAfter}"), @"(?<endBefore><(?!pre).*?>)\s+", "${endBefore}").Trim();
            }

            Article.Serializer = serializer;

            var article = new Article(new Uri("https://localhost/article"), "Great article", "by Ulysses", "", "en",
                "Nobody", doc.Body, new Metadata(), true, reader);

            // restore standard serializer
            Article.Serializer = (AngleSharp.Dom.IElement element) => { return element.InnerHtml; };

            // check that the text returned is correct
            Assert.Equal(
                @"<p></p><p>This is a paragraph with some text.</p><p>This  	 is a paragraph   with some other text and lots of whitespace  .</p><p>This is 			a paragraph with different<br>other text.</p><pre>   Space inside here is       magic</pre>",
                article.Content);
        }

        [Fact]
        public void TestCustomConverter()
        {
            // creating element
            var parser = new HtmlParser(new HtmlParserOptions());
            var doc = parser.ParseDocument(@"<html>
               <head></head>
               <body>
                    <p>JavaScript is a great language for system programming.</p>                   
               </body> 
               </html>");

            var reader = new Reader("https://localhost/article");

            static string converter(IElement element)
            {
                return element.TextContent.Replace("JavaScript", "**********").Trim();
            }

            var oldConverter = Article.Converter;

            Article.Converter = converter;

            var article = new Article(new Uri("https://localhost/article"), "Great article", "by Ulysses", "", "en",
                "Nobody", doc.Body, new Metadata(), true, reader);

            // check that the text returned is correct
            Assert.Equal(@"********** is a great language for system programming.", article.TextContent);

            // restore standard converter
            Article.Converter = oldConverter;
        }

        [Fact]
        public void TestGetMetadataAuthorFromJsonLD()
        {
            var parser = new HtmlParser(new HtmlParserOptions());
            var doc = parser.ParseDocument(@"<html>
               <head>                    
                    <meta name=""author"" content=""Secret Man"">
					<script type=""application/ld+json"">
					{
						""@context"": ""http://schema.org""
						,""@type"": ""Article""						
						,""author"": {
                            ""@type"": ""Person"",
                            ""name"": ""Real Author""
                        }
					}
					</script>
               </head>
               <body></body>
               </html>");

            Assert.Equal("Real Author",
                Readability.GetArticleMetadata(doc, new Uri("https://localhost/"), "", Readability.GetJSONLD(doc))
                    .Author);
        }

        [Fact]
        public void TestGetMetadataAuthorsFromJsonLD()
        {
            var parser = new HtmlParser(new HtmlParserOptions());
            var doc = parser.ParseDocument(@"<html>
               <head>                    
                    <meta name=""author"" content=""Secret Man"">
					<script type=""application/ld+json"">
					{
						""@context"": ""http://schema.org""
						,""@type"": ""Article""						
						,""author"": [{
                            ""@type"": ""Person"",
                            ""name"": ""Real Author""
                        },{
                            ""@type"": ""Person"",
                            ""name"": ""Secret Man""
                        }]
					}
					</script>
               </head>
               <body></body>
               </html>");

            Assert.Equal("Real Author, Secret Man",
                Readability.GetArticleMetadata(doc, new Uri("https://localhost/"), "", Readability.GetJSONLD(doc))
                    .Author);
        }

        [Fact]
        public void TestGetMetadataTypeOnlyCorrectJsonLDType()
        {
            var parser = new HtmlParser(new HtmlParserOptions());
            var doc = parser.ParseDocument(@"<html>
               <head>                    
                    <meta name=""author"" content=""Secret Man"">
					<script type=""application/ld+json"">
					{
						""@context"": ""http://schema.org""
						,""@type"": ""FakeArticle""
						,""author"": {
                            ""@type"": ""Person"",
                            ""name"": ""Real Author""
                        }
					}
					</script>
               </head>
               <body></body>
               </html>");

            Assert.Equal("Secret Man",
                Readability.GetArticleMetadata(doc, new Uri("https://localhost/"), "", Readability.GetJSONLD(doc))
                    .Author);
        }

        [Fact]
        public void TestForbiddenUrlLeadToFailureButNotException()
        {
            // creating element
            var parser = new HtmlParser(new HtmlParserOptions());
            var doc = parser.ParseDocument(@"<html>
               <head></head>
               <body>
                    <p>This is a paragraph with some text.</p>
                    <p>This is a paragraph with some other text.</p>
               </body>
               </html>");

            // setting up mocking HttpClient
            var mockHttp = new MockHttpMessageHandler();

            mockHttp.When("https://localhost/article")
                .Respond(HttpStatusCode.Forbidden);

            var reader = new Reader("https://localhost/article");

            Reader.SetBaseHttpClientHandler(mockHttp);
            Assert.False(reader.GetArticle().Completed);
        }

        [Fact]
        public void TestMaxElemsToParseThrowException()
        {
            // setting up mocking HttpClient
            var mockHttp = new MockHttpMessageHandler();

            mockHttp.When("https://localhost/article")
                .Respond("text/html", @"<html>
               <head></head>
               <body>
                    <p>This is a paragraph with some text.</p>
                    <p>This is a paragraph with some other text.</p>
               </body>
               </html>");

            var reader = new Reader("https://localhost/article");
            reader.MaxElemsToParse = 1;

            Reader.SetBaseHttpClientHandler(mockHttp);
            Article article = reader.GetArticle();

            Assert.False(article.Completed);
            Assert.Equal("Aborting parsing document; 5 elements found", article.Errors[0].Message);
        }

        [Fact]
        public void CheckRelativeURI()
        {
            var parser = new HtmlParser(new HtmlParserOptions());
            var doc = parser.ParseDocument(@"<html>
               <head><link rel=""alternate"" hreflang=""de"" href=""/dummy-de/dummy1/test"" title=""German | German""></head>
               <body>
                    <p>This is a paragraph with some text.</p>
                    <p>This is a paragraph with some other text.</p>
                    <p>This is a paragraph with an image <a href='/dummy-de/dummy1/test'>Hello</a>.</p>
               </body>
               </html>");
            
            var result = Readability.GetArticleMetadata(doc, new Uri("https://localhost/"), "en",
                    new Dictionary<string, string>());
            Assert.Single(result.AlternativeLanguageUris);
            Assert.Equal(new Uri("https://localhost/dummy-de/dummy1/test"), result.AlternativeLanguageUris["de"]);
        }

        [Fact]
        public async Task CheckCancellationIsHonored()
        {
            // setting up mocking HttpClient
            var mockHttp = new MockHttpMessageHandler();

            mockHttp.When("https://localhost/article")
                .Respond("text/html", @"<html>
               <head></head>
               <body>" +
                    string.Concat(Enumerable.Repeat(@"<p>This is a paragraph with some text.</p>
                    <p>This is a paragraph with some other text.</p>", 10000)) +
               @"</body>
               </html>");

            var reader = new Reader("https://localhost/article");

            CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(10));
            // Wait to ensure activation of CancellationToken
            Thread.Sleep(100);

            Reader.SetBaseHttpClientHandler(mockHttp);            
            Article article = await reader.GetArticleAsync(cts.Token);

            Assert.False(article.Completed);
            Assert.Equal("The operation was canceled.", article.Errors[0].Message);
        }

        [Fact]
        public async Task CheckCancellationIsHonoredDuringParsing()
        {
            // setting up mocking HttpClient
            var mockHttp = new MockHttpMessageHandler();

            mockHttp.When("https://localhost/article")
                .Respond("text/html", @"<html>
               <head></head>
               <body>" +
                    string.Concat(Enumerable.Repeat(@"<p>This is a paragraph with some text.</p>
                    <p>This is a paragraph with some other text.</p>", 50000)) +
               @"</body>
               </html>");

            var reader = new Reader("https://localhost/article");

            CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));

            Reader.SetBaseHttpClientHandler(mockHttp);
            Article article = await reader.GetArticleAsync(cts.Token);

            // The operation it is cancelled if the cancellation happens during parsing,
            // which is the important thing. However, the parsing can happen before of later than
            // 30 milliseconds, so it is difficult to make the test reliable
            Assert.False(article.Completed);
            Assert.True("A task was canceled." == article.Errors[0].Message 
                || "The operation was canceled." == article.Errors[0].Message);
        }

        [Fact]
        public async Task CheckCancellationIsHonoredDuringAnalysis()
        {
            // setting up mocking HttpClient
            var mockHttp = new MockHttpMessageHandler();

            mockHttp.When("https://localhost/article")
                .Respond("text/html", @"<html>
               <head></head>
               <body>" +
                    string.Concat(Enumerable.Repeat(@"<p>This is a paragraph with some text.</p>
                    <p>This is a paragraph with some other text.</p>", 50000)) +
               @"</body>
               </html>");

            var reader = new Reader("https://localhost/article");

            CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(2000));

            Reader.SetBaseHttpClientHandler(mockHttp);
            Article article = await reader.GetArticleAsync(cts.Token);

            Assert.False(article.Completed);
            Assert.Equal("The operation was canceled.", article.Errors[0].Message);
            Assert.Equal("System.Private.CoreLib", article.Errors[0].Source);
        }
    }
}