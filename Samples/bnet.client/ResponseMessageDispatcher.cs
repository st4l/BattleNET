// ----------------------------------------------------------------------------------------------------
// <copyright file="ResponseMessageDispatcher.cs" company="Me">Copyright (c) 2012 St4l.</copyright>
// ----------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Diagnostics;
using BNet.Client.Datagrams;

namespace BNet.Client
{
    internal class ResponseMessageDispatcher
    {
        private ResponseHandler loginHandler;
        private readonly Dictionary<byte, ResponseHandler> cmdResponseHandlers =
            new Dictionary<byte, ResponseHandler>();

        public void Register(ResponseHandler handler)
        {
            if (handler.SentDatagram.Type == DatagramType.Login)
            {
                this.loginHandler = handler;
                return;
            }

            // it's a command.
            var cmdDgram = (CommandDatagram)handler.SentDatagram;
            lock (this.cmdResponseHandlers)
            {
                this.cmdResponseHandlers.Add(cmdDgram.SequenceNumber, handler);
            }
        }


        public void Dispatch(IInboundDatagram dgram)
        {
            if (dgram.Type == DatagramType.Login
                && this.loginHandler != null)
            {
                this.loginHandler.Return(dgram);
                return;
            }

            // it's a command response.
            var cmdDgram = (CommandResponseDatagram)dgram;
            lock (this.cmdResponseHandlers)
            {
                var handler = this.cmdResponseHandlers[cmdDgram.OriginalSequenceNumber];
                this.cmdResponseHandlers.Remove(cmdDgram.OriginalSequenceNumber);
                handler.Return(cmdDgram);
                Debug.WriteLine("handler for command packet {0} invoked", cmdDgram.OriginalSequenceNumber);
            }
        }
    }
}
