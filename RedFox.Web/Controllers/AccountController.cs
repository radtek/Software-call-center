using log4net; 

using System.Linq;
using System.Threading.Tasks;

using System.Web;
using System.Web.Mvc;

using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;

using RedFox.Web.Models;

namespace RedFox.Web.Controllers
{
    public class AccountController : Controller
    {
        private static ILog _audit = LogManager.GetLogger("Audit");

        public ActionResult UserProfile()
        {
            return View();
        }

        public ActionResult Login()
        {
            return View();
        }

        public ActionResult ForgotPassword()
        {
            return View();
        }

        public ActionResult ResetPassword()
        {
            return View();
        }

        public ActionResult EmailConfirmed()
        {
            return View();
        }
    }
}