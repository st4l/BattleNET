using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using BNet.Client.Datagrams;

namespace BNet.Client
{
    internal sealed partial class MessageDispatcher
    {
        #region Nested type: KeepAliveTracker

        internal class KeepAliveTracker
        {
            public bool Acknowledged { get; set; }
            private readonly MessageDispatcher msgDispatcher;
            private readonly TimeSpan period = TimeSpan.FromSeconds(1);
            private readonly List<ResponseHandler> sentHandlers = new List<ResponseHandler>();
            private DateTime lastSendTime = DateTime.MinValue;
            private int maxTries = 5;
            private int sentCount;
            private SpinWait spinWait = new SpinWait();

            public bool Expired { get; set; }

            public KeepAliveTracker(MessageDispatcher msgDispatcher)
            {
                this.msgDispatcher = msgDispatcher;
            }


            public bool Ping()
            {
                
                if (this.Acknowledged)
                {
                    return true;
                }

                this.spinWait.SpinOnce();

                // check if we received an ack for any of the sent ones
                int acks = this.sentHandlers.Count(handler => handler.ResponseDatagram != null);
                // Debug.WriteLine("{1:mm:ss:fffff} acks = {0}", acks, DateTime.Now);
                if (acks > 0)
                {
                    this.msgDispatcher.keepAlivePacketsAcks += acks;
                    this.Acknowledged = true;
                    return true;
                }
                
                // if we haven't sent one
                // or last one sent more than (period) ago
                if (DateTime.Now - this.lastSendTime > this.period)
                {
                    if (this.sentCount == this.maxTries)
                    {
                        // we already sent (maxTries) and we're past waiting for the last sent one
                        this.Expired = true;
                        return false;
                    }

                    this.SendKeepAlivePacket();
                }

                return false;
            }




            private void SendKeepAlivePacket()
            {
                Debug.WriteLine("keep alive packet {0} sent", this.sentCount + 1);
                var keepAliveDgram = new CommandDatagram(
                    this.msgDispatcher.GetNextCommandSequenceNumber(), 
                    string.Empty);
                this.sentHandlers.Add(this.msgDispatcher.SendDatagramAsync(keepAliveDgram).Result);
                this.lastSendTime = DateTime.Now;
                this.sentCount++;
                this.msgDispatcher.keepAlivePacketsSent++;
                this.msgDispatcher.LogTraceFormat("C#{0:000} Sent keep alive command.", keepAliveDgram.SequenceNumber);
            }
        }

        #endregion
    }
}
