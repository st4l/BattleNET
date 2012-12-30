using BattleNET;
using log4net;

namespace bnet.IoC
{
    public abstract class RConCommandBase : IRConCommand
    {
        public abstract string Name { get; }
        public abstract void Execute(BattlEyeClient beClient);
        public ILog Log { get; set; }
        public abstract string Description { get; }
    }
}