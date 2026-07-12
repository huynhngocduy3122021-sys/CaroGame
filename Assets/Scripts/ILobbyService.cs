using System;
using System.Collections.Generic;

public interface ILobbyService
{
    // Event-driven reactive interface
    event Action<LobbyData> OnLobbyCreated;
    event Action<LobbyData> OnLobbyJoined;
    event Action OnLobbyLeft;
    event Action<LobbyData> OnLobbyUpdated;
    event Action<string> OnPlayerJoined;
    event Action<string> OnPlayerLeft;
    event Action OnLobbyDeleted;

    void CreateLobby(LobbyData lobbyData, Action<bool, string> onComplete);
    void JoinLobby(LobbyData lobbyData, Action<bool, string> onComplete);
    void JoinByCode(string code, Action<bool, string> onComplete);
    void QuickJoin(Action<bool, string> onComplete);
    void LeaveLobby(Action<bool, string> onComplete);
    void UpdateLobby(LobbyData lobbyData, Action<bool, string> onComplete);
    void DeleteLobby(Action<bool, string> onComplete);
    
    List<LobbyData> QueryPublicLobbies();
}
