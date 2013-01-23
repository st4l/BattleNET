using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace bnet.WinClient
{
    using System.IO;
    using log4net.Config;


    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            XmlConfigurator.ConfigureAndWatch(new FileInfo("log4net.config"));
            base.OnStartup(e);
        }
    }
}
