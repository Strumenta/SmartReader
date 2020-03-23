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

namespace SmartReaderTests
{
    public interface IArticleTest
    {
        Uri Uri { get; set; }
        String Title { get; set; }
        String Byline { get; set; }
        String Dir { get; set; }
        String Content { get; set; }
        String TextContent { get; set; }
        String Excerpt { get; set; }
        String Language { get; set; }
        String Author { get; set; }
        String SiteName { get; set; }
        String FeaturedImage { get; set; }
        int Length { get; set; }
        TimeSpan TimeToRead { get; set; }
        DateTime? PublicationDate { get; set; }
        bool IsReadable { get; set; }
    }

    public class PagesTests
    {
        private readonly ITestOutputHelper _output;
        public PagesTests(ITestOutputHelper output)
        {
            _output = output;            
        }

        public IArticleTest GetTestArticle(JObject metadata, string content)
        {
            var mockArticle = new Mock<IArticleTest>();
            mockArticle.Setup(x => x.Uri).Returns(new Uri("https://localhost/"));
            mockArticle.Setup(x => x.IsReadable).Returns(Boolean.Parse(metadata["readerable"].ToString()));
            mockArticle.Setup(x => x.Title).Returns(metadata["title"].ToString());
            mockArticle.Setup(x => x.Dir).Returns(metadata["dir"]?.ToString() ?? "");
            mockArticle.Setup(x => x.Byline).Returns(metadata["byline"]?.ToString() ?? "");
            mockArticle.Setup(x => x.Author).Returns(String.IsNullOrEmpty(metadata["author"]?.ToString()) ? null : metadata["author"].ToString());
            mockArticle.Setup(x => x.PublicationDate).Returns(String.IsNullOrEmpty(metadata["publicationDate"]?.ToString()) ? (DateTime?) null : DateTime.Parse(metadata["publicationDate"].ToString()));
            mockArticle.Setup(x => x.Language).Returns(String.IsNullOrEmpty(metadata["language"]?.ToString()) ? null : metadata["language"].ToString());			
            mockArticle.Setup(x => x.Excerpt).Returns(metadata["excerpt"]?.ToString() ?? "");
            mockArticle.Setup(x => x.SiteName).Returns(metadata["siteName"]?.ToString() ?? "");
            mockArticle.Setup(x => x.TimeToRead).Returns(TimeSpan.Parse(metadata["timeToRead"].ToString()));
            mockArticle.Setup(x => x.Content).Returns(content);
            mockArticle.Setup(x => x.FeaturedImage).Returns(metadata["featuredImage"]?.ToString() ?? "");

            return mockArticle.Object;
        }

        private void UpdateExpectedJson(Article article, string directory)
        {
            var obj = new
            {
                title = article.Title,
                byline = article.Byline,
                dir = article.Dir,
                excerpt = article.Excerpt,
                readerable = article.IsReadable,
                language = article.Language,
                timeToRead = article.TimeToRead,
                publicationDate = article.PublicationDate,
                author = article.Author,
                siteName = article.SiteName,
                featuredImage = article.FeaturedImage
            };

            File.WriteAllText(Path.Combine(directory, @"expected-metadata.json"), JsonConvert.SerializeObject(obj, Formatting.Indented));
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
            Assert.Equal(expected.Content, found.Content);
            Assert.Equal(expected.FeaturedImage, found.FeaturedImage);
        }

        public static IEnumerable<object[]> GetTests()
        {            
            foreach (var d in Directory.EnumerateDirectories(@"..\..\..\test-pages\"))
            {                
                yield return new object[] { d };               
            }
        }

        [Theory]
        [MemberData(nameof(GetTests))]
        public void TestPages(string directory)
        {
            var sourceContent = File.ReadAllText(Path.Combine(directory, @"source.html"));
            var expectedContent = File.ReadAllText(Path.Combine(directory, @"expected.html"));
            var expectedMetadataString = File.ReadAllText(Path.Combine(directory, @"expected-metadata.json"));            
            var expectedMetadata = JObject.Parse(expectedMetadataString); 
            
            Article found = Reader.ParseArticle("https://localhost/", sourceContent);
            
            IArticleTest expected = GetTestArticle(expectedMetadata, expectedContent);       

            AssertProperties(expected, found);
        }        
    }
}