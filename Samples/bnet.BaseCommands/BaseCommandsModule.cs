namespace bnet.BaseCommands
{
    using Autofac;
    using bnet.IoC;


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
