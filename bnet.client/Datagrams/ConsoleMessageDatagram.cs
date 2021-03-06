﻿// ----------------------------------------------------------------------------------------------------
// <copyright file="ConsoleMessageDatagram.cs" company="Me">Copyright (c) 2012 St4l.</copyright>
// ----------------------------------------------------------------------------------------------------
namespace BNet.Client.Datagrams
{
    using System.Text;


    public class ConsoleMessageDatagram : InboundDatagramBase
    {
        public ConsoleMessageDatagram(byte[] buffer)
        {
            this.Parse(buffer);
        }


        public override DatagramType Type
        {
            get { return DatagramType.Message; }
        }

        public byte SequenceNumber { get; private set; }

        public string MessageBody { get; private set; }


        private void Parse(byte[] buffer)
        {
            this.SequenceNumber = buffer[Constants.ConsoleMessageSequenceNumberIndex];

            this.MessageBody = Encoding.ASCII.GetString(
                buffer, 
                Constants.ConsoleMessageBodyStartIndex, 
                buffer.Length - Constants.ConsoleMessageBodyStartIndex);
        }
    }
}
