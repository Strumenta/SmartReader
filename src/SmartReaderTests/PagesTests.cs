using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Moq;
using SmartReader;
using Xunit;
using Xunit.Abstractions;

namespace SmartReaderTests
{
    public class PagesTests
    {
        private readonly ITestOutputHelper _output;
        public PagesTests(ITestOutputHelper output)
        {
            _output = output;
        }

        public IArticleTest GetTestArticle(ArticleMetadata metadata, string content)
        {
            var mockArticle = new Mock<IArticleTest>();
            mockArticle.Setup(x => x.Uri).Returns(new Uri("https://localhost/"));
            mockArticle.Setup(x => x.IsReadable).Returns(metadata.Readerable);
            mockArticle.Setup(x => x.Title).Returns(metadata.Title);
            mockArticle.Setup(x => x.Dir).Returns(metadata.Dir);
            mockArticle.Setup(x => x.Byline).Returns(metadata.Byline ?? "");
            mockArticle.Setup(x => x.Author).Returns(string.IsNullOrEmpty(metadata.Author) ? null : metadata.Author);
            mockArticle.Setup(x => x.PublicationDate).Returns(string.IsNullOrEmpty(metadata.PublicationDate) ? (DateTime?)null : DateTime.Parse(metadata.PublicationDate.ToString()));
            mockArticle.Setup(x => x.Language).Returns(string.IsNullOrEmpty(metadata.Language) ? null : metadata.Language.ToString());
            mockArticle.Setup(x => x.Excerpt).Returns(metadata.Excerpt ?? "");
            mockArticle.Setup(x => x.SiteName).Returns(metadata.SiteName ?? "");
            mockArticle.Setup(x => x.TimeToRead).Returns(TimeSpan.Parse(metadata.TimeToRead ?? "0"));
            mockArticle.Setup(x => x.Content).Returns(content);
            mockArticle.Setup(x => x.FeaturedImage).Returns(metadata.FeaturedImage ?? "");

            return mockArticle.Object;
        }

        private void UpdateExpectedJson(Article article, string directory)
        {
            var jso = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
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

            File.WriteAllText(Path.Combine(directory, @"expected-metadata.json"), JsonSerializer.Serialize(obj, jso));
        }

        private void UpdateExpectedHtml(string html, string directory)
        {
            File.WriteAllText(Path.Combine(directory, @"expected.html"), html);
        }

        private void AssertProperties(IArticleTest expected, Article found)
        {
            Assert.Equal(expected.IsReadable, found.IsReadable);
            Assert.Equal(expected.Title, found.Title);
            Assert.Equal(expected.Dir, found.Dir);
            Assert.Equal(expected.Byline, found.Byline);
            Assert.Equal(expected.Author, found.Author);
            Assert.Equal(expected.PublicationDate?.ToString(), found.PublicationDate?.ToString());
            Assert.Equal(expected.Language, found.Language);
            Assert.Equal(expected.Excerpt, found.Excerpt);
            Assert.Equal(expected.SiteName, found.SiteName);
            Assert.Equal(expected.TimeToRead, found.TimeToRead);
            Assert.Equal(expected.Content, found.Content, ignoreLineEndingDifferences: true);
            Assert.Equal(expected.FeaturedImage, found.FeaturedImage);
        }

        public static IEnumerable<object[]> GetTests()
        {
            foreach (var d in Directory.EnumerateDirectories(Path.Combine("..", "..", "..", "test-pages")))
            {
                yield return new object[] { d };
            }
        }

        [Theory]
        [MemberData(nameof(GetTests))]
        public void TestPages(string directory)
        {
            var jso = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var sourceContent = File.ReadAllText(Path.Combine(directory, @"source.html"));

            Article found = Reader.ParseArticle("https://localhost/", text: sourceContent);

            var expectedContent = File.ReadAllText(Path.Combine(directory, @"expected.html"));
            var expectedMetadataText = File.ReadAllText(Path.Combine(directory, @"expected-metadata.json"));
            var expectedMetadata = JsonSerializer.Deserialize<ArticleMetadata>(expectedMetadataText, jso);

            IArticleTest expected = GetTestArticle(expectedMetadata, expectedContent);

            AssertProperties(expected, found);
        }

    }
}