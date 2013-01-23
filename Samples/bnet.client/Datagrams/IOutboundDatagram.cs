// ----------------------------------------------------------------------------------------------------
// <copyright file="IOutboundDatagram.cs" company="Me">Copyright (c) 2012 St4l.</copyright>
// ----------------------------------------------------------------------------------------------------
namespace BNet.Client.Datagrams
{
    public interface IOutboundDatagram : IDatagram
    {
        bool ExpectsResponse { get; }

        byte[] Build();
    }
}
