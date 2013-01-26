using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace BNet.Client
{
    internal class NetUdpClient : UdpClient, IUdpClient
    {
        internal NetUdpClient(string hostname, int port) : base(hostname, port)
        {
        }
    }
}
