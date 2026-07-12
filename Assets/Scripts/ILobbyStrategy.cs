using System;

public interface ILobbyStrategy
{
    void CreateLobby(LobbyData lobbyData, Action<bool, string> onComplete);
    void JoinLobby(LobbyData lobbyData, Action<bool, string> onComplete);
    void JoinByCode(string code, Action<bool, string> onComplete);
    void QuickJoin(Action<bool, string> onComplete);
    void LeaveLobby(Action<bool, string> onComplete);
    void UpdateLobby(LobbyData lobbyData, Action<bool, string> onComplete);
    void DeleteLobby(Action<bool, string> onComplete);
}
