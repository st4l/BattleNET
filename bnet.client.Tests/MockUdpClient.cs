using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using BNet.Client;

namespace bnet.client.Tests
{
    public class MockUdpClient : UdpClient, IUdpClient
    {
        public MockServer Server { get; set; }

        public MockServerSetup ServerSetup { get; private set; }


        public MockUdpClient()
        {
        }


        internal void Setup(MockServerSetup setup)
        {
            this.Server = new MockServer(setup);
            this.ServerSetup = setup;
        }



        /// <summary>
        /// Should spawn a new thread on continuation.
        /// </summary>
        public Task<int> SendAsync(byte[] buffer, int length)
        {
            var task = Task.Factory.StartNew<int>(
                () => this.Server.ReceivePacket(buffer, length));
            return task;
        }


        /// <summary>
        /// Should spawn a new thread on continuation.
        /// </summary>
        public Task<UdpReceiveResult> ReceiveAsync()
        {
            var task = Task.Factory.StartNew<UdpReceiveResult>(
                () => this.Server.SendPacket());
            return task;
        }


        public void Close()
        {
            this.Server.Shutdown();
        }


    }
}
