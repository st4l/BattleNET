// ----------------------------------------------------------------------------------------------------
// <copyright file="UpdateDbPlayersCommand.cs" company="Me">Copyright (c) 2012 St4l.</copyright>
// ----------------------------------------------------------------------------------------------------
namespace BNet.AdvCommands
{
    using System.Data.Entity;
    using BattleNET;
    using BNet.AdvCommands.misc;
    using BNet.BaseCommands;
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


        public override bool ExecAwaitResponse(BattlEyeClient beClient, int timeoutSecs = 10)
        {
            if (base.ExecAwaitResponse(beClient, timeoutSecs))
            {
                this.UpdateDatabase();
                return true;
            }

            return false;
        }


        private void UpdateDatabase()
        {
            using (var db = new BNetDb(this.Context.DbConnectionString))
            {
                db.HookSaveChanges(this.LogSql);

                db.dayz_clear_online(this.Context.Server.ServerId);

                foreach (var p in this.Result)
                {
                    db.dayz_online.Add(
                        new dayz_online
                            {
                                dayz_server_id = this.Context.Server.ServerId, 
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


        private void LogSql(DbContext dbcontext, string command)
        {
            // http://www.codeproject.com/Articles/499902/Profiling-Entity-Framework-5-in-code
            // call HookSaveChanges extension method
            this.Log.Debug(command);
        }
    }
}
