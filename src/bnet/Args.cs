using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using Plossum.CommandLine;

namespace BNet
{

    [CommandLineManager(
        ApplicationName="bnet", 
        Copyright="Copyright (C) 2012 St4l",
        EnabledOptionStyles=OptionStyles.Unix | OptionStyles.Windows)]
    class Args
    {
        [CommandLineOption(Name="host", Aliases = "h", MinOccurs=1, MaxOccurs=1,
            Description="BattlEye server hostname")]
        public string Host { get; set; }

        [CommandLineOption(Name="port", Aliases = "p", MaxOccurs=1,
            Description="BattlEye server port")]
        public int Port { get; set; }

        [CommandLineOption(Name = "password", Aliases = "s", MinOccurs = 1, MaxOccurs = 1,
            Description="RCon password")]
        public string Password { get; set; }

        [CommandLineOption(Name = "command", Aliases = "c", MinOccurs = 1, MaxOccurs = 1,
            Description = "Command to send")]
        public string Command { get; set; }

	
        [CommandLineOption(Name="v", Aliases="verbose", Description="Produce verbose output")]
        public bool Verbose { get; set; }
	
        [CommandLineOption(Name="help", Aliases="?>", Description="Shows this help text")]
        public bool Help { get; set; }

    }

}
