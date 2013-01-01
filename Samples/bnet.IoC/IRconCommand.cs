using BattleNET;
using log4net;

namespace bnet.IoC
{
    public interface IRConCommand
    {
        string Name { get; }
        void Execute(BattlEyeClient beClient);
        ILog Log { get; }
        string Description { get; }
        string RConCommandText { get; }
        bool ExecuteSingle(BattlEyeLoginCredentials credentials);
    }


}