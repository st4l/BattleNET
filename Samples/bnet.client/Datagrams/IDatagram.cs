﻿// ----------------------------------------------------------------------------------------------------
// <copyright file="IDatagram.cs" company="Me">Copyright (c) 2012 St4l.</copyright>
// ----------------------------------------------------------------------------------------------------
namespace BNet.Client.Datagrams
{
    public interface IDatagram
    {
        DatagramType Type { get; }
    }
}
