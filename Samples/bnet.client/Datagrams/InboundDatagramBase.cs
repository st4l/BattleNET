// ----------------------------------------------------------------------------------------------------
// <copyright file="InboundDatagramBase.cs" company="Me">Copyright (c) 2012 St4l.</copyright>
// ----------------------------------------------------------------------------------------------------
namespace BNet.Client.Datagrams
{
    using System;


    public abstract class InboundDatagramBase : DatagramBase, IInboundDatagram
    {
        /// <summary>The inbound datagram base.</summary>
        /// <remarks>
        ///     'B'(0x42) | 'E'(0x45) | 4-byte CRC32 checksum of the subsequent bytes | 0xFF
        ///          0         1                2       3       4       5                6
        /// 
        ///     0x00 | (0x01 (successfully logged in) OR 0x00 (failed))
        ///      7                       8
        /// 
        ///     0x01 | received 1-byte sequence number | (possible header and/or response (ASCII string without null-terminator) OR nothing)
        ///      7                  8                               9  . . .
        /// 
        ///              0x00 | number of packets for this response | 0-based index of the current packet
        ///               10                  11                                12  . . .
        /// 
        ///     0x02 | 1-byte sequence number (starting at 0) | server message (ASCII string without null-terminator)
        ///      7                   8                                   9  . . .
        /// </remarks>
        protected InboundDatagramBase()
        {
            this.Timestamp = DateTime.Now;
        }


        public DateTime Timestamp { get; private set; }

        /// <summary>
        /// Parses received bytes from a BattlEye RCon server.
        /// </summary>
        /// <param name="bytes">The received bytes.</param>
        /// <returns>An <see cref="IInboundDatagram"/> containing the received information.</returns>
        public static IInboundDatagram ParseReceivedBytes(byte[] bytes)
        {
            var type = (DatagramType)bytes[Constants.DatagramTypeIndex];
            switch (type)
            {
                case DatagramType.Login:
                    return new LoginResponseDatagram(bytes);
                case DatagramType.Command:
                    return new CommandResponseDatagram(bytes);
                case DatagramType.Message:
                    return new ConsoleMessageDatagram(bytes);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
