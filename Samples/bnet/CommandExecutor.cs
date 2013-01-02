using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using BattleNET;
using bnet.IoC;
using log4net;

namespace BNet
{
    internal class CommandExecutor
    {
        private List<Timer> runningTimers;
        public Dictionary<string, IRConCommand> Commands { get; internal set; }
        public ILog Log { get; set; }
        public BattlEyeLoginCredentials LoginCredentials { get; set; }


        public void StartService(string[] commands, int period)
        {
            runningTimers = new List<Timer>();

            int start = 1000;
            period *= 1000;
            foreach (string command in commands)
            {
                var timer = new Timer(ExecuteTimedCommand, command, start, period);
                start += 1000;
                runningTimers.Add(timer);
            }
        }


        private void ExecuteTimedCommand(object state)
        {
            ExecuteCommand((string)state);
        }


        public void ExecuteCommand(string command, object state = null)
        {
            command = command.ToLower(CultureInfo.InvariantCulture);
            if (Commands.ContainsKey(command))
            {
                IRConCommand rConInstance = Commands[command];

                //TODO: ugly, ugly... 
                bool hasResults = rConInstance.GetType()
                                              .GetInterfaces()
                                              .Any(x =>
                                                   x.IsGenericType &&
                                                   x.GetGenericTypeDefinition() ==
                                                   typeof (IRConCommand<>));
                try
                {
                    if (hasResults)
                    {
                        // use covariance to get the generic version's virtual table
                        ((IRConCommand<object>)rConInstance).ExecSingleAwaitResponse(LoginCredentials);
                    }
                    else
                    {
                        rConInstance.ExecuteSingle(LoginCredentials);
                    }
                }
                catch (TimeoutException timeoutException)
                {
                    Log.Error(timeoutException.Message, timeoutException);
                }
                catch (ApplicationException applicationException)
                {
                    Log.Error(applicationException.Message, applicationException);
                }
            }
            else
            {
                ExecCustomCmd(command);
            }
        }


        private void ExecCustomCmd(string command)
        {
            var beClient = new BattlEyeClient(LoginCredentials);
            //beClient.MessageReceived += OutputMessage;
            beClient.DisconnectEvent += Disconnected;
            beClient.ReconnectOnPacketLoss = true;
            beClient.Connect();

            Log.Info("> " + command);
            var result = beClient.SendCommandPacket(command, false);
            Log.Info(result.ToString());

            while (beClient.CommandQueue > 0)
            {
                /* wait until server received all packets */
            }
            beClient.Disconnect();
            //~beClient();
        }


        private void Disconnected(BattlEyeDisconnectEventArgs args)
        {
            Log.Info(args.Message);
        }


        private void OutputMessage(BattlEyeMessageEventArgs args)
        {
            Log.Info(args.Message);
        }


        public string GetCommandsHelp()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Available extra commands:");

            foreach (var command in Commands)
            {
                sb.AppendFormat("{0} - {1}", command.Value.Name, command.Value.Description);
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
