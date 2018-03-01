using log4net;
using RedFox.Shared.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Text;
using System.Text.RegularExpressions;

namespace RedFox.Converters.CEA608
{
    [Export(typeof(ICaptionConverter)), ExtensionMetadata("CEA608", "1.0")]
    public class CaptionConverter : ICaptionConverter
    {
        private ILog logger = LogManager.GetLogger("Session");

        private static Dictionary<char, byte> map;

        static CaptionConverter()
        {
            // These are the differences from the ASCII table; see https://en.wikipedia.org/wiki/EIA-608
            map = new Dictionary<char, byte>
            {
                // á
                { (char)225, 42   },
                // é
                { (char)233, 92   },
                // í
                { (char)237, 94   },
                // ó
                { (char)243, 95   },
                // ú
                { (char)250, 96   },
                // ç
                { (char)231, 123  },
                // ÷
                { (char)247, 124  },
                // Ñ
                { (char)209, 125  },
                // ñ
                { (char)241, 126  },
                // SB, █ 
                { (char)9608, 127 }
            };
        }

        public IEnumerable<byte[]> Convert(string text, Encoding encoding)
        {
            text = Regex.Replace(text, "(%hesitation)", "...", RegexOptions.IgnoreCase);
            
            // Make sure each line is not more than 32 characters 
            int    index    = 0,
                   start    = 0,
                   position = 0,
                   count    = 0,
                   charsmax = 32;

            List<string> lines = new List<string>();

            while (index < text.Length)
            {
                if (char.IsWhiteSpace(text[index]))
                {
                    position = index;
                }

                if (index == text.Length - 1 && count > 0)
                {
                    position = index;
                    count    = charsmax;
                }

                if (count >= charsmax)
                {
                    var length = position - start;
                    var line   = text.Substring(start, length).ToUpper();

                    index = position;
                    start = position + 1;
                    count = 0;

                    // Return a line (max 32 chars)
                    yield return Prepare(line, encoding);
                }

                index++;
                count++;
            }
        }

        private byte[] Prepare(string text, Encoding encoding)
        {
            byte[] result = null;

            try
            {
                var bytes = encoding.GetBytes(text);
                var chars = encoding.GetChars(bytes);

                result = new byte[text.Length];
                
                for (var i = 0; i < text.Length; i++)
                {
                    try
                    {
                        if (map.ContainsKey(chars[i]))
                        {
                            result[i] = map[chars[i]];
                        }
                        else
                        {
                            result[i] = Encoding.Convert(encoding, Encoding.ASCII, encoding.GetBytes(chars, i, 1))[0];
                        }
                    }
                    catch (OverflowException e)
                    {
                        logger.Error(
                            String.Format(
                                "Could not convert this char: {0}. Currently only Basic North American character set is supported", chars[i]), e);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error("Could not convert this text: " + text, ex);
            }
            
            return result;
        }
    }
}
