// ----------------------------------------------------------------------------------------------------
// <copyright file="RConClientTests.cs" company="Me">Copyright (c) 2012 St4l.</copyright>
// ----------------------------------------------------------------------------------------------------

using System.Diagnostics;
using BNet.Client.Datagrams;

namespace bnet.client.Tests
{
    using System;
    using System.Security.Authentication;
    using BNet.Client;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using log4net.Config;
    using log4net.Core;
    using log4net.Filter;
    using log4net.Layout;


    [TestClass]
    public class RConClientTests
    {
        ///<summary>
        ///  Gets or sets the test context which provides
        ///  information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext { get; set; }


        [ClassInitialize]
        public static void Initialize(TestContext ctx)
        {
            var appender = new MyDebugAppender
                {
                    Layout =
                        new PatternLayout(
                        "%date{HH:mm:ss:fffff} %-10logger{1} %-5level - [%thread] %message%newline")
                };

            var filterLevels = new LevelRangeFilter { LevelMin = Level.All, AcceptOnMatch = true };
            //filterLevels.LevelMax = Level.Fatal;
            filterLevels.ActivateOptions();

            // var filterDeny = new DenyAllFilter();

            appender.AddFilter(filterLevels);
            appender.Threshold = Level.Trace;

            appender.ActivateOptions();

            BasicConfigurator.Configure(appender);
        }


        [TestMethod]
        [TestCategory("Correctness")]
        public void ShouldLogin()
        {
            var client = CreateClient(new MockServerSetup { OnlyLogin = true });
            var rcc = new RConClient(client, client.ServerSetup.Password);
            var connected = rcc.ConnectAsync().Result;
            Assert.IsTrue(connected, "not connected");
        }


        [TestMethod]
        [TestCategory("Correctness")]
        public void ShouldLoginTheThirdTime()
        {
            Assert.Fail();
        }


        [TestMethod]
        [TestCategory("Correctness")]
        public void ShouldThrowOnLoginWithWrongPassword()
        {
            var client = CreateClient(new MockServerSetup { OnlyLogin = true });
            var rcc = new RConClient(client, "fnipw93457");

            bool threw = false;
            try
            {
                var connected = rcc.ConnectAsync().Result;
                Assert.IsFalse(connected, "should not return true");
            }
            catch (AggregateException aex)
            {
                if (aex.InnerExceptions.Count == 1
                    && aex.InnerExceptions[0] is InvalidCredentialException)
                {
                    threw = true;
                }
                else
                {
                    throw;
                }
            }
            catch (InvalidCredentialException)
            {
                threw = true;
            }

            if (!threw)
            {
                Assert.Fail("Should have thrown an Invalid Credential Exception");
            }
        }


        [TestMethod]
        [TestCategory("Correctness")]
        public void ShouldThrowWhenServerDown()
        {
            var conf = new MockServerSetup { LoginServerDown = true, OnlyLogin = true };
            var client = CreateClient(conf);
            var rcc = new RConClient(client, client.ServerSetup.Password);

            bool threw = false;
            try
            {
                var connected = rcc.ConnectAsync().Result;
                Assert.IsFalse(connected, "should not return true");
            }
            catch (AggregateException aex)
            {
                if (aex.InnerExceptions.Count == 1 && aex.InnerExceptions[0] is TimeoutException)
                {
                    threw = true;
                }
                else
                {
                    throw;
                }
            }
            catch (TimeoutException)
            {
                threw = true;
            }

            if (!threw)
            {
                Assert.Fail("Should have thrown a Timeout Exception");
            }
        }


        [TestMethod]
        [TestCategory("Performance")]
        public void ShouldAcceptTonsOfPackets()
        {
            var conf = new MockServerSetup
            {
                LoadTestConsoleMessages = 50000,
                LoadTestOnly = true
            };
            var client = CreateClient(conf);
            var rcc = new RConClient(client, client.ServerSetup.Password) {DiscardConsoleMessages = true};
            RunUntilShutdown(rcc);
            Assert.IsTrue(rcc.Metrics.InboundPacketCount > 50000);
        }


        [TestMethod]
        [TestCategory("Performance")]
        public void ShouldParseTonsOfPackets()
        {
            var conf = new MockServerSetup
            {
                LoadTestConsoleMessages = 50000,
                LoadTestOnly = true,
                ShutdownServerTime = DateTime.Now.AddMinutes(5)
            };
            var client = CreateClient(conf);
            var rcc = new RConClient(client, client.ServerSetup.Password);
            int parsedCount = 0;
            rcc.MessageReceived += (sender, args) => { parsedCount++; };

            RunUntilShutdown(rcc);
            Assert.IsTrue(parsedCount >= 50000);
        }


        [TestMethod]
        [TestCategory("Performance")]
        public void ShouldSendKeepAliveUnderHeavyLoad()
        {
            var conf = new MockServerSetup
            {
                LoadTestConsoleMessages = -1,
                KeepAliveOnly = true
            };

            var client = CreateClient(conf);
            var rcc = new RConClient(client, client.ServerSetup.Password);
            RunUntilShutdown(rcc);
            var metrics = client.Server.GetMetrics();
            Debug.WriteLine("Console Messages generated by server: {0}", metrics.TotalConsoleMessagesGenerated);
            Debug.WriteLine("Inbound Packets received by client: {0}", rcc.Metrics.InboundPacketCount);
            Debug.WriteLine("Console Packets Acknowledgments received by server: {0}", metrics.AckPacketsReceived);
            Debug.WriteLine("Keep Alive packets received by server: {0}", metrics.KeepAlivePacketsReceived);
            Assert.IsTrue(metrics.KeepAlivePacketsReceived > 0);
        }


        [TestMethod]
        [TestCategory("Correctness")]
        public void ShouldDiscardConsolePackets()
        {
            var conf = new MockServerSetup
            {
                LoadTestConsoleMessages = 100,
                LoadTestOnly = true
            };

            var client = CreateClient(conf);
            var rcc = new RConClient(client, client.ServerSetup.Password);
            rcc.DiscardConsoleMessages = true;
            RunUntilShutdown(rcc);
            Assert.IsTrue(rcc.Metrics.DispatchedConsoleMessages == 0);
        }


        [TestMethod]
        [TestCategory("Correctness")]
        public void ShouldDiscardCorruptedPackets()
        {
            var conf = new MockServerSetup
            {
                LoadTestConsoleMessages = 100,
                LoadTestOnly = true,
                CorruptConsoleMessages = true
            };

            var client = CreateClient(conf);
            var rcc = new RConClient(client, client.ServerSetup.Password);
            RunUntilShutdown(rcc);
            Assert.IsTrue(rcc.Metrics.InboundPacketCount > 100);
            Assert.IsTrue(rcc.Metrics.DispatchedConsoleMessages == 0);
        }


        [TestMethod]
        [TestCategory("Correctness")]
        public void ShouldNotifyAboutCorruptedPackets()
        {
            var conf = new MockServerSetup
            {
                LoadTestConsoleMessages = 100,
                LoadTestOnly = true,
                CorruptConsoleMessages = true
            };
            var client = CreateClient(conf);
            var rcc = new RConClient(client, client.ServerSetup.Password);

            int notificationsCount = 0;
            rcc.PacketProblem += (sender, args) =>
                                     { 
                                         if (args.PacketProblemType == PacketProblemType.Corrupted) notificationsCount++;
                                     };

            RunUntilShutdown(rcc);

            var serverMetrics = client.Server.GetMetrics();
            Debug.WriteLine("Console Messages generated by server: {0}", serverMetrics.TotalConsoleMessagesGenerated);
            Debug.WriteLine("Inbound Packets received by client: {0}", rcc.Metrics.InboundPacketCount);
            Debug.WriteLine("Console Packets Acknowledgments received by server: {0}", serverMetrics.AckPacketsReceived);
            Debug.WriteLine("Keep Alive packets received by server: {0}", serverMetrics.KeepAlivePacketsReceived);
            Assert.IsTrue(notificationsCount == serverMetrics.TotalConsoleMessagesGenerated);
        }


        [TestMethod]
        [TestCategory("Correctness")]
        public void ShouldDiscardRepeatedConsoleMessages()
        {
            var conf = new MockServerSetup
            {
                LoadTestConsoleMessages = 100,
                LoadTestOnly = true,
                RepeatedConsoleMessages = true
            };
            var client = CreateClient(conf);
            var rcc = new RConClient(client, client.ServerSetup.Password);
            RunUntilShutdown(rcc);
            Assert.IsTrue(rcc.Metrics.DispatchedConsoleMessages > 10);
            Assert.IsTrue(rcc.Metrics.DispatchedConsoleMessages < 52);
            Debug.WriteLine("Inbound Packets received by client: {0}", rcc.Metrics.InboundPacketCount);
            Debug.WriteLine("Console messages dispatched by client: {0}", rcc.Metrics.DispatchedConsoleMessages);
        }


        [TestMethod]
        [TestCategory("Correctness")]
        public void ShouldReturnCommandResponsesCorrectly()
        {
            var conf = new MockServerSetup
                           {
                               LoginAtOnce = true
                           };
            var client = CreateClient(conf);
            var rcc = new RConClient(client, client.ServerSetup.Password);
            var connected = rcc.ConnectAsync().Result;
            Assert.IsTrue(connected, "not connected");

            var handler = rcc.SendCommand("getplayers");
            Assert.IsNotNull(handler);

            CommandResponseDatagram response = null;
            if (handler.WaitForResponse().Result)
            {
                response = handler.ResponseDatagram as CommandResponseDatagram;
            }

            Assert.IsNotNull(response);
            Assert.IsTrue(response.Body.StartsWith("Players on server:"));
            rcc.Close();

        }


        [TestMethod]
        [TestCategory("Correctness")]
        public void ShoulNotifyPacketLoss()
        {
            throw new NotImplementedException();
        }


        [TestMethod]
        [TestCategory("Correctness")]
        public void ShouldDiscardRepeatedCommandResponses()
        {
            Assert.Fail();
        }


        [TestMethod]
        [TestCategory("Correctness")]
        public void ShouldDiscardRepeatedCommandResponseParts()
        {
            Assert.Fail();
        }


        [TestMethod]
        [TestCategory("Correctness")]
        public void ShouldAcceptOutOfOrderCommandResponsesCorrectly()
        {
            Assert.Fail();
        }


        private static MockUdpClient CreateClient(MockServerSetup conf)
        {
            var client = new MockUdpClient();
            client.Setup(conf);
            return client;
        }


        private static void RunUntilShutdown(RConClient rcc)
        {
            var connected = rcc.ConnectAsync().Result;
            Assert.IsTrue(connected, "not connected");
            rcc.WaitUntilShutdown();
            rcc.Close();
        }
    }
}
