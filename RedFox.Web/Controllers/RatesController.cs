using System.Web.Mvc;

namespace RedFox.Web.Controllers
{
    public class RatesController : Controller
    {
        // GET: Rates
        public ActionResult Index()
        {
            return View();
        }

        // GET: Rates/Edit/5
        public ActionResult Edit(int? id)
        {
            return View();
        }

        // GET: Rates/Create
        public ActionResult Create()
        {
            return View();
        }
    }
}