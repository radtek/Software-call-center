using RedFox.Shared.Interfaces;

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Text;
using System.Text.RegularExpressions;

namespace RedFox.Converters.Mock
{
    [Export(typeof(ICaptionConverter)), ExtensionMetadata("Mock", "1.0")]
    public class CaptionConverter : ICaptionConverter
    {
        public IEnumerable<byte[]> Convert(string text, Encoding encoding)
        {
            text = Regex.Replace(text, "(%hesitation)", "...", RegexOptions.IgnoreCase);

            return new List<byte[]>
            {
                Encoding.Convert(encoding, Encoding.ASCII, encoding.GetBytes(text.ToUpper()))
            };
        }
    }
}
