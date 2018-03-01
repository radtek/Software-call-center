using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Transfer;

using log4net;

using NAudio.Wave;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RedFox.Shared.Interfaces;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Configuration;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.Recorders.WavRecorder
{
    [Export(typeof(IRecorder)), ExtensionMetadata("Wave", "1.0")]
    public class Recorder : IRecorder
    {
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

        private static ILog   systemLog = LogManager.GetLogger("System");
        private static string prefix    = "IRecorder [Wave 1.0]:";

        private WaveFormat     format;
        private WaveFileWriter writer;

        private bool   recording;
        private string path;
        private string filename;
        private string bucket;
        private int    sessionId;

        public void Start(int sessionId)
        {
            this.sessionId = sessionId;

            var location  = Assembly.GetEntryAssembly().Location;
            var directory = Path.GetDirectoryName(location);

            filename  = string.Format("Recordings/{0}.wav", sessionId);
            path      = string.Format(@"{0}/{1}", directory, filename);
            format    = WaveFormat.CreateMuLawFormat(8000, 1);
            writer    = new WaveFileWriter(filename, format);
            recording = true;
        }

        public async void Record(byte[] data, int count)
        {
            if (!recording) return;

            try
            { 
                await writer.WriteAsync(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                systemLog.WarnFormat("{0} Could not write data", prefix);
                systemLog.Debug(ex.Message);
                systemLog.Debug(ex.StackTrace);
            }
        }

        public async void Stop()
        {
            recording = false;

            await writer.FlushAsync();

            writer.Close();
            writer.Dispose();

            Upload();
        }

        private void Upload()
        {
            bucket = ConfigurationManager.AppSettings["RedFox.Recorders.WavRecorder.S3.Bucket"];

            var accessKey   = ConfigurationManager.AppSettings["RedFox.Recorders.WavRecorder.S3.AccessKey"];
            var secretKey   = ConfigurationManager.AppSettings["RedFox.Recorders.WavRecorder.S3.SecretKey"];
            var credentials = new BasicAWSCredentials(accessKey, secretKey);
            var client      = new AmazonS3Client(credentials, RegionEndpoint.USEast1);
            var utility     = new TransferUtility(client);
            var request     = new TransferUtilityUploadRequest {  BucketName = bucket, FilePath = path, Key = filename };

            request.UploadProgressEvent += Request_UploadProgressEvent;

            try
            { 
                utility.UploadAsync(request);
            }
            catch (Exception ex)
            {
                request.UploadProgressEvent -= Request_UploadProgressEvent;
                request = null;
                
                systemLog.ErrorFormat("{0} Could not upload {1} to to S3 bucket {2}", prefix, path, bucket);
                systemLog.Debug(ex.Message);
                systemLog.Debug(ex.StackTrace);
            }
        }

        private async void Request_UploadProgressEvent(object sender, UploadProgressArgs e)
        {
            if (e.PercentDone < 100) return;
            
            Token token = null;

            var url = string.Format(@"{0}/", ConfigurationManager.AppSettings["RedFox.Recorders.WavRecorder.API.BaseAddr"]);
            var uri = new Uri(url);
            
            using (var httpClient = new HttpClient())
            {
                var request = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "password"),
                    new KeyValuePair<string, string>("username", ConfigurationManager.AppSettings["RedFox.Recorders.WavRecorder.API.Username"]),
                    new KeyValuePair<string, string>("password", ConfigurationManager.AppSettings["RedFox.Recorders.WavRecorder.API.Password"])
                });

                try
                {
                    var result  = httpClient.PostAsync(ConfigurationManager.AppSettings["RedFox.Recorders.WavRecorder.API.TokenUrl"], request).Result;
                    var content = result.Content.ReadAsStringAsync().Result;

                    token         = JsonConvert.DeserializeObject<Token>(content);
                    token.Expires = new DateTime().AddSeconds(token.expires_in);
                }
                catch (Exception ex)
                {
                    systemLog.ErrorFormat("{0} Error obtaining auth token", prefix);
                    systemLog.Debug(ex.Message);
                    systemLog.Debug(ex.StackTrace);
                }
            }

            using (var httpClient = new HttpClient())
            {
                dynamic json = new JObject();

                json.Path = string.Format("https://s3.amazonaws.com/{0}/{1}", bucket, filename);

                var data    = JsonConvert.SerializeObject(json);
                var content = new StringContent(data, Encoding.UTF8, "application/json");
                
                httpClient.BaseAddress = uri;
                httpClient.DefaultRequestHeaders.Accept.Clear();
                httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + token.access_token);

                try
                {
                    var response = await httpClient.PutAsync(sessionId.ToString(), content);

                    if (!response.IsSuccessStatusCode)
                    {
                        systemLog.ErrorFormat("{0} Could not update session; response {1}", prefix, response.StatusCode);
                        systemLog.ErrorFormat("{0}", response.Content.ReadAsStringAsync().Result);
                    }
                }
                catch (Exception ex)
                {
                    systemLog.Error(prefix + " Error in POST to Recordings API", ex);
                }
            }

            bool.TryParse(ConfigurationManager.AppSettings["RedFox.Recorders.WavRecorder.S3.KeepLocal"], out bool keep);
            
            if (!keep) Task.Run(() => Delete(e.FilePath));
        }

        private void Delete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex)
            {
                systemLog.ErrorFormat("{0} Could not delete {1}", prefix, path);
                systemLog.Debug(ex.Message);
                systemLog.Debug(ex.StackTrace);
            }
        }
    }
}
