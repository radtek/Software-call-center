using System.Web.Mvc;

namespace RedFox.Web.Controllers
{
    public class AccountsController : Controller
    {
        // GET: Accounts
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult Edit(string id)
        {
            return View();
        }

        public ActionResult Create()
        {
            return View();
        }
    }
}