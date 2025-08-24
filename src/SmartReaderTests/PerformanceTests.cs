using AngleSharp.Html.Parser;
using RichardSzalay.MockHttp;
using SmartReader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace SmartReaderTests
{
    public class PerformanceTests
    {
        [Fact]
        public void TestGetArticleDoesNotHang()
        {
            // setting up mocking HttpClient
            var mockHttp = new MockHttpMessageHandler();
            var sourceContent = File.ReadAllText(Path.Combine("..", "..", "..", "test-performance", @"testFile.html"));
            mockHttp.When("https://localhost/article")
                .Respond("text/html", sourceContent);

            var reader = new Reader("https://localhost/article");

            Reader.SetBaseHttpClientHandler(mockHttp);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            Article article = reader.GetArticle();                       
            
            watch.Stop();
            var elapsedMs = watch.ElapsedMilliseconds;

            Assert.True(article.Completed);
            Assert.True(elapsedMs < 5000);
        }
    }
}
