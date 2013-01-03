// ----------------------------------------------------------------------------------------------------
// <copyright file="UpdateDbPlayersCommand.cs" company="Me">Copyright (c) 2012 St4l.</copyright>
// ----------------------------------------------------------------------------------------------------
namespace BNet.AdvCommands
{
    using System;
    using System.Collections.Generic;
    using System.Data.Entity;
    using BNet.BaseCommands;
    using BNet.IoC;
    using BattleNET;
    using BNet.AdvCommands.misc;
    using BNet.Data;


    public class UpdateDbPlayersCommand : GetPlayersCommand
    {
        public override string Description
        {
            get { return "Updates online players in the database."; }
        }

        public override string Name
        {
            get { return "update_dbplayers"; }
        }


        public override void ExecAwaitResponse(BattlEyeClient beClient, int timeoutSecs = 10)
        {
            throw new NotImplementedException();
        }


        public override void Execute(BattlEyeClient beClient, int timeoutSecs = 10)
        {
            throw new NotImplementedException();
        }


        public override bool ExecSingleAwaitResponse(ServerInfo serverInfo)
        {
            if (base.ExecSingleAwaitResponse(serverInfo))
            {
                this.UpdateDatabase(serverInfo, this.Result);
                return true;
            }

            return false;
        }


        #region Methods



        private void LogSql(DbContext dbcontext, string command)
        {
            // http://www.codeproject.com/Articles/499902/Profiling-Entity-Framework-5-in-code
            // call HookSaveChanges extension method
            this.Log.Debug(command);
        }


        private void UpdateDatabase(ServerInfo state, IEnumerable<PlayerInfo> players)
        {
            using (var db = new BNetDb())
            {
                db.HookSaveChanges(this.LogSql);

                db.dayz_clear_online(state.ServerId);

                foreach (var p in players)
                {
                    db.dayz_online.Add(
                        new dayz_online
                            {
                                dayz_server_id = state.ServerId, 
                                guid = p.Guid, 
                                ip_address = p.IpAddress, 
                                lobby = (byte)(p.InLobby ? 1 : 0), 
                                name = p.Name, 
                                ping = p.Ping, 
                                slot = (byte)p.Id, 
                                verified = (sbyte)(p.Verified ? 1 : 0)
                            });
                }

                db.SaveChanges();
            }
        }



        #endregion


    }
}
