using System;
using BattleNET;
using bnet.IoC;
using log4net;

namespace RConDirect
{
    public class RCon
    {
        public RCon(ILog log)
        {
            this.Log = log;
        }
        public ILog Log { get; private set; }


    }
}