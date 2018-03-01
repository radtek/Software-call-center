using System.Web.Mvc;

namespace RedFox.Web.Controllers
{
    public class SessionsController : Controller
    {
        // GET: Sessions
        public ActionResult Index()
        {
            return View();
        }

        // GET: Sessions/Details/5
        public ActionResult Edit(int? id)
        {
            return View();
        }

        public ActionResult Create()
        {
            return View();
        }
    }
}