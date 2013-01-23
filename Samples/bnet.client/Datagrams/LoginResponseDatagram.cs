// ----------------------------------------------------------------------------------------------------
// <copyright file="LoginResponseDatagram.cs" company="Me">Copyright (c) 2012 St4l.</copyright>
// ----------------------------------------------------------------------------------------------------
namespace BNet.Client.Datagrams
{
    public class LoginResponseDatagram : InboundDatagramBase
    {
        public LoginResponseDatagram(byte[] buffer)
        {
            this.Success = buffer[Constants.LoginReturnCodeIndex] == 1;
        }


        public bool Success { get; private set; }

        public override DatagramType Type
        {
            get { return DatagramType.Login; }
        }
    }
}
