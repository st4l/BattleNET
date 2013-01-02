using System;
using System.Collections.Generic;
using System.Globalization;
using bnet.IoC;

namespace bnet.BaseCommands
{
    public class GetPlayersCommand : RConCommandBase<List<PlayerInfo>>
    {
        public override string RConCommandText
        {
            get { return "players"; }
        }


        public override string Name
        {
            get { return "getplayers"; }
        }


        public override string Description
        {
            get { return "Retrieves the list of players online."; }
        }


        protected override List<PlayerInfo> ParseResponse(string rawResponse)
        {
            // Players on server:
            // [#] [IP Address]:[Port] [Ping] [GUID] [Name]
            // --------------------------------------------------
            // 0   103.77.52.177:2304    32   1ef92993d1e8f2512422da34c9f975f1(OK) Jhon Denton (Lobby)
            // 0   103.77.52.177:2304    32   -  Pixie
            // (19 players in total)

            var results = new List<PlayerInfo>();
            string[] lines = rawResponse.Split(new[] {(char)0x0A}, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 3; i <= lines.Length - 2; i++)
            {
                string[] tokens = lines[i].Split(new char[0], StringSplitOptions.RemoveEmptyEntries);

                bool lobby;
                string name = ParseName(tokens, out lobby);
                // split Guid token
                string[] guidtokens = tokens[3] == "-" ? new[] {"", ""} : tokens[3].Split('(');

                results.Add(new PlayerInfo
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
            LogResults(results);
            return results;
        }


        private void LogResults(List<PlayerInfo> results)
        {
            Log.Info("Players received: ");
            Log.InfoFormat("{0,2}  {1,-30}  {2,32} {3,1} {4,5} {5,22} {6}",
                           "Id", "Name", "Guid", "V", "Ping", "Ip address",
                           "In Lobby");
            foreach (PlayerInfo p in results)
            {
                Log.InfoFormat("{0:00}  {1,-30}  {2,32} {3,1} {4,5} {5,22} {6}",
                               p.Id, p.Name, p.Guid, p.Verified.ToString()[0], p.Ping, p.IpAddress,
                               p.InLobby ? "(in lobby)" : string.Empty);
            }
            Log.InfoFormat("{0} players", results.Count);
            Log.Info("---");
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
