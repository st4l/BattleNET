using System;
using System.Threading;
using BattleNET;

namespace bnet.IoC
{
    public abstract class RConCommandBase<TResultType> : RConCommandBase, IRConCommand<TResultType>
        where TResultType : class
    {
        public TResultType Result { get; protected set; }


        public virtual bool ExecSingleAwaitResponse(BattlEyeLoginCredentials credentials)
        {
            var beClient = new BattlEyeClient(credentials)
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

            Log.DebugFormat("Sending command: '{0}'", Name);
            ExecAwaitResponse(beClient);
            beClient.Disconnect();

            return Result != null;

            // ~beClient()
        }


        public virtual void ExecAwaitResponse(BattlEyeClient beClient, int timeoutSecs = 10)
        {
            Execute(beClient, timeoutSecs);

            DateTime timeout = DateTime.Now.AddSeconds(timeoutSecs);
            while (DateTime.Now < timeout && RawResponse == null)
            {
                Thread.Sleep(500);
            }
            if (RawResponse == null)
            {
                throw new TimeoutException("ERROR: Timeout while waiting for command response.");
            }

            try
            {
                Result = ParseResponse(RawResponse);
            }
            catch (Exception e)
            {
                throw new ApplicationException("ERROR: Could not parse response: \r\n" + RawResponse, e);
            }
        }


        protected abstract TResultType ParseResponse(string rawResponse);
    }
}
