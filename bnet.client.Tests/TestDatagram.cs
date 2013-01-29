using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace bnet.client.Tests
{
    public class TestDatagram
    {
        public TestDatagram(byte[] payload)
        {
            this.Payload = payload;
            this.Timestamp = DateTime.Now;
        }

        public DateTime Timestamp { get; set; }

        public byte[] Payload { get; set; }
    }
}
