// ----------------------------------------------------------------------------------------------------
// <copyright file="CommandExecutor.cs" company="Me">Copyright (c) 2012 St4l.</copyright>
// ----------------------------------------------------------------------------------------------------
namespace BNet.IoC
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using Autofac;
    using BattleNET;
    using log4net;


    public class CommandExecutor
    {
        private List<Timer> runningTimers;


        public CommandExecutor(IEnumerable<Lazy<IRConCommand, RConCommandMetadata>> commands)
        {
            this.BNetCommandsMetadata = commands.ToDictionary(
                lazy => lazy.Metadata.Name, lazy => lazy.Metadata);
        }


        public IEnumerable<ServerInfo> Servers { get; set; }

        public IEnumerable<string> Commands { get; set; }

        public string DbConnectionString { get; set; }

        public IContainer Container { get; set; }

        // ReSharper disable MemberCanBePrivate.Global
        public ILog Log { get; set; }

        // ReSharper restore MemberCanBePrivate.Global
        private Dictionary<string, RConCommandMetadata> BNetCommandsMetadata { get; set; }


        public string GetCommandsHelp()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Available extra commands:");

            foreach (var command in this.BNetCommandsMetadata)
            {
                sb.AppendFormat("{0} - {1}", command.Key, command.Value.Description);
                sb.AppendLine();
            }

            return sb.ToString();
        }


        public void StartService(int period)
        {
            this.runningTimers = new List<Timer>();


            int start = 0;
            period *= 1000;
            var interleave = (int)Math.Floor((decimal)(period / this.Servers.Count() / this.Commands.Count()));
            interleave = Math.Max(1000, interleave);
            foreach (var serverInfo in this.Servers)
            {
                foreach (string command in this.Commands)
                {
                    var context = new CommandExecContext
                                      {
                                          CommandString = command, 
                                          Server = serverInfo, 
                                          DbConnectionString =
                                              this.DbConnectionString
                                      };
                    var timer = new Timer(this.ExecuteTimedCommand, context, start, period);
                    start += interleave;
                    this.runningTimers.Add(timer);
                }
            }
        }


        public void ExecuteCommands()
        {
            foreach (var serverInfo in this.Servers)
            {
                foreach (string command in this.Commands)
                {
                    var context = new CommandExecContext
                                      {
                                          CommandString = command, 
                                          Server = serverInfo, 
                                          DbConnectionString =
                                              this.DbConnectionString
                                      };
                    this.ExecuteCommand(context);
                }
            }
        }


        private void ExecuteCommand(CommandExecContext commandCtx)
        {
            string command = commandCtx.CommandString.ToLower(CultureInfo.InvariantCulture);
            if (this.BNetCommandsMetadata.ContainsKey(command))
            {
                // new cmd()
                var cmdInstance = this.Container.ResolveNamed<IRConCommand>(command);
                cmdInstance.Metadata = this.BNetCommandsMetadata[command];
                cmdInstance.Context = commandCtx;

                try
                {
                    // covariance FTW
                    var instanceWithResults = cmdInstance as IRConCommand<object>;
                    if (instanceWithResults != null)
                    {
                        // use covariance to get the generic version's virtual table               
                        instanceWithResults.ExecSingleAwaitResponse();
                    }
                    else
                    {
                        cmdInstance.ExecuteSingle();
                    }
                }
                catch (TimeoutException timeoutException)
                {
                    this.Log.Error(timeoutException.Message, timeoutException);
                }
                catch (ApplicationException applicationException)
                {
                    this.Log.Error(applicationException.Message, applicationException);
                }

                // ~cmd()
            }
            else
            {
                this.ExecCustomCmd(commandCtx);
            }
        }


        private void ExecCustomCmd(CommandExecContext commandCtx)
        {
            string command = commandCtx.CommandString;
            var beClient = new BattlEyeClient(commandCtx.Server.LoginCredentials)
                               {
                                   ReconnectOnPacketLoss = true, 
                                   DiscardConsoleMessages = true
                               };

            beClient.DisconnectEvent += this.Disconnected;
            beClient.Connect();

            this.Log.Info("> " + command);
            BattlEyeCommandResult result = beClient.SendCommandPacket(command, false);
            this.Log.Info(result.ToString());

            while (beClient.CommandQueue > 0)
            {
                /* wait until server received all packets */
            }

            beClient.Disconnect();

            // ~beClient();
        }


        private void ExecuteTimedCommand(object state)
        {
            this.ExecuteCommand((CommandExecContext)state);
        }


        private void Disconnected(BattlEyeDisconnectEventArgs args)
        {
            this.Log.Info(args.Message);
        }


        private void OutputMessage(BattlEyeMessageEventArgs args)
        {
            this.Log.Info(args.Message);
        }
    }
}
