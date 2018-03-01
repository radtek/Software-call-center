using Microsoft.Owin.Security.Infrastructure;

using System;

namespace RedFox.WebAPI.Security
{
    public class CustomRefreshTokenProvider : AuthenticationTokenProvider
    {
        public override void Create(AuthenticationTokenCreateContext context)
        {
            context.Ticket.Properties.ExpiresUtc   = new DateTimeOffset(DateTime.UtcNow.AddMinutes(30));
            context.Ticket.Properties.AllowRefresh = true;

            context.SetToken(context.SerializeTicket());
        }

        public override void Receive(AuthenticationTokenReceiveContext context)
        {
            context.DeserializeTicket(context.Token);
        }
    }
}
