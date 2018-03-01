using log4net;

using Newtonsoft.Json.Linq;

using RedFox.Consumers;
using RedFox.Core;
using RedFox.Messaging;
using RedFox.Server.Messages;
using RedFox.Shared;

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using System.Threading;

namespace RedFox.Server
{
    public partial class Service : ServiceBase
    {
        private static ILog   logger = LogManager.GetLogger("System");
        private static string LOG_PREFIX;

        private CompositionContainer container;
        private IMessageClient       messageClient;

        private JObject config;
        private Timer   keepAliveTimer;
        private Timer   registrationTimer;

        private int sendCount, waitCount;

        private List<Tuple<IConsumerMetadata, IConsumer>> consumers = new List<Tuple<IConsumerMetadata, IConsumer>>();

        public Service()
        {
            InitializeComponent();

            log4net.Config.XmlConfigurator.Configure();

            var assembly = Assembly.GetExecutingAssembly();
            var name     = assembly.GetName();
            var date     = new FileInfo(assembly.Location).LastWriteTimeUtc;

            logger.Info($"Red Fox Server version { name.Version } - { date.ToShortDateString() }");
            logger.Info($"Copyright { date.Year } (C) Country World Productions Inc. All Rights Reserved");
            logger.Info("");

            LOG_PREFIX = string.Format("Service [{0} {1}]", name.Name, name.Version);
        }

        public void Start()
        {
            OnStart(null);
        }

        protected override void OnStart(string[] args)
        {
            logger.Debug($"{ LOG_PREFIX } Load configuration from RedFox.config.json");

            try
            {
                // Load configuration values
                config = JObject.Parse(
                    File.ReadAllText(
                        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + @"/RedFox.config.json"));

                logger.Debug($"{ LOG_PREFIX } Configuration loaded succesfully");
            }
            catch (Exception e)
            {
                logger.Error($"{ LOG_PREFIX } Error while trying to load configuration; { e.Message }");
                logger.Debug(e.StackTrace);
            }
            
            // Try ... Catch is handled in the method
            ComposeExtensions();
            
            try
            {
                // Set up messaging
                logger.Debug($"{ LOG_PREFIX } Set up messaging");

                // Initialize the message client
                messageClient = Shared.Extensions.Instance.MessageClient.Value;
                messageClient.Init(config["Server"]["IPv4"].Value<string>());

                // React on incoming messages
                messageClient.MessageReceived += MessageClient_MessageReceived;
                messageClient.Listen();
            }
            catch (Exception e)
            {
                logger.Error($"{ LOG_PREFIX } Error while initializing messaging client");
                logger.Debug(e.Message);
                logger.Debug(e.StackTrace);
            }

            // Try ... Catch is handled in the method
            RegisterServer(config);
        }

        protected override void OnStop()
        {
            logger.Debug($"{ LOG_PREFIX } Force end all Sessions");

            SessionManager.Instance.Crumble();

            logger.Debug($"{ LOG_PREFIX } Stop keepalive timer");

            if (keepAliveTimer != null)
                keepAliveTimer.Change(Timeout.Infinite, Timeout.Infinite);

            logger.Debug($"{ LOG_PREFIX } Unregister server");

            UnregisterServer();

            logger.Debug($"{ LOG_PREFIX } Shut down message client");

            if (messageClient != null)
                messageClient.Shutdown();

            logger.Debug($"{ LOG_PREFIX } Dispose extensions container");

            if (container != null)
                container.Dispose();

            messageClient  = null;
            keepAliveTimer = null;
            container      = null;

            logger.Info($"{ LOG_PREFIX } Good night");
        }

        private void ComposeExtensions()
        {
            logger.Debug($"{ LOG_PREFIX } Compose extensions");
            logger.Debug($"{ LOG_PREFIX } Initializing new extensions catalog");
            
            var catalog    = new AggregateCatalog();
            var path       = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var folder     = string.Format("{0}/Extensions/", path);
            var directory  = new DirectoryCatalog(folder);
            var extensions = Shared.Extensions.Instance;
            var entities   = new Entities();
            
            // Extensions
            logger.Debug($"{ LOG_PREFIX } Extensions folder is " + directory.FullPath);

            try
            {
                catalog.Catalogs.Add(directory);

                container = new CompositionContainer(catalog);
                container.ComposeParts(extensions);

                logger.Info($"{ LOG_PREFIX } Extensions loaded and composed");
            }
            catch (Exception e)
            {
                logger.Error($"{ LOG_PREFIX } Error while loading extensions");
                logger.Debug(e.Message);
                logger.Debug(e.StackTrace);

                if (e.InnerException != null)
                {
                    logger.Debug("");
                    logger.Debug($"{ LOG_PREFIX } Inner exception");
                    logger.Debug(e.InnerException.Message);
                    logger.Debug(e.InnerException.StackTrace);
                }

                if (e is ReflectionTypeLoadException)
                {
                    var typeLoadException = e as ReflectionTypeLoadException;
                    var loaderExceptions  = typeLoadException.LoaderExceptions;

                    logger.Debug("");
                    logger.Debug($"{ LOG_PREFIX } Loader exceptions");

                    foreach (var loaderException in loaderExceptions)
                    {
                        logger.Debug(loaderException.Message);
                        logger.Debug(loaderException.StackTrace);

                        if (loaderException.InnerException != null)
                        {
                            logger.Debug("");
                            logger.Debug($"{ LOG_PREFIX } Inner exception");
                            logger.Debug(loaderException.InnerException.Message);
                            logger.Debug(loaderException.InnerException.StackTrace);

                            if (loaderException.InnerException is ReflectionTypeLoadException)
                            {
                                var innerTypeLoadException = e as ReflectionTypeLoadException;
                                var innerLoaderExceptions = typeLoadException.LoaderExceptions;

                                foreach (var innerLoaderException in innerLoaderExceptions)
                                {
                                    logger.Debug(innerLoaderException.Message);
                                    logger.Debug(innerLoaderException.StackTrace);
                                }
                            }
                        }
                    }
                }
            }
        }

        private void RegisterServer(JObject config)
        {
            // Register server with controller
            logger.Info($"{ LOG_PREFIX } Registering server with controller");

            try
            {
                var extensions = Shared.Extensions.Instance;
                var collection = new List<object>();

                collection.AddRange(extensions.Consumers.Select(
                    t => { return new
                        {
                            t.Metadata.Name,
                            t.Metadata.Version,
                            Direction = t.Metadata.Direction.ToString(),
                            Type      = ExtensionType.Consumer.ToString()
                        };
                }));

                collection.AddRange(extensions.Transcribers.Select(
                    t => { return new
                        {
                            t.Metadata.Name,
                            t.Metadata.Version,
                            Type      = ExtensionType.Transcriber.ToString()
                        };
                }));

                collection.AddRange(extensions.Converters.Select(
                    t => { return new
                        {
                            t.Metadata.Name,
                            t.Metadata.Version,
                            Type      = ExtensionType.Converter.ToString()
                        };
                }));

                collection.AddRange(extensions.CaptionProviders.Select(
                    t => {
                        return new
                        {
                            t.Metadata.Name,
                            t.Metadata.Version,
                            Type    = ExtensionType.Provider.ToString()
                        };
                }));

                collection.AddRange(extensions.Recorders.Select(
                    t => {
                        return new
                        {
                            t.Metadata.Name,
                            t.Metadata.Version,
                            Type    = ExtensionType.Recorder.ToString()
                        };
                }));

                config["Server"]["Extensions"] = JToken.FromObject(collection);
                
                // Send registration message
                messageClient.Send(
                    "controller", 
                    new Composer().RegisterServerRequest(config)
                );

                logger.Info($"{ LOG_PREFIX } Server registration pending");

                // Wait for registrationg confirmation
                registrationTimer = new Timer(CheckRegistration, config, 0, 5000);
            }
            catch (Exception e)
            {
                logger.Fatal($"{ LOG_PREFIX } Error while registering server; { e.Message }");
                logger.Debug(e.StackTrace);
            }
        }

        private void UnregisterServer()
        {
            messageClient.Send("controller", new Composer().UnregisterServerRequest(Core.Server.Instance.Id));
        }

        private void CheckRegistration(object state)
        {
            if (Core.Server.Instance == null || Core.Server.Instance.Id == 0)
            {
                logger.Debug($"{ LOG_PREFIX } Waiting for registration confirmation from Controller (" + (waitCount + 1) + ") ...");

                if (waitCount > 4)
                {
                    sendCount++;
                    waitCount = 0;

                    logger.Warn($"{ LOG_PREFIX } No response from Controller; resend registration request (" + (sendCount + 1) + ")");

                    RegisterServer((JObject) state);
                }

                waitCount++;
                return;
            }

            logger.Debug($"{ LOG_PREFIX } Registration complete; server id is " + Core.Server.Instance.Id);

            registrationTimer.Change(Timeout.Infinite, Timeout.Infinite);
            registrationTimer = null;

            //Initialize consumers that require listening
            logger.Debug($"{ LOG_PREFIX } Initialize consumers");

            InitializeConsumers();

            logger.Debug($"{ LOG_PREFIX } Consumers initialization complete");
            
            // Start a timer that sends keepalive messages on interval
            var config   = (JObject) state;
            var interval = config["Server"].Value<int>("TTL") * 1000;

            keepAliveTimer = new Timer(KeepAlive, messageClient, interval, interval);

            // Test Code!
            //var factory = Shared.Extensions.Instance.Consumers.FirstOrDefault(t => t.Metadata.Name == "VoIP Dialer");
            //var consumer = factory.CreateExport().Value;

            //consumer.Init();

            //Consumer_StateChanged(consumer, new ConsumerStateEventArgs(State.Listening, "4747285576", 0));
        }

        private void KeepAlive(object state)
        {
            var messageClient = (IMessageClient) state;
            var message       = new Composer().KeepAliveRequest(Core.Server.Instance.Id);

            messageClient.Send("controller", message);
        }

        private void InitializeConsumers()
        {
            foreach (var factory in Shared.Extensions.Instance.Consumers.Where(t => t.Metadata.Direction == Direction.In))
            {
                try
                {
                    var consumer = factory.CreateExport()?.Value;
                    
                    if (consumer != null)
                    {
                        logger.InfoFormat($"{ LOG_PREFIX } Initializing consumer extension {0} version {1}", factory.Metadata.Name, factory.Metadata.Version);
                    
                        consumer.StateChanged += Consumer_StateChanged;
                        consumer.Init();

                        consumers.Add(Tuple.Create(factory.Metadata, consumer));
                    }
                }
                catch (Exception ex)
                {
                    logger.Error($"{ LOG_PREFIX } Could not load consumer extension", ex);
                }
            }
        }

        private void Consumer_StateChanged(object sender, ConsumerStateEventArgs e)
        {
            var consumer = (IConsumer) sender;

            if (e.NewState == State.Listening)
            {
                SessionManager.CreateSession(consumer, e.Endpoint);
                return;
            }

            if (e.NewState == State.Finished)
            {
                SessionManager.DestroySession(e.SessionId);
                return;
            }

            logger.Debug($"{ LOG_PREFIX } Consumer State change for session { consumer.SessionId } was ignored; state was changed to { e.NewState }");
        }

        private void MessageClient_MessageReceived(object sender, MessageEventArgs e)
        {
            new IncomingMessage().Handle(e.Sender, e.Message);
        }
    }
}
