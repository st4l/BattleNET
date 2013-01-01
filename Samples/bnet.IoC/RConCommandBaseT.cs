using System;
using System.Collections.Generic;
using System.Threading;
using BattleNET;

namespace bnet.IoC
{
    public abstract class RConCommandBase<TResultType> : RConCommandBase, IRConCommand<TResultType>
        where TResultType : class
    {
        public TResultType Result { get; protected set; }
        protected abstract void ParseResponse();


        public bool ExecSingleAwaitResponse(BattlEyeLoginCredentials credentials)
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
                this.ExecAwaitResponse(beClient);
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

            return
                !EqualityComparer<TResultType>.Default.Equals(this.Result, default(TResultType));

            // ~beClient()
        }




        public virtual void ExecAwaitResponse(BattlEyeClient beClient, int timeoutSecs = 10)
        {
            this.RawResponse = null;

            var result = beClient.SendCommandPacket(RConCommandText,
                handler: (o, args) => this.RawResponse = args.Message,
                timeOutInSecs: timeoutSecs);

            if (result != BattlEyeCommandResult.Success)
            {
                throw new ApplicationException("Could not send command: " + result);
            }
            while (beClient.CommandQueue > 0)
            { /* wait until server acknowledged all commands */ }

            var timeout = DateTime.Now.AddSeconds(timeoutSecs);
            while (DateTime.Now < timeout && RawResponse == null)
            { Thread.Sleep(500); }

            if (RawResponse == null)
            {
                throw new TimeoutException("ERROR: Timeout while waiting for command response.");
            }

            try
            {
                ParseResponse();
            }
            catch (Exception e)
            {
                this.Result = default(TResultType);
                throw new ApplicationException("ERROR: Could not parse response: \r\n" + RawResponse, e);
            }
        }


    }
}
