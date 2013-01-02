namespace BNet
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using BattleNET;
    using bnet.IoC;
    using log4net;


    public class CommandExecutor
    {
        private List<Timer> runningTimers;

        public Dictionary<string, IRConCommand> Commands { get; internal set; }

        public ILog Log { get; set; }

        public BattlEyeLoginCredentials LoginCredentials { get; set; }


        public void ExecuteCommand(string command, object state = null)
        {
            command = command.ToLower(CultureInfo.InvariantCulture);
            if (this.Commands.ContainsKey(command))
            {
                IRConCommand cmdInstance = this.Commands[command];

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
                            this.LoginCredentials);
                    }
                    else
                    {
                        cmdInstance.ExecuteSingle(this.LoginCredentials);
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
                this.ExecCustomCmd(command);
            }
        }


        public string GetCommandsHelp()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Available extra commands:");

            foreach (var command in this.Commands)
            {
                sb.AppendFormat("{0} - {1}", command.Value.Name, command.Value.Description);
                sb.AppendLine();
            }

            return sb.ToString();
        }


        public void StartService(string[] commands, int period)
        {
            this.runningTimers = new List<Timer>();

            int start = 1000;
            period *= 1000;
            foreach (string command in commands)
            {
                var timer = new Timer(this.ExecuteTimedCommand, command, start, period);
                start += 1000;
                this.runningTimers.Add(timer);
            }
        }


        #region Methods

        private void Disconnected(BattlEyeDisconnectEventArgs args)
        {
            this.Log.Info(args.Message);
        }


        private void ExecCustomCmd(string command)
        {
            var beClient = new BattlEyeClient(this.LoginCredentials);

            // beClient.MessageReceived += OutputMessage;
            beClient.DisconnectEvent += this.Disconnected;
            beClient.ReconnectOnPacketLoss = true;
            beClient.Connect();

            this.Log.Info("> " + command);
            var result = beClient.SendCommandPacket(command, false);
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
            this.ExecuteCommand((string)state);
        }


        private void OutputMessage(BattlEyeMessageEventArgs args)
        {
            this.Log.Info(args.Message);
        }

        #endregion
    }
}
