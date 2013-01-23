// ----------------------------------------------------------------------------------------------------
// <copyright file="DatagramType.cs" company="Me">Copyright (c) 2012 St4l.</copyright>
// ----------------------------------------------------------------------------------------------------
namespace BNet.Client.Datagrams
{
    public enum DatagramType : byte
    {
        Login = 0, 

        Command = 1, 

        Message = 2
    }
}
