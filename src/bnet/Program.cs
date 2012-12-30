#region

using System;
using System.Net;
using System.Text;
using System.Threading;
using BattleNET;
using Plossum.CommandLine;

#endregion

namespace BNet
{
    internal class Program
    {

        static void Main(string[] args)
        {
            var options = new Args();
            var parser = new CommandLineParser(options);
            parser.Parse();

            if (options.Help)
            {
                Console.WriteLine(parser.UsageInfo.ToString(78, false));
                Environment.Exit(0);
            }
            else if (parser.HasErrors)
            {
                Console.WriteLine(parser.UsageInfo.ToString(78, true));
                Environment.Exit(1);
            }

            // No errors present and all arguments correct 
            // Do work according to arguments   
            Start(options);

        }

        private static void Start(Args args)
        {
            BattlEyeLoginCredentials? loginCredentials;

            Console.OutputEncoding = Encoding.UTF8;
            Console.Title = "BattleNET Client";

            loginCredentials = GetLoginCredentials(args);

            string command = args.Command;
            if (string.IsNullOrEmpty(command) || loginCredentials == null)
            {
                Console.Read();
                Environment.Exit(1);
            }

            BattlEyeClient b = new BattlEyeClient(loginCredentials.Value);
            b.MessageReceivedEvent += DumpMessage;
            b.DisconnectEvent += Disconnected;
            b.ReconnectOnPacketLoss(true);
            b.Connect();

            switch (command)
            {
                case "kickall":
                    for (int i = 0; i <= 100; i++)
                    {
                        var cmd = "Kick " + i.ToString();
                        Console.WriteLine(cmd);
                        Console.WriteLine(b.SendCommandPacket(cmd).ToString());
                        while (b.CommandQueue > 0) { /* wait until server received packet */ };
                    }
                    break;
                default:
                    var result = b.SendCommandPacket(command, false);
                    while (b.CommandQueue > 0) { /* wait until server received packet */ };
                    //Console.WriteLine(args.Command);
                    Console.WriteLine(result.ToString());
                    //Thread.Sleep(5000);
                    break;
            }

            b.Disconnect();
            Environment.Exit(0);
        }


        private static BattlEyeLoginCredentials? GetLoginCredentials(Args args)
        {
            IPAddress host;
            if (!IPAddress.TryParse(args.Host, out host))
            {
                Console.WriteLine("No valid host given!", args.Host);
                return null;
            }

            return new BattlEyeLoginCredentials() { 
                Host = host.ToString(), 
                Port = args.Port, 
                Password = args.Password };
        }


        private static void Disconnected(BattlEyeDisconnectEventArgs args)
        {
            Console.WriteLine(args.Message);
        }


        private static void DumpMessage(BattlEyeMessageEventArgs args)
        {
            Console.WriteLine(args.Message);
        }
    }
}