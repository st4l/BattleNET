#region

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using Autofac;
using BattleNET;
using Plossum.CommandLine;
using bnet.IoC;

#endregion

namespace BNet
{
    internal class Program
    {

        static void Main()
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.Title = "bnet - " + Environment.MachineName;
            var options = new Args();
            var parser = new CommandLineParser(options);
            var app = new MainApp(Console.Out);

            parser.Parse();

            if (options.Help)
            {
                Console.WriteLine(parser.UsageInfo.ToString(78, false));
                app.PrintHelp();
                Environment.Exit(0);
            }
            
            if (parser.HasErrors)
            {
                Console.WriteLine(parser.UsageInfo.ToString(78, true));
                app.PrintHelp();
                Environment.Exit(1);
            }

            var loginCredentials = GetLoginCredentials(options);

            if (string.IsNullOrEmpty(options.Command) || loginCredentials == null)
            {
                Console.WriteLine(parser.UsageInfo.ToString(78, true));
                Environment.Exit(1);
            }

            // No errors present and all arguments correct 
            // Do work according to arguments   
            app.Start(options, loginCredentials.Value);

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
            Environment.Exit(0);
        }


        private static BattlEyeLoginCredentials? GetLoginCredentials(Args args)
        {
            IPAddress host;
            if (!IPAddress.TryParse(args.Host, out host))
            {
                Console.WriteLine("No valid host given! " + args.Host);
                return null;
            }

            return new BattlEyeLoginCredentials
                {
                    Host = host.ToString(),
                    Port = args.Port,
                    Password = args.Password
                };
        }
    }
}