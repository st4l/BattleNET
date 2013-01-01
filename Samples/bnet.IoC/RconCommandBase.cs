using System;
using System.Collections.Generic;
using BattleNET;
using log4net;

namespace bnet.IoC
{
    public abstract class RConCommandBase : IRConCommand
    {
        public abstract string RConCommandText { get; }
        public abstract string Name { get; }
        public ILog Log { get; set; }
        public abstract string Description { get; }
        protected string RawResponse;


        public virtual bool ExecuteSingle(BattlEyeLoginCredentials credentials)
        {

            var beClient = new BattlEyeClient(credentials) { ReconnectOnPacketLoss = true, DiscardConsoleMessages = true };

            var connect = beClient.Connect();
            if (connect != BattlEyeConnectionResult.Success)
            {
                beClient.Disconnect();
                Log.Error("ERROR: Could not connect to the server. " + connect);
                return false;
            }

            try
            {
                Log.DebugFormat("Sending command: '{0}'", this.Name);
                this.Execute(beClient);
                while (beClient.CommandQueue > 0)
                {
                    /* wait until server received all packets */
                }
            }
            catch (TimeoutException timeoutException)
            {
                Log.Error(timeoutException.Message, timeoutException);
                return false;
            }
            catch (ApplicationException applicationException)
            {
                Log.Error(applicationException.Message, applicationException);
                return false;
            }
            finally
            {
                beClient.Disconnect();
            }

            return true;
            // ~beClient()
        }


        public virtual void Execute(BattlEyeClient beClient)
        {
            this.RawResponse = null;

            var result = beClient.SendCommandPacket(RConCommandText,
                handler: (o, args) => this.RawResponse = args.Message);

            if (result != BattlEyeCommandResult.Success)
            {
                throw new ApplicationException("Could not send command: " + result);
            }
            while (beClient.CommandQueue > 0)
            { /* wait until server acknowledged all commands */ }
        }



    }

}