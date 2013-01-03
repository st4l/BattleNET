namespace BNet.BaseCommands
{
    using Autofac;
    using BNet.IoC;


    public class BaseCommandsModule : Module
    {
        #region Methods

        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<GetPlayersCommand>().As<IRConCommand>().PropertiesAutowired();
            builder.RegisterType<KickAllCommand>().As<IRConCommand>().PropertiesAutowired();
        }

        #endregion
    }
}
