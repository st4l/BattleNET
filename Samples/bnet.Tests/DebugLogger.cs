namespace bnet.Tests
{
    using System;
    using log4net;
    using log4net.Appender;
    using log4net.Config;
    using log4net.Layout;


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
