using System.Net;
using System.Net.Sockets;

namespace RedFox.Consumers.Asterisk
{
    class IpAddressLookup
    {
        public static IPEndPoint QueryRoutingInterface(string remoteAddress, int port)
        {
            var remoteEndPoint  = new IPEndPoint(IPAddress.Parse(remoteAddress), port);
            var socket          = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            var address         = remoteEndPoint.Serialize();
            var remoteAddrBytes = new byte[address.Size];

            for (int i = 0; i < address.Size; i++)
            {
                remoteAddrBytes[i] = address[i];
            }

            var outBytes = new byte[remoteAddrBytes.Length];

            socket.IOControl(
                        IOControlCode.RoutingInterfaceQuery,
                        remoteAddrBytes,
                        outBytes);

            for (int i = 0; i < address.Size; i++)
            {
                address[i] = outBytes[i];
            }
            
            return (IPEndPoint) remoteEndPoint.Create(address);
        }
    }
}
