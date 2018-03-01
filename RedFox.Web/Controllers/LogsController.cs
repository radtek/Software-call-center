using System.Web.Mvc;

namespace RedFox.Web.Controllers
{
    public class LogsController : Controller
    {
        // GET: Logs
        public ActionResult Audit()
        {
            return View();
        }
    }
}