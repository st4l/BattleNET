/* * * * * * * * * * * * * * * * * * * * * * * * * * * * * *
 * BattleNET v1.2 - BattlEye Library and Client            *
 *                                                         *
 *  Copyright (C) 2012 by it's authors.                    *
 *  Some rights reserved. See COPYING.TXT, AUTHORS.TXT.    *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */

// http://www.battleye.com/downloads/BERConProtocol.txt

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace BattleNET
{
    public class StateObject
    {
        public Socket WorkSocket = null;
        public const int BufferSize = 4096;
        public byte[] Buffer = new byte[BufferSize];
        public StringBuilder Message = new StringBuilder();
        public int PacketsTodo = 0;
    }

    class CommandResponseHandlerInfo
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

        public bool Connected
        {
            get
            {
                return socket != null && socket.Connected;
            }
        }

        public bool ReconnectOnPacketLoss
        {
            get;
            set;
        }

        public int CommandQueue
        {
            get
            {
                return packetLog.Count;
            }
        }

        public bool DiscardConsoleMessages { get; set; }

        
        public BattlEyeClient(BattlEyeLoginCredentials loginCredentials)
        {
            this.DiscardConsoleMessages = false;
            this.loginCredentials = loginCredentials;
        }

        public BattlEyeConnectionResult Connect()
        {
            commandSend = DateTime.Now;
            responseReceived = DateTime.Now;

            packetNumber = 0;
            packetLog = new SortedDictionary<int, string>();
            cmdCallbacks = new Dictionary<int, CommandResponseHandlerInfo>();

            keepRunning = true;
            IPAddress ipAddress = IPAddress.Parse(loginCredentials.Host);
            EndPoint remoteEP = new IPEndPoint(ipAddress, loginCredentials.Port);

            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.ReceiveBufferSize = UInt16.MaxValue;
            socket.ReceiveTimeout = 5000;

            try
            {
                socket.Connect(remoteEP);

                if (SendLoginPacket(loginCredentials.Password) == BattlEyeCommandResult.Error)
                    return BattlEyeConnectionResult.ConnectionFailed;

                var bytesReceived = new Byte[4096];
                int bytes = 0;

                bytes = socket.Receive(bytesReceived, bytesReceived.Length, 0);

                if (bytesReceived[7] == 0x00)
                {
                    if (bytesReceived[8] == 0x01)
                    {
                        OnConnect(loginCredentials, BattlEyeConnectionResult.Success);

                        Receive();
                    }
                    else
                    {
                        OnConnect(loginCredentials, BattlEyeConnectionResult.InvalidLogin);
                        return BattlEyeConnectionResult.InvalidLogin;
                    }
                }
            }
            catch
            {
                if (disconnectionType == BattlEyeDisconnectionType.ConnectionLost)
                {
                    Disconnect(BattlEyeDisconnectionType.ConnectionLost);
                    Connect();
                    return BattlEyeConnectionResult.ConnectionFailed;
                }
                else
                {
                    OnConnect(loginCredentials, BattlEyeConnectionResult.ConnectionFailed);
                    return BattlEyeConnectionResult.ConnectionFailed;
                }
            }

            return BattlEyeConnectionResult.Success;
        }

        private BattlEyeCommandResult SendLoginPacket(string command)
        {
            try
            {
                if (!socket.Connected)
                    return BattlEyeCommandResult.NotConnected;

                byte[] packet = ConstructPacket(0, 0, command);
                socket.Send(packet);

                commandSend = DateTime.Now;
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
                if (!socket.Connected)
                    return BattlEyeCommandResult.NotConnected;

                byte[] packet = ConstructPacket(2, 0, command);
                socket.Send(packet);

                commandSend = DateTime.Now;
            }
            catch
            {
                return BattlEyeCommandResult.Error;
            }

            return BattlEyeCommandResult.Success;
        }

        public BattlEyeCommandResult SendCommandPacket(BattlEyeCommand command, string parameters = "", CommandResponseReceivedEventHandler handler = null, int timeOutInSecs = 10)
        {
            return SendCommandPacket(Helpers.StringValueOf(command) + " " + parameters, true, handler, timeOutInSecs);
        }

        public BattlEyeCommandResult SendCommandPacket(string command, bool log = true, CommandResponseReceivedEventHandler handler = null, int timeOutInSecs = 10)
        {
            try
            {
                if (!socket.Connected)
                    return BattlEyeCommandResult.NotConnected;

                byte[] packet = ConstructPacket(1, packetNumber, command);

                socket.Send(packet);
                commandSend = DateTime.Now;
                if (handler != null)
                {
                    cmdCallbacks.Add(packetNumber, new CommandResponseHandlerInfo
                        {
                            Expires = DateTime.Now.AddSeconds(timeOutInSecs),
                            Handler = handler
                        });
                }

                if (log)
                {
                    packetLog.Add(packetNumber, command);
                }
                packetNumber = (packetNumber == 255) ? (byte)0 : (byte)(packetNumber + 1);
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

            byte[] byteArray = new CRC32().ComputeHash(Helpers.String2Bytes(type + ((packetType != 1) ? "" : count) + command));

            string hash = new string(Helpers.Hex2Ascii(BitConverter.ToString(byteArray).Replace("-", "")).ToCharArray().Reverse().ToArray());

            string packet = "BE" + hash + type + ((packetType != 1) ? "" : count) + command;

            return Helpers.String2Bytes(packet);
        }


        public void Disconnect()
        {
            keepRunning = false;

            if (socket.Connected)
            {
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
            }

            OnDisconnect(loginCredentials, BattlEyeDisconnectionType.Manual);
        }

        private void Disconnect(BattlEyeDisconnectionType? disconnectionType)
        {
            if (disconnectionType == BattlEyeDisconnectionType.ConnectionLost)
                this.disconnectionType = BattlEyeDisconnectionType.ConnectionLost;

            keepRunning = false;

            if (socket.Connected)
            {
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
            }

            if (disconnectionType != null)
                OnDisconnect(loginCredentials, disconnectionType);
        }

        private void Receive()
        {
            StateObject state = new StateObject();
            state.WorkSocket = socket;

            disconnectionType = null;

            socket.BeginReceive(state.Buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReceiveCallback), state);

            new Thread(delegate() {
                while (socket.Connected && keepRunning)
                {
                    TimeSpan timeoutClient = DateTime.Now - commandSend;
                    TimeSpan timeoutServer = DateTime.Now - responseReceived;

                    if (timeoutClient.TotalSeconds >= 5)
                    {
                        if (timeoutServer.TotalSeconds >= 20)
                        {
                            Disconnect(BattlEyeDisconnectionType.ConnectionLost);
                            keepRunning = true;
                        }
                        else
                        {
                            if (packetLog.Count == 0)
                            {
                                SendCommandPacket(null, false);
                            }
                        }
                    }

                    if (packetLog.Count > 0 && socket.Available == 0)
                    {
                        try
                        {
                            int key = packetLog.First().Key;
                            string value = packetLog[key];
                            SendCommandPacket(value, false);
                            packetLog.Remove(key);
                        }
                        catch
                        {
                            // Prevent possible crash when packet is received at the same moment it's trying to resend it.
                        }
                    }

                    Thread.Sleep(500);
                    RemoveCmdCallbacks(null);
                }

                if (!socket.Connected)
                {
                    if (ReconnectOnPacketLoss && keepRunning)
                    {
                        Connect();
                    }
                    else if (!keepRunning)
                    {
                         //let the thread finish without further action
                    }
                    else
                    {
                        OnDisconnect(loginCredentials, BattlEyeDisconnectionType.ConnectionLost);
                    }
                }
            }).Start();
        }


        private void RemoveCmdCallbacks(IEnumerable<int> callbackIds = null)
        {
            lock (cmdCallbacks)
            {
                if (callbackIds == null)
                {
                    callbackIds = from kv in cmdCallbacks
                                  where DateTime.Now > kv.Value.Expires
                                  select kv.Key;
                }

                foreach (var callbackId in callbackIds)
                {
                    cmdCallbacks.Remove(callbackId);
                }
            }
        }


        private void ReceiveCallback(IAsyncResult ar)
        {
            // this method can be called from the middle of a .Disconnect() call
            // test with Debug > Exception > CLR exs on
            if (!keepRunning)
            {
                return;
            }

            try
            {
                StateObject state = (StateObject)ar.AsyncState;
                Socket client = state.WorkSocket;

                int bytesRead = client.EndReceive(ar);

                if (state.Buffer[7] == 0x02 && !DiscardConsoleMessages)
                {
                    SendAcknowledgePacket(Helpers.Bytes2String(new[] { state.Buffer[8] }));
                    OnBattlEyeMessage(Helpers.Bytes2String(state.Buffer, 9, bytesRead - 9));
                }
                    // 01 means it's a command ack or response
                else if (state.Buffer[7] == 0x01)
                {
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
                                state.Message.Append(Helpers.Bytes2String(state.Buffer, 12, bytesRead - 12));
                                state.PacketsTodo--;
                            }

                            if (state.PacketsTodo == 0)
                            {
                                OnCommandResponseReceived(cmdSeqId, state.Message.ToString());
                                state.Message = new StringBuilder();
                                state.PacketsTodo = 0;
                            }
                        }
                        else // single packet response: everything from 9 onwards is the command response
                        {
                            state.Message = new StringBuilder();
                            state.PacketsTodo = 0;

                            OnCommandResponseReceived(cmdSeqId, Helpers.Bytes2String(state.Buffer, 9, bytesRead - 9));
                        }
                    }
                    else // it was just a command ack
                    {
                        OnCommandResponseReceived(cmdSeqId, "OK");
                    }

                    if (packetLog.ContainsKey(state.Buffer[8]))
                    {
                        packetLog.Remove(state.Buffer[8]);
                    }
                }

                responseReceived = DateTime.Now;

                client.BeginReceive(state.Buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReceiveCallback), state);
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
                var handler = cmdCallbacks[cmdId].Handler;
                RemoveCmdCallbacks(new[] {cmdId});
                handler(this, new BattlEyeCommandResponseEventArgs(message));
            }

            if (CommandResponseReceived != null)
            {
                CommandResponseReceived(this, new BattlEyeCommandResponseEventArgs(message));
            }
        }

        private void OnBattlEyeMessage(string message)
        {
            if (MessageEvent != null)
                MessageEvent(new BattlEyeMessageEventArgs(message));
        }

        private void OnConnect(BattlEyeLoginCredentials loginDetails, BattlEyeConnectionResult connectionResult)
        {
            if (connectionResult == BattlEyeConnectionResult.ConnectionFailed || connectionResult == BattlEyeConnectionResult.InvalidLogin)
                Disconnect(null);

            if (ConnectEvent != null)
                ConnectEvent(new BattlEyeConnectEventArgs(loginDetails, connectionResult));
        }

        private void OnDisconnect(BattlEyeLoginCredentials loginDetails, BattlEyeDisconnectionType? disconnectionType)
        {
            if (DisconnectEvent != null)
                DisconnectEvent(new BattlEyeDisconnectEventArgs(loginDetails, disconnectionType));
        }

        public event CommandResponseReceivedEventHandler CommandResponseReceived;
        public event BattlEyeMessageEventHandler MessageEvent;
        public event BattlEyeConnectEventHandler ConnectEvent;
        public event BattlEyeDisconnectEventHandler DisconnectEvent;
    }
}
