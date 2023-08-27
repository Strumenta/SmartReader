using Xunit;
using SmartReader;
using System.Text.RegularExpressions;

namespace SmartReaderTests
{
    public class TextUtilityTests
    {
        private static readonly Regex G_RE_Commas = new Regex(@"\u002C|\u060C|\uFE50|\uFE10|\uFE11|\u2E41|\u2E34|\u2E32|\uFF0C", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        [Fact]
        public void TestCountWordsSeparatedByComma()
        {
            Assert.Equal(2, G_RE_Commas.Split("hello,world").Length);
        }

        [Fact]
        public void TestCountWordsSeparatedByMultilingualComma()
        {
            Assert.Equal(3, G_RE_Commas.Split("DeepMind表示，这款名为DNC（可微神经计算机）的AI模型可以接受家谱和伦敦地铁网络地图这样的信息，还可以回答与那些数据结构中的不同项目之间的关系有关的复杂问题。").Length);
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

