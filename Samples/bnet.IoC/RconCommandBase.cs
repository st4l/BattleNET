using System;
using BattleNET;
using log4net;

namespace bnet.IoC
{
    public abstract class RConCommandBase : IRConCommand
    {
        protected string RawResponse;
        public abstract string RConCommandText { get; }
        public abstract string Name { get; }

        public ILog Log { get; set; }
        public abstract string Description { get; }


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

            Log.DebugFormat("Sending command: '{0}'", Name);
            Execute(beClient);
            beClient.Disconnect();
            return true;
            // ~beClient()
        }


        public virtual void Execute(BattlEyeClient beClient, int timeoutSecs = 10)
        {
            RawResponse = null;

            BattlEyeCommandResult result = beClient.SendCommandPacket(RConCommandText,
                                                                      handler: (o, args) => RawResponse = args.Message,
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
    }
}
