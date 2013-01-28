// ----------------------------------------------------------------------------------------------------
// <copyright file="CommandResponseDatagram.cs" company="Me">Copyright (c) 2012 St4l.</copyright>
// ----------------------------------------------------------------------------------------------------

using System;
using System.Text;

namespace BNet.Client.Datagrams
{
    public class CommandResponseDatagram : InboundDatagramBase
    {
        internal CommandResponseDatagram(byte[] buffer)
        {
            this.OriginalSequenceNumber = buffer[Constants.CommandResponseSequenceNumberIndex];
            int len = Buffer.ByteLength(buffer);
            var body = new byte[len - 9];
            Buffer.BlockCopy(buffer, 9, body, 0, len - 9);
            this.Body = Encoding.ASCII.GetString(body);
        }


        public string Body { get; private set; }


        public override DatagramType Type
        {
            get { return DatagramType.Command; }
        }

        public byte OriginalSequenceNumber { get; private set; }
    }
}
