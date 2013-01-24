// ----------------------------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Me">Copyright (c) 2012 St4l.</copyright>
// ----------------------------------------------------------------------------------------------------
namespace bnet.WinClient
{
    using System;
    using System.Net.Sockets;
    using System.Security.Authentication;
    using System.Windows;
    using BNet.Client;


    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
        }


        private async void ConnectClick(object sender, RoutedEventArgs e)
        {
            var host = "68.233.230.165";
            var port = 2302;
            var rcc = new RConClient(host, port, "70e02f66");
            rcc.MessageReceived += this.OnRccOnMessageReceived;


            bool connected = false;
            try
            {
                var msg = string.Format("Connecting to {0} on port {1}...", host, port);
                this.WriteLine(msg);

                connected = await rcc.ConnectAsync();
                this.WriteLine(
                    !connected ? "Could not connect to the specified remote host." : "Connected!");
            }
            catch (TimeoutException te)
            {
                this.WriteLine(te.Message);
            }
            catch (InvalidCredentialException te)
            {
                this.WriteLine(te.Message);
            }
            finally
            {
                if (!connected)
                {
                    rcc.MessageReceived -= this.OnRccOnMessageReceived;
                }
            }
        }


        private void OnRccOnMessageReceived(object s, MessageReceivedEventArgs args)
        {
            this.WriteLine(args.MessageBody);
        }


        private void WriteLine(string text)
        {
            this.TxtLog.AppendText(text + "\r\n");
            this.TxtLog.ScrollToEnd();
        }


        private void asdf()
        {
            var c = new TcpClient();
            c.ConnectAsync("localhost", 23);
        }

    }
}
