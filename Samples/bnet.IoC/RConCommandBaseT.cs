namespace BNet.IoC
{
    using System;
    using System.Threading;
    using BattleNET;


    public abstract class RConCommandBase<TResultType> : RConCommandBase, IRConCommand<TResultType>
        where TResultType : class
    {
        public TResultType Result { get; protected set; }


        public virtual void ExecAwaitResponse(BattlEyeClient beClient, int timeoutSecs = 10)
        {
            this.Execute(beClient, timeoutSecs);

            DateTime timeout = DateTime.Now.AddSeconds(timeoutSecs);
            while (DateTime.Now < timeout && this.RawResponse == null)
            {
                Thread.Sleep(500);
            }

            if (this.RawResponse == null)
            {
                throw new TimeoutException("ERROR: Timeout while waiting for command response.");
            }

            try
            {
                this.Result = this.ParseResponse(this.RawResponse);
            }
            catch (Exception e)
            {
                throw new ApplicationException(
                    "ERROR: Could not parse response: \r\n" + this.RawResponse, e);
            }
        }


        public virtual bool ExecSingleAwaitResponse(ServerInfo serverInfo)
        {
            var beClient = new BattlEyeClient(serverInfo.LoginCredentials)
                               {
                                   ReconnectOnPacketLoss = true, 
                                   DiscardConsoleMessages = true
                               };

            BattlEyeConnectionResult connect = beClient.Connect();
            if (connect != BattlEyeConnectionResult.Success)
            {
                beClient.Disconnect();
                throw new ApplicationException("ERROR: Could not connect to the server. " + connect);
            }

            this.Log.DebugFormat("Sending command: '{0}'", this.Name);
            this.ExecAwaitResponse(beClient);
            beClient.Disconnect();

            return this.Result != null;

            // ~beClient()
        }


        #region Methods

        protected abstract TResultType ParseResponse(string rawResponse);

        #endregion
    }
}
