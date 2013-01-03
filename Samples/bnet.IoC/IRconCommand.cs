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

        void Execute(BattlEyeClient beClient, int timeoutSecs = 10);

        bool ExecuteSingle(BattlEyeLoginCredentials credentials);
    }
}
