using log4net;

using RedFox.Shared;

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Configuration;
using System.Linq;
using System.Net;

using SIPSorcery.Net;
using SIPSorcery.SIP;
using SIPSorcery.SIP.App;
using SIPSorcery.Sys;
using SIPSorcery.Sys.Net;

namespace RedFox.Consumers.Asterisk
{
    [Export(typeof(IConsumer)), ConsumerMetadata("Dial-up", "1.2", Direction.In)]
    public class Receiver : IConsumer
    {
        private static ILog     logger    = LogManager.GetLogger("System");
        private static string   prefix    = "IConsumer [Dial-Up 1.2]";
        private static string[] numbers   = ConfigurationManager.AppSettings["RedFox.Consumers.Asterisk.Numbers"].Split(',');
        private static string   asterisk  = ConfigurationManager.AppSettings["RedFox.Consumers.Asterisk.Server"];
        private static string[] passwords = ConfigurationManager.AppSettings["RedFox.Consumers.Asterisk.Passwords"].Split(',');

        public event DataHandler           DataAvailable;
        public event ConsumerStateDelegate StateChanged;

        private SIPTransport               transport;
        private SIPServerUserAgent         agent;
        private UASInviteTransaction       transaction;
        private IPAddress                  publicIPAddress;
        private IPEndPoint                 localIPEndPoint;
        private RTPChannel                 rtpChannel;
        private string                     Endpoint;
        private bool                       paused;
        private long                       received;

        private List<SIPRegistrationUserAgent> registrations = new List<SIPRegistrationUserAgent>();

        public State State     { get; private set; }
        public int   SessionId { get; set; }

        public void Init()
        {
            var port = FreePort.FindNextAvailableUDPPort(15090);

            transport       = new SIPTransport(SIPDNSManager.ResolveSIPService, new SIPTransactionEngine());
            publicIPAddress = STUNClient.GetPublicIPAddress("stun.ekiga.net");
            localIPEndPoint = IpAddressLookup.QueryRoutingInterface(asterisk, port);
            
            var endpoint = new IPEndPoint(localIPEndPoint.Address, port);
            var channel  = new SIPUDPChannel(endpoint);
            
            //channel.SIPMessageReceived += (SIPChannel sipChannel, SIPEndPoint remoteEndPoint, byte[] buffer) => { logger.Debug("Channel SIP message received " + channel.SIPChannelEndPoint + "<-" + remoteEndPoint); };

            transport.AddSIPChannel(channel);

            for (var i = 0; i < numbers.Length; i++)
            {
                var number       = numbers[i];
                var password     = passwords[i];
                var registration = new SIPRegistrationUserAgent(
                    transport,
                    null,
                    new SIPEndPoint(SIPProtocolsEnum.udp, publicIPAddress, port),
                    new SIPURI(number, asterisk, null, SIPSchemesEnum.sip, SIPProtocolsEnum.udp),
                    number,
                    password,
                    null,
                    asterisk,
                    new SIPURI(SIPSchemesEnum.sip, new SIPEndPoint(SIPProtocolsEnum.udp, publicIPAddress, port)),
                    180,
                    null,
                    null,
                    (e) => { logger.Debug(e.Message); }
                );

                logger.Debug($"{prefix} Registration attempt for {number}@{endpoint.Address}:{endpoint.Port}");

                registration.RegistrationSuccessful += Registration_RegistrationSuccessful;
                registration.RegistrationFailed     += Registration_RegistrationFailed;
                registration.Start();

                registrations.Add(registration);
            }
        }

        public void Finish()
        {
            transport.SIPTransportRequestReceived -= Transport_SIPTransportRequestReceived;

            foreach (var registration in registrations)
            {
                registration.RegistrationSuccessful -= Registration_RegistrationSuccessful;
                registration.RegistrationFailed     -= Registration_RegistrationFailed;
                registration.Stop();
            }

            registrations.Clear();

            StateChanged?.Invoke(this, new ConsumerStateEventArgs(State.Disposed, Endpoint, SessionId));
        }

        public void Start(string endpoint)
        {
            Endpoint = endpoint;
        }
        
        public void Stop()
        {
           // TODO Check if connection is open; if so, HANGUP
        }

        public void Pause()
        {
            paused = true;
        }

        public void Resume()
        {
            paused = false;
        }
        
        private void SetState(State state, string endpoint)
        {
            State = state;
            StateChanged?.Invoke(this, new ConsumerStateEventArgs(state, endpoint, SessionId));
        }

        private void Registration_RegistrationFailed(SIPURI sipURI, string error)
        {
            logger.ErrorFormat("{0} Registration failed; reason: {1}", prefix, error);
        }

        private void Registration_RegistrationSuccessful(SIPURI sipURI)
        {
            logger.DebugFormat("{0} Registration successful for {1}@{2}", prefix, sipURI.User, sipURI.CanonicalAddress);

            transport.SIPTransportRequestReceived  += Transport_SIPTransportRequestReceived;
            //transport.SIPTransportResponseReceived += (localSIPEndPoint, remoteEndPoint, sipResponse) => { logger.Debug("Transport Response Received: " + localSIPEndPoint + "<-" + remoteEndPoint + "\r\n" + sipResponse.ToString()); };

            //transport.SIPRequestInTraceEvent   += (localSIPEndPoint, endPoint, sipRequest)  => { logger.Debug("Request Received : " + localSIPEndPoint + "<-" + endPoint + "\r\n" + sipRequest.ToString());  };
            //transport.SIPRequestOutTraceEvent  += (localSIPEndPoint, endPoint, sipRequest)  => { logger.Debug("Request Sent     : " + localSIPEndPoint + "->" + endPoint + "\r\n" + sipRequest.ToString());  };
            //transport.SIPResponseInTraceEvent  += (localSIPEndPoint, endPoint, sipResponse) => { logger.Debug("Response Received: " + localSIPEndPoint + "<-" + endPoint + "\r\n" + sipResponse.ToString()); };
            //transport.SIPResponseOutTraceEvent += (localSIPEndPoint, endPoint, sipResponse) => { logger.Debug("Response Sent    : " + localSIPEndPoint + "->" + endPoint + "\r\n" + sipResponse.ToString()); };

            //transport.SIPBadRequestInTraceEvent  += (localSIPEndPoint, remotePoint, message, errorField, rawMessage) => { logger.Debug(prefix + "Bad Request Received  (" + errorField.ToString() + "): " + localSIPEndPoint + "<-" + remotePoint + "\r\n" + message); };
            //transport.SIPBadResponseInTraceEvent += (localSIPEndPoint, remotePoint, message, errorField, rawMessage) => { logger.Debug("Bad Response Received (" + errorField.ToString() + "): " + localSIPEndPoint + "<-" + remotePoint + "\r\n" + message); };

            //transport.STUNRequestReceived += (localEndpoint, remoteEndpoint, buffer, bufferLength) => { logger.Debug("STUN Request Received: " + localEndpoint + "<-" + remoteEndpoint); };
            
            StateChanged?.Invoke(this, new ConsumerStateEventArgs(State.Ready, Endpoint, SessionId));
        }
        
        private void Transport_SIPTransportRequestReceived(SIPEndPoint localSIPEndPoint, SIPEndPoint remoteEndPoint, SIPRequest sipRequest)
        {
            var endpoint = new SIPEndPoint(SIPProtocolsEnum.udp, publicIPAddress, localSIPEndPoint.Port);

            if (sipRequest.Method == SIPMethodsEnum.INVITE)
            {
                if (transaction != null) return;

                logger.DebugFormat("{0} Incoming call from {1}", prefix, sipRequest.Header.From.FromURI.User);
                
                transaction = transport.CreateUASTransaction(sipRequest, remoteEndPoint, endpoint, null);
                agent       = new SIPServerUserAgent(
                    transport, 
                    null,
                    sipRequest.Header.From.FromURI.User, 
                    null, 
                    SIPCallDirection.In, 
                    null, 
                    null, 
                    null, 
                    transaction);
                
                agent.CallCancelled       += Agent_CallCancelled;
                agent.TransactionComplete += Agent_TransactionComplete;

                agent.Progress(SIPResponseStatusCodesEnum.Trying, null, null, null, null);
                agent.Progress(SIPResponseStatusCodesEnum.Ringing, null, null, null, null);

                var answer  = SDP.ParseSDPDescription(agent.CallRequest.Body);
                var address = IPAddress.Parse(answer.Connection.ConnectionAddress);
                var port    = answer.Media.FirstOrDefault(m => m.Media == SDPMediaTypesEnum.audio).Port;
                var random  = Crypto.GetRandomInt(5).ToString();
                var sdp     = new SDP
                {
                    Version     = 2,
                    Username    = "usc",
                    SessionId   = random,
                    Address     = localIPEndPoint.Address.ToString(),
                    SessionName = "redfox_" + random,
                    Timing      = "0 0",
                    Connection  = new SDPConnectionInformation(publicIPAddress.ToString())
                };

                rtpChannel = new RTPChannel
                {
                    DontTimeout    = true,
                    RemoteEndPoint = new IPEndPoint(address, port)
                };
                
                rtpChannel.SetFrameType(FrameTypesEnum.Audio);
                // TODO Fix hardcoded ports
                rtpChannel.ReservePorts(15000, 15090);
                rtpChannel.OnFrameReady += Channel_OnFrameReady;
                rtpChannel.Start();

                // Send some setup parameters to punch a hole in the firewall/router
                rtpChannel.SendRTPRaw(new byte[] { 80, 95, 198, 88, 55, 96, 225, 141, 215, 205, 185, 242, 00 });

                rtpChannel.OnControlDataReceived       += (b) => { logger.Debug($"{prefix} Control Data Received; {b.Length} bytes"); };
                rtpChannel.OnControlSocketDisconnected += ()  => { logger.Debug($"{prefix} Control Socket Disconnected"); };

                var announcement = new SDPMediaAnnouncement
                    {
                        Media        = SDPMediaTypesEnum.audio,
                        MediaFormats = new List<SDPMediaFormat>() { new SDPMediaFormat((int) SDPMediaFormatsEnum.PCMU, "PCMU", 8000) },
                        Port         = rtpChannel.RTPPort
                    };
                
                sdp.Media.Add(announcement);

                SetState(State.Listening, sipRequest.Header.From.FromURI.User);

                agent.Progress(SIPResponseStatusCodesEnum.Accepted, null, null, null, null);
                agent.Answer(SDP.SDP_MIME_CONTENTTYPE, sdp.ToString(), null, SIPDialogueTransferModesEnum.NotAllowed);
                
                SetState(State.Busy, "");
                return;
            }
            if (sipRequest.Method == SIPMethodsEnum.BYE)
            {
                if (State != State.Busy) return;

                logger.DebugFormat("{0} Hangup from {1}", prefix, sipRequest.Header.From.FromURI.User);

                var noninvite = transport.CreateNonInviteTransaction(sipRequest, remoteEndPoint, endpoint, null);
                var response  = SIPTransport.GetResponse(sipRequest, SIPResponseStatusCodesEnum.Ok, null);

                noninvite.SendFinalResponse(response);

                SetState(State.Finished, Endpoint);
                
                rtpChannel.OnFrameReady -= Channel_OnFrameReady;
                rtpChannel.Close();
                
                agent.TransactionComplete -= Agent_TransactionComplete;
                agent.CallCancelled       -= Agent_CallCancelled;
                agent       = null;
                transaction = null;

                SetState(State.Ready, Endpoint);
                
                return;
            }
            if (sipRequest.Method == SIPMethodsEnum.ACK)
            {

            }
            if (sipRequest.Method == SIPMethodsEnum.CANCEL)
            {

            }
        }

        private void Agent_TransactionComplete(ISIPServerUserAgent uas)
        {
            
        }

        private void Channel_OnFrameReady(RTPFrame frame)
        {
            if (paused) return;

            var payload = frame.GetFramePayload();

            received += payload.LongLength;

            DataAvailable?.Invoke(this, new DataEventArgs(payload, payload.Length));
        }

        private void Agent_CallCancelled(ISIPServerUserAgent uas)
        {
            agent = null;
        }
    }
}