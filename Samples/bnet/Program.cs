// ----------------------------------------------------------------------------------------------------
// <copyright file="Program.cs" company="Me">Copyright (c) 2012 St4l.</copyright>
// ----------------------------------------------------------------------------------------------------
namespace BNet
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using Autofac;
    using BattleNET;
    using BNet.Data;
    using BNet.IoC;
    using BNet.IoC.Log4Net;
    using IniParser;
    using log4net.Config;
    using Plossum.CommandLine;


    internal static class Program
    {
        private static IContainer Container { get; set; }


        private static void Main()
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.Title = "bnet - " + Environment.MachineName;

            XmlConfigurator.ConfigureAndWatch(new FileInfo("log4net.config"));
            SetupIoC();

            var app = Container.Resolve<CommandExecutor>();
            var options = GetAppArguments(app);

#if DEBUG

            // Give me a chance to attach the debugger... (launching from .bat)
            Console.WriteLine("Press Enter to begin...");
            Console.ReadLine();
#endif

            // No errors present and all arguments correct 
            // Do work according to arguments   
            if (options.AsService > 0)
            {
                app.StartService(options.AsService);
                while (true)
                {
                    Console.ReadKey(true);
                }
            }

            app.ExecuteCommands();
            Environment.Exit(0);
        }


        /// <summary>
        ///     Sets up IoC container for dependency injections.
        /// </summary>
        private static void SetupIoC()
        {
            var builder = new ContainerBuilder();

            // builder.RegisterInstance(Console.Out).As<TextWriter>().ExternallyOwned();
            builder.RegisterModule<Log4NetModule>();
            RegisterAssembly(builder, "bnet.BaseCommands");
            RegisterAssembly(builder, "bnet.AdvCommands");
            builder.RegisterType<CommandExecutor>().AsSelf().PropertiesAutowired();
            Container = builder.Build();
        }


        private static void RegisterAssembly(ContainerBuilder builder, string assemblyName)
        {
            var baseCommands = Assembly.Load(assemblyName);
            if (baseCommands == null)
            {
                throw new FileNotFoundException("File not found.", assemblyName + ".dll");
            }

            builder.RegisterAssemblyModules(baseCommands);
        }


        /// <summary>
        ///     Parses command line or batch file arguments and configures the
        ///     <paramref name="executor" /> according to them.
        /// </summary>
        /// <param name="executor">
        ///     The <see cref="CommandExecutor" /> to be configured
        /// </param>
        /// <returns></returns>
        private static Args GetAppArguments(CommandExecutor executor)
        {
            var options = new Args();
            var parser = new CommandLineParser(options);

            parser.Parse();

            if (options.Help)
            {
                Console.WriteLine(parser.UsageInfo.ToString(78, false));
                Console.WriteLine(executor.GetCommandsHelp());
                Console.ReadKey();
                Environment.Exit(0);
            }

            if (parser.HasErrors)
            {
                ExitWithError(executor, parser);
            }

            if (options.Servers.Count > 0)
            {
                executor.Servers = ParseServerUris(options.Servers);
                if (executor.Servers == null)
                {
                    ExitWithError(executor, parser);
                }
            }
            else if (options.BatchFile != null)
            {
                if (!ParseBatchFile(options.BatchFile, executor))
                {
                    ExitWithError(executor, parser);
                }
            }
            else
            {
                Console.WriteLine("Invalid connection settings.");
                Console.WriteLine(parser.UsageInfo.ToString(78, true));
                Console.WriteLine(executor.GetCommandsHelp());
                Console.ReadKey();
                Environment.Exit(1);
            }

            return options;
        }


        private static void ExitWithError(CommandExecutor executor, CommandLineParser parser)
        {
            Console.WriteLine(parser.UsageInfo.ToString(78, true));
            Console.WriteLine(executor.GetCommandsHelp());
            Console.ReadKey();
            Environment.Exit(1);
        }


        private static bool ParseBatchFile(string batchFile, CommandExecutor executor)
        {
            var parser = new FileIniDataParser();

            IniData data;
            try
            {
                data = parser.LoadFile(batchFile);
            }
            catch (ParsingException pex)
            {
                Console.WriteLine(pex.Message);
                return false;
            }

            if (!ValidateIniSettingExists(data, "Servers")
                || ValidateIniSettingExists(data, "Commands"))
            {
                return false;
            }

            if (IsIniTrue(data["Servers"]["UseBNetDb"]))
            {
                var connString = GetIniDbConnectionString(data);
                if (connString == null)
                {
                    return false;
                }

                executor.BNetDbConnectionString = connString;
                executor.Servers = GetDbServers(connString);
            }
            else
            {
                var serverUris = from keyData in data["Servers"]
                                 where keyData.KeyName != "UseBNetDb"
                                 select keyData.Value;
                executor.Servers = ParseServerUris(serverUris);
            }

            executor.Commands = from keyData in data["Commands"] select keyData.Value;
            return true;
        }


        private static string GetIniDbConnectionString(IniData data)
        {
            if (!ValidateIniSettingExists(data, "Database")
                || ValidateIniSettingExists(data, "Database", "Host")
                || ValidateIniSettingExists(data, "Database", "Port")
                || ValidateIniSettingExists(data, "Database", "Database")
                || ValidateIniSettingExists(data, "Database", "Username")
                || ValidateIniSettingExists(data, "Database", "Password"))
            {
                return null;
            }

            return ConstructBNetConnectionString(
                data["Database"]["Username"], 
                data["Database"]["Password"], 
                data["Database"]["Host"], 
                data["Database"]["Port"], 
                data["Database"]["Database"]);
        }


        private static string ConstructBNetConnectionString(
            string user, string pwd, string host, string port, string db)
        {
            Uri uri;
            try
            {
                string uriString = string.Format(
                    "mysql://{0}:{1}@{2}:{3}/{4}", user, pwd, host, port, db);
                uri = new Uri(uriString);
            }
            catch (UriFormatException)
            {
                uri = null;
            }

            if (uri == null || !uri.IsWellFormedOriginalString()
                || string.IsNullOrWhiteSpace(uri.UserInfo))
            {
                Console.WriteLine("Invalid MySql connection settings.");
                return null;
            }

            string connString =
                string.Format(
                    "metadata=res://*/Data.BNetDb.csdl|res://*/Data.BNetDb.ssdl|res://*/Data.BNetDb.msl;provider=MySql.Data.MySqlClient;"
                    + "provider connection string=\"server={2};port={3};User Id={0};Password={1};persist security info=False;database={4}\"", 
                    user, 
                    pwd, 
                    host, 
                    port, 
                    db);
            return connString;
        }


        private static bool ValidateIniSettingExists(
            IniData data, string sectionName, string keyName = null)
        {
            if (data[sectionName] == null)
            {
                Console.WriteLine("Section '{0}' not found in batch file.", sectionName);
                return false;
            }

            if (keyName != null)
            {
                if (data[sectionName][keyName] == null)
                {
                    Console.WriteLine(
                        "Setting '{1}' in section '{0}' not found in batch file.", 
                        sectionName, 
                        keyName);
                    return false;
                }
            }

            return true;
        }


        private static bool IsIniTrue(string setting)
        {
            if (setting == null)
            {
                return false;
            }

            setting = setting.ToLower();
            return setting == "1" || setting == "true" || setting == "yes";
        }


        private static IEnumerable<ServerInfo> ParseServerUris(IEnumerable<string> serverStrings)
        {
            var results = new List<ServerInfo>();
            int id = 0;
            foreach (string server in serverStrings)
            {
                id++;
                Uri uri;

                try
                {
                    uri = new Uri("bnet://" + server);
                }
                catch (UriFormatException)
                {
                    uri = null;
                }

                if (uri == null || !uri.IsWellFormedOriginalString()
                    || string.IsNullOrEmpty(uri.UserInfo) || uri.UserInfo.IndexOf(':') > -1)
                {
                    Console.WriteLine("Invalid server: " + server);
                    return null;
                }

                int port = uri.IsDefaultPort ? 2302 : uri.Port;

                results.Add(
                    new ServerInfo
                        {
                            ServerId = id, 
                            ServerName = uri.DnsSafeHost + ":" + port, 
                            LoginCredentials =
                                new BattlEyeLoginCredentials
                                    {
                                        Host = uri.DnsSafeHost, 
                                        Port = port, 
                                        Password = uri.UserInfo
                                    }
                        });
            }

            return results;
        }


        private static IEnumerable<ServerInfo> GetDbServers(string connString)
        {
            IEnumerable<ServerInfo> servers;
            using (var db = new BNetDb())
            {
                db.Database.Connection.ConnectionString = connString;
                var dayzServers = from s in db.dayz_server select s;

                servers =
                    dayzServers.ToList()
                               .Select(
                                   s =>
                                   new ServerInfo
                                       {
                                           ServerId = (int)s.id, 
                                           ServerName = s.short_name, 
                                           LoginCredentials =
                                               new BattlEyeLoginCredentials(
                                               s.rcon_host, s.rcon_port, s.rcon_pwd)
                                       });
            }

            return servers;
        }
    }
}
