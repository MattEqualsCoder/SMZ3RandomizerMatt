﻿using System.Collections.Generic;

namespace Randomizer.Shared.Multiplayer;

public class RejoinGameResponse : MultiplayerResponse
{
    public RejoinGameResponse(MultiplayerGameState gameState, string playerGuid, string playerKey, List<MultiplayerPlayerState> allPlayers) : base(gameState)
    {
        PlayerGuid = playerGuid;
        PlayerKey = playerKey;
        AllPlayers = allPlayers;
    }

    public string PlayerGuid { get; }
    public string PlayerKey { get; }
    public List<MultiplayerPlayerState> AllPlayers { get; }


}
