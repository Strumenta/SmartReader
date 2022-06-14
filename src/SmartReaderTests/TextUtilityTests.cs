using Xunit;
using SmartReader;

namespace SmartReaderTests
{
    public class TextUtilityTests
    {
        [Fact]
        public void TestCountWordsSeperatedByComma()
        {
            Assert.Equal(2, TextUtility.CountWordsSeparatedByComma("hello,world"));
        }
    }
}

