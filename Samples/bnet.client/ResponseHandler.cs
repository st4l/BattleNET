// ----------------------------------------------------------------------------------------------------
// <copyright file="ResponseHandler.cs" company="Me">Copyright (c) 2012 St4l.</copyright>
// ----------------------------------------------------------------------------------------------------
namespace BNet.Client
{
    using System.Threading;
    using System.Threading.Tasks;
    using BNet.Client.Datagrams;


    public class ResponseHandler
    {
        private readonly ManualResetEventSlim flag;


        /// <summary>
        ///     Creates a new instance of <see cref="ResponseHandler" /> for
        ///     the specified sent datagram and timeout in milliseconds.
        /// </summary>
        /// <param name="sentDatagram">The sent datagram of which a response is awaited.</param>
        public ResponseHandler(IOutboundDatagram sentDatagram)
        {
            this.SentDatagram = sentDatagram;
            this.flag = new ManualResetEventSlim(false);
        }


        /// <summary>
        ///     The Datagram that was sent, the response of which
        ///     this handler can wait for.
        /// </summary>
        public IOutboundDatagram SentDatagram { get; private set; }

        /// <summary>
        ///     After the response is received, the received response 
        ///     message.
        /// </summary>
        public IInboundDatagram ResponseDatagram { get; private set; }


        /// <summary>
        ///     Blocks the current thread until a response is received,
        ///     after which the <see cref="ResponseDatagram"/> property
        ///     will contain the received datagram.
        /// </summary>
        /// <param name="timeout">Timeout in milliseconds.</param>
        /// <returns>
        ///     True if the response was received; otherwise, false.
        /// </returns>
        public Task<bool> WaitForResponse(int timeout = 1000 * 3)
        {
            var task = Task.Factory.StartNew(() => this.flag.Wait(timeout));
            task.ConfigureAwait(false);
            return task;
        }


        /// <summary>
        ///     Accepts the response datagram and signals the waiting
        ///     thread to continue.
        /// </summary>
        /// <param name="result"></param>
        internal void Return(IInboundDatagram result)
        {
            this.ResponseDatagram = result;
            this.flag.Set();
        }
    }
}
