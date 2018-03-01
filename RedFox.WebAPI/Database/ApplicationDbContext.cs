using Microsoft.AspNet.Identity.EntityFramework;

using RedFox.WebAPI.Models;

namespace RedFox.WebAPI.Database
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext() : base("RedFoxDb", throwIfV1Schema: false)
        { }

        public static ApplicationDbContext Create()
        {
            return new ApplicationDbContext();
        }
    }
}
