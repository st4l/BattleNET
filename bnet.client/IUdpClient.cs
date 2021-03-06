﻿// ----------------------------------------------------------------------------------------------------
// <copyright file="IUdpClient.cs" company="Me">Copyright (c) 2012 St4l.</copyright>
// ----------------------------------------------------------------------------------------------------

namespace BNet.Client
{
    using System.Net.Sockets;
    using System.Threading.Tasks;


    internal interface IUdpClient
    {
        Task<int> SendAsync(byte[] buffer, int length);

        Task<UdpReceiveResult> ReceiveAsync();

        void Close();
    }
}
