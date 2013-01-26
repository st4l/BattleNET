using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BNet.Client.Datagrams;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace bnet.client.Tests
{
    public class MockServer
    {
        private readonly MockServerSetup setup;
        private readonly int avgResponseTime;

        private ConcurrentQueue<byte[]> OutboundQueue = new ConcurrentQueue<byte[]>();
        private bool shutdown;

        private int loginAttempts;
        private bool clientLoggedIn = false;

        private byte conMsgSequenceNum;

        public List<TestDatagram> receivedDatagrams = new List<TestDatagram>();


        public MockServer(MockServerSetup setup)
        {
            this.avgResponseTime = setup.AverageResponseTime; // ms
            this.setup = setup;
        }


        public UdpReceiveResult SendPacket()
        {
            bool sent = false;
            while (!this.shutdown && DateTime.Now < this.setup.ShutdownServerTime)
            {
                Thread.Sleep(this.avgResponseTime);
                if (!setup.LoginServerDown && this.OutboundQueue.Count > 0)
                {
                    byte[] peekBytes;
                    if (this.OutboundQueue.TryPeek(out peekBytes))
                    {
                        if (peekBytes[7] == 0xFF)
                        {
                            this.shutdown = true;
                            return new UdpReceiveResult(peekBytes, this.setup.ServerEndpoint);
                        }
                        byte[] sendBytes;
                        while (!this.OutboundQueue.TryDequeue(out sendBytes))
                        {
                        }
                        return new UdpReceiveResult(sendBytes, this.setup.ServerEndpoint);
                    }
                }
            }

            // Done sending packets for this test
            Thread.Sleep(Timeout.Infinite);
            return new UdpReceiveResult(new byte[0], this.setup.ServerEndpoint);
        }

        
        public int ReceivePacket(byte[] bytes, int length)
        {
            var payload = ValidateInboundHeader(bytes, length);

            byte type = Buffer.GetByte(payload, 1);
            Assert.IsTrue(type <= 2);

            this.receivedDatagrams.Add(new TestDatagram(payload));

            switch (type)
            {
                case 0:
                    this.ReceiveLoginPacket(payload);
                    break;
                case 1:
                    this.ReceiveCommandPacket(payload);
                    break;
                case 2:
                    this.ReceiveAcknowledgePacket(payload);
                    break;
                default:
                    Assert.Fail("Invalid packet type");
                    break;
            }

            return length;
        }


        public void Shutdown()
        {
            this.shutdown = true;
        }




        private static byte[] ValidateInboundHeader(byte[] bytes, int length)
        {
            Assert.IsTrue(length > 8);
            Assert.IsTrue(bytes.Length >= length);

            var buffer = new byte[length];
            Buffer.BlockCopy(bytes, 0, buffer, 0, length);


            Assert.AreEqual(Buffer.GetByte(buffer, 0), 0x42); // "B"
            Assert.AreEqual(Buffer.GetByte(buffer, 1), 0x45); // "E"

            var checksum = new byte[4];
            Buffer.BlockCopy(buffer, 2, checksum, 0, 4);

            var payload = new byte[length - 6];
            Buffer.BlockCopy(buffer, 6, payload, 0, length - 6);

            byte[] computedChecksum;
            using (var crc = new Crc32(Crc32.DefaultPolynomialReversed, Crc32.DefaultSeed))
            {
                computedChecksum = crc.ComputeHash(payload);
                Array.Reverse(computedChecksum);
            }

            Assert.IsTrue(checksum.SequenceEqual(computedChecksum));

            Assert.AreEqual(Buffer.GetByte(payload, 0), 0xFF);
            return payload;
        }


        private void ReceiveLoginPacket(byte[] payload)
        {
            loginAttempts++;
            string recvdPass = Encoding.ASCII.GetString(payload, 2, payload.Length - 2);

            if (this.setup.LoginAtThirdTry)
            {
                // remain mute unless this is the third login attempt
                if (loginAttempts < 3)
                {
                    return;
                }
            }

            var outPayload = new byte[3];
            Buffer.SetByte(outPayload, 0, 0xFF);
            Buffer.SetByte(outPayload, 1, 0x00);
            if (this.setup.Password == recvdPass && !this.setup.LoginIncorrect)
            {
                // Logged in
                Buffer.SetByte(outPayload, 2, 0x01);
                this.OutboundQueue.Enqueue(this.BuildOutboundPacket(outPayload));
                this.LoggedIn();
            }
            else
            {
                Buffer.SetByte(outPayload, 2, 0x00);
                this.OutboundQueue.Enqueue(this.BuildOutboundPacket(outPayload));
            }
            if (this.setup.OnlyLogin)
            {
                this.SendShutdownPacket();
            }

        }

        private void LoggedIn()
        {
            this.clientLoggedIn = true;
            for (int i = 0; i < this.setup.LoadTestConsoleMessages; i++)
			{
                this.OutboundQueue.Enqueue(this.GenerateRandomConsoleMessage());
			}
            if (this.setup.LoadTestOnly)
            {
                this.SendShutdownPacket();
            }
        }


        private void SendShutdownPacket()
        {
            var shutdownPayload = new byte[2];
            Buffer.SetByte(shutdownPayload, 0, 0xFF);
            Buffer.SetByte(shutdownPayload, 1, 0xFF);

            this.OutboundQueue.Enqueue(this.BuildOutboundPacket(shutdownPayload));  // EOF
        }


        private byte[] GenerateRandomConsoleMessage()
        {
            var payload = new byte[500];
            Buffer.SetByte(payload, 0, 0xFF);
            Buffer.SetByte(payload, 1, 0x02);
            Buffer.SetByte(payload, 2, this.conMsgSequenceNum);
            this.IncrementConMsgSequenceNum();

            var random = new Random(3452445);
            for (int i = 3; i < 500; i++)
			{
                Buffer.SetByte(payload, i, (byte)random.Next(33, 168));
			}
            this.totalConMsgsGenerated++;
            return this.BuildOutboundPacket(payload);
        }

        private int totalConMsgsGenerated;

        private void IncrementConMsgSequenceNum()
        {
            this.conMsgSequenceNum = this.conMsgSequenceNum == (byte)255 ? (byte)0 : (byte)(this.conMsgSequenceNum + 1);
        }


        private void ReceiveCommandPacket(byte[] payload)
        {
            // just the keep alive packet
            if (payload.Length == 3)
            {
                Assert.AreEqual(payload[0], (byte)0xFF);
                Assert.AreEqual(payload[1], (byte)0x01);
                // payload[2] seqNum
            }
        }


        private void ReceiveAcknowledgePacket(byte[] payload)
        {
            
        }


        private byte[] BuildOutboundPacket(byte[] payload)
        {
            byte[] checksum;
            using (var crc = new Crc32(Crc32.DefaultPolynomialReversed, Crc32.DefaultSeed))
            {
                checksum = crc.ComputeHash(payload);
                Array.Reverse(checksum);
            }

            var payloadLen = Buffer.ByteLength(payload);
            var result = new byte[6 + payloadLen];
            Buffer.SetByte(result, 0, 0x42); // "B"
            Buffer.SetByte(result, 1, 0x45); // "E"
            Buffer.BlockCopy(checksum, 0, result, 2, 4);
            Buffer.BlockCopy(payload, 0, result, 6, payloadLen);

            return result;
        }


    }
}
