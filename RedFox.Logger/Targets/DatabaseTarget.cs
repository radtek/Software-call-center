using log4net.Core;

using System.ComponentModel.Composition;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace RedFox.Notifications.Targets
{
    [Export(typeof(INotificationTarget)), NotificationMetadata("Database", "1.0", NotificationType.Self)]
    public class DatabaseTarget : INotificationTarget
    {
        public string[] Recipients { get; set; }

        public string Subject { get; set; }
        public string Message { get; set; }

        public int   ID    { get; set; }
        public Level Level { get; set; }

        public void Send()
        {
            var connectionString = ConfigurationManager.ConnectionStrings["RedFoxDb"].ConnectionString;
            
            using (var connection = new SqlConnection(connectionString))
            { 
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "INSERT INTO Notifications (Level, Subject, Message) VALUES (@level, @subject, @message) SET @Id = SCOPE_IDENTITY();";
                    command.CommandType = CommandType.Text;

                    command.Parameters.Add(new SqlParameter("level", Level.Value));
                    command.Parameters.Add(new SqlParameter("subject", Subject));
                    command.Parameters.Add(new SqlParameter("message", Message));
                    command.Parameters.Add("@Id", SqlDbType.Int, 4).Direction = ParameterDirection.Output;

                    command.Connection.Open();
                    command.ExecuteNonQuery();

                    ID = (int) command.Parameters["@Id"].Value;
                }

                connection.Close();
            }
        }
    }
}
