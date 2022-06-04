using Xunit;
using SmartReader;
using System;

namespace SmartReaderTests
{
    public class UriExtensionsTests
    {
        [Fact]
        public void TestToAbsoluteURIDoesNotCrashWithEmptyURI()
        {
            var uri = new Uri("https://example.org/");
            Assert.Equal("https://example.org/", uri.ToAbsoluteURI(""));
        }
    }
}
