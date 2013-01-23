// ----------------------------------------------------------------------------------------------------
// <copyright file="OutboundDatagramBase.cs" company="Me">Copyright (c) 2012 St4l.</copyright>
// ----------------------------------------------------------------------------------------------------
namespace BNet.Client.Datagrams
{
    using System;


    public abstract class OutboundDatagramBase : DatagramBase, IOutboundDatagram
    {
        public DateTime SentTime { get; internal set; }

        public abstract bool ExpectsResponse { get; }


        public byte[] Build()
        {
            var payload = this.BuildPayload();
            
            var crc = new Crc32(Crc32.DefaultPolynomialReversed, Crc32.DefaultSeed);
            var checksum = crc.ComputeHash(payload);
            Array.Reverse(checksum);

            var payloadLen = Buffer.ByteLength(payload);
            var result = new byte[6 + payloadLen];
            Buffer.SetByte(result, 0, 0x42); // "B"
            Buffer.SetByte(result, 1, 0x45); // "E"
            Buffer.BlockCopy(checksum, 0, result, 2, 4);
            Buffer.BlockCopy(payload, 0, result, 6, payloadLen);

            return result;
        }


        protected abstract byte[] BuildPayload();
    }
}
