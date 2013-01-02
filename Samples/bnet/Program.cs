namespace BNet
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Reflection;
    using System.Text;
    using Autofac;
    using BattleNET;
    using bnet.IoC;
    using bnet.IoC.Log4Net;
    using log4net.Config;
    using Plossum.CommandLine;


    internal class Program
    {
        public static IContainer Container { get; private set; }


        #region Methods

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


        private static void Main()
        {
            SetupIoC();

            Console.OutputEncoding = Encoding.UTF8;
            Console.Title = "bnet - " + Environment.MachineName;

            XmlConfigurator.ConfigureAndWatch(new FileInfo("log4net.config"));
            var options = new Args();
            var parser = new CommandLineParser(options);
            var app = Container.Resolve<CommandExecutor>();

            // Get all registered commands
            var commands = Container.Resolve<IEnumerable<IRConCommand>>();

            // And index them by name
            app.Commands = commands.ToDictionary(c => c.Name.ToLower(CultureInfo.InvariantCulture));

            parser.Parse();

            if (options.Help)
            {
                Console.WriteLine(parser.UsageInfo.ToString(78, false));
                Console.WriteLine(app.GetCommandsHelp());
                Console.ReadKey();
                Environment.Exit(0);
            }

            if (parser.HasErrors)
            {
                Console.WriteLine(parser.UsageInfo.ToString(78, true));
                Console.WriteLine(app.GetCommandsHelp());
                Console.ReadKey();
                Environment.Exit(1);
            }

            var loginCredentials = GetLoginCredentials(options);

            if (!loginCredentials.HasValue)
            {
                Console.WriteLine("Invalid connection settings.");
                Console.WriteLine(parser.UsageInfo.ToString(78, true));
                Console.WriteLine(app.GetCommandsHelp());
                Console.ReadKey();
                Environment.Exit(1);
            }

#if DEBUG

            // Give me a chance to attach the debugger... (launching from .bat)
            Console.WriteLine("Press Enter to begin...");
            Console.ReadLine();
#endif

            // No errors present and all arguments correct 
            // Do work according to arguments   
            app.LoginCredentials = loginCredentials.Value;

            if (options.AsService > 0)
            {
                app.StartService(new[] { options.Command }, options.AsService);
                while (true)
                {
                    Console.ReadKey(true);
                }
            }

            app.ExecuteCommand(options.Command);
            Environment.Exit(0);
        }


        private static void SetupIoC()
        {
            var builder = new ContainerBuilder();

            // builder.RegisterInstance(Console.Out).As<TextWriter>().ExternallyOwned();
            builder.RegisterModule<Log4NetModule>();
            var baseCommands = Assembly.Load("bnet.BaseCommands");
            if (baseCommands == null)
            {
                throw new FileNotFoundException("File not found.", "bnet.BaseCommands.dll");
            }

            builder.RegisterAssemblyModules(baseCommands);
            builder.RegisterType<CommandExecutor>().AsSelf().PropertiesAutowired();
            Container = builder.Build();
        }

        #endregion
    }
}
