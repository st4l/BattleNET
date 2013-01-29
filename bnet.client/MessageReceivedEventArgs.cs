// ----------------------------------------------------------------------------------------------------
// <copyright file="MessageReceivedHandlerArgs.cs" company="Me">Copyright (c) 2012 St4l.</copyright>
// ----------------------------------------------------------------------------------------------------
namespace BNet.Client
{
    using System;
    using BNet.Client.Datagrams;


    public class MessageReceivedEventArgs : EventArgs
    {
        public MessageReceivedEventArgs(ConsoleMessageDatagram datagram)
        {
            if (datagram == null)
            {
                throw new ArgumentNullException("datagram");
            }

            this.Datagram = datagram;
            this.MessageBody = datagram.MessageBody;
        }


        public string MessageBody { get; set; }

        internal ConsoleMessageDatagram Datagram { get; private set; }
    }
}
