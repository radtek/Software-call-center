using Newtonsoft.Json.Linq;

using RedFox.Providers;
using RedFox.Shared.Interfaces;

using System;
using System.Linq;

namespace RedFox.Core
{
    public partial class EncoderType
    {
        private Lazy<ICaptionProvider, IExtensionMetadata> provider;

        public Lazy<ICaptionProvider, IExtensionMetadata> Provider
        {
            get
            {
                if (provider == null)
                {
                    provider = Shared.Extensions.Instance.CaptionProviders
                        .Where(t => t.Metadata.Name == JObject.Parse(ProviderJson)
                            .SelectToken("Name")
                            .Value<string>())
                        .FirstOrDefault();
                }

                return provider;
            }
        }
    }
}
