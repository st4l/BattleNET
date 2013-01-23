// ----------------------------------------------------------------------------------------------------
// <copyright file="BattlEyeClient.cs" company="Me">Copyright (c) 2012 St4l.</copyright>
// ----------------------------------------------------------------------------------------------------

namespace BattleNET
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;


    public class StateObject
    {
        public const int BUFFER_SIZE = 4096;

        public Socket WorkSocket;

        public readonly byte[] Buffer = new byte[BUFFER_SIZE];

        public StringBuilder Message = new StringBuilder();

        public int PacketsTodo;
    }


    internal class CommandResponseHandlerInfo
    {
        public CommandResponseReceivedEventHandler Handler { get; set; }

        public DateTime Expires { get; set; }
    }


    public class BattlEyeClient
    {
        private Socket socket;

        private DateTime commandSend;

        private DateTime responseReceived;

        private BattlEyeDisconnectionType? disconnectionType;

        private bool keepRunning;

        private byte packetNumber;

        private SortedDictionary<int, string> packetLog;

        private Dictionary<int, CommandResponseHandlerInfo> cmdCallbacks;

        private BattlEyeLoginCredentials loginCredentials;


        public BattlEyeClient(BattlEyeLoginCredentials loginCredentials)
        {
            this.DiscardConsoleMessages = false;
            this.loginCredentials = loginCredentials;
        }


        public event CommandResponseReceivedEventHandler CommandResponseReceived;

        public event BattlEyeMessageEventHandler MessageEvent;

        public event BattlEyeConnectEventHandler ConnectEvent;

        public event BattlEyeDisconnectEventHandler DisconnectEvent;

        public bool Connected
        {
            get { return this.socket != null && this.socket.Connected; }
        }

        public bool ReconnectOnPacketLoss { get; set; }

        public int CommandQueue
        {
            get { return this.packetLog.Count; }
        }

        public bool DiscardConsoleMessages { get; set; }


        public BattlEyeConnectionResult Connect()
        {
            this.commandSend = DateTime.Now;
            this.responseReceived = DateTime.Now;

            this.packetNumber = 0;
            this.packetLog = new SortedDictionary<int, string>();
            this.cmdCallbacks = new Dictionary<int, CommandResponseHandlerInfo>();

            this.keepRunning = true;
            IPAddress ipAddress = IPAddress.Parse(this.loginCredentials.Host);
            EndPoint remoteEp = new IPEndPoint(ipAddress, this.loginCredentials.Port);

            this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
                              {
                                  ReceiveBufferSize
                                      =
                                      UInt16
                                      .MaxValue,
                                  ReceiveTimeout
                                      =
                                      5000
                              };

            try
            {
                this.socket.Connect(remoteEp);

                if (this.SendLoginPacket(this.loginCredentials.Password)
                    == BattlEyeCommandResult.Error)
                {
                    return BattlEyeConnectionResult.ConnectionFailed;
                }

                var bytesReceived = new Byte[4096];

                this.socket.Receive(bytesReceived, bytesReceived.Length, 0);

                if (bytesReceived[7] == 0x00)
                {
                    if (bytesReceived[8] == 0x01)
                    {
                        this.OnConnect(this.loginCredentials, BattlEyeConnectionResult.Success);

                        this.Receive();
                    }
                    else
                    {
                        this.OnConnect(this.loginCredentials, BattlEyeConnectionResult.InvalidLogin);
                        return BattlEyeConnectionResult.InvalidLogin;
                    }
                }
            }
            catch
            {
                if (this.disconnectionType == BattlEyeDisconnectionType.ConnectionLost)
                {
                    this.Disconnect(BattlEyeDisconnectionType.ConnectionLost);
                    this.Connect();
                    return BattlEyeConnectionResult.ConnectionFailed;
                }
                this.OnConnect(this.loginCredentials, BattlEyeConnectionResult.ConnectionFailed);
                return BattlEyeConnectionResult.ConnectionFailed;
            }

            return BattlEyeConnectionResult.Success;
        }


        public BattlEyeCommandResult SendCommandPacket(
            BattlEyeCommand command,
            string parameters = "",
            CommandResponseReceivedEventHandler handler = null,
            int timeOutInSecs = 10)
        {
            return this.SendCommandPacket(
                Helpers.StringValueOf(command) + " " + parameters, true, handler, timeOutInSecs);
        }


        public BattlEyeCommandResult SendCommandPacket(
            string command,
            bool log = true,
            CommandResponseReceivedEventHandler handler = null,
            int timeOutInSecs = 10)
        {
            try
            {
                if (!this.socket.Connected)
                {
                    return BattlEyeCommandResult.NotConnected;
                }

                byte[] packet = this.ConstructPacket(1, this.packetNumber, command);

                this.socket.Send(packet);
                this.commandSend = DateTime.Now;
                if (handler != null)
                {
                    this.cmdCallbacks.Add(
                        this.packetNumber,
                        new CommandResponseHandlerInfo
                            {
                                Expires =
                                    DateTime.Now.AddSeconds(
                                        timeOutInSecs),
                                Handler = handler
                            });
                }

                if (log)
                {
                    this.packetLog.Add(this.packetNumber, command);
                }
                this.packetNumber = (this.packetNumber == 255)
                                        ? (byte)0
                                        : (byte)(this.packetNumber + 1);
            }
            catch
            {
                return BattlEyeCommandResult.Error;
            }

            return BattlEyeCommandResult.Success;
        }


        public void Disconnect()
        {
            this.keepRunning = false;

            if (this.socket.Connected)
            {
                this.socket.Shutdown(SocketShutdown.Both);
                this.socket.Close();
            }

            this.OnDisconnect(this.loginCredentials, BattlEyeDisconnectionType.Manual);
        }


        private BattlEyeCommandResult SendLoginPacket(string command)
        {
            try
            {
                if (!this.socket.Connected)
                {
                    return BattlEyeCommandResult.NotConnected;
                }

                byte[] packet = this.ConstructPacket(0, 0, command);
                this.socket.Send(packet);

                this.commandSend = DateTime.Now;
            }
            catch
            {
                return BattlEyeCommandResult.Error;
            }

            return BattlEyeCommandResult.Success;
        }


        private BattlEyeCommandResult SendAcknowledgePacket(string command)
        {
            try
            {
                if (!this.socket.Connected)
                {
                    return BattlEyeCommandResult.NotConnected;
                }

                byte[] packet = this.ConstructPacket(2, 0, command);
                this.socket.Send(packet);

                this.commandSend = DateTime.Now;
            }
            catch
            {
                return BattlEyeCommandResult.Error;
            }

            return BattlEyeCommandResult.Success;
        }


        private byte[] ConstructPacket(int packetType, byte sequenceNumber, string command)
        {
            string type;

            switch (packetType)
            {
                case 0:
                    type = Helpers.Hex2Ascii("FF00");
                    break;
                case 1:
                    type = Helpers.Hex2Ascii("FF01");
                    break;
                case 2:
                    type = Helpers.Hex2Ascii("FF02");
                    break;
                default:
                    return new byte[] { };
            }

            string count = Helpers.Bytes2String(new[] { sequenceNumber });

            var x = Helpers.String2Bytes(type + ((packetType != 1) ? "" : count) + command);
            var crc = new CRC32();
            byte[] byteArray = crc.ComputeHash(x);

            var hash =
                new string(
                    Helpers.Hex2Ascii(BitConverter.ToString(byteArray).Replace("-", ""))
                           .ToCharArray()
                           .Reverse()
                           .ToArray());

            string packet = "BE" + hash + type + ((packetType != 1) ? "" : count) + command;

            return Helpers.String2Bytes(packet);
        }


        private void Disconnect(BattlEyeDisconnectionType? type)
        {
            if (type == BattlEyeDisconnectionType.ConnectionLost)
            {
                this.disconnectionType = BattlEyeDisconnectionType.ConnectionLost;
            }

            this.keepRunning = false;

            if (this.socket.Connected)
            {
                this.socket.Shutdown(SocketShutdown.Both);
                this.socket.Close();
            }

            if (type != null)
            {
                this.OnDisconnect(this.loginCredentials, type);
            }
        }


        private void Receive()
        {
            var state = new StateObject { WorkSocket = this.socket };

            this.disconnectionType = null;

            this.socket.BeginReceive(
                state.Buffer, 0, StateObject.BUFFER_SIZE, 0, this.ReceiveCallback, state);

            new Thread(this.MainLoop).Start();
        }


        private void MainLoop()
        {
            while (this.socket.Connected && this.keepRunning)
            {
                TimeSpan timeoutClient = DateTime.Now - this.commandSend;
                TimeSpan timeoutServer = DateTime.Now - this.responseReceived;

                if (timeoutClient.TotalSeconds >= 5)
                {
                    if (timeoutServer.TotalSeconds >= 20)
                    {
                        this.Disconnect(BattlEyeDisconnectionType.ConnectionLost);
                        this.keepRunning = true;
                    }
                    else
                    {
                        if (this.packetLog.Count == 0)
                        {
                            this.SendCommandPacket(null, false);
                        }
                    }
                }

                if (this.packetLog.Count > 0 && this.socket.Available == 0)
                {
                    try
                    {
                        int key = this.packetLog.First().Key;
                        string value = this.packetLog[key];
                        this.SendCommandPacket(value, false);
                        this.packetLog.Remove(key);
                    }
                    catch
                    {
                        // Prevent possible crash when packet is received at the same moment it's trying to resend it.
                    }
                }

                Thread.Sleep(500);
                this.RemoveCmdCallbacks();
            }

            if (!this.socket.Connected)
            {
                if (this.ReconnectOnPacketLoss && this.keepRunning)
                {
                    this.Connect();
                }
                else if (!this.keepRunning)
                {
                    //let the thread finish without further action
                }
                else
                {
                    this.OnDisconnect(this.loginCredentials, BattlEyeDisconnectionType.ConnectionLost);
                }
            }
        }


        private void RemoveCmdCallbacks(IEnumerable<int> callbackIds = null)
        {
            lock (this.cmdCallbacks)
            {
                if (callbackIds == null)
                {
                    callbackIds =
                        (from kv in this.cmdCallbacks
                         where DateTime.Now > kv.Value.Expires
                         select kv.Key).ToArray();
                }

                foreach (var callbackId in callbackIds)
                {
                    this.cmdCallbacks.Remove(callbackId);
                }
            }
        }


        private void ReceiveCallback(IAsyncResult ar)
        {
            // this method can be called from the middle of a .Disconnect() call
            // test with Debug > Exception > CLR exs on
            if (!this.keepRunning)
            {
                return;
            }

            try
            {
                var state = (StateObject)ar.AsyncState;
                Socket client = state.WorkSocket;

                int bytesRead = client.EndReceive(ar);

                if (state.Buffer[7] == 0x02)
                {
                    // 01 = console message
                    this.SendAcknowledgePacket(Helpers.Bytes2String(new[] { state.Buffer[8] }));
                    if (!this.DiscardConsoleMessages)
                    {
                        this.OnBattlEyeMessage(Helpers.Bytes2String(state.Buffer, 9, bytesRead - 9));
                    }
                }
                else if (state.Buffer[7] == 0x01)
                {
                    // 01 means it's a command ack or response
                    var cmdSeqId = (int)state.Buffer[8];

                    // do we have more than just an ack?
                    if (bytesRead > 9)
                    {
                        // is it part of a multi-packet response?
                        if (state.Buffer[7] == 0x01 && state.Buffer[9] == 0x00)
                        {
                            if (state.Buffer[11] == 0)
                            {
                                state.PacketsTodo = state.Buffer[10];
                            }

                            if (state.PacketsTodo > 0)
                            {
                                state.Message.Append(
                                    Helpers.Bytes2String(state.Buffer, 12, bytesRead - 12));
                                state.PacketsTodo--;
                            }

                            if (state.PacketsTodo == 0)
                            {
                                this.OnCommandResponseReceived(cmdSeqId, state.Message.ToString());
                                state.Message = new StringBuilder();
                                state.PacketsTodo = 0;
                            }
                        }
                        else
                        {
                            // single packet response: everything from 9 onwards is the command response
                            state.Message = new StringBuilder();
                            state.PacketsTodo = 0;

                            this.OnCommandResponseReceived(
                                cmdSeqId, Helpers.Bytes2String(state.Buffer, 9, bytesRead - 9));
                        }
                    }
                    else // it was just a command ack
                    {
                        this.OnCommandResponseReceived(cmdSeqId, "OK");
                    }

                    if (this.packetLog.ContainsKey(state.Buffer[8]))
                    {
                        this.packetLog.Remove(state.Buffer[8]);
                    }
                }

                this.responseReceived = DateTime.Now;

                client.BeginReceive(
                    state.Buffer, 0, StateObject.BUFFER_SIZE, 0, this.ReceiveCallback, state);
            }
            catch
            {
                // do nothing
            }
        }


        private void OnCommandResponseReceived(int cmdId, string message)
        {
            if (this.cmdCallbacks.ContainsKey(cmdId))
            {
                var handler = this.cmdCallbacks[cmdId].Handler;
                this.RemoveCmdCallbacks(new[] { cmdId });
                handler(this, new BattlEyeCommandResponseEventArgs(message));
            }

            if (this.CommandResponseReceived != null)
            {
                this.CommandResponseReceived(this, new BattlEyeCommandResponseEventArgs(message));
            }
        }


        private void OnBattlEyeMessage(string message)
        {
            if (this.MessageEvent != null)
            {
                this.MessageEvent(new BattlEyeMessageEventArgs(message));
            }
        }


        private void OnConnect(
            BattlEyeLoginCredentials loginDetails, BattlEyeConnectionResult connectionResult)
        {
            if (connectionResult == BattlEyeConnectionResult.ConnectionFailed
                || connectionResult == BattlEyeConnectionResult.InvalidLogin)
            {
                this.Disconnect(null);
            }

            if (this.ConnectEvent != null)
            {
                this.ConnectEvent(new BattlEyeConnectEventArgs(loginDetails, connectionResult));
            }
        }


        private void OnDisconnect(
            BattlEyeLoginCredentials loginDetails, BattlEyeDisconnectionType? type)
        {
            if (this.DisconnectEvent != null)
            {
                this.DisconnectEvent(
                    new BattlEyeDisconnectEventArgs(loginDetails, type));
            }
        }
    }
}
