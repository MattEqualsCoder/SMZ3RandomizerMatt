﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Randomizer.SMZ3.Tracking.AutoTracking
{
    /// <summary>
    /// Connector for Lua scripts that opens a socket server that the
    /// Lua scripts will connect to. 
    /// </summary>
    public class LuaConnector : IEmulatorConnector
    {

        private TcpListener? _tcpListener = null;
        private bool _isEnabled = false;
        private bool _isConnected = false;
        private Socket? _socket = null;
        private ILogger<LuaConnector> _logger;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logger"></param>
        public LuaConnector(ILogger<LuaConnector> logger)
        {
            _logger = logger;
            _isEnabled = true;
            _ = Task.Factory.StartNew(() => StartSocketServer());
        }

        /// <summary>
        /// When a connection with the emulator is established
        /// </summary>
        public event EventHandler? OnConnected;

        /// <summary>
        /// When the connection with the emulator has been lost
        /// </summary>
        public event EventHandler? OnDisconnected;

        /// <summary>
        /// When a message from the emulator with memory has been returned
        /// </summary>
        public event EmulatorDataReceivedEventHandler? MessageReceived;

        /// <summary>
        /// If the connector is currently connected to the emulator
        /// </summary>
        /// <returns></returns>
        public bool IsConnected() => _isConnected;

        /// <summary>
        /// Sends a message to the emulator
        /// </summary>
        /// <param name="message">The message to send to the emulator</param>
        public void SendMessage(EmulatorAction message)
        {
            if (!_isEnabled) return;
            LuaMessage? request = null;

            // Request memory from the emulator
            if (message.Type == EmulatorActionType.ReadBlock)
            {
                request = new()
                {
                    Action = "read_block",
                    Address = message.Address,
                    Length = message.Length,
                    Domain = GetDomainString(message.Domain)
                };
            }

            if (request == null) return;

            var msgString = JsonSerializer.Serialize(request) + "\0";
            try
            {
                _socket.Send(Encoding.ASCII.GetBytes(msgString));
            }
            catch (SocketException ex)
            {
                _logger.LogError(ex, "Error sending message");
                if (_isConnected)
                {
                    _isConnected = false;
                    OnDisconnected?.Invoke(this, new());
                }
            }
        }

        /// <summary>
        /// Closes the connection so that the connector can be destroyed
        /// </summary>
        public void Dispose()
        {
            _isConnected = false;
            _isEnabled = false;
            if (_tcpListener != null)
            {
                _tcpListener.Stop();
            }
            if (_socket != null && _socket.Connected)
            {
                _socket.Close();
            }
            GC.SuppressFinalize(this);
        }

        private void StartSocketServer()
        {
            _tcpListener = new TcpListener(IPAddress.Loopback, 6969);
            _tcpListener.Start();
            while (_isEnabled)
            {
                try
                {
                    _socket = _tcpListener.AcceptSocket();
                    _logger.LogInformation("Socket accepted");
                    if (_socket.Connected)
                    {
                        using (var stream = new NetworkStream(_socket))
                        using (var writer = new StreamWriter(stream))
                        using (var reader = new StreamReader(stream))
                        {
                            try
                            {
                                _isConnected = true;
                                _logger.LogInformation("Socket connected");
                                OnConnected?.Invoke(this, new());
                                var line = reader.ReadLine();
                                while (line != null && _socket.Connected)
                                {
                                    var message = JsonSerializer.Deserialize<LuaMessage>(line);
                                    if (message != null && message.Bytes != null)
                                    {
                                        var data = new EmulatorMemoryData(Convert.FromBase64String(message.Bytes));
                                        MessageReceived?.Invoke(this, new(message.Address, data));
                                    }
                                    line = reader.ReadLine();
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error with connection");
                                if (_isConnected)
                                {
                                    _isConnected = false;
                                    OnDisconnected?.Invoke(this, new());
                                }
                            }
                        }

                    }
                }
                catch (SocketException se)
                {
                    _logger.LogError(se, "Error in accepting socket");
                }
            }
        }

        private string GetDomainString(MemoryDomain domain)
        {
            switch (domain)
            {
                case MemoryDomain.WRAM:
                    return "WRAM";
                case MemoryDomain.CartRAM:
                    return "CARTRAM";
                case MemoryDomain.CartROM:
                    return "CARTROM";
                default:
                    return "";
            }
        }
    }
}
