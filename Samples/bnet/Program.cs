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
    using log4net;
    using log4net.Config;
    using log4net.Core;
    using Plossum.CommandLine;
    using log4netRepoHierarchy = log4net.Repository.Hierarchy;


    internal static class Program
    {
        private static IContainer Container { get; set; }


        private static void Main()
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.Title = "bnet - " + Environment.MachineName;

#if DEBUG

            // Give me a chance to attach the debugger... (launching from .bat)
            Console.WriteLine("Press Enter to begin...");
            Console.ReadLine();
#endif

            XmlConfigurator.ConfigureAndWatch(new FileInfo("log4net.config"));
            SetupIoC();

            var app = Container.Resolve<CommandExecutor>();
            app.Container = Container;
            var options = GetAppArguments(app);

            if (options.Verbose)
            {
                var loggingRepo = (log4netRepoHierarchy.Hierarchy)LogManager.GetRepository();
                loggingRepo.Root.Level = Level.Debug;
                loggingRepo.RaiseConfigurationChanged(EventArgs.Empty);
            }

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
                Console.WriteLine();
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                Environment.Exit(0);
            }

            if (parser.HasErrors)
            {
                Exit(executor, parser, 1);
            }

            // batch file or command-line?
            if (options.BatchFile != null)
            {
                if (!ParseBatchFile(options.BatchFile, executor))
                {
                    Exit(executor, parser, 1);
                }
            }
            else
            {
                executor.Servers = ParseServerUris(options.Servers);
                if (executor.Servers == null || !executor.Servers.Any())
                {
                    Console.WriteLine("No servers specified.");
                    Exit(executor, parser, 1);
                }

                executor.Commands = options.Commands;
                if (executor.Commands == null || options.Commands.Count == 0)
                {
                    Console.WriteLine("No commands specified.");
                    Exit(executor, parser, 1);
                }
            }

            return options;
        }


        private static void Exit(CommandExecutor executor, CommandLineParser parser, int errorLevel)
        {
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine(parser.UsageInfo.ToString(78, true));
            Console.WriteLine(executor.GetCommandsHelp());
            Console.WriteLine();
            Console.WriteLine("Examples: bnet -b SampleBatch.bnet -svc 60");
            Console.WriteLine(
                "          bnet -u rconpass@127.0.0.1:2302 -u rconpass@127.0.0.1:3302 -svc 60 -c getplayers -c update_dbplayers");
            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
            Environment.Exit(errorLevel);
        }


        private static bool ParseBatchFile(string batchFile, CommandExecutor executor)
        {
            var parser = new FileIniDataParser { CommentDelimiter = (char)0 };
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
                || !ValidateIniSettingExists(data, "Commands"))
            {
                return false;
            }

            // Get db settings even if we don't need to get the servers from it
            executor.DbConnectionString = GetIniDbConnectionString(data);

            if (IsIniTrue(data["Servers"]["UseBNetDb"]))
            {
                if (executor.DbConnectionString == null)
                {
                    Console.WriteLine("Invalid or not specified connection string.");
                    return false;
                }

                executor.Servers = GetDbServers(executor.DbConnectionString);
            }
            else
            {
                var serverUris = from keyData in data["Servers"]
                                 where keyData.KeyName != "UseBNetDb"
                                 select keyData.Value;
                executor.Servers = ParseServerUris(serverUris);
            }

            if (executor.Servers == null || !executor.Servers.Any())
            {
                Console.WriteLine("No servers specified.");
                return false;
            }

            executor.Commands = from keyData in data["Commands"] select keyData.Value;
            if (!executor.Commands.Any())
            {
                Console.WriteLine("No commands specified.");
                return false;
            }

            return true;
        }


        private static string GetIniDbConnectionString(IniData data)
        {
            if (!ValidateIniSettingExists(data, "Database")
                || !ValidateIniSettingExists(data, "Database", "Host")
                || !ValidateIniSettingExists(data, "Database", "Port")
                || !ValidateIniSettingExists(data, "Database", "Database")
                || !ValidateIniSettingExists(data, "Database", "Username")
                || !ValidateIniSettingExists(data, "Database", "Password"))
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
                string uriString = string.Format("mysql://{0}:{1}", host, port);
                uri = new Uri(uriString);
            }
            catch (UriFormatException)
            {
                uri = null;
            }

            if (uri == null || !uri.IsWellFormedOriginalString() || string.IsNullOrWhiteSpace(user)
                || string.IsNullOrWhiteSpace(pwd) || string.IsNullOrWhiteSpace(db))
            {
                Console.WriteLine("Invalid MySql connection settings.");
                return null;
            }

            string connString =
                string.Format(
                    "metadata=res://*/Data.BNetDb.csdl|res://*/Data.BNetDb.ssdl|res://*/Data.BNetDb.msl;provider=MySql.Data.MySqlClient;"
                    + "provider connection string=\"server='{2}';port={3};database='{4}';User Id='{0}';Password='{1}';"
                    + "Check Parameters=false;"
                    + "Persist Security Info=False;Allow Zero Datetime=True;Convert Zero Datetime=True;\"", 
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

            if (results.Count > 0)
            {
                return results;
            }

            return null;
        }


        private static IEnumerable<ServerInfo> GetDbServers(string connString)
        {
            IEnumerable<ServerInfo> servers;
            using (var db = new BNetDb(connString))
            {
                var dayzServers = from s in db.dayz_server 
                                  where s.server_id == 1
                                  select s;

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
