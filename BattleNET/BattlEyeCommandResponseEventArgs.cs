using System;

namespace BattleNET
{
    public delegate void CommandResponseReceivedEventHandler(object source, BattlEyeCommandResponseEventArgs args);

    public class BattlEyeCommandResponseEventArgs : EventArgs
    {
        public BattlEyeCommandResponseEventArgs(string message)
        {
            Message = message;
        }
        public string Message { get; private set; }
    }
}