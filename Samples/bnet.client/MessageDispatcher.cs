// ----------------------------------------------------------------------------------------------------
// <copyright file="MessageDispatcher.cs" company="Me">Copyright (c) 2012 St4l.</copyright>
// ----------------------------------------------------------------------------------------------------
namespace BNet.Client
{
    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.Net.Sockets;
    using System.Security.Permissions;
    using System.Threading;
    using System.Threading.Tasks;
    using BNet.Client.Datagrams;
    using log4net;


    /// <summary>
    ///     Receives messages from a remote Battleye RCon server
    ///     using a supplied <see cref="UdpClient" /> and
    ///     dispatches them accordingly.
    /// </summary>
    internal sealed class MessageDispatcher
    {
        private readonly ResponseMessageDispatcher responseDispatcher;

        private UdpClient udpClient;

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
        internal MessageDispatcher(UdpClient udpClient)
        {
            this.udpClient = udpClient;
            this.responseDispatcher = new ResponseMessageDispatcher();
            this.Log = LogManager.GetLogger(this.GetType());
        }


        /// <summary>
        ///     Occurs when a console message is received from the RCon server.
        /// </summary>
        internal event EventHandler<MessageReceivedEventArgs> MessageReceived;

        /// <summary>
        ///     Gets or sets a <see cref="Boolean" /> value that specifies
        ///     whether this <see cref="MessageDispatcher" /> discards all
        ///     console message datagrams received (the <see cref="MessageReceived" />
        ///     event is never raised).
        /// </summary>
        internal bool DiscardConsoleMessages { get; set; }

        private ILog Log { get; set; }


        /// <summary>
        ///     Starts acquiring and dispatching inbound messages in a new thread.
        /// </summary>
        /// <remarks>Starts the main message pump in a new thread.</remarks>
        internal void Start()
        {
            if (this.isRunning)
            {
                throw new InvalidOperationException("Already running.");
            }

            this.isRunning = true;

            var state = new object();
            this.asyncOperation = AsyncOperationManager.CreateOperation(null);
            new ParameterizedThreadStart(this.MainLoop).BeginInvoke(state, null, null);

            // let's give the main pump some headway to start listening
            Thread.Sleep(500);
        }


        /// <summary>
        ///     Stops acquiring messages.
        /// </summary>
        /// <remarks>Exits the main pump thread politely.</remarks>
        internal void Shutdown()
        {
            this.LogTrace("SHUTDOWN COMMENCING");
            this.shutdownLock = new ManualResetEventSlim(false);
            this.shutdown = true;

            // wait until the main thread is exited
            this.LogTrace("WAITING FOR THREADS TO EXIT");
            this.shutdownLock.Wait();

            this.LogTrace("SHUTDOWN ACHIEVED");
            this.udpClient = null;
        }


        /// <summary>
        ///     Registers a handler to be notified when a response
        ///     message arrives, and which accepts the response message itself.
        /// </summary>
        /// <param name="handler"></param>
        internal void RegisterResponseHandler(ResponseHandler handler)
        {
            this.responseDispatcher.Register(handler);
        }


        /// <summary>
        ///     Raises the <see cref="MessageReceived" /> event.
        /// </summary>
        /// <param name="e">
        ///     An <see cref="MessageReceivedEventArgs" /> that
        ///     contains the event data.
        /// </param>
        internal void OnMessageReceived(MessageReceivedEventArgs e)
        {
            if (this.MessageReceived != null)
            {
                this.MessageReceived(this, e);
            }
        }


        [HostProtection(Synchronization = true, ExternalThreading = true)]
        internal async Task<ResponseHandler> SendDatagramAsync(IOutboundDatagram dgram)
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
            this.LogTrace("BEFORE await SendDatagramAsync");
            int transferredBytes =
                await
                this.udpClient.SendAsync(bytes, bytes.Length)
                    .ConfigureAwait(continueOnCapturedContext: false);

            this.LogTrace("AFTER  await SendDatagramAsync");

            Debug.Assert(
                transferredBytes == bytes.Length, 
                "Sent bytes count equal count of bytes meant to be sent.");

            if (dgram.Type == DatagramType.Command)
            {
                this.lastCmdSentTime = DateTime.Now;
            }

            return handler;
        }


        [Conditional("TRACE")]
        private void LogTrace(string msg)
        {
            this.Log.Debug(msg);
        }


        [Conditional("TRACE")]
        private void LogTraceFormat(string fmt, params object[] args)
        {
            this.Log.DebugFormat(CultureInfo.InvariantCulture, fmt, args);
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
                Thread.CurrentThread.Name = "MainPUMP" + Thread.CurrentThread.ManagedThreadId;
            }

            var keepAlivePeriod = TimeSpan.FromSeconds(25);

            this.lastCmdSentTime = DateTime.Now.AddSeconds(10);
            while (!this.shutdown)
            {
                this.LogTrace("Scheduling new receive task.");
                var task = this.ReceiveDatagram();
                this.LogTrace("AFTER  scheduling new receive task.");

                while (!task.IsCompleted && !this.shutdown)
                {
                    if (DateTime.Now - this.lastCmdSentTime > keepAlivePeriod)
                    {
                        var alive = this.SendKeepAlivePacket().Result;
                    }

                    this.LogTraceFormat("BEFORE waiting receive task, Status={0}", task.Status);
                    task.Wait(1000);
                    this.LogTraceFormat("AFTER  waiting receive task, Status={0}", task.Status);
                }
            }

            this.LogTrace("Main loop exited.");

            // signal we're exiting the thread
            this.ExitMainLoop();
        }


        private async Task<bool> SendKeepAlivePacket()
        {
            var keepAliveDgram = new CommandDatagram(string.Empty);
            var result = await this.SendDatagramAsync(keepAliveDgram);

            this.LogTraceFormat("C#{0:000} Sent keep alive command.", keepAliveDgram.SequenceNumber);

            await result.WaitForResponse(1000);
            var responseDgram = result.ResponseDatagram as CommandResponseDatagram;
            return responseDgram != null && responseDgram.Type == DatagramType.Command
                   && responseDgram.OriginalSequenceNumber == keepAliveDgram.SequenceNumber;
        }


        private void ExitMainLoop()
        {
            this.isRunning = false;

            // signal we're exiting the thread
            this.LogTrace("shutdownLock set.");
            this.shutdownLock.Set();
        }


        /// <summary>
        ///     Handles a message asynchronously that was received from
        ///     the RCon server.
        /// </summary>
        [HostProtection(Synchronization = true, ExternalThreading = true)]
        private async Task ReceiveDatagram()
        {
            // ReceiveAsync (BeginRead) will spawn a new thread
            // which blocks head-on against the IO Completion Port
            // http://msdn.microsoft.com/en-us/library/windows/desktop/aa364986(v=vs.85).aspx
            var task = this.udpClient.ReceiveAsync();

            this.LogTrace("BEFORE await ReceiveAsync");
            UdpReceiveResult result = await task

                                                // do not incurr in ANOTHER context switch cost
                                                .ConfigureAwait(false);
            this.LogTrace("AFTER  await ReceiveAsync");

            this.lastDgramReceivedTime = DateTime.Now;
            byte dgramType = result.Buffer[Constants.DatagramTypeIndex];
            this.LogTraceFormat("{0:0}    Type dgram received.", dgramType);

            if (dgramType == (byte)DatagramType.Message)
            {
                byte conMsgSeq = result.Buffer[Constants.ConsoleMessageSequenceNumberIndex];
                this.LogTraceFormat("M#{0:000} Received", conMsgSeq);

                if (this.DiscardConsoleMessages)
                {
                    await this.AcknowledgeMessage(conMsgSeq);
                    return;
                }
            }

            var dgram = InboundDatagramBase.ParseReceivedBytes(result.Buffer);
            if (dgram != null)
            {
                await this.DispatchReceivedMessage(dgram);
            }
        }


        /// <summary>
        ///     Dispatches the received datagram to the appropriate target.
        /// </summary>
        /// <param name="dgram">
        ///     The received <see cref="IDatagram" />.
        /// </param>
        private async Task DispatchReceivedMessage(IInboundDatagram dgram)
        {
            if (dgram != null)
            {
                if (dgram.Type == DatagramType.Message)
                {
                    var conMsg = (ConsoleMessageDatagram)dgram;
                    await this.AcknowledgeMessage(conMsg.SequenceNumber);
                    this.DispatchConsoleMessage(conMsg);
                    return;
                }

                // else, dgram is either login or command response
                this.LogTrace("BEFORE response.Dispatch");
                this.responseDispatcher.Dispatch(dgram);
                this.LogTrace("AFTER  response.Dispatch");
            }
        }


        /// <summary>
        ///     Sends a datagram back to the server acknowledging receipt of
        ///     a console message datagram.
        /// </summary>
        /// <param name="seqNumber">
        ///     The sequence number of the received <see cref="ConsoleMessageDatagram" />.
        /// </param>
        private async Task AcknowledgeMessage(byte seqNumber)
        {
            await this.SendDatagramAsync(new AcknowledgeMessageDatagram(seqNumber));
            this.LogTraceFormat("M#{0:000} Acknowledged", seqNumber);
        }


        /// <summary>
        ///     Dispatches received console messages to the appropriate
        ///     threading context (e.g. the UI thread or the ASP.NET context),
        ///     by using AsyncOperation.
        /// </summary>
        /// <param name="dgram">
        ///     The <see cref="ConsoleMessageDatagram" />
        ///     representing the received console message.
        /// </param>
        /// <remarks>
        ///     The context switch is costly, but usually what the
        ///     library user will expect.
        /// </remarks>
        private void DispatchConsoleMessage(ConsoleMessageDatagram dgram)
        {
            var args = new MessageReceivedEventArgs(dgram);

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
            this.OnMessageReceived((MessageReceivedEventArgs)args);
        }
    }
}
