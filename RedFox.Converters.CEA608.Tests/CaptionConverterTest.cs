using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RedFox.Converters.CEA608.Tests
{
    [TestClass]
    public class CaptionConverterTest
    {
        [TestMethod]
        public void Convert()
        {
            var input     = "Thé qúíçk browñ fox jumps óver the lázy dog";
            var converter = new CaptionConverter();
            var result    = converter.Convert(input, System.Text.Encoding.UTF8);

            // Check if conversion went well
            Assert.IsNotNull(result);
        }
    }
}
