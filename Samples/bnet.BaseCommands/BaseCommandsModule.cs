using Autofac;
using bnet.IoC;

namespace bnet.BaseCommands
{
    public class BaseCommandsModule : Module
    {

        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<GetPlayersCommand>().As<IRConCommand>().PropertiesAutowired();
            builder.RegisterType<KickAllCommand>().As<IRConCommand>().PropertiesAutowired();
        }
    }
}
