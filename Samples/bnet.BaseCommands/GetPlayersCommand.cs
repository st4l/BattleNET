using System;
using System.Collections.Generic;
using System.Globalization;
using BattleNET;
using bnet.IoC;

namespace bnet.BaseCommands
{
    public class GetPlayersCommand : RConCommandBase
    {
        private string rawResponse;
        public List<PlayerInfo> Players { get; private set; }

        public override string Name
        {
            get { return "getplayers"; }
        }

        public override string Description
        {
            get { return "Retrieves the list of players online."; }
        }

        public override void Execute(BattlEyeClient beClient)
        {
            this.Players = null;
            this.rawResponse = null;
            this.Players = new List<PlayerInfo>();
            beClient.MessageReceivedEvent += beClient_MessageReceivedEvent;
            var result = beClient.SendCommandPacket(EBattlEyeCommand.Players);
            if (result != EBattlEyeCommandResult.Success )
            {
                beClient.MessageReceivedEvent -= beClient_MessageReceivedEvent; 
                throw new ApplicationException("Could not send command. Return value: " + result);
            }
            while (beClient.CommandQueue > 0) 
            { /* wait until server received all packets */ }
            
            var timeout = DateTime.Now.AddSeconds(10);
            while(DateTime.Now < timeout && rawResponse == null) 
            { /* wait for a response, or timeout */}

            if (rawResponse == null)
            {
                beClient.MessageReceivedEvent -= beClient_MessageReceivedEvent;
                throw new TimeoutException("ERROR: Timeout while trying to fetch online players.");
            }

            try
            {
                ParseResponse();
            }
            catch (Exception e)
            {
                this.Players = null;
                beClient.MessageReceivedEvent -= beClient_MessageReceivedEvent;
                throw new ApplicationException("ERROR: Could not parse response. " + e);
            }
            beClient.MessageReceivedEvent -= beClient_MessageReceivedEvent;
        }


        private void ParseResponse()
        {

            // Players on server:
            // [#] [IP Address]:[Port] [Ping] [GUID] [Name]
            // --------------------------------------------------
            // 0   108.33.76.177:2304    32   1ef9299ed1e8f2e12422da34c9f975f1(OK) Jhon Denton
            // 1   82.83.202.243:2304    125  8c2015095e96f5a67c431d4392ebb4f7(OK) Shark2202
            // 2   174.118.129.215:2304  63   ff3b96e0179e91a1e479cd8ba1ed0c5b(OK) Ghoast
            // 3   65.33.125.56:2304     15   0fb44b2eb6158fb274843e92a436a813(OK) Owner
            // 4   217.246.218.88:2304   125  50005937b73cbf70ca4559a03463fef1(OK) Igneous
            // 5   24.137.37.223:2304    79   b947435c5f5feafc2fbf4d98376d4a4e(OK) Jesse
            // 6   97.103.173.7:2304     16   211ead0333445f3a6018bd58ce7f5c9c(OK) 7r1gg3r[M4L]
            // 7   68.202.255.22:2304    16   a6a6e787e0783e75911c53e8925098f7(OK) Mario[M4L]
            // 9   24.235.157.45:2304    94   d1c0efb8825028703eaf7f876debb564(OK) Ryan
            // 10  107.196.169.93:2304   110  917722e5d55a3002a128cb9b9f6de945(OK) Chang
            // 11  177.18.139.78:2304    141  34e9c0e0c8364a403c9a5bebf13d9dba(OK) pitekinhoo
            // 12  177.158.187.200:2304  78   5e7da66d7555d3608bd8f440f97e40bb(OK) Brother
            // 13  187.79.143.132:53577  110  882647f87dbe03cefb60354c7d6fe9fa(OK) FelipeS2Lais
            // 16  200.74.87.210:2309    125  1fda09e974a92b179c699661ea5cc43e(OK) GIULY !
            // 17  76.23.66.252:2304     47   b3b1c2134be1236749837a9078d82e4b(OK) Super Bambi
            // 20  72.28.210.64:2304     16   ff96dda4d83ce37151145a4d2cdbceff(OK) Rephase
            // 21  68.111.66.19:2304     63   c36adcbcd7d8cb8193c8013d02890790(OK) Hipster Police
            // 23  177.19.73.59:2304     78   cd8b8ee31eeed25b5008317551d6c43f(OK) Allan
            // 25  99.230.0.128:2304     47   0958e9ef908fcdad2f20a0ec406e1827(OK) NORMA
            // (19 players in total)
            
            Players = new List<PlayerInfo>();
            var lines = rawResponse.Split(new[] {(char)0x0A}, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 3; i <= lines.Length - 2; i++)
            {
                var tokens = lines[i].Split(new char[0], StringSplitOptions.RemoveEmptyEntries);

                bool lobby;
                var name = ParseName(tokens, out lobby);
                // split Guid token
                var guidtokens = tokens[3] == "-" ? new[] {"", ""} : tokens[3].Split('(');

                Players.Add( new PlayerInfo
                    {
                        Id = int.Parse(tokens[0], CultureInfo.InvariantCulture),
                        IpAddress = tokens[1],
                        Ping = int.Parse(tokens[2], CultureInfo.InvariantCulture),
                        Guid = guidtokens[0],
                        Verified = guidtokens[1] == "OK)",
                        Name = name,
                        InLobby = lobby
                    });
            }
            LogResults();
        }


        private void LogResults()
        {
            Log.Info("Players received: ");
            Log.InfoFormat("{0,2}  {1,-30}  {2,32} {3,1} {4,5} {5,22} {6}",
                              "Id", "Name", "Guid", "V", "Ping", "Ip address",
                              "In Lobby");
            foreach (var p in Players)
            {
                Log.InfoFormat("{0:00}  {1,-30}  {2,32} {3,1} {4,5} {5,22} {6}",
                                  p.Id, p.Name, p.Guid, p.Verified.ToString()[0], p.Ping, p.IpAddress,
                                  p.InLobby ? "(in lobby)" : string.Empty);
            }
            Log.InfoFormat("{0} players", Players.Count);
        }


        private static string ParseName(string[] tokens, out bool lobby)
        {
            string name;
            lobby = false;
            if (tokens[tokens.Length - 1] == "(Lobby)")
            {
                name = string.Join(" ", tokens, 4, tokens.Length - 5);
                lobby = true;
            }
            else
            {
                name = string.Join(" ", tokens, 4, tokens.Length - 4);
            }
            return name;
        }


        void beClient_MessageReceivedEvent(BattlEyeMessageEventArgs args)
        {
            if (args.Message.StartsWith("Players on server:"))
            {
                this.rawResponse = args.Message;
            }
        }


    }
}
