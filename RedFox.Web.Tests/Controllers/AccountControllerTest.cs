using Microsoft.VisualStudio.TestTools.UnitTesting;

using RedFox.Web.Controllers;
using RedFox.Web.Models;

using System.Web.Mvc;

namespace RedFox.Web.Tests.Controllers
{
    [TestClass]
    public class AccountControllerTest
    {
        [TestMethod]
        public void Login()
        {
            var returnUrl  = "http://www.google.com";
            var controller = new AccountController();
            var model      = new LoginViewModel()
            {
                Email    = "karel.boek@raskenlud.com",
                Password = "3928Pors"
            };

            var result = controller.Login(returnUrl);

            Assert.IsInstanceOfType(result, typeof(ViewResult));
            
            ((ViewResult) result).ViewData.TryGetValue("ReturnUrl", out object viewResultReturnUrl);

            Assert.AreEqual(viewResultReturnUrl, returnUrl);
        }
    }
}
