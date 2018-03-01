using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;

using Newtonsoft.Json.Linq;

using RedFox.WebAPI.Security;

using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;


namespace RedFox.WebAPI.Controllers
{
    public class PasswordsController : ApiController
    {
        public async Task<HttpResponseMessage> Put(string id, [FromBody] JObject value)
        {
            try
            {
                var manager = Request.GetOwinContext().GetUserManager<ApplicationUserManager>();
                var user    = await manager.FindByIdAsync(id);

                if (user == null)
                {
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                }

                user.PasswordHash = manager.PasswordHasher.HashPassword(value["Password"].Value<string>());

                var result = await manager.UpdateAsync(user);

                if (result.Succeeded)
                {
                    return Request.CreateResponse(HttpStatusCode.OK);
                }
                else
                {
                    return Request.CreateResponse(HttpStatusCode.InternalServerError, result.Errors);
                }
            }
            catch (Exception e)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, e);
            }
        }
    }

    public class RecoveryController : ApiController
    {
        public async Task<HttpResponseMessage> Get(string emailId,string siteurl)
        {
            try
            {
                var manager = Request.GetOwinContext().GetUserManager<ApplicationUserManager>();
                var user    = manager.FindByEmail(emailId);

                if (manager.FindById(user.Id) == null || user == null)
                {
                    return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "User not found");
                }

                var provider    = new CustomAuthorizationServerProvider();
                var token       = await manager.GeneratePasswordResetTokenAsync(user.Id);
                var callbackUrl = siteurl + "/Account/ResetPassword?UserId=" + System.Web.HttpUtility.UrlEncode(user.Id) + "&token=" + System.Web.HttpUtility.UrlEncode(token);

                await manager.SendEmailAsync(user.Id.ToString(), "Reset Password", "Please reset your password<br> by clicking here: <a href='http://"+callbackUrl+"'>link</a>");

                return Request.CreateResponse(HttpStatusCode.OK, callbackUrl, "application/json");
            }
            catch (Exception e)
            {
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, e);
            }
        }
    }

    public class SendConfirmEmailController : ApiController
    {
        public async Task<HttpResponseMessage> Get(string emailId, string siteurl)
        {
            try
            {
                var manager = Request.GetOwinContext().GetUserManager<ApplicationUserManager>();
                var user    = manager.FindByEmail(emailId);

                if (manager.FindById(user.Id) == null || user == null)
                {
                    return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "User not found");
                }

                var provider    = new CustomAuthorizationServerProvider();
                var token       = await manager.GenerateEmailConfirmationTokenAsync(user.Id);
                var callbackUrl = siteurl + "/Account/EmailConfirmed?UserId=" + System.Web.HttpUtility.UrlEncode(user.Id) + "&token=" + System.Web.HttpUtility.UrlEncode(token);

                await manager.SendEmailAsync(user.Id.ToString(), "Confirm your account", "Please confirm your account by clicking : <a href='http://" + callbackUrl + "'>link</a>");

                return Request.CreateResponse(HttpStatusCode.OK, callbackUrl, "application/json");
            }
            catch (Exception e)
            {
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, e);
            }
        }
    }

    public class ConfirmEmailController : ApiController
    {
        public HttpResponseMessage Get(string userId, string token)
        {
            try
            {
                var tokendecode = System.Web.HttpUtility.UrlDecode(token);
                var UserManager = Request.GetOwinContext().GetUserManager<ApplicationUserManager>();
                var result      = UserManager.ConfirmEmail(userId, token);

                if (result.Succeeded)
                {
                    return Request.CreateResponse(HttpStatusCode.OK);
                }
                else
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, result.Errors);
                }
            }
            catch (Exception e)
            {
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, e);
            }
        }
    }

    public class ChangePasswordController : ApiController
    {
        public HttpResponseMessage Get(string userId,string token,string newPassword)
        {
            try
            {
                var tokendecode = System.Web.HttpUtility.UrlDecode(token);
                var UserManager = Request.GetOwinContext().GetUserManager<ApplicationUserManager>();
                var result      = UserManager.ResetPassword(userId, token, newPassword);

                if (result.Succeeded)
                {
                    return Request.CreateResponse(HttpStatusCode.OK);
                }
                else
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, result.Errors);
                }
            }
            catch (Exception e)
            {
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, e);
            }
        }
    }
}
