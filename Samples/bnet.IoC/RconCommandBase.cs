namespace BNet.IoC
{
    using System;
    using BattleNET;
    using log4net;


    public abstract class RConCommandBase : IRConCommand
    {
        public abstract string Description { get; }

        public ILog Log { get; set; }

        public abstract string Name { get; }

        public abstract string RConCommandText { get; }


        #region Properties

        protected string RawResponse { get; set; }

        #endregion


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


        public virtual bool ExecuteSingle(BattlEyeLoginCredentials credentials)
        {
            var beClient = new BattlEyeClient(credentials)
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

            this.Log.DebugFormat("Sending command: '{0}'", this.Name);
            this.Execute(beClient);
            beClient.Disconnect();
            return true;

            // ~beClient()
        }
    }
}
