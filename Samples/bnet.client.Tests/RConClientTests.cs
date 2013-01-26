// ----------------------------------------------------------------------------------------------------
// <copyright file="RConClientTests.cs" company="Me">Copyright (c) 2012 St4l.</copyright>
// ----------------------------------------------------------------------------------------------------

namespace bnet.client.Tests
{
    using System;
    using System.Security.Authentication;
    using System.Threading;
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
            var rcc = CreateClient(new MockServerSetup { OnlyLogin = true });
            var connected = rcc.ConnectAsync().Result;
            Assert.IsTrue(connected, "not connected");
        }


        [TestMethod]
        [TestCategory("Correctness")]
        public void ShouldThrowOnLoginWithWrongPassword()
        {
            var rcc = CreateClient(new MockServerSetup { OnlyLogin = true }, "fnipw93457");

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
            var rcc = CreateClient(conf);

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
                    LoadTestOnly = true,
                    ShutdownServerTime = DateTime.Now.AddMinutes(5)
                };
            var rcc = CreateClient(conf);
            var connected = rcc.ConnectAsync().Result;
            Assert.IsTrue(connected, "not connected");
            rcc.WaitUntilShutdown();
            rcc.Close();
            Assert.IsTrue(rcc.Metrics.InboundPacketCount > 50000);
        }


        [TestMethod]
        [TestCategory("Correctness")]
        public void ShouldDiscardCorruptedPackets()
        {
            Assert.Fail();
        }


        [TestMethod]
        [TestCategory("Correctness")]
        public void ShouldNotifyAboutCorruptedPackets()
        {
            Assert.Fail();
        }


        [TestMethod]
        [TestCategory("Correctness")]
        public void ShouldDiscardRepeatedPackets()
        {
            Assert.Fail();
        }


        [TestMethod]
        [TestCategory("Correctness")]
        public void ShoulNotifyPacketLoss()
        {
            Assert.Fail();
        }


        [TestMethod]
        [TestCategory("Correctness")]
        public void ShouldReturnCommandResponsesCorrectly()
        {
            Assert.Fail();
        }


        [TestMethod]
        [TestCategory("Correctness")]
        public void ShouldAcceptOutOfOrderCommandResponsesCorrectly()
        {
            Assert.Fail();
        }


        [TestMethod]
        [TestCategory("Correctness")]
        public void ShouldParseTonsOfConsoleMessages()
        {
            Assert.Fail();
        }


        private static RConClient CreateClient(MockServerSetup conf, string password = null)
        {
            var client = new MockUdpClient();
            client.Setup(conf);

            var pwd = password ?? conf.Password;
            var rcc = new RConClient(client, pwd);
            return rcc;
        }
    }
}
