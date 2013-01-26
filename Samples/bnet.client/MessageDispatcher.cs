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
    using System.Runtime.ExceptionServices;
    using System.Security.Permissions;
    using System.Threading;
    using System.Threading.Tasks;
    using Datagrams;
    using log4net;
    using log4net.Core;


    /// <summary>
    ///     Receives messages from a remote Battleye RCon server
    ///     using the supplied <see cref="UdpClient" /> and
    ///     dispatches them accordingly.
    /// </summary>
    internal sealed class MessageDispatcher : IDisposable
    {
        private readonly ResponseMessageDispatcher responseDispatcher;

        private IUdpClient udpClient;

        private bool shutdown;

        private DateTime lastDgramReceivedTime;

        private ManualResetEventSlim shutdownLock;

        private bool hasStarted;

        private bool disposed;

        private AsyncOperation asyncOperation;

        private DateTime lastCmdSentTime;

        private int inCount;

        private int outCount;

        private bool forceShutdown;

        private bool mainLoopDead;
        private int parsedDatagramsCount;
        private int dispatchedConsoleMessages;

        private ILog Log { get; set; }

        /// <summary>
        ///     Initializes a new instance of <see cref="MessageDispatcher" />
        ///     and establishes the <see cref="UdpClient" /> to be used.
        /// </summary>
        /// <param name="udpClient">
        ///     The <see cref="UdpClient" /> to be used to connect to the
        ///     RCon server.
        /// </param>
        internal MessageDispatcher(IUdpClient udpClient)
        {
            // throw new ArgumentException("Test shall not pass.");
            this.udpClient = udpClient;
            this.responseDispatcher = new ResponseMessageDispatcher();
            this.Log = LogManager.GetLogger(this.GetType());
        }


        /// <summary>
        ///     Use C# destructor syntax for finalization code. 
        /// </summary>
        /// <remarks>
        ///     This destructor will run only if the Dispose method 
        ///     does not get called. 
        ///     It gives your base class the opportunity to finalize. 
        ///     Do not provide destructors in types derived from this class.
        /// </remarks>
        ~MessageDispatcher()
        {
            this.Dispose(false);
        }

        
        /// <summary>
        ///     Implement IDisposable.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }


        /// <summary>
        ///     Occurs when a console message is received from the RCon server.
        /// </summary>
        internal event EventHandler<MessageReceivedEventArgs> MessageReceived;


        /// <summary>
        ///     Occurs when a console message is received from the RCon server.
        /// </summary>
        internal event EventHandler<DisconnectedEventArgs> Disconnected;


        /// <summary>
        ///     Gets or sets a <see cref="Boolean" /> value that specifies
        ///     whether this <see cref="MessageDispatcher" /> discards all
        ///     console message datagrams received (the <see cref="MessageReceived" />
        ///     event is never raised).
        /// </summary>
        internal bool DiscardConsoleMessages { get; set; }


        /// <summary>
        ///     Starts acquiring and dispatching inbound messages in a new thread.
        /// </summary>
        /// <remarks>Starts the main message pump in a new thread.</remarks>
        internal void Start()
        {
            if (this.hasStarted)
            {
                throw new InvalidOperationException("Already running.");
            }

            this.hasStarted = true;

            this.asyncOperation = AsyncOperationManager.CreateOperation(null);

            var task = new Task(MainLoop);
            task.ContinueWith(this.ExceptionHandler, TaskContinuationOptions.OnlyOnFaulted);
            task.ContinueWith(this.AfterMainLoop, TaskContinuationOptions.OnlyOnRanToCompletion);
            task.ConfigureAwait(true);
            task.Start();

            // let's give the main pump some headway to start listening
            // task.Wait(500);
        }


        private void AfterMainLoop(Task task)
        {
            this.LogTrace("AFTER MAIN LOOP");
            this.Close();
        }


        /// <summary>
        ///     Stops all processing gracefully and disposes this instance.
        /// </summary>
        /// <remarks>Exits the main pump thread politely.</remarks>
        internal void Close()
        {
            if (this.disposed)
            {
                return;
            }

            this.LogTrace("CLOSE");

            if (!this.forceShutdown)
            {
                var args = new DisconnectedEventArgs();

                if (this.asyncOperation != null)
                {
                    this.asyncOperation.Post(this.RaiseDisconnected, args);
                }
                else
                {
                    this.RaiseDisconnected(args);
                }

                
                this.LogTrace("SHUTDOWN COMMENCING");
                this.shutdown = true;

                if (!this.mainLoopDead)
                {
                    this.shutdownLock = new ManualResetEventSlim(false);

                    // wait until the main thread is exited
                    this.LogTrace("WAITING FOR THREADS TO EXIT");
                    this.shutdownLock.Wait();
                }

                this.LogTrace("SHUTDOWN ACHIEVED - DISPOSING");
            }

            this.Dispose();
        }


        private void RaiseDisconnected(object args)
        {
            this.OnDisconnected((DisconnectedEventArgs)args);
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


        /// <summary>
        ///     Raises the <see cref="Disconnected" /> event.
        /// </summary>
        /// <param name="e">
        ///     An <see cref="DisconnectedEventArgs" /> that
        ///     contains the event data.
        /// </param>
        internal void OnDisconnected(DisconnectedEventArgs e)
        {
            if (this.Disconnected != null)
            {
                this.Disconnected(this, e);
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
            dgram.SentTime = DateTime.Now;
            this.outCount++;

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

        
        internal void UpdateMetrics(RConMetrics rConMetrics)
        {
            rConMetrics.InboundPacketCount += this.inCount;
            rConMetrics.OutboundPacketCount += this.outCount;
            rConMetrics.ParsedDatagramsCount += this.parsedDatagramsCount;
            rConMetrics.DispatchedConsoleMessages += this.dispatchedConsoleMessages;
        }


        /// <summary>
        ///     Dispose managed and unmanaged resources.
        /// </summary>
        /// <param name="disposing">
        ///     True unless we're called from the finalizer,
        ///     in which case only unmanaged resources can be disposed.
        /// </param>
        private void Dispose(bool disposing)
        {
            // Check to see if Dispose has already been called. 
            if (!this.disposed)
            {
                // If disposing equals true, dispose all managed 
                // and unmanaged resources. 
                if (disposing)
                {
                    // Release managed resources.
                    this.udpClient = null;

                    if (this.shutdownLock != null)
	                {
                        this.shutdownLock.Dispose();
                    }
                    
                }

                // Note disposing has been done.
                this.disposed = true;
            }
        }


        /// <summary>
        ///     The main message pump.
        /// </summary>
        [HostProtection(Synchronization = true, ExternalThreading = true)]
        private void MainLoop()
        {
            // throw new ArgumentException("Test shall not pass.");
            
            // Check whether the thread has previously been named 
            // to avoid a possible InvalidOperationException. 
            if (Thread.CurrentThread.Name == null)
            {
                Thread.CurrentThread.Name = "MainPUMP" + Thread.CurrentThread.ManagedThreadId;
            }

            var keepAlivePeriod = TimeSpan.FromSeconds(25);
            this.lastCmdSentTime = DateTime.Now.AddSeconds(-10);

            while (!this.shutdown)
            {
                this.LogTrace("Scheduling new receive task.");
                var task = this.ReceiveDatagramAsync();
                this.LogTrace("AFTER  scheduling new receive task.");

                do
                {
                    if (DateTime.Now - this.lastCmdSentTime > keepAlivePeriod)
                    {
                        var alive = this.SendKeepAlivePacket().Result;
                    }

                    this.LogTraceFormat("===== WAITING RECEIVE =====, Status={0}", task.Status);
                    task.Wait(500);
                    this.LogTraceFormat("====== DONE WAITING =======, Status={0}", task.Status);
                }
                while (!task.IsCompleted && !this.shutdown);
            }
            
            this.mainLoopDead = true;
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
            this.LogTrace("EXIT MAIN LOOP");

            if (this.shutdownLock != null)
            {
                // signal we're exiting the thread
                this.LogTrace("shutdownLock set.");
                this.shutdownLock.Set();
            }
        }


        /// <summary>
        ///     Handles a message asynchronously that was received from
        ///     the RCon server.
        /// </summary>
        [HostProtection(Synchronization = true, ExternalThreading = true)]
        private async Task ReceiveDatagramAsync()
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
            if (!this.ValidateReceivedDatagram(result))
            {
                this.LogTrace("INVALID datagram received");
                return;
            }

            this.lastDgramReceivedTime = DateTime.Now;
            byte dgramType = result.Buffer[Constants.DatagramTypeIndex];
            this.inCount++;
            this.LogTraceFormat("{0:0}    Type dgram received.", dgramType);

#if DEBUG
            // shutdown msg from server (used only for testing)
            if (dgramType == 0xFF)
            {
                this.LogTrace("SHUTDOWN DATAGRAM RECEIVED - SHUTTING DOWN.");
                this.shutdown = true;
                return;
            }
#endif

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


        private bool ValidateReceivedDatagram(UdpReceiveResult result)
        {
            if (result.Buffer == null || result.Buffer.Length < 7)
            {
                return false;
            }
            return true;
        }


        /// <summary>
        ///     Dispatches the received datagram to the appropriate target.
        /// </summary>
        /// <param name="dgram">
        ///     The received <see cref="IDatagram" />.
        /// </param>
        private async Task DispatchReceivedMessage(IInboundDatagram dgram)
        {
            this.parsedDatagramsCount++;
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
            this.dispatchedConsoleMessages++;
        }


        private void RaiseMessageReceived(object args)
        {
            this.OnMessageReceived((MessageReceivedEventArgs)args);
        }


        private void ExceptionHandler(Task task)
        {
            var exInfo = ExceptionDispatchInfo.Capture(task.Exception);
            this.forceShutdown = true;
            exInfo.Throw();
        }


        [Conditional("TRACE")]
        private void LogTrace(string msg)
        {
            this.Log.Logger.Log(
                this.Log.GetType(),
                Level.Debug,
                msg,
                null);
        }


        [Conditional("TRACE")]
        private void LogTraceFormat(string fmt, params object[] args)
        {
            this.Log.Logger.Log(
                this.Log.GetType(),
                Level.Debug,
                string.Format(CultureInfo.InvariantCulture, fmt, args),
                null);
        }
    }
}
