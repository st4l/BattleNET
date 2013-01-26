using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace bnet.client.Tests
{
    public class MockServerSetup
    {
        public MockServerSetup()
        {
            this.ServerEndpoint = new IPEndPoint(IPAddress.Parse("45.87.98.123"), 2302);
            this.ShutdownServerTime = DateTime.Now.AddMinutes(1);
            this.Password = DefaultPassword;
        }

        public static string DefaultPassword { get { return "fnu(\\$lkd\"fh"; } }

        public IPEndPoint ServerEndpoint { get; set; }
        public string Password { get; set; }
        public DateTime ShutdownServerTime { get; set; }

        internal int AverageResponseTime { get; set; } // ms

        public bool LoginAtOnce { get; set; }
        public bool LoginIncorrect { get; set; }
        public bool LoginServerDown { get; set; }
        public bool LoginAtThirdTry { get; set; }
        public bool LoginSlow { get; set; }
        public bool CorruptConsoleMessages { get; set; }

        public bool LoadTestOnly { get; set; }
        public int LoadTestConsoleMessages { get; set; }

        public bool OnlyLogin { get; set; }

        public bool KeepAliveOnly { get; set; }
    }
}
