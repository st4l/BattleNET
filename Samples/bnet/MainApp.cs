using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Autofac;
using BattleNET;
using bnet.IoC;
using cp.logging;
using log4net;

namespace BNet
{
    internal class MainApp
    {
        private BattlEyeClient beClient;
        public Dictionary<string, IRConCommand> Commands { get; internal set; }
        public static IContainer Container { get; private set; }
        public ILog Log { get; set; }




        public void Start(BattlEyeLoginCredentials loginCredentials, string command)
        {

            beClient = new BattlEyeClient(loginCredentials);
            //beClient.MessageReceivedEvent += OutputMessage;
            beClient.DisconnectEvent += Disconnected;
            beClient.ReconnectOnPacketLoss(true);
            beClient.Connect();

            Log.Info("> " + command);

            if (Commands.ContainsKey(command.ToLower(CultureInfo.InvariantCulture)))
            {
                try
                {
                    Commands[command].Execute(beClient);
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
                var result = beClient.SendCommandPacket(command, false);
                Log.Info(result.ToString());
            }

            while (beClient.CommandQueue > 0)
            {
                /* wait until server received all packets */
            }
            beClient.Disconnect();
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
                sb.AppendFormat(command.Value.Name + " - " + command.Value.Description);
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
