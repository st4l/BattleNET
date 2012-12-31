using System;
using log4net;

namespace bnet.IoC.Log4Net
{
    public class Log4NetModule : LogModule<ILog>
    {
        protected override ILog CreateLoggerFor(Type type)
        {
            return LogManager.GetLogger(type);
        }
    }

}