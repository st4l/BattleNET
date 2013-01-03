namespace BNet.BaseCommands
{
    using System.Globalization;
    using BNet.IoC;
    using BattleNET;


    public class KickAllCommand : RConCommandBase
    {
        public KickAllCommand()
        {
            this.KickReason = "SERVER IS RESTARTING";
        }


        public override string Description
        {
            get { return "Kicks all players from the server."; }
        }

        public string KickReason { get; set; }

        public override string Name
        {
            get { return "kickall"; }
        }

        public override string RConCommandText
        {
            get { return "Kick "; }
        }


        public override void Execute(BattlEyeClient beClient, int timeoutSecs = 10)
        {
            for (int i = 0; i <= 100; i++)
            {
                string cmd = string.Format(
                    CultureInfo.InvariantCulture, "Kick {0} {1}", i, this.KickReason);
                this.Log.InfoFormat("Sending command: '{0}'", cmd);
                this.Log.Info(beClient.SendCommandPacket(cmd).ToString());
            }

            while (beClient.CommandQueue > 0)
            {
                /* wait until server received all packets */
            }
        }
    }
}
