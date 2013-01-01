using System;
using System.IO;
using log4net;
using log4net.Appender;
using log4net.Config;
using log4net.Layout;

namespace bnet.Tests
{
    public class DebugLogger
    {
        static DebugLogger()
        {
            var appender = new DebugAppender();
            appender.Layout = new PatternLayout("%-5level %date{HH:mm:ss} - %message%newline"); 

            BasicConfigurator.Configure(appender);
        }

        public static ILog GetLogger(Type t)
        {
            return LogManager.GetLogger(t);
        }
    }
}
