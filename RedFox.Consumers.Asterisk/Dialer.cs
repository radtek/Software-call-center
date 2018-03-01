using log4net;

using RedFox.Shared;

using SIPSorcery.Net;
using SIPSorcery.SIP;
using SIPSorcery.SIP.App;
using SIPSorcery.Sys;
using SIPSorcery.Sys.Net;

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Configuration;
using System.Linq;
using System.Net;

namespace RedFox.Consumers.Asterisk
{
    [Export(typeof(IConsumer)), ConsumerMetadata("VoIP Dialer", "1.0", Direction.Out)]
    public class Dialer : IConsumer
    {
        private static ILog     logger    = LogManager.GetLogger("System");
        private static string   prefix    = "IConsumer [Asterisk Dialer 1.0]";
        private static string[] numbers   = ConfigurationManager.AppSettings["RedFox.Consumers.Asterisk.Numbers"].Split(',');
        private static string   asterisk  = ConfigurationManager.AppSettings["RedFox.Consumers.Asterisk.Server"];
        private static string[] passwords = ConfigurationManager.AppSettings["RedFox.Consumers.Asterisk.Passwords"].Split(',');

        public State State     { get; private set; }
        public int   SessionId { get; set; }

        public event DataHandler           DataAvailable;
        public event ConsumerStateDelegate StateChanged;

        private SIPClientUserAgent         uac;
        private SIPTransport               transport;
        private IPAddress                  publicIPAddress;
        private IPEndPoint                 localIPEndPoint;
        private RTPChannel                 rtpChannel;
        private bool                       paused;
        private long                       received;
        private string                     endpoint;

        public void Init()
        {
            var port = FreePort.FindNextAvailableUDPPort(15090);

            transport       = new SIPTransport(SIPDNSManager.ResolveSIPService, new SIPTransactionEngine());
            publicIPAddress = STUNClient.GetPublicIPAddress("stun.ekiga.net");
            localIPEndPoint = IpAddressLookup.QueryRoutingInterface(asterisk, port);
            
            var endpoint = new IPEndPoint(localIPEndPoint.Address, port);
            var channel  = new SIPUDPChannel(endpoint);

            transport.AddSIPChannel(channel);
            transport.SIPTransportRequestReceived += Transport_SIPTransportRequestReceived;

            //var registration = new SIPRegistrationUserAgent(
            //        transport,
            //        null,
            //        new SIPEndPoint(SIPProtocolsEnum.udp, publicIPAddress, port),
            //        new SIPURI("1003", asterisk, null, SIPSchemesEnum.sip, SIPProtocolsEnum.udp),
            //        "1003",
            //        passwords[0],
            //        null,
            //        asterisk,
            //        new SIPURI(SIPSchemesEnum.sip, new SIPEndPoint(SIPProtocolsEnum.udp, publicIPAddress, port)),
            //        180,
            //        null,
            //        null,
            //        (e) => { logger.Debug($"{ prefix } SIPRegistrationUserAgent; { e.Message }"); }
            //    );

            //registration.RegistrationSuccessful += Registration_RegistrationSuccessful;
            //registration.RegistrationFailed     += Registration_RegistrationFailed;
            //registration.Start();
        }

        public void Start(string endpoint)
        {
            this.endpoint = endpoint;

            var caller   = "1003";
            var password = passwords[0];
            var port     = FreePort.FindNextAvailableUDPPort(15090);

            rtpChannel = new RTPChannel
            {
                DontTimeout    = true,
                RemoteEndPoint = new IPEndPoint(IPAddress.Parse(asterisk), port)
            };

            rtpChannel.SetFrameType(FrameTypesEnum.Audio);
            rtpChannel.ReservePorts(15000, 15090);
            rtpChannel.OnFrameReady += RtpChannel_OnFrameReady;

            uac = new SIPClientUserAgent(transport, null, null, null, null);

            var uri    = SIPURI.ParseSIPURIRelaxed($"{ endpoint }@{ asterisk }");
            var from   = (new SIPFromHeader(caller, new SIPURI(caller, asterisk, null), null)).ToString();
            var random = Crypto.GetRandomInt(5).ToString();
            var sdp    = new SDP
            {
                Version     = 2,
                Username    = "usc",
                SessionId   = random,
                Address     = localIPEndPoint.Address.ToString(),
                SessionName = "redfox_" + random,
                Timing      = "0 0",
                Connection  = new SDPConnectionInformation(publicIPAddress.ToString())
            };

            var announcement = new SDPMediaAnnouncement
            {
                Media        = SDPMediaTypesEnum.audio,
                MediaFormats = new List<SDPMediaFormat>() { new SDPMediaFormat((int) SDPMediaFormatsEnum.PCMU, "PCMU", 8000) },
                Port         = rtpChannel.RTPPort
            };

            sdp.Media.Add(announcement);
            
            var descriptor = new SIPCallDescriptor(caller, password, uri.ToString(), from, null, null, null, null, SIPCallDirection.Out, SDP.SDP_MIME_CONTENTTYPE, sdp.ToString(), null);

            uac.CallTrying   += Uac_CallTrying;
            uac.CallRinging  += Uac_CallRinging;
            uac.CallAnswered += Uac_CallAnswered;
            uac.CallFailed   += Uac_CallFailed;

            uac.Call(descriptor);
        }

        public void Pause()
        {
            paused = true;
        }

        public void Resume()
        {
            paused = false;
        }

        public void Stop()
        {
            logger.Debug($"{ prefix } Stop consumer for { endpoint }");

            if (uac.IsUACAnswered && uac.SIPDialogue != null)
                uac.SIPDialogue.Hangup(transport, null);
            
            rtpChannel.OnFrameReady -= RtpChannel_OnFrameReady;

            if (!rtpChannel.IsClosed)
                 rtpChannel.Close();

            StateChanged?.Invoke(this, new ConsumerStateEventArgs(State.Ready, endpoint, SessionId));
        }

        public void Finish()
        {
            
        }
        
        private void Transport_SIPTransportRequestReceived(SIPEndPoint localSIPEndPoint, SIPEndPoint remoteEndPoint, SIPRequest sipRequest)
        {
            var sipEndPoint = new SIPEndPoint(SIPProtocolsEnum.udp, publicIPAddress, localSIPEndPoint.Port);

            switch (sipRequest.Method)
            {
                case SIPMethodsEnum.BYE:
                    logger.Debug($"{ prefix } Hangup from { sipRequest.Header.From.FromURI.User }");

                    var noninvite = transport.CreateNonInviteTransaction(sipRequest, remoteEndPoint, sipEndPoint, null);
                    var response  = SIPTransport.GetResponse(sipRequest, SIPResponseStatusCodesEnum.Ok, null);

                    noninvite.SendFinalResponse(response);

                    StateChanged?.Invoke(this, new ConsumerStateEventArgs(State.Finished, endpoint, SessionId));
                    
                    rtpChannel.OnFrameReady -= RtpChannel_OnFrameReady;
                    rtpChannel.Close();

                    StateChanged?.Invoke(this, new ConsumerStateEventArgs(State.Ready, endpoint, SessionId));
                    return;

                case SIPMethodsEnum.CANCEL:
                    break;
            }
        }

        private void Registration_RegistrationSuccessful(SIPURI sipURI)
        {
            logger.Debug($"{ prefix } Registration successful for {sipURI.User}@{sipURI.CanonicalAddress}");
        }

        private void Registration_RegistrationFailed(SIPURI sipURI, string arg2)
        {
            logger.Debug($"{ prefix } Registration failed for {sipURI.User}@{sipURI.CanonicalAddress}");
        }

        private void RtpChannel_OnFrameReady(RTPFrame frame)
        {
            if (paused) return;

            var payload = frame.GetFramePayload();
              received += payload.LongLength;

            DataAvailable?.Invoke(this, new DataEventArgs(payload, payload.Length));
        }
        
        private void Uac_CallTrying(ISIPClientUserAgent uac, SIPResponse sipResponse)
        {
            logger.Debug($"{ prefix } Trying to call");
        }

        private void Uac_CallRinging(ISIPClientUserAgent uac, SIPResponse sipResponse)
        {
            logger.Debug($"{ prefix } Ringing ...");
        }

        private void Uac_CallAnswered(ISIPClientUserAgent uac, SIPResponse sipResponse)
        {
            logger.Debug($"{ prefix } Call answered; { sipResponse.StatusCode } { sipResponse.Status }");

            switch (sipResponse.StatusCode)
            {
                case 404:
                    logger.Error($"{ prefix } Received 404 Not Found from remote endpoint");
                    break;

                case 486:
                    // Busy
                    logger.Error($"{ prefix } Received 486 Remote endpoint is busy; try again later");
                    break;

                case 488:
                    // Possible audio format issue
                    logger.Error($"{ prefix } Received 488 Not Acceptable from remote endpoint; check audio format");
                    break;

                case 503:
                    // Check Twilio service and geo-permissions
                    logger.Error($"{ prefix } Received 503 Service Unavailable from remote endpoint; check service permissions");
                    break;

                case 200:
                    if (sipResponse.Header.ContentType != SDP.SDP_MIME_CONTENTTYPE)
                    {
                        logger.Error($"{ prefix } Received incorrect content type");

                        Stop();
                        return;
                    }

                    if (sipResponse.Body.IsNullOrBlank())
                    {
                        logger.Error($"{ prefix } Received an empty SDP payload");

                        Stop();
                        return;
                    }
                    
                    var sdp          = SDP.ParseSDPDescription(sipResponse.Body);
                    var ip           = IPAddress.Parse(sdp.Connection.ConnectionAddress);
                    var announcement = sdp.Media.Where(x => x.Media == SDPMediaTypesEnum.audio).FirstOrDefault();

                    if (announcement != null)
                    {
                        if (announcement.Port != 0)
                        {
                            rtpChannel.OnControlDataReceived       += (b) => { logger.Debug($"{prefix} Control Data Received; {b.Length} bytes"); };
                            rtpChannel.OnControlSocketDisconnected += ()  => { logger.Debug($"{prefix} Control Socket Disconnected"); };

                            rtpChannel.RemoteEndPoint = new IPEndPoint(ip, announcement.Port);
                            rtpChannel.Start();

                            // Send some setup parameters to punch a hole in the firewall/router
                            rtpChannel.SendRTPRaw(new byte[] { 80, 95, 198, 88, 55, 96, 225, 141, 215, 205, 185, 242, 00 });
                        }
                        else
                        {
                            logger.Error($"{ prefix } Remote endpoint did not specify a port number");
                            return;
                        }
                    }
                    else
                    {
                        logger.Error($"{ prefix } Remote endpoint has not valid audio announcement");
                        return;
                    }
                    break;
            }
        }

        private void Uac_CallFailed(ISIPClientUserAgent uac, string errorMessage)
        {
            logger.Debug($"{ prefix } Call to { endpoint } failed");

            uac.Cancel();
        }
    }
}
