using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RedFox.Web.Controllers.Api;

namespace RedFox.Web.Tests.Controllers.Api
{
    [TestClass]
    public class SessionsControllerTest
    {
        [TestMethod]
        public void Get()
        {
            var controller = new SessionsController();
            var result     = controller.Get();

            Assert.IsInstanceOfType(result, typeof(Array));

            var array = (string[]) result;

            Assert.IsTrue(array.Length >= 0);
        }

        [TestMethod]
        public void Post()
        {
            var controller = new SessionsController();
            var formData   = "";

            controller.Post(formData);

            var session    = controller.Get(0);

            Assert.IsNotNull(session);

        }
    }
}
