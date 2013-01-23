// ----------------------------------------------------------------------------------------------------
// <copyright file="RConClient.cs" company="Me">Copyright (c) 2012 St4l.</copyright>
// ----------------------------------------------------------------------------------------------------
namespace BNet.Client
{
    using System;
    using System.Diagnostics;
    using System.Net.Sockets;
    using System.Security.Authentication;
    using System.Threading.Tasks;
    using BNet.Client.Datagrams;
    using log4net;


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
    public sealed class RConClient
    {
        private readonly string host;

        private readonly int port;

        private readonly string password;

        private readonly OutboundDatagramQueue outboundQueue = new OutboundDatagramQueue();

        private UdpClient udpClient;

        private MessageDispatcher msgDispatcher;

        private bool closed;


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
            this.Log = LogManager.GetLogger(this.GetType());
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

        private ILog Log { get; set; }


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
            if (this.closed)
            {
                throw new ObjectDisposedException(
                    "RConClient", "This RConClient has been disposed.");
            }

            this.StartListening();

            var loggedIn = false;
            try
            {
                this.LogDebug("BEFORE LOGIN await Login()");
                loggedIn = await this.Login();
                this.LogDebug("AFTER LOGIN await Login()");
            }
            finally
            {
                this.LogDebug("FINALLY LOGIN await Login()");
                if (!loggedIn)
                {
                    this.StopListening();
                }
            }

            return loggedIn;
        }


        public void Shutdown()
        {
            this.StopListening();
            this.udpClient.Close();
            this.udpClient = null;
            this.closed = true;
        }


        [Conditional("TRACE")]
        private void LogDebug(string msg)
        {
            this.Log.Debug(msg);
        }


        [Conditional("TRACE")]
        private void LogDebugFormat(string fmt, params object[] args)
        {
            this.Log.DebugFormat(fmt, args);
        }


        private async Task<bool> Login()
        {
            this.LogDebug("BEFORE LOGIN await SendDatagramAsync");
            ResponseHandler responseHandler =
                await this.msgDispatcher.SendDatagramAsync(new LoginDatagram(this.password));
            this.LogDebug("AFTER  LOGIN await SendDatagramAsync");

            this.LogDebug("BEFORE LOGIN await WaitForResponse");
            bool received = await responseHandler.WaitForResponse();
            this.LogDebug("AFTER  LOGIN await WaitForResponse");
            if (!received)
            {
                this.LogDebug("       LOGIN TIMEOUT");
                throw new TimeoutException("Timeout while trying to login to the remote host.");
            }

            var result = (LoginResponseDatagram)responseHandler.ResponseDatagram;
            if (!result.Success)
            {
                this.LogDebug("       LOGIN INCORRECT");
                throw new InvalidCredentialException(
                    "RCon server actively refused access with the specified password.");
            }

            this.LogDebug("       LOGIN SUCCESS");
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
