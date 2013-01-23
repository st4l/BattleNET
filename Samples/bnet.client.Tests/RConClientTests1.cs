// ----------------------------------------------------------------------------------------------------
// <copyright file="RConClientTests1.cs" company="Me">Copyright (c) 2012 St4l.</copyright>
// ----------------------------------------------------------------------------------------------------
namespace bnet.client.Tests
{
    using System.Diagnostics;
    using System.Threading;
    using BNet.Client;
    using Microsoft.VisualStudio.TestTools.UnitTesting;


    [TestClass]
    public class RConClientTests1
    {
        [TestMethod]
        public void ShouldLogin()
        {
            var rcc = new RConClient("68.233.230.165", 2302, "70e02f66");
            rcc.MessageReceived += (sender, args) => Debug.WriteLine(args.MessageBody);
            rcc.ConnectAsync();
            Thread.Sleep(5 * 60 * 1000);
        }
    }
}
