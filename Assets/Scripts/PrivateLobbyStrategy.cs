using System;

public class PrivateLobbyStrategy : ILobbyStrategy
{
    private readonly ILobbyService lobbyService;

    public PrivateLobbyStrategy(ILobbyService lobbyService)
    {
        this.lobbyService = lobbyService;
    }

    public void CreateLobby(LobbyData lobbyData, Action<bool, string> onComplete)
    {
        lobbyData.IsPrivate = true;
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
        onComplete?.Invoke(false, "Vào nhanh không được hỗ trợ đối với phòng riêng tư.");
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
