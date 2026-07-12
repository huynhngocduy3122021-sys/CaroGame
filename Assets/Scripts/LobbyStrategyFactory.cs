using System;
using System.Collections.Generic;

public class LobbyStrategyFactory
{
    private readonly Dictionary<LobbyType, ILobbyStrategy> strategies = new Dictionary<LobbyType, ILobbyStrategy>();

    public LobbyStrategyFactory(ILobbyService lobbyService)
    {
        strategies[LobbyType.Public] = new PublicLobbyStrategy(lobbyService);
        strategies[LobbyType.Private] = new PrivateLobbyStrategy(lobbyService);
        strategies[LobbyType.Ranking] = new RankingLobbyStrategy(lobbyService);
    }

    public ILobbyStrategy GetStrategy(LobbyType type)
    {
        if (strategies.TryGetValue(type, out var strategy))
        {
            return strategy;
        }
        throw new Exception("No strategy registered for LobbyType: " + type);
    }
}
