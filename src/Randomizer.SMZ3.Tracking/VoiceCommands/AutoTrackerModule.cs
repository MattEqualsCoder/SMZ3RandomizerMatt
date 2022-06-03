﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Randomizer.Shared;
using Randomizer.SMZ3.Tracking.Configuration;

namespace Randomizer.SMZ3.Tracking.VoiceCommands
{
    /// <summary>
    /// Module that handles the basics of reading the rom's memory while being played
    /// for autotracking purposes. It creates a listening tcp port for the lua script
    /// to connect to and then submits requests to the lua script and interprets the
    /// responses
    /// </summary>
    public class AutoTrackerModule : TrackerModule, IDisposable
    {
        private readonly Tracker _tracker;
        private readonly ILogger<AutoTrackerModule> _logger;
        private readonly List<AutoTrackerMessage> _requestMessages = new();
        private readonly Dictionary<int, Action<AutoTrackerMessage>> _responseActions = new();
        private readonly Dictionary<int, AutoTrackerMessage> _previousResponses = new();
        private int _currentRequestIndex = 0;
        private Game _currentGame;
        private TcpListener? _tcpListener = null;
        private bool _hasStarted;
        private Socket? _socket = null;
        private AutoTrackerZeldaState _previousZeldaState;
        private AutoTrackerMetroidState _previousMetroidState;
        private HashSet<DungeonInfo> _enteredDungeons = new();
        private int _previousZeldaOverworldValue = -1;
        private int _previousMetroidRegionValue = -1;

        /// <summary>
        /// Initializes a new instance of the <see cref="AutoTrackerModule"/>
        /// class.
        /// </summary>
        /// <param name="tracker">The tracker instance.</param>
        /// <param name="logger">Used to write logging information.</param>
        public AutoTrackerModule(Tracker tracker, ILogger<AutoTrackerModule> logger) : base(tracker, logger)
        {
            _tracker = tracker;
            _logger = logger;
            _tracker.AutoTracker = this;

            // Check if the game has started. SM locations start as cleared in memory until you get to the title screen.
            AddEvent("read_block", "WRAM", 0x7e0020, 0x1, Game.Neither, (AutoTrackerMessage message) =>
            {
                var value = message.ReadUInt8(0);
                if (value != 0)
                {
                    _logger.LogInformation("Game started");
                    _hasStarted = true;
                    Tracker.Say(x => x.AutoTracker.GameStarted, Tracker.Rom.Seed);
                }
                _previousResponses[message.Address] = message;
            });

            // Active game
            AddEvent("read_block", "CARTRAM", 0x7033fe, 0x2, Game.Both, (AutoTrackerMessage message) =>
            {
                var value = message.ReadUInt8(0);
                if (value == 0x00)
                {
                    _currentGame = Game.Zelda;
                    _previousMetroidRegionValue = -1;
                    if (Tracker.Options.AutoTrackerChangeMap)
                    {
                        Tracker.UpdateMap("Zelda Combined");
                    }
                }
                else if (value == 0xFF)
                {
                    _currentGame = Game.SM;
                    _previousZeldaOverworldValue = -1;
                }
                _logger.LogInformation($"Game changed to: {_currentGame} {value}");
                _previousResponses[message.Address] = message;
            });

            // Zelda Room Locations
            AddEvent("read_block", "WRAM", 0x7ef000, 0x250, Game.Zelda, (AutoTrackerMessage message) =>
            {
                CheckLocations(message, LocationMemoryType.ZeldaRoom, true);
                CheckDungeons(message);
                _previousResponses[message.Address] = message;
            });

            // Zelda NPC Locations
            AddEvent("read_block", "WRAM", 0x7ef410, 0x2, Game.Zelda, (AutoTrackerMessage message) =>
            {
                CheckLocations(message, LocationMemoryType.ZeldaNPC, false);
                _previousResponses[message.Address] = message;
            });

            // Zelda Overworld Locations
            AddEvent("read_block", "WRAM", 0x7ef280, 0x82, Game.Zelda, (AutoTrackerMessage message) =>
            {
                CheckLocations(message, LocationMemoryType.ZeldaOverworld, false);
                _previousResponses[message.Address] = message;
            });

            // Zelda items while playing Zelda
            AddEvent("read_block", "WRAM", 0x7ef300, 0xD0, Game.Zelda, (AutoTrackerMessage message) =>
            {
                // CheckZeldaItemMemory
                CheckLocations(message, LocationMemoryType.ZeldaMisc, false);
                _previousResponses[message.Address] = message;
            });

            // Super Metroid locations
            AddEvent("read_block", "WRAM", 0x7ed870, 0x20, Game.SM, (AutoTrackerMessage message) =>
            {
                CheckLocations(message, LocationMemoryType.SMLocation, false);
                _previousResponses[message.Address] = message;
            });

            // Super Metroid bosses
            AddEvent("read_block", "WRAM", 0x7ed828, 0x08, Game.SM, (AutoTrackerMessage message) =>
            {
                CheckSMBosses(message);
                _previousResponses[message.Address] = message;
            });

            // Zelda State Checks
            AddEvent("read_block", "WRAM", 0x7e0000, 0x250, Game.Zelda, (AutoTrackerMessage message) =>
            {
                ZeldaStateChecks(message);
                _previousResponses[message.Address] = message;
            });

            // Metroid State Checks
            AddEvent("read_block", "WRAM", 0x7e0750, 0x400, Game.SM, (AutoTrackerMessage message) =>
            {
                MetroidStateChecks(message);
                _previousResponses[message.Address] = message;
            });


            // SM items while playing Zelda
            /*AddEvent("read_block", CorrectSRAMAddress(0xa17900), 0x10, Game.Zelda, (AutoTrackerMessage message) =>
            {
                var speed = message.Check8(0x03, 0x20);
                var plasma = message.Check8(0x06, 0x08);
                var gravity = message.Check8( 0x02, 0x20);
                var bombs = message.Check8(0x03, 0x10);
                _logger.LogInformation($"speed {speed} | plasma {plasma} | Gravity suit {gravity} | morph bombs {bombs}");
                _previousResponses[message.Address] = message;
            });*/
        }

        /// <summary>
        /// Occurs when the tracker's auto tracker is enabled 
        /// </summary>
        public event EventHandler? AutoTrackerEnabled;

        /// <summary>
        /// Occurs when the tracker's auto tracker is disabled 
        /// </summary>
        public event EventHandler? AutoTrackerDisabled;

        /// <summary>
        /// Occurs when the tracker's auto tracker is connected 
        /// </summary>
        public event EventHandler? AutoTrackerConnected;

        /// <summary>
        /// Occurs when the tracker's auto tracker is disconnected 
        /// </summary>
        public event EventHandler? AutoTrackerDisconnected;

        /// <summary>
        /// If autotracking is currently enabled
        /// </summary>
        public bool IsEnabled { get; private set; } = false;

        /// <summary>
        /// Enables autotracking and listening for connections
        /// </summary>
        public void Enable()
        {
            _logger.LogInformation("Auto tracker Enabled");
            IsEnabled = true;
            _ = Task.Factory.StartNew(() => StartServer());
            AutoTrackerEnabled?.Invoke(this, new());
        }

        /// <summary>
        /// Disconnects current connections and prevents autotracking
        /// </summary>
        public void Disable()
        {
            _logger.LogInformation("Auto tracker Disabled");
            IsEnabled = false;
            if (_tcpListener != null) {
                _tcpListener.Stop();
            }
            if (_socket != null && _socket.Connected)
            {
                _socket.Close();
            }
            AutoTrackerDisabled?.Invoke(this, new());
        }

        /// <summary>
        /// Adds a memory request/response to be processed
        /// </summary>
        /// <param name="action">The command for the lua script to execute</param>
        /// <param name="domain"></param>
        /// <param name="address">The initial memory address</param>
        /// <param name="length">The number of bytes to obtain</param>
        /// <param name="game">Which game(s) this should be executed over</param>
        /// <param name="response">The action to perform upon receiving a response</param>
        protected void AddEvent(string action, string domain, int address, int length, Game game, Action<AutoTrackerMessage> response)
        {
            var request = new AutoTrackerMessage()
            {
                Action = action,
                Domain = domain,
                Address = address,
                Length = length,
                Game = game
            };
            _requestMessages.Add(request);
            _responseActions.Add(address, response);
            _previousResponses.Add(address, new());
        }

        /// <summary>
        /// Starts a listen server for the lua script to connect to
        /// </summary>
        protected void StartServer()
        {
            _tcpListener = new TcpListener(IPAddress.Loopback, 6969);
            _tcpListener.Start();
            while (IsEnabled)
            {
                try
                {
                    _socket = _tcpListener.AcceptSocket();
                    if (_socket.Connected)
                    {
                        using (var stream = new NetworkStream(_socket))
                        using (var writer = new StreamWriter(stream))
                        using (var reader = new StreamReader(stream))
                        {
                            try
                            {
                                Tracker.Say(x => x.AutoTracker.WhenConnected);
                                AutoTrackerConnected?.Invoke(this, new());
                                _ = Task.Factory.StartNew(() => SendMessages());
                                var line = reader.ReadLine();
                                while (line != null && _socket.Connected)
                                {
                                    var message = JsonSerializer.Deserialize<AutoTrackerMessage>(line);
                                    if (message != null && _responseActions.ContainsKey(message.Address) && !message.IsMemoryEqualTo(_previousResponses[message.Address]))
                                    {
                                        _responseActions[message.Address].Invoke(message);
                                    }
                                    line = reader.ReadLine();
                                }
                            }
                            catch (Exception ex)
                            {
                                AutoTrackerDisconnected?.Invoke(this, new());
                                _logger.LogError(ex, "Error sending message");
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

        /// <summary>
        /// Sends requests out to the connected lua script
        /// </summary>
        protected void SendMessages()
        {
            _logger.LogInformation("Start sending");
            while (_socket != null && _socket.Connected)
            {
                try
                {
                    while (!_requestMessages[_currentRequestIndex].ShouldSend(_currentGame, _hasStarted))
                    {
                        _currentRequestIndex = (_currentRequestIndex + 1) % _requestMessages.Count;
                    }

                    var message = JsonSerializer.Serialize(_requestMessages[_currentRequestIndex]) + "\0";
                    _socket.Send(Encoding.ASCII.GetBytes(message));
                    _currentRequestIndex = (_currentRequestIndex + 1) % _requestMessages.Count;
                    Task.Delay(TimeSpan.FromSeconds(0.25f)).Wait();
                }
                catch (Exception e)
                {
                    AutoTrackerDisconnected?.Invoke(this, new());
                    _logger.LogError(e.StackTrace, "Error sending message");
                    break;
                }
            }
        }

        /// <summary>
        /// Tracks changes to the Zelda inventory in memory
        /// </summary>
        /// <param name="message"></param>
        protected void CheckZeldaInventory(AutoTrackerMessage message)
        {
            foreach (var item in _tracker.Items.Where(x => x.InternalItemType.IsInCategory(ItemCategory.Zelda) && x.MemoryAddress != null))
            {
                var location = item.MemoryAddress ?? 0;
                var giveItem = false;

                if (item.InternalItemType == ItemType.Bottle)
                {
                    int numBottles = 0;
                    for (var i = 0; i < 4; i++)
                    {
                        if (message.CompareUInt8(_previousResponses[message.Address], message.Address, location + i, item.MemoryFlag ?? 0))
                        {
                            numBottles++;
                        }
                    }

                    if (numBottles > 0 && item.TrackingState < numBottles)
                    {
                        giveItem = true;
                    }
                }
                else if (item.InternalItemType == ItemType.Mirror)
                {
                    var valueChanged = message.CompareUInt8(_previousResponses[message.Address], message.Address, location, item.MemoryFlag);
                    var value = message.ReadUInt8(location);
                    if (valueChanged && (value == 1 || value == 2) && item.TrackingState == 0)
                    {
                        giveItem = true;
                    }
                }
                else if (item.Multiple && item.HasStages)
                {
                    var valueChanged = message.CompareUInt8(_previousResponses[message.Address], message.Address, location, item.MemoryFlag);
                    var value = message.ReadUInt8(location);
                    if (valueChanged && item.TrackingState < value)
                    {
                        giveItem = true;
                    }
                }
                else
                {
                    giveItem = message.CompareUInt8(_previousResponses[message.Address], message.Address, location, item.MemoryFlag) && item.TrackingState == 0;
                }

                if (giveItem)
                {
                    _tracker.TrackItem(item);
                }
            }
        }

        /// <summary>
        /// Checks locations to see if they have accessed or not
        /// </summary>
        /// <param name="message">The response from the lua script</param>
        /// <param name="type">The type of location to find the correct LocationInfo objects</param>
        /// <param name="is16Bit">Set to true if this is a 16 bit value or false for 8 bit</param>
        protected void CheckLocations(AutoTrackerMessage message, LocationMemoryType type, bool is16Bit)
        {
            foreach (var locationInfo in _tracker.WorldInfo.Locations.Where(x => x.MemoryType == type))
            {
                try
                {
                    var loc = locationInfo.MemoryAddress ?? 0;
                    var flag = locationInfo.MemoryFlag ?? 0;
                    var location = _tracker.World.Locations.Where(x => x.Id == locationInfo.Id).First();
                    if (!location.Cleared && ((is16Bit && message.CheckUInt16(loc * 2, flag)) || (!is16Bit && message.CheckUInt8(loc, flag))))
                    {
                        if (!location.Item.Type.IsInAnyCategory(ItemCategory.Map, ItemCategory.Compass))
                        {
                            _tracker.TrackItem(_tracker.Items.Where(x => x.InternalItemType == location.Item.Type).First(), location);
                            _logger.LogInformation($"Auto tracked {location.Item.Name} from {location.Name}");
                        }
                        else
                        {
                            _tracker.Clear(location);
                            _logger.LogInformation($"Auto tracked {location.Name} as cleared");
                        }
                    }

                }
                catch (Exception e)
                {
                    _logger.LogError("Unable to auto track Zelda Room: " + locationInfo.Name);
                    _logger.LogError(e.Message);
                    _logger.LogTrace(e.StackTrace);
                }
            }
        }

        /// <summary>
        /// Checks the status of if dungeons have been cleared
        /// </summary>
        /// <param name="message">The response from the lua script</param>
        protected void CheckDungeons(AutoTrackerMessage message)
        {
            foreach (var dungeonInfo in _tracker.WorldInfo.Dungeons)
            {
                try
                {
                    if (!dungeonInfo.Cleared && message.CheckUInt16(dungeonInfo.MemoryAddress * 2 ?? 0, dungeonInfo.MemoryFlag ?? 0))
                    {
                        _tracker.MarkDungeonAsCleared(dungeonInfo);
                        _logger.LogInformation($"Auto tracked {dungeonInfo.Name} as cleared");
                    }

                }
                catch (Exception e)
                {
                    _logger.LogError("Unable to auto track Dungeon: " + dungeonInfo.Name);
                    _logger.LogError(e.Message);
                    _logger.LogTrace(e.StackTrace);
                }
            }
        }

        /// <summary>
        /// Checks the status of if the Super Metroid bosses have been defeated
        /// </summary>
        /// <param name="message">The response from the lua script</param>
        protected void CheckSMBosses(AutoTrackerMessage message)
        {
            foreach (var bossInfo in _tracker.WorldInfo.Bosses.Where(x => x.MemoryAddress != null))
            {
                try
                {
                    if (!bossInfo.Defeated && message.CheckUInt8(bossInfo.MemoryAddress ?? 0, bossInfo.MemoryFlag ?? 0))
                    {
                        _tracker.MarkBossAsDefeated(bossInfo);
                        _logger.LogInformation($"Auto tracked {bossInfo.Name} as defeated");
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError("Unable to mark boss as defated: " + bossInfo.Name);
                    _logger.LogError(e.Message);
                    _logger.LogTrace(e.StackTrace);
                }
            }
        }

        /// <summary>
        /// Tracks the current memory state of LttP for Tracker voice lines
        /// </summary>
        /// <param name="message">The message from the emulator with the memory state</param>
        protected void ZeldaStateChecks(AutoTrackerMessage message)
        {
            AutoTrackerZeldaState state = new(message);
            _logger.LogInformation(state.ToString());
            if (_previousZeldaState == null)
            {
                _previousZeldaState = state;
                return;
            }

            // Changed overworld (commented out for now as apparently SMZ3 breaks the memory update when transitioning worlds)
            /* if (state.OverworldValue == _previousZeldaState.OverworldValue && (state.OverworldValue == 0x00 || state.OverworldValue == 0x40) && state.OverworldValue != _previousZeldaOverworldValue)
            {
                Tracker.UpdateRegion(state.OverworldValue == 0x40 ? Tracker.World.LightWorldSouth : Tracker.World.DarkWorldSouth);
                _previousZeldaOverworldValue = state.OverworldValue;
                _previousMetroidRegionValue = -1;
            }*/

            // Falling down from Moldorm (detect if player was in Moldorm room and is now in the room below it)
            if (state.CurrentRoom == 23 && state.PreviousRoom == 7 && _previousZeldaState.CurrentRoom == 7)
            {
                Tracker.Say(x => x.AutoTracker.FallFromMoldorm);
            }
            // Falling down from Ganon (detect if player was in Ganon room and is now in the room below it)
            else if (state.CurrentRoom == 16 && state.PreviousRoom == 0 && _previousZeldaState.CurrentRoom == 0)
            {
                Tracker.Say(x => x.AutoTracker.FallFromGanon);
            }
            // Hera pot (player is in the pot room but does not have the big key)
            else if (state.CurrentRoom == 167 && _previousZeldaState.CurrentRoom == 119 && Tracker.Items.First(x => x.InternalItemType == ItemType.BigKeyTH).TrackingState == 0)
            {
                Tracker.Say(x => x.AutoTracker.HeraPot);
            }
            // Ice breaker (player is on the right side of the wall but was previous in the room to the left)
            else if (state.CurrentRoom == 31 && state.PreviousRoom == 30 && state.LinkX >= 0x48 && _previousZeldaState.LinkX < 0x48)
            {
                Tracker.Say(x => x.AutoTracker.IceBreaker);
            }
            // Diver Down (player is now at the lower section and on the ground, but not from the ladder)
            else if (state.CurrentRoom == 118 && state.LinkX < 0x9F && state.LinkX != 0x68 && state.LinkY <= 0x98 && _previousZeldaState.LinkY > 0x98 && state.LinkState == 0)
            {
                Tracker.Say(x => x.AutoTracker.DiverDown);
            }
            // Entered a dungeon (now in Dungeon state but was previously in Overworld or entering Dungeon state)
            else if (state.State == 0x07 && (_previousZeldaState.State == 0x06 || _previousZeldaState.State == 0x09 || _previousZeldaState.State == 0x0F || _previousZeldaState.State == 0x10 || _previousZeldaState.State == 0x11))
            {
                var dungeonInfo = Tracker.WorldInfo.Dungeons.FirstOrDefault(x => x.StartingRooms != null && x.StartingRooms.Contains(state.CurrentRoom));
                if (dungeonInfo == null || _enteredDungeons.Contains(dungeonInfo)) return;

                if (dungeonInfo.Reward == RewardItem.RedPendant || dungeonInfo.Reward == RewardItem.GreenPendant || dungeonInfo.Reward == RewardItem.BluePendant)
                {
                    Tracker.Say(x => x.AutoTracker.EnterPendantDungeon, dungeonInfo.Name, dungeonInfo.Reward.GetName());
                }
                else if (dungeonInfo.Is(Tracker.World.CastleTower))
                {
                    Tracker.Say(x => x.AutoTracker.EnterHyruleCastleTower);
                }

                _enteredDungeons.Add(dungeonInfo);
            }
            // Death
            else if (state.State == 18 && state.Substate == 9 && _previousZeldaState.Substate != 9)
            {
                // Here we should be able to track where the player died
                Tracker.TrackItem(Tracker.Items.First(x => x.ToString().Equals("Death", StringComparison.OrdinalIgnoreCase)));
            }

            _previousZeldaState = state;
        }

        /// <summary>
        /// Tracks the current memory state of SM for Tracker voice lines
        /// </summary>
        /// <param name="message">The message from the emulator with the memory state</param>
        protected void MetroidStateChecks(AutoTrackerMessage message)
        {
            AutoTrackerMetroidState state = new(message);
            _logger.LogInformation(state.ToString());
            if (_previousMetroidState == null)
            {
                _previousMetroidState = state;
                return;
            }

            // Update the region that the player is currently in
            if (state.CurrentRegion != _previousMetroidRegionValue)
            {
                if (state.CurrentRegion == 0)
                {
                    Tracker.UpdateRegion(Tracker.World.CentralCrateria, Tracker.Options.AutoTrackerChangeMap);
                }
                else if (state.CurrentRegion == 1)
                {
                    Tracker.UpdateRegion(Tracker.World.GreenBrinstar, Tracker.Options.AutoTrackerChangeMap);
                }
                else if (state.CurrentRegion == 2)
                {
                    Tracker.UpdateRegion(Tracker.World.UpperNorfairEast, Tracker.Options.AutoTrackerChangeMap);
                }
                else if (state.CurrentRegion == 3)
                {
                    Tracker.UpdateRegion(Tracker.World.WreckedShip, Tracker.Options.AutoTrackerChangeMap);
                }
                else if (state.CurrentRegion == 4)
                {
                    Tracker.UpdateRegion(Tracker.World.InnerMaridia, Tracker.Options.AutoTrackerChangeMap);
                }
                _previousMetroidRegionValue = state.CurrentRegion;
                _previousZeldaOverworldValue = -1;
            }

            // Approaching Kraid's Awful Son
            if (state.CurrentRegion == 1 && state.CurrentRoomInRegion == 45 && _previousMetroidState.CurrentRoomInRegion == 44)
            {
                Tracker.Say(x => x.AutoTracker.NearKraidsAwfulSon);
            }
            // Approaching Shaktool
            else if (state.CurrentRegion == 4 && state.CurrentRoomInRegion == 36 && _previousMetroidState.CurrentRoomInRegion == 28)
            {
                Tracker.Say(x => x.AutoTracker.NearShaktool);
            }
            // Death (health and reserve tanks all 0 (have to check to make sure the player isn't warping between games)
            else if (state.Health == 0 && state.ReserveTanks == 0 && _previousMetroidState.Health != 0 && !(state.CurrentRoom == 0 && state.CurrentRegion == 0 && state.SamusY == 0))
            {
                // Here we should be able to track where the player died
                Tracker.TrackItem(Tracker.Items.First(x => x.ToString().Equals("Death", StringComparison.OrdinalIgnoreCase)));
            }

            _previousMetroidState = state;
        }

        /// <summary>
        /// Called when the module is destroyed
        /// </summary>
        public void Dispose() {
            Disable();
        }
    }

}
