// ----------------------------------------------------------------------------------------------------
// <copyright file="OutboundDatagramQueue.cs" company="Me">Copyright (c) 2012 St4l.</copyright>
// ----------------------------------------------------------------------------------------------------
namespace BNet.Client
{
    using System.Collections.Concurrent;
    using BNet.Client.Datagrams;


    internal class OutboundDatagramQueue : ConcurrentQueue<IDatagram>
    {
    }
}
