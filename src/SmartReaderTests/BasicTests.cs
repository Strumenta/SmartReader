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
        int Length { get; set; }
        TimeSpan TimeToRead { get; set; }
        DateTime? PublicationDate { get; set; }
        bool IsReadable { get; set; }
    }

    public class BasicTests
    {
        private readonly ITestOutputHelper _output;
        public BasicTests(ITestOutputHelper output)
        {
            _output = output;
        }

        public IArticleTest GetTestArticle(JObject metadata)
        {
            var mockArticle = new Mock<IArticleTest>();
            mockArticle.Setup(x => x.Uri).Returns(new Uri("https://localhost/"));
            mockArticle.Setup(x => x.IsReadable).Returns(Boolean.Parse(metadata["readerable"].ToString()));
            mockArticle.Setup(x => x.Title).Returns(metadata["title"].ToString());
            mockArticle.Setup(x => x.Dir).Returns(metadata["dir"]?.ToString() ?? "");
            mockArticle.Setup(x => x.Byline).Returns(metadata["byline"]?.ToString() ?? "");
            //mockArticle.Setup(x => x.Author).Returns(sr.ReadLine());
            //mockArticle.Setup(x => x.PublicationDate).Returns(DateTime.Par(sr.ReadLine  ()));
            //mockArticle.Setup(x => x.Language).Returns(sr.ReadLine());			
            mockArticle.Setup(x => x.Excerpt).Returns(metadata["excerpt"]?.ToString() ?? "");
            //mockArticle.Setup(x => x.TimeToRead).Returns(TimeSpan.Pars(sr.ReadLin()));

            return mockArticle.Object;
        }

        private void AssertProperties(IArticleTest expected, Article found)
        {
            Assert.Equal(expected.IsReadable, found.IsReadable);
            Assert.Equal(expected.Title, found.Title);
            Assert.Equal(expected.Dir, found.Dir);
            Assert.Equal(expected.Byline, found.Byline);
            //Assert.Equal(expected.Author, found.Author);
            //Assert.Equal(expected.PublicationDate, found.PublicationDate);
            //Assert.Equal(expected.Language, found.Language);			
            Assert.Equal(expected.Excerpt, found.Excerpt);
            //Assert.Equal(expected.TimeToRead, found.TimeToRead);
        }

        public static IEnumerable<object[]> GetTests()
        {
            foreach (var d in Directory.EnumerateDirectories(@"..\..\..\test-pages"))
            {
                yield return new object[] { d };               
            }
        }

        [Theory]
        [MemberData(nameof(GetTests))]
        public void TestPages(string directory)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var sourceContent = File.ReadAllText(Path.Combine(directory, @"source.html"));
            var expectedContent = File.ReadAllText(Path.Combine(directory, @"expected.html"));
            var expectedMetadataString = File.ReadAllText(Path.Combine(directory, @"expected-metadata.json"));
            var expectedMetadata = JObject.Parse(expectedMetadataString);            
            Article found = Reader.ParseArticle("https://localhost/", sourceContent);

            IArticleTest expected = GetTestArticle(expectedMetadata);

            AssertProperties(expected, found);        
        }
    }
}