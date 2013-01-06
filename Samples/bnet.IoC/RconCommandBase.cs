// ----------------------------------------------------------------------------------------------------
// <copyright file="RConCommandBase.cs" company="Me">Copyright (c) 2012 St4l.</copyright>
// ----------------------------------------------------------------------------------------------------
namespace BNet.IoC
{
    using System;
    using BattleNET;
    using log4net;


    public abstract class RConCommandBase : IRConCommand
    {
        public RConCommandMetadata Metadata { get; set; }

        public abstract string RConCommandText { get; }

        public ILog Log { get; set; }

        public CommandExecContext Context { get; set; }

        protected string RawResponse { get; set; }


        public virtual void Execute(BattlEyeClient beClient, int timeoutSecs = 10)
        {
            this.RawResponse = null;

            BattlEyeCommandResult result = beClient.SendCommandPacket(
                this.RConCommandText, 
                handler: (o, args) => this.RawResponse = args.Message, 
                timeOutInSecs: timeoutSecs);

            if (result != BattlEyeCommandResult.Success)
            {
                throw new ApplicationException("Could not send command: " + result);
            }

            while (beClient.CommandQueue > 0)
            {
                /* wait until server acknowledged all commands */
            }
        }


        public virtual bool ExecuteSingle()
        {
            var beClient = new BattlEyeClient(this.Context.Server.LoginCredentials)
                               {
                                   ReconnectOnPacketLoss = true, 
                                   DiscardConsoleMessages = true
                               };

            var connect = beClient.Connect();
            if (connect != BattlEyeConnectionResult.Success)
            {
                beClient.Disconnect();
                throw new ApplicationException("ERROR: Could not connect to the server. " + connect);
            }

            this.Log.DebugFormat("Sending command: '{0}'", this.Metadata.Name);
            this.Execute(beClient);
            beClient.Disconnect();
            return true;

            // ~beClient()
        }
    }
}
