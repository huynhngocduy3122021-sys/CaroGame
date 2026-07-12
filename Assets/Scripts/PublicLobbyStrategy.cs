using System;

public class PublicLobbyStrategy : ILobbyStrategy
{
    private readonly ILobbyService lobbyService;

    public PublicLobbyStrategy(ILobbyService lobbyService)
    {
        this.lobbyService = lobbyService;
    }

    public void CreateLobby(LobbyData lobbyData, Action<bool, string> onComplete)
    {
        lobbyData.IsPrivate = false;
        lobbyData.Password = string.Empty; // Public rooms have no password
        lobbyService.CreateLobby(lobbyData, onComplete);
    }

    public void JoinLobby(LobbyData lobbyData, Action<bool, string> onComplete)
    {
        lobbyService.JoinLobby(lobbyData, onComplete);
    }

    public void JoinByCode(string code, Action<bool, string> onComplete)
    {
        lobbyService.JoinByCode(code, onComplete);
    }

    public void QuickJoin(Action<bool, string> onComplete)
    {
        lobbyService.QuickJoin(onComplete);
    }

    public void LeaveLobby(Action<bool, string> onComplete)
    {
        lobbyService.LeaveLobby(onComplete);
    }

    public void UpdateLobby(LobbyData lobbyData, Action<bool, string> onComplete)
    {
        lobbyService.UpdateLobby(lobbyData, onComplete);
    }

    public void DeleteLobby(Action<bool, string> onComplete)
    {
        lobbyService.DeleteLobby(onComplete);
    }
}
