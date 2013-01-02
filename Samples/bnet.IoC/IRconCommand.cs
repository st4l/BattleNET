using BattleNET;
using log4net;

namespace bnet.IoC
{
    public interface IRConCommand
    {
        string Name { get; }
        ILog Log { get; }
        string Description { get; }
        string RConCommandText { get; }
        void Execute(BattlEyeClient beClient, int timeoutSecs = 10);
        bool ExecuteSingle(BattlEyeLoginCredentials credentials);
    }
}
