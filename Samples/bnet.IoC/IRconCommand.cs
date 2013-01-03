// ----------------------------------------------------------------------------------------------------
// <copyright file="IRconCommand.cs" company="Me">Copyright (c) 2012 St4l.</copyright>
// ----------------------------------------------------------------------------------------------------
namespace BNet.IoC
{
    using BattleNET;
    using log4net;


    public interface IRConCommand
    {
        string Description { get; }

        ILog Log { get; }

        string Name { get; }

        string RConCommandText { get; }

        CommandExecContext Context { get; set; }

        void Execute(BattlEyeClient beClient, int timeoutSecs = 10);

        bool ExecuteSingle();
    }
}
