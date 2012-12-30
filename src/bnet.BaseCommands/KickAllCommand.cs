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
        public override string Name { get { return "kickall"; } }
        public override string Description
        {
            get { return "Kicks all the players from the server."; }
        }

        public string KickReason { get; set; }  

        public override void Execute(BattlEyeClient beClient)
        {
            
            for (var i = 0; i <= 100; i++)
            {
                var cmd = "Kick " + i.ToString(CultureInfo.InvariantCulture) + " " + this.KickReason;
                Log.InfoFormat("Sending command: '{0}'", cmd);
                Log.Info(beClient.SendCommandPacket(cmd).ToString());
            }
            while (beClient.CommandQueue > 0) { /* wait until server received all packets */ }

        }
    }
}
