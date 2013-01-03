namespace BNet.IoC.Log4Net
{
    using System;
    using log4net;


    public class Log4NetModule : LogModule<ILog>
    {
        #region Methods

        protected override ILog CreateLoggerFor(Type type)
        {
            return LogManager.GetLogger(type);
        }

        #endregion
    }
}
