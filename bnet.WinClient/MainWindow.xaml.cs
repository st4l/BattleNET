// ----------------------------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Me">Copyright (c) 2012 St4l.</copyright>
// ----------------------------------------------------------------------------------------------------

using BNet.Client.Datagrams;

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
            this.UpdateUiStatus();
        }

        string host = "68.233.230.165";
        int port = 2302;
        private RConClient rcc;
        private bool connected = false;


        private async void ConnectClick(object sender, RoutedEventArgs e)
        {
            this.btnConnect.IsEnabled = false;
            if (this.connected)
            {
                this.rcc.MessageReceived -= this.OnRccOnMessageReceived;
                this.rcc.Disconnected -= this.RccOnDisconnected;
                this.rcc.Close();
                this.rcc = null;
                this.connected = false;
                this.UpdateUiStatus();
                this.btnConnect.IsEnabled = true;
                return;
            }

            this.rcc = new RConClient(host, port, "70e02f66");
            this.rcc.MessageReceived += this.OnRccOnMessageReceived;
            this.rcc.Disconnected += this.RccOnDisconnected;

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
                if (!this.connected)
                {
                    this.rcc.MessageReceived -= this.OnRccOnMessageReceived;
                    this.rcc.Disconnected -= this.RccOnDisconnected;
                }
            }
            this.btnConnect.IsEnabled = true;
            this.UpdateUiStatus();
        }


        private void UpdateUiStatus()
        {
            if (this.connected)
            {
                this.btnConnect.Content = "Disconnect";
                this.txtCommand.IsEnabled = true;
                this.btnSendCommand.IsEnabled = true;
                this.txtCommand.Visibility = Visibility.Visible;
                this.btnSendCommand.Visibility = Visibility.Visible;
            }
            else
            {
                this.btnConnect.Content = "Connect";
                this.txtCommand.IsEnabled = false;
                this.btnSendCommand.IsEnabled = false;
                this.txtCommand.Visibility = Visibility.Hidden;
                this.btnSendCommand.Visibility = Visibility.Hidden;
            }
        }


        private void RccOnDisconnected(object sender, DisconnectedEventArgs disconnectedEventArgs)
        {
            this.WriteLine("Disconnected!");
            this.connected = false;
            this.UpdateUiStatus();
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


        
        private async void btnSendCommand_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(this.txtCommand.Text))
            {
                return;
            }
            this.WriteLine("> " + this.txtCommand.Text);

            var handler = await this.rcc.SendCommandAsync(txtCommand.Text);
            await handler.WaitForResponse();
            if (handler.ResponseDatagram != null)
            {
                var response = handler.ResponseDatagram as CommandResponseDatagram;
                if (response != null)
                {
                    this.WriteLine(response.Body);
                }
            }
        }

    }
}
