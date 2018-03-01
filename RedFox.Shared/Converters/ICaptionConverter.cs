using RedFox.Shared.Interfaces;

namespace RedFox.Converters
{
    public interface ICaptionConverter : IRedFox
    {
        System.Collections.Generic.IEnumerable<byte[]> Convert(string text, System.Text.Encoding encoding);
    }
}
