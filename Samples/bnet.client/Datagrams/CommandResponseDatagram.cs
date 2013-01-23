// ----------------------------------------------------------------------------------------------------
// <copyright file="CommandResponseDatagram.cs" company="Me">Copyright (c) 2012 St4l.</copyright>
// ----------------------------------------------------------------------------------------------------
namespace BNet.Client.Datagrams
{
    public class CommandResponseDatagram : InboundDatagramBase
    {
        public CommandResponseDatagram(byte[] buffer)
        {
            this.OriginalSequenceNumber = buffer[Constants.CommandResponseSeqIndex];
        }


        public override DatagramType Type
        {
            get { return DatagramType.Command; }
        }

        public byte OriginalSequenceNumber { get; set; }
    }
}
