using System.Globalization;
using BattleNET;
using bnet.IoC;

namespace bnet.BaseCommands
{
    public class KickAllCommand : RConCommandBase
    {
        public KickAllCommand()
        {
            KickReason = "SERVER IS RESTARTING";
        }


        public override string RConCommandText
        {
            get { return "Kick "; }
        }

        public override string Name
        {
            get { return "kickall"; }
        }

        public override string Description
        {
            get { return "Kicks all the players from the server."; }
        }

        public string KickReason { get; set; }


        public override void Execute(BattlEyeClient beClient, int timeoutSecs = 10)
        {
            for (int i = 0; i <= 100; i++)
            {
                string cmd = "Kick " + i.ToString(CultureInfo.InvariantCulture) + " " + KickReason;
                Log.InfoFormat("Sending command: '{0}'", cmd);
                Log.Info(beClient.SendCommandPacket(cmd).ToString());
            }
            while (beClient.CommandQueue > 0)
            {
                /* wait until server received all packets */
            }
        }
    }
}
