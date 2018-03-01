using System;
using System.Linq;

using Quartz;

using log4net;

using RedFox.Core;
using RedFox.Controller.Messages;
using RedFox.Shared;
using System.Data.Entity;

namespace RedFox.Controller.Scheduler
{
    public class TranscribeJob : IJob
    {
        private static ILog systemLog  = LogManager.GetLogger("System");
        private static ILog sessionLog = LogManager.GetLogger("Session");

        public void Execute(IJobExecutionContext context)
        {
            var detail    = context.JobDetail;
            var sessionId = int.Parse(detail.Key.Name);
            var entities  = new Entities();

            entities.Configuration.ProxyCreationEnabled = false;

            var session = entities.Sessions
                .Include(s => s.Station)
                .Include(s => s.Encoder)
                .FirstOrDefault(s => s.Id == sessionId);
         
            var scheduler = context.Scheduler;
            var next      = context.NextFireTimeUtc;
            var servers   = entities.Servers.Where(t => 
                t.Active &&
                t.MaxSessions > t.Sessions.Count &&
                DbFunctions.DiffSeconds(t.Seen, DateTime.UtcNow) <= t.TTL 
            ).OrderBy(t =>
                t.Sessions.Count
            );

            if (servers.Count() == 0)
            {
                var warning = $"There are no servers available that can execute the requested job for session { sessionId }";

                sessionLog.Warn(warning);
                 systemLog.Warn(warning);

                return;
            }

            // Pick the server with the least number of ongoing sessions
            var server = servers.First();

            // TODO Remove
            server = entities.Servers.Find(16);

            entities.Configuration.ProxyCreationEnabled = true;

            // Tell that server to start a new session
            var message       = new Composer().SessionStartRequest(session);
            var messageClient = Extensions.Instance.MessageClient.Value;

            messageClient.Send(server.IPv4, message);

            // TODO Hook onto messages coming from that server? 
            //      Won't this come via the message client? 

            
        }
    }
}
