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


        public ILog Log { get; set; }

        public Dictionary<string, IRConCommand> BNetCommands { get; internal set; }

        public IEnumerable<ServerInfo> Servers { get; set; }

        public IEnumerable<string> Commands { get; set; }

        public string BNetDbConnectionString { get; set; }


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
                                          Server = serverInfo
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
                                          Server = serverInfo
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

                // TODO: ugly, ugly... 
                bool hasResults =
                    cmdInstance.GetType()
                               .GetInterfaces()
                               .Any(
                                   x =>
                                   x.IsGenericType
                                   && x.GetGenericTypeDefinition() == typeof(IRConCommand<>));
                try
                {
                    if (hasResults)
                    {
                        // use covariance to get the generic version's virtual table               
                        ((IRConCommand<object>)cmdInstance).ExecSingleAwaitResponse(
                            commandCtx.Server);
                    }
                    else
                    {
                        cmdInstance.ExecuteSingle(commandCtx.Server.LoginCredentials);
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


        private class CommandExecContext
        {
            public ServerInfo Server { get; set; }

            public string CommandString { get; set; }
        }
    }
}
