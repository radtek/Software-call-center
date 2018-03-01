using log4net;

using Quartz;
using Quartz.Impl;

using RedFox.Controller.Messages;
using RedFox.Messaging;
using RedFox.Shared;

using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Reflection;
using System.ServiceProcess;

namespace RedFox.Controller
{
    public partial class Service : ServiceBase
    {
        private static ILog   logger = LogManager.GetLogger("System");
        private static string LOG_PREFIX;

        private CompositionContainer container;
        private IScheduler           scheduler;
        private IMessageClient       messageClient;
        private WebAPI.Host          webAPI;

        public Service()
        {
            InitializeComponent();

            log4net.Config.XmlConfigurator.Configure();
            
            var assembly = Assembly.GetExecutingAssembly();
            var name     = assembly.GetName();
            var date     = new FileInfo(assembly.Location).LastWriteTimeUtc;

            logger.Info($"Red Fox Controller version { name.Version } - { date.ToShortDateString() }");
            logger.Info($"Copyright { date.Year } (C) Country World Productions Inc. All Rights Reserved");
            logger.Info("");

            LOG_PREFIX = string.Format("Controller [{0} {1}]", name.Name, name.Version);
        }

        public void Start()
        {
            OnStart(null);
        }

        protected override void OnStart(string[] args)
        {
            logger.Debug($"{ LOG_PREFIX } Initializing new extensions catalog");

            var extensions = Extensions.Instance;
            var catalog    = new AggregateCatalog();
            var path       = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var folder     = string.Format("{0}/Extensions/", path);
            var directory  = new DirectoryCatalog(folder);

            catalog.Catalogs.Add(directory);

            container = new CompositionContainer(catalog);
            container.ComposeParts(extensions);

            // Scheduler
            logger.Debug($"{ LOG_PREFIX } Starting scheduler");

            scheduler = StdSchedulerFactory.GetDefaultScheduler();
            scheduler.Start();

            // TODO Schedule a job for sync with Pulse
            //var job     = JobBuilder.Create<PulseSyncJob>().Build();
            //var trigger = TriggerBuilder.Create().StartNow().WithSimpleSchedule(t => t.WithIntervalInHours(1).RepeatForever()).Build();

            //StdSchedulerFactory.GetDefaultScheduler().ScheduleJob(job, trigger);
            
            // Web API
            logger.Debug($"{ LOG_PREFIX } Starting Web API host");

            webAPI = new WebAPI.Host();
            webAPI.Start();

            // Messaging
            logger.Debug($"{ LOG_PREFIX } Starting messaging client");

            // TODO Choose message client from config
            extensions.MessageClient.Value.MessageReceived += MessageClient_MessageReceived;
            extensions.MessageClient.Value.Init("controller");
            extensions.MessageClient.Value.Listen();
        }

        protected override void OnStop()
        {
            if (scheduler != null)
            {
                logger.Debug($"{ LOG_PREFIX } Shutdown scheduler");
                scheduler.Shutdown();
            }

            scheduler = null;

            if (messageClient != null)
            {
                logger.Debug($"{ LOG_PREFIX } Shutdown message client");
                messageClient.Shutdown();
            }

            messageClient = null;

            logger.Info($"{ LOG_PREFIX } Out of sight; out of mind");
        }

        private void MessageClient_MessageReceived(object sender, MessageEventArgs e)
        {
            new IncomingMessage().Handle(e.Sender, e.Message);
        }
    }
}
