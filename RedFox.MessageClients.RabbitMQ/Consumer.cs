using RabbitMQ.Client;
using RabbitMQ.Client.Events;

using RedFox.Messaging;
using RedFox.Shared.Interfaces;

using System;
using System.Configuration;
using System.ComponentModel.Composition;
using System.Text;
using System.Net;

using log4net;

namespace RedFox.MessageClients.RabbitMQ
{
    [Export(typeof(IMessageClient)), ExtensionMetadata("RabbitMQ", "1.0")]
    public class Consumer : IMessageClient
    {
        private static string prefix = "IMessageClient [RabbitMQ 1.0]";
        private static ILog   logger = LogManager.GetLogger("System");

        private IModel      channel;
        private IConnection connection;
        private string      id;

        EventingBasicConsumer consumer;

        public event MessageDelegate MessageReceived;

        public void Init(string identity)
        {
            var hostName = ConfigurationManager.AppSettings["RedFox.MessageClients.RabbitMQ.HostName"];
            var userName = ConfigurationManager.AppSettings["RedFox.MessageClients.RabbitMQ.UserName"];
            var password = ConfigurationManager.AppSettings["RedFox.MessageClients.RabbitMQ.Password"];

            connection = new ConnectionFactory { HostName = hostName, Password = password, UserName = userName }.CreateConnection();
            channel    = connection.CreateModel();
            id         = identity;

            if (identity == "")
            {
                var entry       = Dns.GetHostEntryAsync(string.Empty).Result;
                var addressList = entry.AddressList;
                var addresses   = Array.FindAll(addressList, a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

                if (addresses.Length > 0)
                {
                    logger.Debug($"{ prefix } No message client identity set in config; using { addresses[0].ToString() }");

                    identity = addresses[0].ToString();
                }
                else
                {
                    logger.Debug($"{ prefix } No message client identity set in config; using random");

                    identity = Guid.NewGuid().ToString(); 
                }
            }
            
            channel.ExchangeDeclare(
                exchange: "server",
                type    : "direct", 
                durable : true);
        }

        public void Listen()
        {
            var queueName = channel.QueueDeclare().QueueName;
            var broadcast = channel.QueueDeclare().QueueName;

            channel.QueuePurge(queueName);
            channel.QueuePurge(broadcast);

            // One queue for direct messages
            channel.QueueBind(
                queue     : queueName,
                exchange  : "server",
                routingKey: id);

            // One queue for messages to all servers
            channel.QueueBind(
                queue     : broadcast,
                exchange  : "server",
                routingKey: "all");

            consumer = new EventingBasicConsumer(channel);
            consumer.Received += Consumer_Received;

            channel.BasicConsume(
                queue   : queueName,
                autoAck : true,
                consumer: consumer);

            channel.BasicConsume(
                queue   : broadcast,
                autoAck : true,
                consumer: consumer);
        }

        public void Shutdown()
        {
            consumer.Received -= Consumer_Received;

            channel.Close();
            channel.Dispose();

            connection.Close();
            connection.Dispose();
        }

        public void Send(string recipient, string message)
        {
            if (channel == null)
            {
                logger.Error($"{ prefix } Cannot send message; channel unavailable (null)");
                return;
            }

            if (channel.IsClosed || !channel.IsOpen)
            {
                logger.Error($"{ prefix } Cannot send message; channel is closed");
                return;
            }

            try
            { 
                if (channel.IsOpen)
                    channel.BasicPublish(
                        exchange       : "server",
                        routingKey     : recipient,
                        basicProperties: null,
                        body           : Encoding.UTF8.GetBytes(message));
            }
            catch (Exception e)
            {
                logger.Error($"{ prefix } Could not send message");
                logger.Debug(e.Message);
                logger.Debug(e.StackTrace);
            }
        }

        private void Consumer_Received(object sender, BasicDeliverEventArgs e)
        {
            MessageReceived?.Invoke(this, 
                new MessageEventArgs(
                    e.ConsumerTag,
                    Encoding.UTF8.GetString(e.Body)));
        }
    }
}

// TODO Implement encryption