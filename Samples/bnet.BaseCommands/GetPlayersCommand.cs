using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
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
            
            var result = beClient.SendCommandPacket(BattlEyeCommand.Players, "", CmdResponseHandler);
            if (result != BattlEyeCommandResult.Success )
            {
                throw new ApplicationException("Could not send command: " + result);
            }
            while (beClient.CommandQueue > 0) 
            { /* wait until server acknowledged all commands */ }
            
            var timeout = DateTime.Now.AddSeconds(10);
            while(DateTime.Now < timeout && rawResponse == null) 
            { Thread.Sleep(500); }

            if (rawResponse == null)
            {
                throw new TimeoutException("ERROR: Timeout while trying to fetch online players.");
            }

            try
            {
                ParseResponse();
            }
            catch (Exception e)
            {
                this.Players = null;
                throw new ApplicationException("ERROR: Could not parse response. " + e);
            }
        }


        private void CmdResponseHandler(object source, BattlEyeCommandResponseEventArgs args)
        {
            if (args.Message.StartsWith("Players on server:"))
            {
                this.rawResponse = args.Message;
            }
        }



        private void ParseResponse()
        {

            // Players on server:
            // [#] [IP Address]:[Port] [Ping] [GUID] [Name]
            // --------------------------------------------------
            // 0   103.77.52.177:2304    32   1ef92993d1e8f2512422da34c9f975f1(OK) Jhon Denton (Lobby)
            // 0   103.77.52.177:2304    32   -  Pixie
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




    }
}
