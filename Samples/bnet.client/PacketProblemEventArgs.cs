using System;

namespace BNet.Client
{
    public class PacketProblemEventArgs : EventArgs
    {
        public PacketProblemType PacketProblemType { get; private set; }


        public PacketProblemEventArgs(PacketProblemType packetProblemType)
        {
            this.PacketProblemType = packetProblemType;
        }
    }
}