using log4net;

using RedFox.Core;

using Spire.Pdf;
using Spire.Pdf.Graphics;

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;

namespace RedFox.WebAPI.Controllers
{
    [Authorize]
    public class ArchiveController : ApiController
    {
        private static ILog auditLog  = LogManager.GetLogger("Audit");
        private static ILog systemLog = LogManager.GetLogger("System");

        private static ReaderWriterLockSlim locker = new ReaderWriterLockSlim();

        //[Authorize(Roles = "System")]
        [Authorize(Roles = "Administrator")]
        public async Task<HttpResponseMessage> Post(int id)
        {
            var location  = System.Reflection.Assembly.GetEntryAssembly().Location;
            var directory = Path.GetDirectoryName(location);
            var path      = string.Format(@"{0}/Sessions/{1}.txt", directory, id);
            var bytes     = await Request.Content.ReadAsByteArrayAsync();
            var text      = Encoding.UTF8.GetString(bytes) + Environment.NewLine;
            int index     = 0, count = 0;

            auditLog.DebugFormat("Session script added to {0}", path);

            try
            {
                locker.EnterWriteLock();

                File.AppendAllText(path, text);
            }
            catch (Exception e)
            {
                systemLog.Error("Error on POST to archive", e);
            }
            finally
            {
                locker.ExitWriteLock();
            }

            while (index < text.Length)
            {
                // Check if current char is part of a word
                while (index < text.Length && !char.IsWhiteSpace(text[index]))
                    index++;

                count++;

                // Skip whitespace until next word
                while (index < text.Length && char.IsWhiteSpace(text[index]))
                    index++;
            }

            var entities = new Entities();
            var session = entities.Sessions.SingleOrDefault(s => s.Id == id);

            if (session != null)
            {
                session.WordCount = session.WordCount + count;

                await entities.SaveChangesAsync();
            }

            return Request.CreateResponse(HttpStatusCode.Created);
        }

        public HttpResponseMessage Get(int id)
        {
            // TODO Check if User.Identity is allowed to request this ID
            HttpResponseMessage response = Request.CreateResponse(HttpStatusCode.BadRequest);
            auditLog.DebugFormat("Request for Session script {0} by {1}", id, User.Identity.Name);

            var location  = System.Reflection.Assembly.GetEntryAssembly().Location;
            var directory = Path.GetDirectoryName(location);
            var path      = string.Format(@"{0}/Sessions/{1}.txt", directory, id);
            var text      = "";

            if (!File.Exists(path))
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            using (var reader = new StreamReader(File.OpenRead(path), Encoding.UTF8))
            {
                text = reader.ReadToEnd();
            }

            if (Request.Headers.Accept.ToString() == "application/octet-stream")
            {
                var bytes = Encoding.UTF8.GetBytes(text);

                response = Request.CreateResponse(HttpStatusCode.OK);

                response.Content = new ByteArrayContent(bytes);
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                return response;

                //var stream  = new MemoryStream();

                //var doc     = new PdfDocument();
                //var section = doc.Sections.Add();
                //var page    = section.Pages.Add();
                //var arr     = text.Split("<br />".ToCharArray());
                //int y       = 0;

                //foreach (var item in arr)
                //{
                //    if (item != "")
                //    {
                //        var widget = new PdfTextWidget(
                //            item,
                //            new PdfFont(PdfFontFamily.Courier, 11),
                //            PdfBrushes.Black)
                //        {
                //            StringFormat = new PdfStringFormat { LineSpacing = 20f, WordWrap = PdfWordWrapType.Word }
                //        };

                //        var layout = new PdfTextLayout
                //        {
                //            Break  = PdfLayoutBreakType.FitPage,
                //            Layout = PdfLayoutType.Paginate
                //        };

                //        widget.Draw(page, 0, y);
                //        y += 20;
                //    }
                //}

                //doc.SaveToStream(stream);

                //response = Request.CreateResponse(HttpStatusCode.OK);

                //response.Content = new ByteArrayContent(stream.ToArray());
                //response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                //return response;
            }

            return Request.CreateResponse(HttpStatusCode.OK, text.Replace(Environment.NewLine, @"<br />"));
        }
    }
}
