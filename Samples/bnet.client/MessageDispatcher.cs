// ----------------------------------------------------------------------------------------------------
// <copyright file="MessageDispatcher.cs" company="Me">Copyright (c) 2012 St4l.</copyright>
// ----------------------------------------------------------------------------------------------------
namespace BNet.Client
{
    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Net.Sockets;
    using System.Security.Permissions;
    using System.Threading;
    using System.Threading.Tasks;
    using BNet.Client.Datagrams;
    using log4net;


    /// <summary>
    ///     Receives and dispatches messages from a remote Battleye RCon server.
    /// </summary>
    public class MessageDispatcher
    {
        private readonly UdpClient udpClient;

        private readonly ResponseMessageDispatcher responseDispatcher;

        private bool shutdown;

        private DateTime lastDgramReceivedTime;

        private ManualResetEventSlim shutdownLock;

        private bool isRunning;

        private AsyncOperation asyncOperation;

        private DateTime lastCmdSentTime;


        /// <summary>
        ///     Initializes a new instance of <see cref="MessageDispatcher" />
        ///     and establishes the <see cref="UdpClient" /> to be used.
        /// </summary>
        /// <param name="udpClient">
        ///     The <see cref="UdpClient" /> to be used to connect to the
        ///     RCon server.
        /// </param>
        public MessageDispatcher(UdpClient udpClient)
        {
            this.udpClient = udpClient;
            this.responseDispatcher = new ResponseMessageDispatcher();
            this.Log = LogManager.GetLogger(this.GetType());
        }


        /// <summary>
        ///     Occurs when a console message is received from the RCon server.
        /// </summary>
        public event MessageReceivedHandler MessageReceived;

        /// <summary>
        ///     Gets or sets a <see cref="Boolean" /> value that specifies
        ///     whether this <see cref="MessageDispatcher" /> discards all
        ///     console message datagrams received (the <see cref="MessageReceived" />
        ///     event is never raised).
        /// </summary>
        public bool DiscardConsoleMessages { get; set; }

        private ILog Log { get; set; }


        /// <summary>
        ///     Starts acquiring and dispatching inbound messages in a new thread.
        /// </summary>
        /// <remarks>Starts the main message pump in a new thread.</remarks>
        public void Start()
        {
            if (this.isRunning)
            {
                throw new InvalidOperationException("Already running.");
            }

            this.isRunning = true;

            var state = new object();
            this.asyncOperation = AsyncOperationManager.CreateOperation(null);
            new ParameterizedThreadStart(this.MainLoop).BeginInvoke(state, null, null);
        }


        /// <summary>
        ///     Stops acquiring messages.
        /// </summary>
        /// <remarks>Exits the main pump thread gracefully.</remarks>
        public void Shutdown()
        {
            this.shutdownLock = new ManualResetEventSlim(false);
            this.shutdown = true;

            // wait until the main thread is exited
            this.shutdownLock.Wait();
        }


        /// <summary>
        ///     Registers a handler to be notified when a response
        ///     message arrives, and which accepts the response message itself.
        /// </summary>
        /// <param name="handler"></param>
        public void RegisterResponseHandler(ResponseHandler handler)
        {
            this.responseDispatcher.Register(handler);
        }


        /// <summary>
        ///     Raises the <see cref="MessageReceived" /> event.
        /// </summary>
        /// <param name="e">
        ///     An <see cref="MessageReceivedHandlerArgs" /> that
        ///     contains the event data.
        /// </param>
        public virtual void OnMessageReceived(MessageReceivedHandlerArgs e)
        {
            if (this.MessageReceived != null)
            {
                this.MessageReceived(this, e);
            }
        }


        internal async Task<ResponseHandler> SendDatagram(IOutboundDatagram dgram)
        {
            // this.outboundQueue.Enqueue(dgram);
            byte[] bytes = dgram.Build();

            ResponseHandler handler = null;
            if (dgram.ExpectsResponse)
            {
                handler = new ResponseHandler(dgram);
                this.RegisterResponseHandler(handler);
            }

            // socket is thread safe
            // i.e. it is ok to send & receive at the same time from different threads
            int transferredBytes = await this.udpClient.SendAsync(bytes, bytes.Length);

            // .ConfigureAwait(continueOnCapturedContext: false); 
            Debug.Assert(
                transferredBytes == bytes.Length, 
                "Sent bytes count equal count of bytes meant to be sent.");

            if (dgram.Type == DatagramType.Command)
            {
                this.lastCmdSentTime = DateTime.Now;
            }

            return handler;
        }


        /// <summary>
        ///     The main message loop.
        /// </summary>
        /// <param name="state">State object.</param>
        [HostProtection(Synchronization = true, ExternalThreading = true)]
        private void MainLoop(object state)
        {
            // Check whether the thread has previously been named 
            // to avoid a possible InvalidOperationException. 
            if (Thread.CurrentThread.Name == null)
            {
                Thread.CurrentThread.Name = "MainMsgPumpThread";
            }

            this.lastCmdSentTime = DateTime.Now.AddSeconds(10);
            while (!this.shutdown)
            {
                this.Log.Debug("Scheduling new receive task.");
                var task = this.ReceiveDatagram();
                while (task.Status != TaskStatus.RanToCompletion
                       && task.Status != TaskStatus.Faulted)
                {
                    Thread.Sleep(10);
                    if (DateTime.Now - this.lastCmdSentTime > TimeSpan.FromSeconds(25))
                    {
                        this.SendKeepAlivePacket();
                    }
                }
            }

            // signal we're exiting the thread
            this.ExitMainLoop();
        }


        private bool SendKeepAlivePacket()
        {
            var keepAliveDgram = new CommandDatagram(string.Empty);
            var task = this.SendDatagram(keepAliveDgram);
            ResponseHandler result = task.Result; // blocking
            this.Log.DebugFormat("#{0:000} Sent keep alive dgram.", keepAliveDgram.SequenceNumber);
            var responseDgram = result.ResponseDatagram as CommandResponseDatagram;
            return responseDgram != null && responseDgram.Type == DatagramType.Command
                   && responseDgram.OriginalSequenceNumber == keepAliveDgram.SequenceNumber;
        }


        private void ExitMainLoop()
        {
            this.isRunning = false;

            // signal we're exiting the thread
            this.shutdownLock.Set();
        }


        /// <summary>
        ///     Returns a message asynchronously that was received from
        ///     the RCon server.
        /// </summary>
        /// <remarks>
        ///     Usage: IInboundDatagram x = await ReceiveDatagram();
        /// </remarks>
        /// <returns>
        ///     When the task executes, the received <see cref="IInboundDatagram" />,
        ///     or null if <see cref="DiscardConsoleMessages" /> is true and
        ///     the received datagram was a console message (type 2).
        /// </returns>
        [HostProtection(Synchronization = true, ExternalThreading = true)]
        private async Task ReceiveDatagram()
        {
            // ReceiveAsync (BeginRead) will block a new thread head-on against the I/O completion port
            // http://msdn.microsoft.com/en-us/library/windows/desktop/aa364986(v=vs.85).aspx
            UdpReceiveResult result = await this.udpClient.ReceiveAsync();

            this.lastDgramReceivedTime = DateTime.Now;
            byte dgramType = result.Buffer[Constants.DatagramTypeIndex];
            this.Log.DebugFormat("Received - type {0:0} dgram.", dgramType);

            if (dgramType == (byte)DatagramType.Message)
            {
                byte conMsgSeq = result.Buffer[Constants.ConsoleMessageSequenceNumberIndex];
                this.Log.DebugFormat("#{0:000} Received - Console message.", conMsgSeq);
                
                if (this.DiscardConsoleMessages)
                {
                    this.AcknowledgeMessage(conMsgSeq);
                    return;
                }
            }

            var dgram = InboundDatagramBase.ParseReceivedBytes(result.Buffer);
            if (dgram != null)
            {
                this.DispatchReceivedMessage(dgram);
            }
        }


        /// <summary>
        ///     Dispatches the received datagram to the appropriate target.
        /// </summary>
        /// <param name="dgram">
        ///     The received <see cref="IDatagram" />.
        /// </param>
        private void DispatchReceivedMessage(IInboundDatagram dgram)
        {
            if (dgram != null)
            {
                if (dgram.Type == DatagramType.Message)
                {
                    var conMsg = (ConsoleMessageDatagram)dgram;
                    this.AcknowledgeMessage(conMsg.SequenceNumber);
                    this.DispatchConsoleMessage(conMsg);
                }

                // else, dgram is either login or command response
                this.responseDispatcher.Dispatch(dgram);
            }
        }


        /// <summary>
        ///     Sends a datagram back to the server acknowledging receipt of
        ///     a console message datagram.
        /// </summary>
        /// <param name="seqNumber">
        ///     The sequence number of the received <see cref="ConsoleMessageDatagram" />.
        /// </param>
        private void AcknowledgeMessage(byte seqNumber)
        {
            var task = this.SendDatagram(new AcknowledgeMessageDatagram(seqNumber));
            task.Wait(); // blocking
            this.Log.DebugFormat("#{0:000} Acknowledged - Console message.", seqNumber);
        }


        /// <summary>
        ///     Dispatches received console messages through current AsyncOperation.
        /// </summary>
        /// <param name="dgram">
        ///     The <see cref="ConsoleMessageDatagram" />
        ///     representing the received console message.
        /// </param>
        private void DispatchConsoleMessage(ConsoleMessageDatagram dgram)
        {
            var args = new MessageReceivedHandlerArgs(dgram);

            if (this.asyncOperation != null)
            {
                this.asyncOperation.Post(this.RaiseMessageReceived, args);
            }
            else
            {
                this.RaiseMessageReceived(args);
            }
        }


        private void RaiseMessageReceived(object args)
        {
            this.OnMessageReceived((MessageReceivedHandlerArgs)args);
        }
    }
}
