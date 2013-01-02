namespace bnet.IoC
{
    using BattleNET;


    public interface IRConCommand<out TResultType> : IRConCommand
        where TResultType : class
    {
        TResultType Result { get; }

        void ExecAwaitResponse(BattlEyeClient beClient, int timeoutSecs);

        bool ExecSingleAwaitResponse(BattlEyeLoginCredentials credentials);
    }
}
