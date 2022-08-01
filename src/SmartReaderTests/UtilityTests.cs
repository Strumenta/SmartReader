using SmartReader;
using Xunit;

namespace SmartReaderTests
{
    public class UtilityTests
    {
        [Fact]
        public void TestStyleWithMultipleVisibilityTermsReturnsCorrectVisibility()
        {
            var style = "background-color: rgb(255, 255, 255); border: 1px solid rgb(204, 204, 204); box-shadow: rgba(0, 0, 0, 0.2) 2px 2px 3px; position: absolute; transition: visibility 0s linear 0.3s, opacity 0.3s linear 0s; opacity: 0; visibility: hidden; z-index: 2000000000; left: 0px; top: -10000px;";

            Assert.Equal("hidden", NodeUtility.GetVisibilityFromStyle(style).ToString());
        }
    }
}
