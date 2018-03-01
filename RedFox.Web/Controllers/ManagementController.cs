using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace RedFox.Web.Controllers
{
    public class ManagementController : Controller
    {
        // GET: Rates
        public ActionResult Rates()
        {
            return View();
        }

        // GET: Rates
        public ActionResult Accounts()
        {
            return View();
        }
    }
}