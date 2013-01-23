// ----------------------------------------------------------------------------------------------------
// <copyright file="ResponseMessageDispatcher.cs" company="Me">Copyright (c) 2012 St4l.</copyright>
// ----------------------------------------------------------------------------------------------------
namespace BNet.Client
{
    using BNet.Client.Datagrams;


    internal class ResponseMessageDispatcher
    {
        private ResponseHandler loginHandler;


        public void Register(ResponseHandler handler)
        {
            if (handler.SentDatagram.Type == DatagramType.Login)
            {
                this.loginHandler = handler;
            }
        }


        public void Dispatch(IInboundDatagram dgram)
        {
            if (dgram.Type == DatagramType.Login
                && this.loginHandler != null)
            {
                this.loginHandler.Return(dgram);
            }
        }
    }
}
