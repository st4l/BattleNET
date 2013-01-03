// ----------------------------------------------------------------------------------------------------
// <copyright file="AdvCommandsModule.cs" company="Me">Copyright (c) 2012 St4l.</copyright>
// ----------------------------------------------------------------------------------------------------
namespace BNet.AdvCommands
{
    using Autofac;
    using BNet.IoC;


    public class AdvCommandsModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<UpdateDbPlayersCommand>().As<IRConCommand>().PropertiesAutowired();
        }
    }
}
