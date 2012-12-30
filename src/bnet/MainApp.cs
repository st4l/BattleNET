using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Autofac;
using BattleNET;
using bnet.IoC;

namespace BNet
{
    internal class MainApp
    {
        private BattlEyeClient beClient;
        protected TextWriter TextOut { get; private set; }
        public Dictionary<string, IRConCommand> Commands { get; internal set; }


        public MainApp(TextWriter @out)
        {
            TextOut = @out;
            SetupIoC();
        }


        public void Start(Args args, BattlEyeLoginCredentials loginCredentials)
        {
            var command = args.Command;

            beClient = new BattlEyeClient(loginCredentials);
            //beClient.MessageReceivedEvent += OutputMessage;
            beClient.DisconnectEvent += Disconnected;
            beClient.ReconnectOnPacketLoss(true);
            beClient.Connect();

            TextOut.WriteLine();
            TextOut.WriteLine("> " + command);

            if (Commands.ContainsKey(command.ToLower(CultureInfo.InvariantCulture)))
            {
                try
                {
                    Commands[command].Execute(beClient);
                }
                catch (TimeoutException timeoutException)
                {
                    TextOut.WriteLine(timeoutException.Message);
                }
                catch (ApplicationException applicationException)
                {
                    TextOut.WriteLine(applicationException.Message);
                }
            }
            else
            {
                var result = beClient.SendCommandPacket(command, false);
                TextOut.WriteLine(result.ToString());
            }

            while (beClient.CommandQueue > 0)
            {
                /* wait until server received all packets */
            }
            beClient.Disconnect();
            TextOut.WriteLine();
        }


        private void Disconnected(BattlEyeDisconnectEventArgs args)
        {
            TextOut.WriteLine();
            TextOut.WriteLine(args.Message);
        }


        private void OutputMessage(BattlEyeMessageEventArgs args)
        {
            TextOut.WriteLine(args.Message);
        }


        private void SetupIoC()
        {
            var builder = new ContainerBuilder();
            builder.RegisterInstance(Console.Out)
                   .As<TextWriter>().ExternallyOwned();

            var baseCommands = Assembly.Load("bnet.BaseCommands");
            if (baseCommands == null)
            {
                throw new FileNotFoundException("File not found.", "bnet.BaseCommands.dll");
            }
            builder.RegisterAssemblyModules(baseCommands);

            var container = builder.Build();

            // Get all registered commands
            var commands = container.Resolve<IEnumerable<IRConCommand>>();

            // And index them by name
            this.Commands = commands.ToDictionary(
                command => command.Name.ToLower(CultureInfo.InvariantCulture));
        }


        public void PrintHelp()
        {
            TextOut.WriteLine("Available extra commands:");

            foreach (var command in Commands)
            {
                TextOut.WriteLine(command.Value.Name + " - " + command.Value.Description);
            }

        }
    }
}
