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

        [Fact]
        public void TestAttributeNameIsCleanedCorrectly()
        {
            Assert.Equal("correct", TextUtility.CleanXmlName("1correct"));
            Assert.Equal("valid", TextUtility.CleanXmlName("123valid"));
            Assert.Equal("", TextUtility.CleanXmlName("1234"));
        }
    }
}

