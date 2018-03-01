using log4net;
using Newtonsoft.Json;

using RedFox.Shared.Interfaces;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Configuration;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RedFox.Providers.Archive
{
    [Export(typeof(ICaptionProvider)), ExtensionMetadata("Archive", "1.1")]
    public class CaptionProvider : ICaptionProvider
    {
        private static ILog session = LogManager.GetLogger("Session");
        
        private BlockingCollection<byte[]> collection = new BlockingCollection<byte[]>();

        private class Token
        {
#pragma warning disable 0649
            public string access_token;
            public string refresh_token;
            public int    expires_in;
#pragma warning restore 0649

            public DateTime Expires;

            public bool IsExpired
            {
                get { return DateTime.Now > Expires; }
            }
        }

        public string Endpoint  { get; set; }
        public string Settings  { get; set; }
        public int    SessionId { private get; set; }

        private Token token;
        private byte[] bytes;

        public void Init()
        {
            token = Login();

            var url = string.Format(@"{0}/", ConfigurationManager.AppSettings["RedFox.Providers.Archive.BaseAddr"]);
            var uri = new Uri(url);

            Task.Run(() =>
            {
                while (collection.TryTake(out bytes, Timeout.Infinite))
                {
                    if (token.IsExpired)
                    {
                        Login();
                    }

                    if (bytes != null && bytes.Length > 0)
                    {
                        Post(uri, bytes);
                    }
                }
            });
        }

        public void Send(byte[] data)
        {
            if (data == null || data.Length == 0) return;

            var time    = Encoding.UTF8.GetBytes(DateTime.Now.ToString("HH:mm:ss.fff") + "  ");
            var content = new byte[data.Length + time.Length];

            Array.Copy(time, content, time.Length);
            Array.Copy(data, 0, content, time.Length, data.Length);

            collection.Add(content);
        }

        public void Stop()
        {
            token = null;
        }

        public void Mute()
        {
        }

        public void Unmute()
        {
        }

        private async void Post(Uri uri, byte[] data)
        {
            using (var httpClient = new HttpClient())
            {
                var content = new ByteArrayContent(data);

                content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                httpClient.BaseAddress = uri;
                httpClient.DefaultRequestHeaders.Accept.Clear();
                httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + token.access_token);

                try
                {
                    var response = await httpClient.PostAsync(SessionId.ToString(), content);

                    if (!response.IsSuccessStatusCode)
                    {
                        session.ErrorFormat("Provider could not post transcript; response {0}", response.StatusCode);
                        session.ErrorFormat("{0}", response.Content.ReadAsStringAsync().Result);
                    }
                }
                catch (Exception e)
                {
                    session.Error("ICaptionProvider [Archive 1.1]: Error in POST to Archive API", e);
                }
            }
        }

        private Token Login()
        {
            Token token = null;

            using (var httpClient = new HttpClient())
            {
                var request = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "password"),
                    new KeyValuePair<string, string>("username", ConfigurationManager.AppSettings["RedFox.Providers.Archive.Username"]),
                    new KeyValuePair<string, string>("password", ConfigurationManager.AppSettings["RedFox.Providers.Archive.Password"])
                });

                try
                { 
                    var result  = httpClient.PostAsync(ConfigurationManager.AppSettings["RedFox.Providers.Archive.TokenUrl"], request).Result;
                    var content = result.Content.ReadAsStringAsync().Result;

                    token = JsonConvert.DeserializeObject<Token>(content);
                    token.Expires = new DateTime().AddSeconds(token.expires_in);
                }
                catch (Exception ex)
                {
                    session.Error("ICaptionProvider [Archive 1.1]: Error obtaining auth token", ex);
                }
            }

            return token;
        }
    }
}
