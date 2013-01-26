using System.Net.Sockets;

namespace BNet.Client
{
    internal class NetUdpClient : UdpClient, IUdpClient
    {
        internal NetUdpClient(string hostname, int port) : base(hostname, port)
        {
        }
    }
}
