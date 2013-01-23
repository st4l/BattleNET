// ----------------------------------------------------------------------------------------------------
// <copyright file="RConClient.cs" company="Me">Copyright (c) 2012 St4l.</copyright>
// ----------------------------------------------------------------------------------------------------
namespace BNet.Client
{
    using System;
    using System.Net.Sockets;
    using System.Security.Authentication;
    using System.Threading.Tasks;
    using BNet.Client.Datagrams;


    /// <summary>
    ///     The <see cref='RConClient' /> class provides access to BattlEye RCon services.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The
    ///         <see href="http://www.battleye.com/downloads/BERConProtocol.txt">
    ///             BattlEye RCon protocol
    ///         </see>
    ///         uses the ArmA game server's network interface, i.e. its UDP game port.
    ///     </para>
    ///     <para>
    ///         The use of the underlying UDP protocol means that a persistent connection
    ///         to the server is not maintained, and the client only registers with the
    ///         server in order to receive messages from it, and subsequently be able to
    ///         send further messages which the server will interpret as belonging to the
    ///         same session.
    ///     </para>
    ///     <para>
    ///         <see cref="RConClient" /> encapsulates and augments
    ///         <see cref='System.Net.Sockets.UdpClient' />, which
    ///         is used to connect to the RCon server.
    ///     </para>
    /// </remarks>
    public class RConClient
    {
        private readonly string host;

        private readonly int port;

        private readonly string password;

        private readonly OutboundDatagramQueue outboundQueue = new OutboundDatagramQueue();

        private UdpClient udpClient;

        private MessageDispatcher msgDispatcher;


        public RConClient(string host, int port, string password)
        {
            this.host = host;
            this.port = port;
            this.password = password;
            this.udpClient = new UdpClient(this.host, this.port)
                                 {
                                    // ExclusiveAddressUse = true, 
                                     DontFragment = true, 
                                     EnableBroadcast = false, 
                                     MulticastLoopback = false
                                 };
        }


        /// <summary>
        ///     Occurs when a console message is received from the RCon server.
        /// </summary>
        public event MessageReceivedHandler MessageReceived;

        /// <summary>
        ///     Gets or sets a <see cref="bool" /> value that specifies
        ///     whether this <see cref="RConClient" /> tries to keep the
        ///     connection to the remote RCon server alive.
        /// </summary>
        public bool KeepAlive
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }

        /// <summary>
        ///     Gets or sets a <see cref="bool" /> value that specifies
        ///     whether this <see cref="RConClient" /> discards all
        ///     console message datagrams received (the <see cref="MessageReceived" />
        ///     event is never raised).
        /// </summary>
        public bool DiscardConsoleMessages
        {
            get { return this.msgDispatcher.DiscardConsoleMessages; }
            set { this.msgDispatcher.DiscardConsoleMessages = value; }
        }


        /// <summary>
        ///     Registers with the established remote Battleye RCon server
        ///     using the provided password and starts listening for messages
        ///     from it.
        /// </summary>
        /// <returns>
        ///     True if connection and login are successful,
        ///     false otherwise.
        /// </returns>
        public async Task<bool> ConnectAsync()
        {
            this.StartListening();

            var loggedIn = false;
            try
            {
                loggedIn = await this.Login();
            }
            finally
            {
                if (!loggedIn)
                {
                    this.StopListening();
                }
            }

            return loggedIn;
        }


        public void Close()
        {
            this.StopListening();
            this.udpClient.Close();
            this.udpClient = null;
        }


        private async Task<bool> Login()
        {
            ResponseHandler responseHandler =
                await this.msgDispatcher.SendDatagram(new LoginDatagram(this.password));

            bool received = await responseHandler.WaitForResponse();
            if (!received)
            {
                throw new TimeoutException("Timeout while trying to login to the remote host.");
            }

            var result = (LoginResponseDatagram)responseHandler.ResponseDatagram;
            if (!result.Success)
            {
                throw new InvalidCredentialException(
                    "RCon server actively refused access with the specified password.");
            }

            return result.Success;
        }


        private void StartListening()
        {
            this.msgDispatcher = new MessageDispatcher(this.udpClient);
            this.msgDispatcher.MessageReceived += this.OnMessageReceived;
            this.msgDispatcher.Start();
        }


        private void OnMessageReceived(object sender, MessageReceivedHandlerArgs e)
        {
            if (this.MessageReceived != null)
            {
                this.MessageReceived(this, e);
            }
        }


        private void StopListening()
        {
            this.msgDispatcher.MessageReceived -= this.OnMessageReceived;
            this.msgDispatcher.Shutdown();
            this.msgDispatcher = null;
        }
    }
}
