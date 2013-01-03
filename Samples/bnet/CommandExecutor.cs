// ----------------------------------------------------------------------------------------------------
// <copyright file="CommandExecutor.cs" company="Me">Copyright (c) 2012 St4l.</copyright>
// ----------------------------------------------------------------------------------------------------
namespace BNet
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using BattleNET;
    using BNet.IoC;
    using log4net;


    public class CommandExecutor
    {
        private List<Timer> runningTimers;


        public CommandExecutor(IEnumerable<IRConCommand> commands)
        {
            this.BNetCommands =
                commands.ToDictionary(c => c.Name.ToLower(CultureInfo.InvariantCulture));
        }


        public IEnumerable<ServerInfo> Servers { get; set; }

        public IEnumerable<string> Commands { get; set; }

        public string DbConnectionString { get; set; }

        private ILog Log { get; set; }

        private Dictionary<string, IRConCommand> BNetCommands { get; set; }


        public string GetCommandsHelp()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Available extra commands:");

            foreach (var command in this.BNetCommands)
            {
                sb.AppendFormat("{0} - {1}", command.Value.Name, command.Value.Description);
                sb.AppendLine();
            }

            return sb.ToString();
        }


        public void StartService(int period)
        {
            this.runningTimers = new List<Timer>();

            int start = 1000;
            period *= 1000;
            foreach (var serverInfo in this.Servers)
            {
                foreach (string command in this.Commands)
                {
                    var context = new CommandExecContext
                                      {
                                          CommandString = command, 
                                          Server = serverInfo,
                                          DbConnectionString = this.DbConnectionString
                                      };
                    var timer = new Timer(this.ExecuteTimedCommand, context, start, period);
                    start += 1000;
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
                                          DbConnectionString = this.DbConnectionString
                                      };
                    this.ExecuteCommand(context);
                }
            }
        }


        private void ExecuteCommand(CommandExecContext commandCtx)
        {
            string command = commandCtx.CommandString.ToLower(CultureInfo.InvariantCulture);
            if (this.BNetCommands.ContainsKey(command))
            {
                IRConCommand cmdInstance = this.BNetCommands[command];
                cmdInstance.Context = commandCtx;

                try
                {
                    // covariance FTW
                    var instance = cmdInstance as IRConCommand<object>;
                    if (instance != null)
                    {
                        // use covariance to get the generic version's virtual table               
                        instance.ExecSingleAwaitResponse();
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
            }
            else
            {
                this.ExecCustomCmd(commandCtx);
            }
        }


        private void ExecCustomCmd(CommandExecContext commandCtx)
        {
            string command = commandCtx.CommandString;
            var beClient = new BattlEyeClient(commandCtx.Server.LoginCredentials);

            // beClient.MessageReceived += OutputMessage;
            beClient.DisconnectEvent += this.Disconnected;
            beClient.ReconnectOnPacketLoss = true;
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
